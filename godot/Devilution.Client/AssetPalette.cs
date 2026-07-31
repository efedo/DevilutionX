using Godot;

namespace Devilution.Client;

/** Loads replaceable presentation assets while retaining a procedural fallback. */
public sealed class AssetPalette
{
    private readonly Dictionary<string, Texture2D?> textures = [];

    public Texture2D? Get(string assetName)
    {
        if (textures.TryGetValue(assetName, out var texture))
            return texture;

        texture = ResourceLoader.Load<Texture2D>($"res://assets/{assetName}.svg");
        textures[assetName] = texture;
        return texture;
    }
}
