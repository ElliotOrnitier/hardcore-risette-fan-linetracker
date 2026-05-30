using System.Reflection;

namespace RiseLineTracker;

public class RiseLine
{
    public int Id { get; set; }
    public string IdHex { get; set; } = "";
    public string InternalId { get; set; } = "";
    public int Index { get; set; }
    public int ByteOffset { get; set; }
    public int BitPosition { get; set; }
    public string Name { get; set; } = "";
    public bool IsHit { get; set; }

    // True if this line's idx slot is already filled by another hit line
    public bool IsRedundant { get; set; }

    // Count of lines sharing this idx
    public int SharedCount { get; set; } = 1;

    public static List<RiseLine> LoadFromTsv(string filePath)
    {
        return LoadFromLines(File.ReadAllLines(filePath));
    }

    /// <summary>
    /// Loads the rise.tsv that is embedded in the application assembly,
    /// so the data ships inside the exe with no loose file required.
    /// </summary>
    public static List<RiseLine> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("rise.tsv", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new InvalidOperationException("Embedded rise.tsv resource was not found in the assembly.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var fileLines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
            fileLines.Add(line);

        return LoadFromLines(fileLines.ToArray());
    }

    private static List<RiseLine> LoadFromLines(string[] fileLines)
    {
        var lines = new List<RiseLine>();

        // Skip header
        for (int i = 1; i < fileLines.Length; i++)
        {
            var parts = fileLines[i].Split('\t');
            if (parts.Length >= 7)
            {
                var line = new RiseLine
                {
                    Id = int.Parse(parts[0]),
                    IdHex = parts[1],
                    InternalId = parts[2],
                    Index = int.Parse(parts[3]),
                    ByteOffset = int.Parse(parts[4]),
                    BitPosition = int.Parse(parts[5]),
                    Name = parts[6]
                };

                // Only include lines with index <= 479 (as per CT file logic)
                if (line.Index <= 479)
                {
                    lines.Add(line);
                }
            }
        }

        // Calculate SharedCount for each line (how many lines share the same idx)
        var indexGroups = lines.GroupBy(l => l.Index).ToDictionary(g => g.Key, g => g.Count());
        foreach (var line in lines)
        {
            line.SharedCount = indexGroups[line.Index];
        }

        return lines;
    }

    public static int GetUniqueIndexCount(List<RiseLine> lines)
    {
        return lines.Select(l => l.Index).Distinct().Count();
    }

    public static int GetHitUniqueIndexCount(List<RiseLine> lines)
    {
        return lines.Where(l => l.IsHit).Select(l => l.Index).Distinct().Count();
    }

    public static void UpdateRedundantStatus(List<RiseLine> lines)
    {
        // Group by index and find which indices have at least one hit
        var hitIndices = new HashSet<int>(
            lines.Where(l => l.IsHit).Select(l => l.Index)
        );

        foreach (var line in lines)
        {
            line.IsRedundant = !line.IsHit && hitIndices.Contains(line.Index);
        }
    }
}
