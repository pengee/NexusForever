using NexusForever.Script.Template.Filter;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.AI
{
    public enum CombatDummies : uint
    {
        NPEU = 75108,
        GlobalOne = 3867,
        GlobalTwo = 3983,
        GlobalThree = 4954,
        GlobalFour = 9652,
        HumanGlobal = 6781,
        WalkingGlobal = 8617,
        HousingDummy = 25282
    }
    
    /// <summary>
   /// Some combat dummies have spells / cast times
   /// Some combat dummies move or allow for being interrupted
   /// </summary>
    [ScriptFilterCreatureId(
        (uint)CombatDummies.NPEU, 
        (uint)CombatDummies.GlobalOne, 
        (uint)CombatDummies.GlobalTwo, 
        (uint)CombatDummies.GlobalThree, 
        (uint)CombatDummies.GlobalFour, 
        (uint)CombatDummies.HumanGlobal, 
        (uint)CombatDummies.WalkingGlobal, 
        (uint)CombatDummies.HousingDummy)]
    public class DefaultCombatDummyScript : IUnitScript, IOwnedScript<ICreatureEntity>
    {
        private ICreatureEntity owner;

        private const float UpdateInterval = 1f;
        private double lastUpdate;

        public void OnLoad(ICreatureEntity entityOwner)
        {
            this.owner = entityOwner;
        }

        public void OnThreatAddTarget(IHostileEntity hostile) { }

        public void OnThreatRemoveTarget(IHostileEntity hostile) { }

        public void OnThreatChange(IHostileEntity hostile) { }

        public void Update(double lastTick)
        {
            //gate update frequency
            if(!UpdateHealth()){ return; }
            
            //update health
            if (owner.Health < owner.MaxHealth)
            {
                owner.ModifyHealth((uint) MathF.Round(owner.MaxHealth * 0.1f), DamageType.Heal, owner);
            }
        }

        private bool UpdateHealth()
        {
            double currentTime = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
            if (!(currentTime - lastUpdate > UpdateInterval)) return false;
            lastUpdate = currentTime;
            return true;
        }
    }
}
