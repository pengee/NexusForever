using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Map.Instance;
using NexusForever.Shared;

namespace NexusForever.Game.Map.Instance
{
    public static class ServiceCollectionExtensions
    {
        public static void AddGameMapInstance(this IServiceCollection sc)
        {
            sc.AddTransient<ContentInstancedMap<IContentMapInstance>>();
            sc.AddTransient<ContentInstancedMap<IContentPvpMapInstance>>();
            sc.AddTransient<ResidenceInstancedMap>();
            sc.AddTransient<TutorialInstancedMap>();

            sc.AddTransientFactory<IContentMapInstance, ContentMapInstance>();
            sc.AddTransientFactory<IContentPvpMapInstance, ContentPvpMapInstance>();
            sc.AddTransientFactory<IResidenceMapInstance, ResidenceMapInstance>();
            sc.AddTransientFactory<ITutorialMapInstance, TutorialMapInstance>();
        }
    }
}
