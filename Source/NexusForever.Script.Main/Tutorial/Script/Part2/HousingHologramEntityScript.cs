using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Shared;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    [ScriptFilterCreatureId((uint) ObjectiveEntities.HousingHologramProjector)]
    public class HousingHologramEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private IWorldEntity owner;

        public void OnLoad(IWorldEntity entityOwner) => owner = entityOwner;
        
        #region Dependency Injection
        private readonly IFactory<ISpellParameters> spellParameterFactory;

        public HousingHologramEntityScript(IFactory<ISpellParameters> spellParameterFactory)
        {
            this.spellParameterFactory = spellParameterFactory;
        }
        #endregion
        
        public void OnActivate(IPlayer activator)
        {
            ISpellParameters parameters = spellParameterFactory.Resolve();
            parameters.UserInitiatedSpellCast = false;
            activator.CastSpell((uint) QuestSpells.Housingleport, parameters);

            //activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.SucceedCSI, owner.CreatureId, 1u);
        }
    }
}