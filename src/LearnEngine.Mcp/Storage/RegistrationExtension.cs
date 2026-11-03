using LearnEngine.Core.Bandit;
using LearnEngine.Core.History;
using Microsoft.Extensions.DependencyInjection;

namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Registers the stores that keep the student's sessions and scores as files, so they outlive the container.
/// </summary>
public static class RegistrationExtension
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the settings and a <see cref="FileHistoryStore"/> as the <see cref="IHistoryStore"/> and a
        /// <see cref="FileScoreStore"/> as the <see cref="IScoreStore"/>.
        /// Call it before the vocabulary, so the engine keeps these stores instead of adding its in-memory ones.
        /// </summary>
        public IServiceCollection AddFileStores(ServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            services.AddSingleton(settings);
            services.AddSingleton<IHistoryStore>(FileHistoryStore.Create(settings));
            services.AddSingleton<IScoreStore>(FileScoreStore.Create(settings));
            return services;
        }
    }
}
