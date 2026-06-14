using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Shared;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterScriptName("ObjectiveRingEntityScript")]
    public class ObjectiveRingEntityScript : NavigatingNexusSequentialEntityScript, IWorldEntityScript
    {
        private const float Distance = 5f;
        
        #region Dependency Injection

        private readonly IFactory<ISpellParameters> spellParameterFactory;

        public ObjectiveRingEntityScript(IFactory<ISpellParameters> spellParameterFactory)
        {
            this.spellParameterFactory = spellParameterFactory;
        }

        #endregion

        public override void OnLoad(IWorldEntity entityOwner)
        {
            base.OnLoad(entityOwner);
            entityOwner.SetInRangeCheck(Distance);
        }

        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer player)
                return;

            uint data = Owner.QuestChecklistIdx switch
            {
                0 => (uint)QuestObjectives.AreaPressurePlateOne,
                1 => (uint)QuestObjectives.AreaPressurePlateTwo,
                2 => (uint)QuestObjectives.AreaPressurePlateThree,
                _ => uint.MaxValue
            };

            if (data >= uint.MaxValue) return;
            
            //Console.Error.WriteLine($"OnEnterRange: ring idx={Owner.QuestChecklistIdx}, data={data}");

            // ushort questId = player.Faction2 == Faction.Exile
            //     ? (ushort)QuestReferences.NavigatingNexusExile
            //     : (ushort)QuestReferences.NavigatingNexusDominion;

            //IQuest quest = player.QuestManager.GetActiveQuest(questId);
            //Console.Error.WriteLine($"OnEnterRange: active quest {questId} found={quest != null}");

            // Quest2Entry questEntry = GameTableManager.Instance.Quest2.GetEntry(questId);
            // if (questEntry != null)
            // {
            //     foreach (uint objectiveId in questEntry.Objectives.Where(o => o != 0u))
            //     {
            //         QuestObjectiveEntry objEntry = GameTableManager.Instance.QuestObjective.GetEntry(objectiveId);
            //         if (objEntry != null && objEntry.Type == (uint)QuestObjectiveType.EnterArea)
            //         {
            //             Console.Error.WriteLine($"  EnterArea: objId={objEntry.Id}, data={objEntry.Data}");
            //         }
            //     }
            // }

            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.EnterArea, data, 1);

            ISpellParameters parameters = spellParameterFactory.Resolve();
            parameters.UserInitiatedSpellCast = false;
            player.CastSpell((uint) QuestSpells.ClothesSpawnIn, parameters);
        }
    }
}
