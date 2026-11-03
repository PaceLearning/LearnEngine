using LearnEngine.Core.Signals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LearnEngine.Core.Registration;

/// <summary>
/// Registers the engine's vocabulary in the app's Program.cs.
/// </summary>
public static class RegistrationExtension
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a signal the AI can report and registers the <see cref="SignalCatalog"/> built from every added signal.
        /// The catalog is built once, when it is first resolved, so signals added after the service provider is built are never seen.
        /// An empty id or description, or an id added twice, throws here so a wrong vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddSignal(string id, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(SignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is SignalRegistration registration
                && registration.Signal.Id == id);
            if (taken)
                throw new InvalidOperationException($"The signal \"{id}\" is added twice.");

            services.AddSingleton(SignalRegistration.Create(Signal.Create(id, description)));
            services.TryAddSingleton(provider => SignalCatalog.Create(
                [.. provider.GetServices<SignalRegistration>().Select(registration => registration.Signal)]));
            return services;
        }
    }
}
