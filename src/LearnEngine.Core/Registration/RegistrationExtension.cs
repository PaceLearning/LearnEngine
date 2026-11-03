using LearnEngine.Core.Moves;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;
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

        /// <summary>
        /// Adds a move the tutor can make and registers the <see cref="MoveCatalog"/> built from every added move.
        /// The catalog is built once, when it is first resolved, so moves added after the service provider is built are never seen.
        /// An empty id or description, or an id added twice, throws here so a wrong vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddMove(string id, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(MoveRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is MoveRegistration registration
                && registration.Move.Id == id);
            if (taken)
                throw new InvalidOperationException($"The move \"{id}\" is added twice.");

            services.AddSingleton(MoveRegistration.Create(Move.Create(id, description)));
            services.TryAddSingleton(provider => MoveCatalog.Create(
                [.. provider.GetServices<MoveRegistration>().Select(registration => registration.Move)]));
            return services;
        }

        /// <summary>
        /// Adds every state the odds engine keeps odds on and registers the <see cref="StateCatalog"/> built from them here.
        /// All states go in this one call so their priors can be checked together: once the last state is added, a total
        /// other than 1 throws here, so a wrong set of priors stops the app at startup instead of skewing the odds mid session.
        /// Calling it a second time throws too, because the priors of two calls could never be checked as one set.
        /// </summary>
        public IServiceCollection AddStates(Action<StatesBuilder> addStates)
        {
            ArgumentNullException.ThrowIfNull(addStates);

            var added = services.Any(descriptor =>
                descriptor.ServiceType == typeof(StateCatalog) && !descriptor.IsKeyedService);
            if (added)
                throw new InvalidOperationException("The states are added twice. Add every state in one AddStates call.");

            var builder = StatesBuilder.Create();
            addStates(builder);
            var states = builder.States;

            // A small tolerance, because priors written as decimals rarely add up to exactly 1 in floating point.
            var total = states.Sum(state => state.Prior);
            var addsUpToOne = Math.Abs(total - 1) <= 1e-9;
            if (!addsUpToOne)
                throw new InvalidOperationException($"The priors of the states add up to {total}, not 1.");

            services.AddSingleton(StateCatalog.Create(states));
            return services;
        }
    }
}
