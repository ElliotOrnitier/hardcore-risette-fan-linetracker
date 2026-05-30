using System.Diagnostics;

namespace RiseLineTracker;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(24, 24, 27);
    public static readonly Color Surface = Color.FromArgb(32, 32, 36);
    public static readonly Color SurfaceAlt = Color.FromArgb(45, 45, 50);
    public static readonly Color Border = Color.FromArgb(64, 64, 70);
    public static readonly Color Text = Color.FromArgb(224, 224, 228);
    public static readonly Color SubtleText = Color.FromArgb(150, 150, 156);

    public static readonly Color Accent = Color.FromArgb(255, 200, 60);
    public static readonly Color AccentHover = Color.FromArgb(255, 214, 96);

    public static readonly Color RowEven = Color.FromArgb(32, 32, 36);
    public static readonly Color RowOdd = Color.FromArgb(38, 38, 43);
    public static readonly Color Selection = Color.FromArgb(38, 79, 120);

    public static readonly Color HitBack = Color.FromArgb(30, 64, 42);
    public static readonly Color HitText = Color.FromArgb(126, 224, 138);
    public static readonly Color CountedBack = Color.FromArgb(48, 48, 52);
    public static readonly Color CountedText = Color.FromArgb(130, 130, 136);

    public static readonly Color Good = Color.FromArgb(102, 187, 106);
    public static readonly Color Warn = Color.FromArgb(255, 167, 38);
    public static readonly Color Bad = Color.FromArgb(239, 83, 80);
}

public class MainForm : Form
{
    private ComboBox _processComboBox = null!;
    private Button _refreshProcessesButton = null!;
    private Button _attachButton = null!;
    private TextBox _manualAddressBox = null!;
    private Button _manualAttachButton = null!;
    private Button _manualHelpButton = null!;
    private ThemedListView _flagListView = null!;
    private Label _statusLabel = null!;
    private Label _trophyLabel = null!;
    private FlatProgressBar _trophyProgressBar = null!;
    private Label _uniqueLabel = null!;
    private Label _totalLabel = null!;
    private Label _trophyStatsLabel = null!;
    private Label _recentHitsLabel = null!;
    private CheckBox _showOnlyMissingCheckBox = null!;
    private CheckBox _hideRedundantCheckBox = null!;
    private CheckBox _hideListCheckBox = null!;
    private TextBox _searchBox = null!;
    private System.Windows.Forms.Timer _updateTimer = null!;

    private MemoryReader _memoryReader = new();
    private List<RiseLine> _riseLines = new();
    private List<RiseLine> _filteredLines = new();
    private List<RiseLine> _recentHits = new();
    private HashSet<int> _previousHitIds = new();
    private HashSet<int> _previousRedundantIds = new();

    private const int TROPHY_REQUIREMENT = 250;

    private const string ManualAddressHelpText =
        "The All-Out Attack counter address is only needed if Auto Attach fails.\n\n" +
        "To find it:\n" +
        "1. Open Cheat Engine and attach to P4G\n" +
        "2. Scan Type: 'Array of byte'\n" +
        "3. Search for your trophy values as hex bytes, e.g.:\n" +
        "   Sweep(86) Tanaka(9) Lunches(1) = '56 09 01'\n" +
        "4. Find the match, then look backwards for 'AF FF' or similar\n" +
        "5. Enter the address of the All-Out Attack byte (AF)\n\n" +
        "The Rise counter is at +2, flags at +4 from this address.";

    public MainForm()
    {
        InitializeComponent();
        LoadRiseLines();
        RefreshProcessList();
    }

