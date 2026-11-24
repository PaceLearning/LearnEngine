using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Mcp.Compliance;
using Microsoft.Extensions.DependencyInjection;

namespace LearnEngine.Mcp.Storage;

/// <summary>
/// Registers the stores that keep the student's sessions and learner model as files, so they outlive the container.
/// </summary>
public static class RegistrationExtension
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the settings and a <see cref="FileHistoryStore"/>, as itself and as the <see cref="IHistoryStore"/>, and a
        /// <see cref="FileLearnerModelStore"/> as the <see cref="ILearnerModelStore"/>, with the <see cref="ComplianceLog"/>
        /// kept beside them.
        /// Call it before the vocabulary, so the engine keeps these stores instead of adding its in-memory ones.
        /// </summary>
        public IServiceCollection AddFileStores(ServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            services.AddSingleton(settings);
            var histories = FileHistoryStore.Create(settings);
            services.AddSingleton(histories);
            services.AddSingleton<IHistoryStore>(histories);
            services.AddSingleton<ILearnerModelStore>(FileLearnerModelStore.Create(settings));
            services.AddSingleton(ComplianceLog.Create(settings));
            return services;
        }
    }
}
