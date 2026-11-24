using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Mcp.Tools;
using LearnEngine.Web.Feed;
using LearnEngine.Web.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace LearnEngine.Web;

/// <summary>
/// Registers what the web app adds on top of the stores and the engine: the turn gate, the feed, the tools and the
/// reader of sessions for the pages.
/// </summary>
public static class RegistrationExtension
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a <see cref="TurnGate"/>, an <see cref="EngineFeed"/>, the <see cref="LearnTools"/> and a
        /// <see cref="SessionReader"/>, each as a singleton.
        /// Call it after the stores, the vocabulary and the engine, since the reader is made from them.
        /// </summary>
        public IServiceCollection AddLearnEngineWeb()
        {
            services.AddSingleton(TurnGate.Create());
            services.AddSingleton(EngineFeed.Create());
            services.AddSingleton<LearnTools>();
            services.AddSingleton(provider => SessionReader.Create(
                provider.GetRequiredService<IHistoryStore>(),
                provider.GetRequiredService<HistoryReplayer>(),
                provider.GetRequiredService<ILearnerModelStore>(),
                provider.GetRequiredService<MoveChooser>()));
            return services;
        }
    }
}
