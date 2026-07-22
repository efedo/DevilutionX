using Devilution.Protocol.V1;

namespace Devilution.Server.Snapshots;

/** Hashes the authoritative fields currently exposed by Snapshot. */
public static class SnapshotStateHasher
{
    public static string Compute(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasher = new CanonicalStateHasher();
        var players = snapshot.Players.OrderBy(player => player.EntityId).ToArray();
        hasher.AppendUInt64((ulong)players.Length);
        foreach (var player in players) {
            hasher.AppendUInt32(player.EntityId);
            hasher.AppendInt32(player.PositionX);
            hasher.AppendInt32(player.PositionY);
            hasher.AppendInt32(player.Life);
            hasher.AppendInt32(player.Mana);
            hasher.AppendUInt32(player.Gold);
            hasher.AppendUInt32(player.Experience);
            hasher.AppendUInt32(player.ActiveStoreId);

            var attributes = player.Attributes ?? new PlayerAttributesSnapshot();
            AppendAttribute(hasher, attributes.Strength);
            AppendAttribute(hasher, attributes.Magic);
            AppendAttribute(hasher, attributes.Dexterity);
            AppendAttribute(hasher, attributes.Vitality);

            var equipment = player.Equipment
                .OrderBy(item => item.Slot)
                .ThenBy(item => item.ItemSeed)
                .ToArray();
            hasher.AppendUInt64((ulong)equipment.Length);
            foreach (var item in equipment) {
                hasher.AppendUInt32(item.Slot);
                hasher.AppendUInt32(item.ItemSeed);
                AppendItemState(hasher, item.State);
            }

            var inventory = player.Inventory
                .OrderBy(item => item.StoreId)
                .ThenBy(item => item.StoreSlot)
                .ThenBy(item => item.ItemSeed)
                .ThenBy(item => item.Price)
                .ThenBy(item => item.PurchasedAtTick)
                .ToArray();
            hasher.AppendUInt64((ulong)inventory.Length);
            foreach (var item in inventory) {
                hasher.AppendUInt32(item.StoreId);
                hasher.AppendUInt32(item.StoreSlot);
                hasher.AppendUInt32(item.ItemSeed);
                hasher.AppendUInt32(item.Price);
                hasher.AppendUInt64(item.PurchasedAtTick);
                AppendItemState(hasher, item.State);
            }

            hasher.AppendUInt64((ulong)player.InventoryGrid.Count);
            foreach (var cell in player.InventoryGrid)
                hasher.AppendInt32(cell);
        }

        return hasher.HexDigest();
    }

    private static void AppendAttribute(CanonicalStateHasher hasher, AttributeSnapshot? attribute)
    {
        hasher.AppendInt32(attribute?.Base ?? 0);
        hasher.AppendInt32(attribute?.Current ?? 0);
    }

    private static void AppendItemState(CanonicalStateHasher hasher, ItemStateSnapshot? state)
    {
        state ??= new ItemStateSnapshot { ItemType = -1, ItemIndex = -1 };
        hasher.AppendUInt32(state.CreateInfo);
        hasher.AppendInt32(state.ItemType);
        hasher.AppendInt32(state.PositionX);
        hasher.AppendInt32(state.PositionY);
        hasher.AppendBool(state.Deleted);
        hasher.AppendBool(state.Identified);
        hasher.AppendInt32(state.Magical);
        hasher.AppendInt32(state.EquipLocation);
        hasher.AppendInt32(state.ItemClass);
        hasher.AppendInt32(state.Value);
        hasher.AppendInt32(state.IdentifiedValue);
        hasher.AppendInt32(state.MinDamage);
        hasher.AppendInt32(state.MaxDamage);
        hasher.AppendInt32(state.ArmorClass);
        hasher.AppendUInt32(state.Flags);
        hasher.AppendInt32(state.MiscId);
        hasher.AppendInt32(state.SpellId);
        hasher.AppendInt32(state.ItemIndex);
        hasher.AppendInt32(state.Charges);
        hasher.AppendInt32(state.MaxCharges);
        hasher.AppendInt32(state.Durability);
        hasher.AppendInt32(state.MaxDurability);
        hasher.AppendInt32(state.PlusDamage);
        hasher.AppendInt32(state.PlusToHit);
        hasher.AppendInt32(state.PlusArmorClass);
        hasher.AppendInt32(state.PlusStrength);
        hasher.AppendInt32(state.PlusMagic);
        hasher.AppendInt32(state.PlusDexterity);
        hasher.AppendInt32(state.PlusVitality);
        hasher.AppendInt32(state.PlusFireResistance);
        hasher.AppendInt32(state.PlusLightningResistance);
        hasher.AppendInt32(state.PlusMagicResistance);
        hasher.AppendInt32(state.PlusMana);
        hasher.AppendInt32(state.PlusHitPoints);
        hasher.AppendInt32(state.PlusDamageModifier);
        hasher.AppendInt32(state.PlusGetHit);
        hasher.AppendInt32(state.PlusLight);
        hasher.AppendInt32(state.SpellLevelAdd);
        hasher.AppendInt32(state.UniqueId);
        hasher.AppendInt32(state.FireMinDamage);
        hasher.AppendInt32(state.FireMaxDamage);
        hasher.AppendInt32(state.LightningMinDamage);
        hasher.AppendInt32(state.LightningMaxDamage);
        hasher.AppendInt32(state.PlusEnemyArmorClass);
        hasher.AppendInt32(state.PrefixPower);
        hasher.AppendInt32(state.SuffixPower);
        hasher.AppendInt32(state.ValueAdd1);
        hasher.AppendInt32(state.ValueMultiply1);
        hasher.AppendInt32(state.ValueAdd2);
        hasher.AppendInt32(state.ValueMultiply2);
        hasher.AppendInt32(state.MinimumStrength);
        hasher.AppendInt32(state.MinimumMagic);
        hasher.AppendInt32(state.MinimumDexterity);
        hasher.AppendBool(state.StatFlag);
        hasher.AppendInt32(state.HellfireDamageArmorFlags);
        hasher.AppendUInt32(state.Buff);
    }
}
