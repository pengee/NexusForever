using NexusForever.Game.Abstract.Combat;

namespace NexusForever.Script.Template
{
    public interface IUnitScript : IWorldEntityScript
    {
        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        void OnThreatAddTarget(IHostileEntity hostile)
        {
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        void OnThreatRemoveTarget(IHostileEntity hostile)
        {
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        void OnThreatChange(IHostileEntity hostile)
        {
        }

        /// <summary>
        /// Invoked after the owner unit has died and its threat list / state has been cleaned up.
        /// Use this to reset any script-local combat state that would otherwise persist into the
        /// next life (cached target reference, leashing flag, cooldowns, etc.).
        /// </summary>
        void OnDeath()
        {
        }

        /// <summary>
        /// Invoked after the owner unit has finished respawning (health restored, position
        /// teleported to leash, movement state reset). Use this as a defensive reset hook
        /// in case <see cref="OnDeath"/> was bypassed (e.g. the AI was disabled at the time
        /// of death).
        /// </summary>
        void OnRespawn()
        {
        }
    }
}