    private void InitializeComponent()
    {
        Text = "P4G Rise Line Tracker — Hardcore Risette Fan";
        Size = new Size(960, 760);
        MinimumSize = new Size(780, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;

        // ----- Top control panel -----
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(14, 12, 14, 8),
            BackColor = Theme.Surface
        };

        // Process selection row
        var processLabel = MakeLabel("Process", new Point(14, 16), Theme.SubtleText);

        _processComboBox = new ComboBox
        {
            Location = new Point(86, 12),
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.SurfaceAlt,
            ForeColor = Theme.Text
        };

        _refreshProcessesButton = new Button
        {
            Text = "Refresh",
            Location = new Point(396, 11),
            Width = 70
        };
        StyleButton(_refreshProcessesButton);
        _refreshProcessesButton.Click += (s, e) => RefreshProcessList();

        _attachButton = new Button
        {
            Text = "Auto Attach",
            Location = new Point(474, 11),
            Width = 100
        };
        StyleAccentButton(_attachButton);
        _attachButton.Click += AttachButton_Click;

        // Manual address input (advanced fallback)
        var manualLabel = MakeLabel("Manual addr", new Point(596, 16), Theme.SubtleText);

        _manualAddressBox = new TextBox
        {
            Location = new Point(682, 12),
            Width = 110
        };
        StyleTextBox(_manualAddressBox);

        _manualAttachButton = new Button
        {
            Text = "Attach",
            Location = new Point(800, 11),
            Width = 64
        };
        StyleButton(_manualAttachButton);
        _manualAttachButton.Click += ManualAttachButton_Click;

        _manualHelpButton = new Button
        {
            Text = "?",
            Location = new Point(868, 11),
            Width = 28
        };
        StyleButton(_manualHelpButton);
        _manualHelpButton.Click += (s, e) =>
            MessageBox.Show(ManualAddressHelpText, "Finding the manual address",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Filter row
        var searchLabel = MakeLabel("Search", new Point(14, 54), Theme.SubtleText);

        _searchBox = new TextBox
        {
            Location = new Point(86, 50),
            Width = 200
        };
        StyleTextBox(_searchBox);
        _searchBox.TextChanged += (s, e) => ApplyFilter();

        _showOnlyMissingCheckBox = MakeCheckBox("Show only missing", new Point(304, 52));
        _showOnlyMissingCheckBox.CheckedChanged += (s, e) => ApplyFilter();

        _hideRedundantCheckBox = MakeCheckBox("Hide already counted", new Point(450, 52));
        _hideRedundantCheckBox.CheckedChanged += (s, e) => ApplyFilter();

        _hideListCheckBox = MakeCheckBox("Hide list", new Point(620, 52));
        _hideListCheckBox.CheckedChanged += (s, e) => _flagListView.Visible = !_hideListCheckBox.Checked;

        // Legend row
        var legendPanel = new Panel
        {
            Location = new Point(14, 88),
            Size = new Size(720, 22),
            BackColor = Theme.Surface
        };
        legendPanel.Controls.AddRange(new Control[]
        {
            CreateLegendItem("Hit", Theme.HitBack, 0),
            CreateLegendItem("Counted (slot filled)", Theme.CountedBack, 70),
            CreateLegendItem("Missing", Theme.RowOdd, 250)
        });

        topPanel.Controls.AddRange(new Control[]
        {
            processLabel, _processComboBox, _refreshProcessesButton, _attachButton,
            manualLabel, _manualAddressBox, _manualAttachButton, _manualHelpButton,
            searchLabel, _searchBox, _showOnlyMissingCheckBox, _hideRedundantCheckBox, _hideListCheckBox,
            legendPanel
        });

        // ----- Bottom status panel -----
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 178,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = Theme.Surface
        };

        _trophyLabel = new Label
        {
            Text = "Trophy: 0 / 250 (0%)",
            Location = new Point(14, 8),
            AutoSize = true,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Theme.Accent
        };

        _trophyProgressBar = new FlatProgressBar
        {
            Location = new Point(16, 42),
            Size = new Size(908, 12),
            Maximum = TROPHY_REQUIREMENT,
            Value = 0,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _uniqueLabel = new Label
        {
            Text = "Unique Slots: 0 / 480",
            Location = new Point(16, 64),
            AutoSize = true,
            ForeColor = Theme.Text
        };

        _totalLabel = new Label
        {
            Text = "Total Lines Hit: 0 / 0",
            Location = new Point(220, 64),
            AutoSize = true,
            ForeColor = Theme.Text
        };

        _trophyStatsLabel = new Label
        {
            Text = "Fusions —   |   All-Out —   |   Weakness —   |   Rise —",
            Location = new Point(430, 64),
            AutoSize = true,
            ForeColor = Theme.SubtleText
        };

        _recentHitsLabel = new Label
        {
            Text = "Recent hits\n  (none yet)",
            Location = new Point(16, 90),
            Size = new Size(908, 56),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Theme.HitText
        };

        _statusLabel = new Label
        {
            Text = "Not attached",
            Location = new Point(16, 150),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            ForeColor = Theme.Bad
        };

        bottomPanel.Controls.AddRange(new Control[]
        {
            _trophyLabel, _trophyProgressBar, _uniqueLabel, _totalLabel,
            _trophyStatsLabel, _recentHitsLabel, _statusLabel
        });

        // ----- Main list view -----
        _flagListView = new ThemedListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true
        };

        _flagListView.Columns.Add("ID", 55, HorizontalAlignment.Center);
        _flagListView.Columns.Add("Idx", 55, HorizontalAlignment.Center);
        _flagListView.Columns.Add("Status", 90, HorizontalAlignment.Center);
        _flagListView.Columns.Add("Rise Line", 600, HorizontalAlignment.Left);
        _flagListView.Columns.Add("Shared", 70, HorizontalAlignment.Center);

        // Add controls to form (order matters for docking)
        Controls.Add(_flagListView);
        Controls.Add(topPanel);
        Controls.Add(bottomPanel);

        // Setup timer
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        // Handle form closing
        FormClosing += (s, e) =>
        {
            _updateTimer.Stop();
            _memoryReader.Dispose();
        };
    }

