namespace Devilution.Server.Content;

/** Loads an ordered directory of UTF-8 TSV files into a content pack. */
public static class ContentPackLoader
{
    public static ContentPack LoadDirectory(string id, string version, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Content pack directory '{rootDirectory}' does not exist.");

        var tables = Directory.EnumerateFiles(root, "*.tsv", SearchOption.AllDirectories)
            .Select(path => new {
                FullPath = path,
                RelativePath = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => TsvTable.Parse(
                file.RelativePath,
                File.ReadAllText(file.FullPath)))
            .ToArray();
        if (tables.Length == 0)
            throw new InvalidDataException($"Content pack directory '{rootDirectory}' contains no TSV tables.");
        return new ContentPack(id, version, tables);
    }
}
