namespace Devilution.Server.Stores;

/** Versioned server-owned save envelope using the generated player and world state shape. */
public sealed record AuthoritativeSaveDocument(int FormatVersion, string SnapshotBase64);

/** Supplies serialized authoritative player saves to the server host. */
public interface IAuthoritativeSaveProvider
{
    string ExportPlayerSave(string sessionId);

    void ImportPlayerSave(string sessionId, string serializedSave);
}