    // ----- Styling helpers -----

    private static Label MakeLabel(string text, Point location, Color color) => new()
    {
        Text = text,
        Location = location,
        AutoSize = true,
        ForeColor = color
    };

    private static CheckBox MakeCheckBox(string text, Point location) => new()
    {
        Text = text,
        Location = location,
        AutoSize = true,
        FlatStyle = FlatStyle.Flat,
        ForeColor = Theme.Text,
        BackColor = Color.Transparent
    };

    private static void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Theme.SurfaceAlt;
        b.ForeColor = Theme.Text;
        b.FlatAppearance.BorderColor = Theme.Border;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Theme.Border;
        b.Cursor = Cursors.Hand;
        b.Height = 26;
    }

    private static void StyleAccentButton(Button b)
    {
        StyleButton(b);
        b.BackColor = Theme.Accent;
        b.ForeColor = Color.FromArgb(28, 28, 30);
        b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        b.FlatAppearance.BorderColor = Theme.Accent;
        b.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
    }

    private static void StyleTextBox(TextBox t)
    {
        t.BackColor = Theme.SurfaceAlt;
        t.ForeColor = Theme.Text;
        t.BorderStyle = BorderStyle.FixedSingle;
    }

    private Panel CreateLegendItem(string text, Color color, int x)
    {
        var panel = new Panel
        {
            Location = new Point(x, 0),
            Size = new Size(text.Length * 7 + 26, 20),
            BackColor = Theme.Surface
        };

        var colorBox = new Panel
        {
            Location = new Point(0, 3),
            Size = new Size(14, 14),
            BackColor = color,
            BorderStyle = BorderStyle.FixedSingle
        };

        var label = new Label
        {
            Text = text,
            Location = new Point(20, 2),
            AutoSize = true,
            ForeColor = Theme.SubtleText
        };

        panel.Controls.AddRange(new Control[] { colorBox, label });
        return panel;
    }

    private void LoadRiseLines()
    {
        try
        {
            // A rise.tsv placed next to the exe takes precedence (lets users update the
            // data without a rebuild); otherwise fall back to the copy embedded in the exe.
            string tsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rise.tsv");

            _riseLines = File.Exists(tsvPath)
                ? RiseLine.LoadFromTsv(tsvPath)
                : RiseLine.LoadEmbedded();

            _filteredLines = new List<RiseLine>(_riseLines);
            UpdateListView();
            UpdateCounters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading Rise lines: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshProcessList()
    {
        _processComboBox.Items.Clear();
        _processComboBox.Items.Add("-- Select Process --");

        // P4G processes
        var p4gProcesses = MemoryReader.GetP4GProcesses();
        foreach (var proc in p4gProcesses)
        {
            _processComboBox.Items.Add(new ProcessItem(proc));
        }

        if (p4gProcesses.Count > 0)
        {
            _processComboBox.Items.Add("-- Other Processes --");
        }

        // other processes
        foreach (var proc in MemoryReader.GetAllProcesses())
        {
            if (!p4gProcesses.Any(p => p.Id == proc.Id))
            {
                _processComboBox.Items.Add(new ProcessItem(proc));
            }
        }

        _processComboBox.SelectedIndex = 0;
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private void ResetFlagsAfterDetach()
    {
        foreach (var line in _riseLines)
        {
            line.IsHit = false;
            line.IsRedundant = false;
        }
        _previousHitIds.Clear();
        _previousRedundantIds.Clear();
        ApplyFilter();
        UpdateCounters();
    }

    private void BeginTracking()
    {
        // Initialize with current hits
        _memoryReader.UpdateAllFlags(_riseLines);
        RiseLine.UpdateRedundantStatus(_riseLines);
        _previousHitIds = new HashSet<int>(_riseLines.Where(l => l.IsHit).Select(l => l.Id));
        _previousRedundantIds = new HashSet<int>(_riseLines.Where(l => l.IsRedundant).Select(l => l.Id));
        _recentHits.Clear();
        UpdateRecentHitsLabel();
        ApplyFilter();
        UpdateCounters();
        UpdateTrophyStats();

        _updateTimer.Start();
    }

    private async void AttachButton_Click(object? sender, EventArgs e)
    {
        if (_memoryReader.IsAttached)
        {
            // Detach
            _updateTimer.Stop();
            _memoryReader.Detach();
            _attachButton.Text = "Auto Attach";
            SetStatus("Not attached", Theme.Bad);
            ResetFlagsAfterDetach();
            return;
        }

        // Attach
        if (_processComboBox.SelectedItem is not ProcessItem processItem)
        {
            MessageBox.Show("Please select a process first.", "No Process Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetStatus("Attaching (scanning memory, please wait)...", Theme.Warn);
        _attachButton.Enabled = false;
        _processComboBox.Enabled = false;

        var process = processItem.Process;
        bool success = await Task.Run(() => _memoryReader.Attach(process));

        _attachButton.Enabled = true;
        _processComboBox.Enabled = true;

        if (success)
        {
            _attachButton.Text = "Detach";
            SetStatus($"Attached to {_memoryReader.ProcessName}", Theme.Good);
            BeginTracking();
        }
        else
        {
            SetStatus("Failed to attach", Theme.Bad);
            MessageBox.Show(
                "Failed to attach to the process.\n\n" +
                "Make sure:\n" +
                "1. The game is running\n" +
                "2. The tracker is run as Administrator\n" +
                "3. A save file is loaded (not just the title screen)\n\n" +
                "If Auto Attach keeps failing, use the manual address (see the ? button).",
                "Attach Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async void ManualAttachButton_Click(object? sender, EventArgs e)
    {
        if (_memoryReader.IsAttached)
        {
            _updateTimer.Stop();
            _memoryReader.Detach();
            _attachButton.Text = "Auto Attach";
            _manualAttachButton.Text = "Attach";
            SetStatus("Not attached", Theme.Bad);
            ResetFlagsAfterDetach();
            return;
        }

        string addressText = _manualAddressBox.Text.Trim();
        if (string.IsNullOrEmpty(addressText))
        {
            MessageBox.Show(ManualAddressHelpText, "No Address", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        long baseAddress;
        try
        {
            addressText = addressText.Replace("0x", "").Replace("0X", "");
            baseAddress = Convert.ToInt64(addressText, 16);
        }
        catch
        {
            MessageBox.Show("Invalid address format. Enter a hex value like 'FFFF0000' or '0xFFFF0000'",
                "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (_processComboBox.SelectedItem is not ProcessItem processItem)
        {
            MessageBox.Show("Please select a process first.", "No Process Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetStatus("Attaching with manual address...", Theme.Warn);
        _manualAttachButton.Enabled = false;
        _attachButton.Enabled = false;

        var process = processItem.Process;
        bool success = await Task.Run(() => _memoryReader.AttachWithTrophyBase(process, new IntPtr(baseAddress)));

        _manualAttachButton.Enabled = true;
        _attachButton.Enabled = true;

        if (success)
        {
            _attachButton.Text = "Detach";
            _manualAttachButton.Text = "Detach";
            SetStatus($"Attached to {_memoryReader.ProcessName} (manual address)", Theme.Good);
            BeginTracking();
        }
        else
        {
            SetStatus("Failed to attach", Theme.Bad);
            MessageBox.Show(
                "Failed to attach with the manual address.\n\n" +
                "Double-check the address is the All-Out Attack counter byte. See the ? button for details.",
                "Attach Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (!_memoryReader.IsAttached) return;

        try
        {
            _memoryReader.UpdateAllFlags(_riseLines);
            RiseLine.UpdateRedundantStatus(_riseLines);

            var currentHitIds = new HashSet<int>(_riseLines.Where(l => l.IsHit).Select(l => l.Id));
            var currentRedundantIds = new HashSet<int>(_riseLines.Where(l => l.IsRedundant).Select(l => l.Id));

            // Only rebuild the list/counters when the displayed state actually changed.
            // Without this guard the timer clears and re-adds every row every 500ms, causing flicker.
            bool stateChanged = !currentHitIds.SetEquals(_previousHitIds)
                             || !currentRedundantIds.SetEquals(_previousRedundantIds);

            // Detect newly hit lines for the "recent hits" panel
            var newHits = _riseLines.Where(l => l.IsHit && !_previousHitIds.Contains(l.Id)).ToList();
            if (newHits.Count > 0)
            {
                // Add new hits to the front of recent list
                _recentHits.InsertRange(0, newHits);
                if (_recentHits.Count > 5)
                    _recentHits = _recentHits.Take(5).ToList();

                UpdateRecentHitsLabel();
            }

            _previousHitIds = currentHitIds;
            _previousRedundantIds = currentRedundantIds;

            if (stateChanged)
            {
                ApplyFilter();
                UpdateCounters();
            }

            UpdateTrophyStats();
        }
        catch
        {
            _updateTimer.Stop();
            _memoryReader.Detach();
            _attachButton.Text = "Auto Attach";
            _manualAttachButton.Text = "Attach";
            SetStatus("Process exited", Theme.Bad);
        }
    }

    private void UpdateRecentHitsLabel()
    {
        if (_recentHits.Count == 0)
        {
            _recentHitsLabel.Text = "Recent hits\n  (none yet)";
        }
        else
        {
            var lines = _recentHits.Select((l, i) => $"  {i + 1}. [{l.Id}] {TruncateName(l.Name, 90)}");
            _recentHitsLabel.Text = "Recent hits\n" + string.Join("\n", lines);
        }
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 3) + "...";
    }

    private void UpdateTrophyStats()
    {
        var stats = _memoryReader.ReadTrophyStats();
        _trophyStatsLabel.Text =
            $"Fusions {stats.FusionCount}   |   All-Out {stats.AllOutAttacks}   |   " +
            $"Weakness {stats.WeaknessExploits}   |   Rise {stats.RiseLines}";
    }

    private void ApplyFilter()
    {
        string searchText = _searchBox.Text.ToLower();
        bool onlyMissing = _showOnlyMissingCheckBox.Checked;
        bool hideRedundant = _hideRedundantCheckBox.Checked;

        _filteredLines = _riseLines.Where(line =>
        {
            // Search filter
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 line.Name.ToLower().Contains(searchText) ||
                                 line.Id.ToString().Contains(searchText) ||
                                 line.Index.ToString().Contains(searchText);

            // Show only missing filter (not hit)
            bool matchesMissing = !onlyMissing || !line.IsHit;

            // Hide redundant filter
            bool matchesRedundant = !hideRedundant || !line.IsRedundant;

            return matchesSearch && matchesMissing && matchesRedundant;
        }).ToList();

        UpdateListView();
    }

    private void UpdateListView()
    {
        _flagListView.BeginUpdate();

        // Remember scroll position
        int topItemIndex = _flagListView.TopItem?.Index ?? 0;

        _flagListView.Items.Clear();

        var hitCountByIdx = _riseLines
            .Where(l => l.IsHit)
            .GroupBy(l => l.Index)
            .ToDictionary(g => g.Key, g => g.Count());

        for (int i = 0; i < _filteredLines.Count; i++)
        {
            var line = _filteredLines[i];

            var item = new ListViewItem(line.Id.ToString());
            item.SubItems.Add(line.Index.ToString());

            // Status column + row colors
            string status;
            if (line.IsHit)
            {
                status = "HIT";
                item.BackColor = Theme.HitBack;
                item.ForeColor = Theme.HitText;
            }
            else if (line.IsRedundant)
            {
                status = "COUNTED";
                item.BackColor = Theme.CountedBack;
                item.ForeColor = Theme.CountedText;
            }
            else
            {
                status = "—";
                item.BackColor = (i % 2 == 0) ? Theme.RowEven : Theme.RowOdd;
                item.ForeColor = Theme.Text;
            }
            item.SubItems.Add(status);

            item.SubItems.Add(line.Name);

            // Show shared count if > 1: "hitCount/totalCount"
            if (line.SharedCount > 1)
            {
                int hitCount = hitCountByIdx.TryGetValue(line.Index, out int count) ? count : 0;
                item.SubItems.Add($"{hitCount}/{line.SharedCount}");
            }
            else
            {
                item.SubItems.Add("");
            }

            _flagListView.Items.Add(item);
        }

        // Restore scroll position
        if (topItemIndex > 0 && topItemIndex < _flagListView.Items.Count)
        {
            _flagListView.TopItem = _flagListView.Items[topItemIndex];
        }

        _flagListView.EndUpdate();
    }

    private void UpdateCounters()
    {
        int uniqueTotal = RiseLine.GetUniqueIndexCount(_riseLines);
        int uniqueHit = RiseLine.GetHitUniqueIndexCount(_riseLines);
        int totalHit = _riseLines.Count(l => l.IsHit);
        int totalCount = _riseLines.Count;

        // Trophy progress (based on total lines hit, capped at 250)
        int trophyProgress = Math.Min(totalHit, TROPHY_REQUIREMENT);
        int trophyPercent = trophyProgress * 100 / TROPHY_REQUIREMENT;

        _trophyProgressBar.Value = trophyProgress;

        if (trophyProgress >= TROPHY_REQUIREMENT)
        {
            _trophyLabel.Text = $"Trophy: {trophyProgress} / {TROPHY_REQUIREMENT} (100%) — UNLOCKED!";
            _trophyLabel.ForeColor = Theme.Good;
            _trophyProgressBar.BarColor = Theme.Good;
        }
        else
        {
            _trophyLabel.Text = $"Trophy: {trophyProgress} / {TROPHY_REQUIREMENT} ({trophyPercent}%)";
            _trophyLabel.ForeColor = Theme.Accent;
            _trophyProgressBar.BarColor = Theme.Accent;
        }

        _uniqueLabel.Text = $"Unique Slots: {uniqueHit} / {uniqueTotal}";
        _totalLabel.Text = $"Total Lines Hit: {totalHit} / {totalCount}";
    }
}

public class ThemedListView : ListView
{
    public ThemedListView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        View = View.Details;
        OwnerDraw = true;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var bg = new SolidBrush(Theme.SurfaceAlt))
            e.Graphics.FillRectangle(bg, e.Bounds);

        using (var pen = new Pen(Theme.Border))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        using var headerFont = new Font(Font, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", headerFont, e.Bounds,
            Theme.SubtleText, AlignmentFlags(e.Header?.TextAlign ?? HorizontalAlignment.Left));
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        // Drawing happens per sub-item in Details view.
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var item = e.Item!;
        Color back = item.Selected ? Theme.Selection : item.BackColor;
        Color fore = item.Selected ? Color.White : item.ForeColor;

        using (var bg = new SolidBrush(back))
            e.Graphics.FillRectangle(bg, e.Bounds);

        var align = e.ColumnIndex < Columns.Count ? Columns[e.ColumnIndex].TextAlign : HorizontalAlignment.Left;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", Font, e.Bounds, fore, AlignmentFlags(align));
    }

    private static TextFormatFlags AlignmentFlags(HorizontalAlignment align)
    {
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.LeftAndRightPadding;
        if (align == HorizontalAlignment.Center) flags |= TextFormatFlags.HorizontalCenter;
        else if (align == HorizontalAlignment.Right) flags |= TextFormatFlags.Right;
        return flags;
    }
}

public class FlatProgressBar : Control
{
    private int _value;
    private int _maximum = 100;

    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(1, value); Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set { _value = Math.Max(0, Math.Min(_maximum, value)); Invalidate(); }
    }

    public Color BarColor { get; set; } = Theme.Accent;
    public Color TrackColor { get; set; } = Theme.SurfaceAlt;

    public FlatProgressBar()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 12;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);

        using (var track = new SolidBrush(TrackColor))
            g.FillRectangle(track, 0, 0, Width, Height);

        int w = (int)((float)_value / _maximum * Width);
        if (w > 0)
        {
            using var bar = new SolidBrush(BarColor);
            g.FillRectangle(bar, 0, 0, w, Height);
        }
    }
}

public class ProcessItem
{
    public Process Process { get; }

    public ProcessItem(Process process)
    {
        Process = process;
    }

    public override string ToString()
    {
        try
        {
            string title = !string.IsNullOrEmpty(Process.MainWindowTitle)
                ? $" - {Process.MainWindowTitle}"
                : "";
            return $"{Process.ProcessName} (PID: {Process.Id}){title}";
        }
        catch
        {
            return $"Process {Process.Id}";
        }
    }
}
