using Devilution.Server.Snapshots;

namespace Devilution.Server.Content;

/** One ordered external content pack and its validated tables. */
public sealed record ContentPack
{
    public ContentPack(string id, string version, IReadOnlyList<TsvTable> tables)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Content pack IDs and versions are required.");
        Id = id;
        Version = version;
        Tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    public string Id { get; }

    public string Version { get; }

    public IReadOnlyList<TsvTable> Tables { get; }
}

/** Deterministic identity for the ordered content set active on a server. */
public sealed class ContentManifest
{
    public ContentManifest(string id, string version, IEnumerable<ContentPack> packs)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A content manifest ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("A content manifest version is required.", nameof(version));
        ArgumentNullException.ThrowIfNull(packs);

        Id = id;
        Version = version;
        Packs = packs.ToArray();
        if (Packs.Count == 0)
            throw new ArgumentException("A content manifest must contain at least one pack.", nameof(packs));
        if (Packs.Select(pack => pack.Id).Distinct(StringComparer.Ordinal).Count() != Packs.Count)
            throw new ArgumentException("Content pack IDs must be unique.", nameof(packs));
        Sha256 = ComputeHash();
    }

    public string Id { get; }

    public string Version { get; }

    public IReadOnlyList<ContentPack> Packs { get; }

    public string Sha256 { get; }

    private string ComputeHash()
    {
        var hasher = new CanonicalStateHasher();
        hasher.AppendString(Id);
        hasher.AppendString(Version);
        hasher.AppendUInt64((ulong)Packs.Count);
        foreach (var pack in Packs) {
            hasher.AppendString(pack.Id);
            hasher.AppendString(pack.Version);
            hasher.AppendUInt64((ulong)pack.Tables.Count);
            foreach (var table in pack.Tables) {
                hasher.AppendString(table.SourcePath);
                hasher.AppendUInt64((ulong)table.Columns.Count);
                foreach (var column in table.Columns)
                    hasher.AppendString(column);
                hasher.AppendUInt64((ulong)table.Rows.Count);
                foreach (var row in table.Rows) {
                    hasher.AppendUInt64((ulong)row.LineNumber);
                    foreach (var column in table.Columns)
                        hasher.AppendString(row[column]);
                }
            }
        }
        return hasher.HexDigest();
    }
}
