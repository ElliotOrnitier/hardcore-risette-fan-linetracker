using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RiseLineTracker;

public class MemoryReader : IDisposable
{
    private Process? _process;
    private IntPtr _processHandle;
    private IntPtr _trophyBaseAddress;
    private bool _isAttached;
    private string _lastError = "";

    // Windows API imports
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int PROCESS_VM_READ = 0x0010;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_WM_READ = PROCESS_VM_READ | PROCESS_QUERY_INFORMATION;

    // 64-bit offsets (based on memory analysis):
    // Trophy stats base + 0 = Fusion count (4 bytes)
    // Trophy stats base + 4 = All-out attacks (1 byte)
    // Trophy stats base + 5 = Weakness exploits (1 byte)
    // Trophy stats base + 6 = Rise Lines counter (2 bytes)
    // Trophy stats base + 8 = Rise Lines flags start
    private const int RISE_COUNTER_OFFSET = 6;
    private const int RISE_FLAGS_OFFSET = 8;

    // Static offset from P4G.exe module base to trophy stats (64-bit Steam version)
    private const long P4G_TROPHY_STATS_OFFSET = 0x51C6500;

    public bool IsAttached => _isAttached;
    public string? ProcessName => _process?.ProcessName;
    public string LastError => _lastError;
    public IntPtr TrophyBaseAddress => _trophyBaseAddress;

    public static List<Process> GetP4GProcesses()
    {
        var processes = new List<Process>();

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (proc.ProcessName.Contains("P4G", StringComparison.OrdinalIgnoreCase) ||
                    proc.ProcessName.Contains("Persona", StringComparison.OrdinalIgnoreCase))
                {
                    processes.Add(proc);
                }
            }
            catch
            {
                // Process may have exited
            }
        }

        return processes;
    }

    public static List<Process> GetAllProcesses()
    {
        return Process.GetProcesses()
            .Where(p =>
            {
                try { return !string.IsNullOrEmpty(p.MainWindowTitle) || p.ProcessName.Length > 0; }
                catch { return false; }
            })
            .OrderBy(p => p.ProcessName)
            .ToList();
    }

    public bool Attach(Process process)
    {
        _lastError = "";

        try
        {
            _process = process;
            _processHandle = OpenProcess(PROCESS_WM_READ, false, process.Id);

            if (_processHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                _lastError = $"OpenProcess failed with error code {error}. Try running as Administrator.";
                return false;
            }

            // module base address
            ProcessModule? mainModule;
            try
            {
                mainModule = _process.MainModule;
            }
            catch (Exception ex)
            {
                _lastError = $"Cannot access MainModule: {ex.Message}. Try running as Administrator.";
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                return false;
            }

            if (mainModule == null)
            {
                _lastError = "MainModule is null";
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                return false;
            }

            // Calculate trophy stats address using static offset
            IntPtr moduleBase = mainModule.BaseAddress;
            _trophyBaseAddress = new IntPtr(moduleBase.ToInt64() + P4G_TROPHY_STATS_OFFSET);

            // Verify by reading the trophy stats area
            byte[] verifyBuffer = new byte[4];
            if (!ReadProcessMemory(_processHandle, _trophyBaseAddress, verifyBuffer, 4, out int bytesRead) || bytesRead < 4)
            {
                _lastError = $"Could not read memory at 0x{_trophyBaseAddress.ToInt64():X}. Game may not be fully loaded.";
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                return false;
            }

            _isAttached = true;
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Exception: {ex.Message}";
            _isAttached = false;
            return false;
        }
    }

    /// <summary>
    /// Manual attach using a user-provided trophy base address (fallback if static offset doesn't work).
    /// </summary>
    public bool AttachWithTrophyBase(Process process, IntPtr trophyBase)
    {
        _lastError = "";

        try
        {
            _process = process;
            _processHandle = OpenProcess(PROCESS_WM_READ, false, process.Id);

            if (_processHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                _lastError = $"OpenProcess failed with error code {error}. Try running as Administrator.";
                return false;
            }

            _trophyBaseAddress = trophyBase;

            // Verify by reading the Rise Lines counter
            IntPtr counterAddr = IntPtr.Add(trophyBase, RISE_COUNTER_OFFSET);
            byte[] counterBuffer = new byte[1];
            if (!ReadProcessMemory(_processHandle, counterAddr, counterBuffer, 1, out _))
            {
                _lastError = $"Could not read Rise Lines counter at 0x{counterAddr.ToInt64():X}. Address may be incorrect.";
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                return false;
            }

            _lastError = $"Attached. Counter={counterBuffer[0]}";
            _isAttached = true;
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Exception: {ex.Message}";
            _isAttached = false;
            return false;
        }
    }

    public void Detach()
    {
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
        _process = null;
        _isAttached = false;
        _trophyBaseAddress = IntPtr.Zero;
    }

    public void UpdateAllFlags(List<RiseLine> lines)
    {
        if (!_isAttached) return;

        IntPtr flagBase = IntPtr.Add(_trophyBaseAddress, RISE_FLAGS_OFFSET);
        byte[] buffer = new byte[256];

        if (ReadProcessMemory(_processHandle, flagBase, buffer, buffer.Length, out int bytesRead) && bytesRead > 0)
        {
            foreach (var line in lines)
            {
                if (line.ByteOffset < bytesRead)
                {
                    line.IsHit = (buffer[line.ByteOffset] & (1 << line.BitPosition)) != 0;
                }
            }
        }
    }

    public TrophyStats ReadTrophyStats()
    {
        var stats = new TrophyStats();
        if (!_isAttached || _trophyBaseAddress == IntPtr.Zero) return stats;

        byte[] buffer = new byte[12];
        if (ReadProcessMemory(_processHandle, _trophyBaseAddress, buffer, buffer.Length, out int bytesRead) && bytesRead >= 8)
        {
            stats.FusionCount = BitConverter.ToInt32(buffer, 0);
            stats.AllOutAttacks = buffer[4];
            stats.WeaknessExploits = buffer[5];
            stats.RiseLines = BitConverter.ToUInt16(buffer, 6);
        }

        return stats;
    }

    public struct TrophyStats
    {
        public int FusionCount;
        public byte AllOutAttacks;
        public byte WeaknessExploits;
        public ushort RiseLines;
    }

    public void Dispose()
    {
        Detach();
    }
}
