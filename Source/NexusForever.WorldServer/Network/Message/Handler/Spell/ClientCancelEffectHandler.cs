using System.Numerics;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Spell
{
    public class ClientCancelEffectHandler : IMessageHandler<IWorldSession, ClientCancelEffect>
    {
        public void HandleMessage(IWorldSession session, ClientCancelEffect cancelSpell)
        {
            session.Player.BuffManager.RemoveBuff(cancelSpell.ServerUniqueId);
            
            IBuff buff = session.Player.BuffManager.GetBuff(cancelSpell.ServerUniqueId);
            
            if (buff?.EffectEntry.EffectType == SpellEffectType.SummonMount)
            {
                session.Player.Dismount();
                
                // this is the tutorial hoverboard
                // player dispel event -> teleport to race start location
                if (buff.SpellInfo.Entry.Id == 85562)
                {
                    session.Player.TeleportToLocal(new Vector3(57.777f, -848.5501f, -504.3975f));
                }
            }
            session.Player.BuffManager.RemoveBuff(cancelSpell.ServerUniqueId);
        }
    }
}