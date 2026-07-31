using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

public partial class Main : Node2D
{
    private readonly AuthoritativeClientModel model = new();
    private AuthoritativeClient? client;
    private WorldPresentation? world;
    private HudPresentation? hud;
    private PresentationFeedback? feedback;
    private Task? connectionTask;
    private Task? pollTask;

    public override void _Ready()
    {
        world = new WorldPresentation();
        AddChild(world);
        hud = new HudPresentation();
        AddChild(hud);
        feedback = new PresentationFeedback();
        AddChild(feedback);
        hud.OpenStoreRequested += storeId => QueueIfConnected(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = storeId } });
        hud.PurchaseRequested += (storeId, storeSlot) => QueueIfConnected(new Command { PurchaseRequested = new PurchaseRequested { StoreId = storeId, StoreSlot = storeSlot } });
        hud.SellItemRequested += inventoryIndex => QueueIfConnected(new Command { SellItemRequested = new SellItemRequested { InventoryIndex = inventoryIndex } });
        hud.RepairItemRequested += inventoryIndex => QueueIfConnected(new Command { RepairItemRequested = new RepairItemRequested { InventoryIndex = inventoryIndex } });
        hud.RechargeItemRequested += inventoryIndex => QueueIfConnected(new Command { RechargeItemRequested = new RechargeItemRequested { InventoryIndex = inventoryIndex } });
        hud.IdentifyItemRequested += inventoryIndex => QueueIfConnected(new Command { IdentifyItemRequested = new IdentifyItemRequested { InventoryIndex = inventoryIndex } });
        hud.MoveInventoryItemRequested += (inventoryIndex, targetCell) => QueueIfConnected(new Command { MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = inventoryIndex, TargetCell = targetCell } });
        hud.OperateObjectRequested += entityId => QueueIfConnected(new Command { OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = entityId } });
        hud.AdvanceQuestRequested += questId => QueueIfConnected(new Command { AdvanceQuestRequested = new AdvanceQuestRequested { QuestId = questId } });
        hud.RefillManaRequested += () => QueueIfConnected(new Command { RefillManaRequested = new RefillManaRequested() });
        hud.ClearEventsRequested += model.ClearRecentEvents;
        client = new AuthoritativeClient(ClientConnectionOptions.FromEnvironment());
        connectionTask = ConnectAsync();
        hud.SetStatus("Connecting to authoritative server...");
    }

    public override void _Process(double delta)
    {
        if (client is not null) {
            foreach (var message in client.DrainMessages()) {
                try {
                    model.Apply(message);
                } catch (Exception exception) {
                    hud?.SetStatus($"Invalid authoritative state: {exception.Message}");
                }
            }

            if (client.IsConnected && (pollTask is null || pollTask.IsCompleted))
                pollTask = client.PollAsync();
        }

        world?.Apply(model.Snapshot, delta, model.PredictedPlayerPosition);
        hud?.Apply(model, client, connectionTask);
        feedback?.Apply(model);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey toggle && toggle.Pressed && !toggle.Echo) {
            if (toggle.Keycode == Key.F3) {
                hud?.ToggleDiagnostics();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (toggle.Keycode == Key.F4) {
                hud?.ToggleHighContrast();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && client?.IsConnected == true) {
            var direction = key.Keycode switch {
                Key.Up or Key.W => Vector2I.Up,
                Key.Down or Key.S => Vector2I.Down,
                Key.Left or Key.A => Vector2I.Left,
                Key.Right or Key.D => Vector2I.Right,
                _ => Vector2I.Zero,
            };
            if (direction != Vector2I.Zero)
                Queue(new Command { MoveRequested = new MoveRequested { DirectionX = direction.X, DirectionY = direction.Y } }, direction);

            if (key.Keycode == Key.O)
                Queue(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = 1 } });
            if (key.Keycode == Key.P)
                Queue(new Command { PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 } });
            if (key.Keycode == Key.M)
                Queue(new Command { OpenStoreRequested = new OpenStoreRequested { StoreId = 10 } });
            if (key.Keycode == Key.R)
                QueueIfConnected(new Command { RefillManaRequested = new RefillManaRequested() });
        }

        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && client?.IsConnected == true) {
            var target = world?.ScreenToTile(mouse.Position) ?? Vector2I.Zero;
            world?.SetTargetPreview(target);
            Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 4,
                    TargetX = target.X,
                    TargetY = target.Y,
                },
            });
        }
    }

    public override async void _ExitTree()
    {
        if (client is not null)
            await client.DisposeAsync();
    }

    private async Task ConnectAsync()
    {
        try {
            await client!.ConnectAsync();
        } catch (Exception exception) {
            model.Apply(new AuthoritativeClientMessage(Error: new ProtocolError {
                Code = ProtocolErrorCode.NotAuthenticated,
                Detail = exception.Message,
            }));
        }
    }

    private void Queue(Command command, Vector2I? predictedDirection = null)
    {
        var sequence = client!.Queue(command, client.SuggestedCommandTick(model.CurrentTick));
        if (predictedDirection is { } direction)
            model.TrackPredictedMove(sequence, direction.X, direction.Y);
    }

    private void QueueIfConnected(Command command)
    {
        if (client?.IsConnected == true)
            Queue(command);
    }
}
