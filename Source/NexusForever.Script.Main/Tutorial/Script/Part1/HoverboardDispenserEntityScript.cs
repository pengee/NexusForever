using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Shared;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterCreatureId((uint) ObjectiveEntities.HoverboardDispenserEntity, (uint) ObjectiveEntities.HoverboardDispenserEntityOther)]
    public class HoverboardDispenserEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private IWorldEntity owner;

        #region Dependency Injection
        private readonly IFactory<ISpellParameters> spellParameterFactory;
        public HoverboardDispenserEntityScript(IFactory<ISpellParameters> spellParameterFactory)
        {
            this.spellParameterFactory = spellParameterFactory;
        }
        #endregion

        public void OnLoad(IWorldEntity entityOwner) => owner = entityOwner;

        public void OnActivate(IPlayer activator)
        {
            ISpellParameters parameters = spellParameterFactory.Resolve();
            parameters.UserInitiatedSpellCast = false;
            activator.CastSpell((uint) QuestSpells.HoverboardEquip, parameters);

            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.SucceedCSI, owner.CreatureId, 1u);
        }
    }
}