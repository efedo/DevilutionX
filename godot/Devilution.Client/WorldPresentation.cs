using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Asset-backed world view driven exclusively by authoritative snapshots. */
public partial class WorldPresentation : Node2D
{
    private const float TileSize = 32;
    private const float OriginX = 96;
    private const float OriginY = 96;
    private readonly Dictionary<uint, Vector2> displayedPositions = [];
    private readonly AssetPalette assets = new();
    private readonly LevelLayoutCatalog levels = new();
    private Snapshot? snapshot;
    private ClientPosition? predictedPlayerPosition;
    private LevelLayout currentLevel = LevelLayout.Empty;

    public void Apply(Snapshot? next, double delta, ClientPosition? predicted = null)
    {
        snapshot = next;
        predictedPlayerPosition = predicted;
        if (snapshot is null)
            return;

        currentLevel = levels.Resolve(snapshot.Players.SingleOrDefault()?.LevelId ?? 0);

        foreach (var player in snapshot.Players)
            UpdatePosition(player.EntityId, ToScreen(predicted?.X ?? player.PositionX, predicted?.Y ?? player.PositionY), delta);
        foreach (var monster in snapshot.Monsters)
            UpdatePosition(monster.EntityId, ToScreen(monster.PositionX, monster.PositionY), delta);
        foreach (var item in snapshot.WorldItems)
            UpdatePosition(item.EntityId, ToScreen(item.PositionX, item.PositionY), delta);
        foreach (var @object in snapshot.Objects)
            UpdatePosition(@object.EntityId, ToScreen(@object.PositionX, @object.PositionY), delta);
        foreach (var projectile in snapshot.Projectiles)
            UpdatePosition(projectile.EntityId, ToScreen(projectile.PositionX, projectile.PositionY), delta);
        QueueRedraw();
    }

    public Vector2I ScreenToTile(Vector2 screenPosition)
    {
        return new Vector2I(
            Mathf.FloorToInt((screenPosition.X - OriginX) / TileSize),
            Mathf.FloorToInt((screenPosition.Y - OriginY) / TileSize));
    }

    public override void _Draw()
    {
        DrawGrid();
        if (snapshot is null || snapshot.Players.Count != 1)
            return;

        var player = snapshot.Players[0];
        var currentLevel = player.LevelId;
        DrawPlayer(player, predictedPlayerPosition);
        foreach (var monster in snapshot.Monsters.Where(monster => IsVisible(monster.LevelId, currentLevel) && monster.Alive))
            DrawMonster(monster);
        foreach (var item in snapshot.WorldItems.Where(item => IsVisible(item.LevelId, currentLevel)))
            DrawItem(item);
        foreach (var @object in snapshot.Objects.Where(@object => IsVisible(@object.LevelId, currentLevel)))
            DrawObject(@object);
        foreach (var projectile in snapshot.Projectiles.Where(projectile => IsVisible(projectile.LevelId, currentLevel)))
            DrawProjectile(projectile);
    }

    private void DrawGrid()
    {
        DrawRect(new Rect2(0, 0, 1280, 720), new Color("090914"));
        if (assets.Get("floor") is { } floor) {
            for (var x = 0; x < currentLevel.Width; x++)
                for (var y = 0; y < currentLevel.Height; y++)
                    if (!currentLevel.IsBlocked(x, y))
                        DrawTextureRect(floor, new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize), false);
        }
        for (var x = 0; x <= currentLevel.Width; x++)
            DrawLine(new Vector2(OriginX + x * TileSize, OriginY), new Vector2(OriginX + x * TileSize, OriginY + currentLevel.Height * TileSize), new Color("17172b"));
        for (var y = 0; y <= currentLevel.Height; y++)
            DrawLine(new Vector2(OriginX, OriginY + y * TileSize), new Vector2(OriginX + currentLevel.Width * TileSize, OriginY + y * TileSize), new Color("17172b"));
        foreach (var cell in currentLevel.Blocked) {
            var x = cell % currentLevel.Width;
            var y = cell / currentLevel.Width;
            DrawRect(new Rect2(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize), new Color("202033"));
        }
    }

    private void DrawPlayer(PlayerSnapshot player, ClientPosition? predicted)
    {
        var center = predicted is { } position
            ? ToScreen(position.X, position.Y)
            : PositionFor(player.EntityId, player.PositionX, player.PositionY);
        DrawTextureOrCircle("player", center, new Vector2(26, 26), new Color("4da6ff"), 11);
    }

    private void DrawMonster(MonsterSnapshot monster)
    {
        DrawTextureOrRect("monster", PositionFor(monster.EntityId, monster.PositionX, monster.PositionY), new Vector2(24, 24), new Color("d95151"));
    }

    private void DrawItem(WorldItemSnapshot item)
    {
        DrawTextureOrRect("item", PositionFor(item.EntityId, item.PositionX, item.PositionY), new Vector2(18, 18), new Color("e5bd52"));
    }

    private void DrawObject(ObjectSnapshot @object)
    {
        var color = @object.Activated ? new Color("4b4b62") : new Color("a68bca");
        DrawTextureOrRect("object", PositionFor(@object.EntityId, @object.PositionX, @object.PositionY), new Vector2(22, 22), color, true);
    }

    private void DrawProjectile(ProjectileSnapshot projectile)
    {
        DrawTextureOrCircle("projectile", PositionFor(projectile.EntityId, projectile.PositionX, projectile.PositionY), new Vector2(12, 12), new Color("6fffe9"), 5);
    }

    private void DrawTextureOrRect(string assetName, Vector2 center, Vector2 size, Color fallback, bool outline = false)
    {
        if (assets.Get(assetName) is { } texture) {
            DrawTextureRect(texture, new Rect2(center - size / 2, size), false, fallback);
            return;
        }

        DrawRect(new Rect2(center - size / 2, size), fallback, !outline, outline ? 2 : -1);
    }

    private void DrawTextureOrCircle(string assetName, Vector2 center, Vector2 size, Color fallback, float radius)
    {
        if (assets.Get(assetName) is { } texture) {
            DrawTextureRect(texture, new Rect2(center - size / 2, size), false, fallback);
            return;
        }

        DrawCircle(center, radius, fallback);
    }

    private Vector2 PositionFor(uint entityId, int x, int y)
    {
        return displayedPositions.TryGetValue(entityId, out var position) ? position : ToScreen(x, y);
    }

    private void UpdatePosition(uint entityId, Vector2 target, double delta)
    {
        if (!displayedPositions.TryGetValue(entityId, out var current))
            displayedPositions[entityId] = target;
        else
            displayedPositions[entityId] = current.Lerp(target, 1.0f - Mathf.Exp((float)(-delta * 16)));
    }

    private static Vector2 ToScreen(int x, int y) => new(OriginX + (x + 0.5f) * TileSize, OriginY + (y + 0.5f) * TileSize);

    private static bool IsVisible(uint levelId, uint currentLevel) => levelId == 0 || levelId == currentLevel;
}
