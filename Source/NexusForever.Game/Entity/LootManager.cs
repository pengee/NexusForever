using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Loot;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Loot;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Game.Entity
{
    public class LootManager : ILootManager
    {
        private readonly IPlayer player;
        private readonly Dictionary<uint, List<LootItem>> lootEntries = new();
        private uint nextLootUnitId = 1u;

        public LootManager(IPlayer player)
        {
            this.player = player;
        }

        public void AddLoot(uint ownerGuid, List<LootItem> lootItems)
        {
            foreach (LootItem item in lootItems)
                item.LootUnitId = nextLootUnitId++;

            lootEntries.Add(ownerGuid, lootItems);

            player.Session.EnqueueMessageEncrypted(new ServerLootNotify
            {
                OwnerUnitId = ownerGuid,
                ParentUnitId = 0,
                Explosion = false,
                LootItems = lootItems
            });
        }

        public List<LootItem> GetLoot(uint ownerGuid)
        {
            return lootEntries.TryGetValue(ownerGuid, out List<LootItem> items) ? items : null;
        }

        public void RemoveLoot(uint ownerGuid)
        {
            lootEntries.Remove(ownerGuid);
            player.Session.EnqueueMessageEncrypted(new ServerLootRemove
            {
                OwnerUnitId = ownerGuid
            });
        }

        public void VacuumLoot()
        {
            foreach (uint ownerGuid in lootEntries.Keys.ToList())
            {
                List<LootItem> items = lootEntries[ownerGuid];
                foreach (LootItem item in items.ToList())
                    PickupLootItem(ownerGuid, item);

                RemoveLoot(ownerGuid);
            }
        }

        public void LootItem(uint ownerGuid, uint lootUnitId)
        {
            if (!lootEntries.TryGetValue(ownerGuid, out List<LootItem> items))
                return;

            LootItem item = items.SingleOrDefault(i => i.LootUnitId == lootUnitId);
            if (item == null)
                return;

            PickupLootItem(ownerGuid, item);
            items.Remove(item);

            if (items.Count == 0)
                RemoveLoot(ownerGuid);
        }

        private void PickupLootItem(uint ownerGuid, LootItem item)
        {
            switch (item.Type)
            {
                case LootItemType.StaticItem:
                    player.Inventory.ItemCreate(InventoryLocation.Inventory, item.ItemId, item.Amount, ItemUpdateReason.Loot);
                    player.QuestManager.ObjectiveUpdate(QuestObjectiveType.CollectItem, item.ItemId, item.Amount);
                    player.QuestManager.ObjectiveUpdate(QuestObjectiveType.VirtualCollect, item.ItemId, item.Amount);
                    break;
                case LootItemType.Cash:
                    player.CurrencyManager.CurrencyAddAmount((CurrencyType)item.ItemId, item.Amount, true);
                    break;
            }

            player.Session.EnqueueMessageEncrypted(new ServerLootGrant
            {
                OwnerUnitId = ownerGuid,
                LooterUnitId = player.Guid,
                LootItem = item
            });
        }
    }
}