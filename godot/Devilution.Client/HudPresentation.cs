using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Authoritative status, inventory, store, and event-dialog presentation. */
public partial class HudPresentation : CanvasLayer
{
    private readonly Label status = new();
    private readonly Label state = new();
    private readonly Label inventory = new();
    private readonly Label store = new();
    private readonly Label dialog = new();

    public override void _Ready()
    {
        var statusPanel = new PanelContainer { Position = new Vector2(16, 16), Size = new Vector2(520, 100) };
        var statusBox = new VBoxContainer();
        statusPanel.AddChild(statusBox);
        statusBox.AddChild(status);
        statusBox.AddChild(state);
        AddChild(statusPanel);

        var sidePanel = new PanelContainer { Position = new Vector2(880, 16), Size = new Vector2(380, 660) };
        var sideBox = new VBoxContainer();
        sidePanel.AddChild(sideBox);
        sideBox.AddChild(new Label { Text = "AUTHORITATIVE CLIENT" });
        sideBox.AddChild(inventory);
        sideBox.AddChild(store);
        sideBox.AddChild(dialog);
        AddChild(sidePanel);

        status.Text = "Starting...";
        inventory.Text = "Inventory\nWaiting for authoritative snapshot";
        store.Text = "Store\nNo active store";
        dialog.Text = "Events\nNone";
    }

    public void SetStatus(string value)
    {
        status.Text = value;
    }

    public void Apply(AuthoritativeClientModel model, AuthoritativeClient? client, Task? connectionTask)
    {
        if (!string.IsNullOrWhiteSpace(model.LastError))
            status.Text = model.LastError;
        else if (client?.IsConnected == true)
            status.Text = $"Connected - retry {client.RetryTimeout.TotalMilliseconds:0} ms";
        else if (connectionTask is { IsFaulted: true })
            status.Text = connectionTask.Exception?.GetBaseException().Message ?? "Connection failed";

        var player = model.Snapshot?.Players.SingleOrDefault();
        state.Text = player is null
            ? "Waiting for authoritative snapshot"
            : $"Tick {model.CurrentTick}   Level {player.LevelId}   Life {player.Life}/{player.LifeMaximum}   Mana {player.Mana}/{player.ManaMaximum}   Gold {player.Gold}   XP {player.Experience}";

        inventory.Text = FormatInventory(player);
        store.Text = FormatStore(model.Snapshot?.ActiveStore);
        dialog.Text = FormatEvents(model.RecentEvents);
    }

    private static string FormatInventory(PlayerSnapshot? player)
    {
        if (player is null)
            return "Inventory\nWaiting for authoritative snapshot";
        if (player.Inventory.Count == 0)
            return "Inventory\n(empty)";

        var lines = new List<string> { $"Inventory ({player.Inventory.Count})" };
        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            lines.Add($"[{index}] seed {item.ItemSeed} value {item.State.Value} " +
                $"{item.State.InventoryWidth}x{item.State.InventoryHeight}");
        }
        return string.Join("\n", lines);
    }

    private static string FormatStore(StoreSnapshot? activeStore)
    {
        if (activeStore is null || activeStore.StoreId == 0)
            return "Store\nNo active store";
        if (activeStore.Items.Count == 0)
            return $"Store {activeStore.StoreId}\n(empty)";

        var lines = new List<string> { $"Store {activeStore.StoreId}" };
        lines.AddRange(activeStore.Items.Select(item => $"slot {item.StoreSlot}: seed {item.ItemSeed} price {item.Price}"));
        return string.Join("\n", lines);
    }

    private static string FormatEvents(IReadOnlyList<GameEvent> events)
    {
        if (events.Count == 0)
            return "Events\nNone";

        var lines = new List<string> { "Events" };
        foreach (var @event in events.TakeLast(5)) {
            lines.Add(@event.EventCase switch {
                GameEvent.EventOneofCase.Damage => $"Damage { @event.Damage.Amount } -> entity {@event.Damage.TargetEntityId}",
                GameEvent.EventOneofCase.Experience => $"XP +{@event.Experience.Amount}",
                GameEvent.EventOneofCase.Healing => $"Healing +{@event.Healing.Amount}",
                _ => "Authoritative event",
            });
        }
        return string.Join("\n", lines);
    }
}
