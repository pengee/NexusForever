using NexusForever.Game.Abstract.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Loot;

namespace NexusForever.WorldServer.Network.Message.Handler.Loot
{
    public class ClientLootItemHandler : IMessageHandler<IWorldSession, ClientLootItem>
    {
        public void HandleMessage(IWorldSession session, ClientLootItem lootItem)
        {
            session.Player.LootManager.LootItem(lootItem.OwnerUnitId, lootItem.LootUnitId);
        }
    }
}