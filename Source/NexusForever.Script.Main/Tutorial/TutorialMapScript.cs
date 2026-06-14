using NexusForever.Game.Abstract.Cinematic;
using NexusForever.Game.Abstract.Cinematic.Cinematics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map.Instance;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial
{
    [ScriptFilterOwnerId(3460)]
    public class TutorialMapScript : IMapScript, IOwnedScript<ITutorialMapInstance>
    {
        #region Dependency Injection

        private readonly ICinematicFactory cinematicFactory;
        private readonly IGlobalQuestManager globalQuestManager;

        public TutorialMapScript(ICinematicFactory cinematicFactory, IGlobalQuestManager globalQuestManager)
        {
            this.cinematicFactory = cinematicFactory;
            this.globalQuestManager = globalQuestManager;
        }

        #endregion

        public void OnAddToMap(IGridEntity entity)
        {
            if (entity is not IPlayer player)
                return;

            player.CinematicManager.QueueCinematic(cinematicFactory.CreateCinematic<INoviceTutorialOnEnter>());

            ushort questId = player.Faction2 == Faction.Exile
                ? (ushort)TutorialReferences.NavigatingNexusExile
                : (ushort)TutorialReferences.NavigatingNexusDominion;

            IQuestInfo questInfo = globalQuestManager.GetQuestInfo(questId);
            if (questInfo != null)
            {
                player.QuestManager.QuestAdd(questInfo);

                // SendInitialPackets was already called during Player.OnAddToMap, before
                // this script fires. Re-sync so the client gets the quest in ServerQuestInit.
                player.QuestManager.SendInitialPackets();

                Console.WriteLine($"Added tutorial quest {questId} for faction {player.Faction2}.");
            }
            else
            {
                Console.Error.WriteLine($"Tutorial quest {questId} not found in game tables.");
            }
        }
    }
}
