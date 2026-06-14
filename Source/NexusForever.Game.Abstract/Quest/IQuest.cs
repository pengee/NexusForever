using NexusForever.Database;
using NexusForever.Database.Character;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Network.World.Message.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Quest
{
    public interface IQuest : IDisposable, IUpdate, IDatabaseCharacter, IDatabaseState, IEnumerable<IQuestObjective>
    {
        ushort Id { get; }
        IQuestInfo Info { get; }
        QuestState State { get; set; }
        QuestStateFlags Flags { get; set; }
        uint? Timer { get; set; }
        DateTime? Reset { get; set; }

        void InitialiseTimer();

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be deleted.
        /// </summary>
        bool CanDelete();

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be abandoned.
        /// </summary>
        bool CanAbandon();

        /// <summary>
        /// Returns if <see cref="IQuest"/> can be shared with another <see cref="IPlayer"/>.
        /// </summary>
        bool CanShare();

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied <see cref="QuestObjectiveType"/> and data with progress.
        /// </summary>
        void ObjectiveUpdate(QuestObjectiveType type, uint data, uint progress);

        /// <summary>
        /// Update any <see cref="IQuestObjective"/>'s with supplied ID with progress.
        /// </summary>
        void ObjectiveUpdate(uint id, uint progress);

        /// <summary>
        /// Send a <see cref="ServerQuestLuaEvent"/> to the client with the supplied event ID and data.
        /// </summary>
        void SendLuaEvent(ushort luaEventId, params ServerQuestLuaEvent.ILuaEventData[] luaEvents);

        /// <summary>
        /// Send a <see cref="ServerQuestLuaEvent"/> to the client with the supplied event ID and data.
        /// </summary>
        void SendLuaEvent(ushort luaEventId, IEnumerable<ServerQuestLuaEvent.ILuaEventData> luaEvents);

        /// <summary>
        /// Return the <see cref="IPlayer"/> that owns this quest.
        /// </summary>
        IPlayer GetOwner();

        /// <summary>
        /// Return the <see cref="IQuestObjective"/> with the supplied id.
        /// </summary>
        IQuestObjective GetQuestObjective(uint id);

        /// <summary>
        /// Return the <see cref="IQuestObjective"/> with the supplied index.
        /// </summary>
        IQuestObjective GetQuestObjectiveByIndex(byte index);

        /// <summary>
        /// Complete this quest, reclaiming pushed items and setting state to Completed.
        /// </summary>
        void CompleteQuest();

        /// <summary>
        /// Reward this quest with the supplied reward id, applying rewards and calling CompleteQuest.
        /// </summary>
        void RewardQuest(ushort reward);
    }
}