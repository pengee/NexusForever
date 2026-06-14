using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Prerequisite
{
    public sealed class PrerequisiteManager : Singleton<PrerequisiteManager>, IPrerequisiteManager
    {
        /// <summary>
        /// Globally enables/disables prerequisite evaluation. When false, Meets() short-circuits
        /// to <see cref="DisabledReturnValue"/> without running any checks.
        /// </summary>
        public static bool Enabled { get; set; } = false;

        /// <summary>
        /// Value returned by Meets() when <see cref="Enabled"/> is false. Defaults to true
        /// (treat all prerequisites as met).
        /// </summary>
        public static bool DisabledReturnValue { get; set; } = true;

        #region Dependency Injection

        private readonly ILogger<PrerequisiteManager> log;
        private readonly IServiceProvider serviceProvider;
        private readonly IGameTableManager gameTableManager;
        private readonly IFactory<IPrerequisiteParameters> prerequisiteParametersFactory;

        public PrerequisiteManager(
            ILogger<PrerequisiteManager> log,
            IServiceProvider serviceProvider,
            IGameTableManager gameTableManager,
            IFactory<IPrerequisiteParameters> prerequisiteParametersFactory)
        {
            this.log                           = log;
            this.serviceProvider               = serviceProvider;
            this.gameTableManager              = gameTableManager;
            this.prerequisiteParametersFactory = prerequisiteParametersFactory;
        }

        #endregion

        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets supplied prerequisite.
        /// </summary>
        public bool Meets(IPlayer player, uint prerequisiteId)
            => Meets(player, prerequisiteId, prerequisiteParametersFactory.Resolve());

        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets supplied prerequisite.
        /// </summary>
        public bool Meets(IPlayer player, uint prerequisiteId, IPrerequisiteParameters parameters)
        {
            if (!Enabled)
                return DisabledReturnValue;
            if (!parameters.VisitingPrerequisites.Add(prerequisiteId))
            {
                log.LogError($"Prerequisite cycle detected visiting prereq {prerequisiteId} for player {player.Name} (chain: {string.Join(" -> ", parameters.VisitingPrerequisites)} -> {prerequisiteId})");
                return false;
            }
            try
            {
                PrerequisiteEntry entry = gameTableManager.Prerequisite.GetEntry(prerequisiteId);
                if (entry == null)
                    throw new ArgumentException();

                switch (entry.Flags)
                {
                    case EvaluationMode.EvaluateAND:
                        return MeetsEvaluateAnd(player, prerequisiteId, entry, parameters);
                    case EvaluationMode.EvaluateOR:
                        return MeetsEvaluateOr(player, prerequisiteId, entry, parameters);
                    default:
                        log.LogTrace($"Unhandled EvaluationMode {entry.Flags}");
                        return false;
                }
            }
            finally
            {
                parameters.VisitingPrerequisites.Remove(prerequisiteId);
            }
        }

        /// <summary>
        /// Checks if the supplied <see cref="IUnitEntity"/> (player or NPC) meets the given prerequisite.
        /// </summary>
        public bool Meets(IUnitEntity entity, uint prerequisiteId)
        {
            IPrerequisiteParameters parameters = prerequisiteParametersFactory.Resolve();
            return Meets(entity, prerequisiteId, parameters);
        }

        /// <summary>
        /// Checks if the supplied <see cref="IUnitEntity"/> (player or NPC) meets the given prerequisite,
        /// using the supplied <see cref="IPrerequisiteParameters"/> (preserves cycle-detection state).
        /// </summary>
        public bool Meets(IUnitEntity entity, uint prerequisiteId, IPrerequisiteParameters parameters)
        {
            if (!Enabled)
                return DisabledReturnValue;
            if (!parameters.VisitingPrerequisites.Add(prerequisiteId))
            {
                log.LogError($"Prerequisite cycle detected visiting prereq {prerequisiteId} for entity {entity.Guid} (chain: {string.Join(" -> ", parameters.VisitingPrerequisites)} -> {prerequisiteId})");
                return false;
            }
            try
            {
                PrerequisiteEntry entry = gameTableManager.Prerequisite.GetEntry(prerequisiteId);
                if (entry == null)
                    return false;

                switch (entry.Flags)
                {
                    case EvaluationMode.EvaluateAND:
                        return MeetsEvaluateAnd(entity, prerequisiteId, entry, parameters);
                    case EvaluationMode.EvaluateOR:
                        return MeetsEvaluateOr(entity, prerequisiteId, entry, parameters);
                    default:
                        log.LogTrace($"Unhandled EvaluationMode {entry.Flags}");
                        return false;
                }
            }
            finally
            {
                parameters.VisitingPrerequisites.Remove(prerequisiteId);
            }
        }

        private bool MeetsEvaluateAnd(IPlayer player, uint prerequisiteId, PrerequisiteEntry entry, IPrerequisiteParameters parameters)
        {
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteType type = entry.PrerequisiteTypeId[i];
                if (type == PrerequisiteType.None)
                    continue;

                PrerequisiteComparison comparison = entry.PrerequisiteComparisonId[i];
                if (!Meets(player, type, comparison, entry.Value[i], entry.ObjectId[i], parameters))
                {
                    log.LogTrace($"Player {player.Name} failed prerequisite AND check ({prerequisiteId}) {type}, {comparison}, {entry.Value[i]}, {entry.ObjectId[i]}");
                    return false;
                }
            }

            return true;
        }

        private bool MeetsEvaluateOr(IPlayer player, uint prerequisiteId, PrerequisiteEntry entry, IPrerequisiteParameters parameters)
        {
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteType type = entry.PrerequisiteTypeId[i];
                if (type == PrerequisiteType.None)
                    continue;

                if (Meets(player, type, entry.PrerequisiteComparisonId[i], entry.Value[i], entry.ObjectId[i], parameters))
                    return true;
            }

            log.LogTrace($"Player {player.Name} failed prerequisite OR check ({prerequisiteId})");
            return false;
        }

        private bool MeetsEvaluateAnd(IUnitEntity entity, uint prerequisiteId, PrerequisiteEntry entry, IPrerequisiteParameters parameters)
        {
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteType type = entry.PrerequisiteTypeId[i];
                if (type == PrerequisiteType.None)
                    continue;

                PrerequisiteComparison comparison = entry.PrerequisiteComparisonId[i];
                if (!Meets(entity, type, comparison, entry.Value[i], entry.ObjectId[i], parameters))
                {
                    log.LogTrace($"Entity {entity.Guid} failed prerequisite AND check ({prerequisiteId}) {type}, {comparison}, {entry.Value[i]}, {entry.ObjectId[i]}");
                    return false;
                }
            }

            return true;
        }

        private bool MeetsEvaluateOr(IUnitEntity entity, uint prerequisiteId, PrerequisiteEntry entry, IPrerequisiteParameters parameters)
        {
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteType type = entry.PrerequisiteTypeId[i];
                if (type == PrerequisiteType.None)
                    continue;

                if (Meets(entity, type, entry.PrerequisiteComparisonId[i], entry.Value[i], entry.ObjectId[i], parameters))
                    return true;
            }

            log.LogTrace($"Entity {entity.Guid} failed prerequisite OR check ({prerequisiteId})");
            return false;
        }

        private bool Meets(IPlayer player, PrerequisiteType type, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            IPrerequisiteCheck handler = serviceProvider.GetKeyedService<IPrerequisiteCheck>(type);
            if (handler == null)
            {
                log.LogWarning($"Unhandled PrerequisiteType {type}!");
                return false;
            }

            return handler.Meets(player, comparison, value, objectId, parameters);
        }

        private bool Meets(IUnitEntity entity, PrerequisiteType type, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            IPrerequisiteCheck handler = serviceProvider.GetKeyedService<IPrerequisiteCheck>(type);
            if (handler == null)
            {
                log.LogWarning($"Unhandled PrerequisiteType {type}!");
                return false;
            }

            return handler.Meets(entity, comparison, value, objectId, parameters);
        }
    }
}
