using System.Collections.Immutable;
using LearnEngine.Core.Bandit;
using LearnEngine.Core.History;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
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
        /// A signal added after AddLikelihoods throws too, because it would have no rates.
        /// </summary>
        public IServiceCollection AddSignal(string id, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            var afterLikelihoods = services.Any(descriptor =>
                descriptor.ServiceType == typeof(LikelihoodTable) && !descriptor.IsKeyedService);
            if (afterLikelihoods)
                throw new InvalidOperationException($"The signal \"{id}\" is added after the likelihoods, so it has no rates. Add every signal before AddLikelihoods.");

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

        /// <summary>
        /// Adds the rate of every signal in every state and registers the <see cref="LikelihoodTable"/> built from them here.
        /// The signals and states must be added before this call, so every rate can be checked against them.
        /// All rates go in this one call so the table can be checked as a whole: once the last signal is added, a signal
        /// left without rates throws here, so a missing rate stops the app at startup instead of skewing the odds mid session.
        /// Calling it a second time throws too, because the rates of two calls could never be checked as one table.
        /// </summary>
        public IServiceCollection AddLikelihoods(Action<LikelihoodsBuilder> addLikelihoods)
        {
            ArgumentNullException.ThrowIfNull(addLikelihoods);

            var added = services.Any(descriptor =>
                descriptor.ServiceType == typeof(LikelihoodTable) && !descriptor.IsKeyedService);
            if (added)
                throw new InvalidOperationException("The likelihoods are added twice. Add every rate in one AddLikelihoods call.");

            var states = services
                .Where(descriptor => descriptor.ServiceType == typeof(StateCatalog) && !descriptor.IsKeyedService)
                .Select(descriptor => descriptor.ImplementationInstance as StateCatalog)
                .FirstOrDefault(catalog => catalog is not null);
            if (states is null)
                throw new InvalidOperationException("The states are not added. Add them with AddStates before the likelihoods.");

            ImmutableArray<string> signalIds = [.. services
                .Where(descriptor => descriptor.ServiceType == typeof(SignalRegistration) && !descriptor.IsKeyedService)
                .Select(descriptor => descriptor.ImplementationInstance as SignalRegistration)
                .OfType<SignalRegistration>()
                .Select(registration => registration.Signal.Id)];
            if (signalIds.IsEmpty)
                throw new InvalidOperationException("No signals are added. Add them with AddSignal before the likelihoods.");

            var builder = LikelihoodsBuilder.Create(signalIds, states);
            addLikelihoods(builder);
            var likelihoods = builder.Likelihoods;

            var missing = signalIds
                .Where(signalId => likelihoods.All(likelihood => likelihood.SignalId != signalId))
                .ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"These signals have no rates: {string.Join(", ", missing)}. Every signal needs a rate in every state.");

            services.AddSingleton(LikelihoodTable.Create(likelihoods));
            return services;
        }

        /// <summary>
        /// Registers the <see cref="OddsEngine"/> and the <see cref="OddsOptions"/> it is used with.
        /// The fade is how far the belief moves back to the priors before every turn, and the clear margin is how far
        /// the leading state must be ahead of the runner up to be clear. Both must be from 0 to 1.
        /// The likelihoods must be added before this call. A value out of range, a missing AddLikelihoods,
        /// or a second call throws here so a wrong setup stops the app at startup.
        /// </summary>
        public IServiceCollection AddOdds(
            double fade = OddsOptions.DefaultFade,
            double clearMargin = OddsOptions.DefaultClearMargin)
        {
            var added = services.Any(descriptor =>
                descriptor.ServiceType == typeof(OddsOptions) && !descriptor.IsKeyedService);
            if (added)
                throw new InvalidOperationException("The odds are added twice. Call AddOdds once.");

            var likelihoodsAdded = services.Any(descriptor =>
                descriptor.ServiceType == typeof(LikelihoodTable) && !descriptor.IsKeyedService);
            if (!likelihoodsAdded)
                throw new InvalidOperationException("The likelihoods are not added. Add them with AddLikelihoods before the odds.");

            // Written so a NaN fails too, which a pair of separate bound checks would let through.
            var fadeInRange = fade >= 0 && fade <= 1;
            if (!fadeInRange)
                throw new ArgumentOutOfRangeException(nameof(fade), fade, "The fade must be from 0 to 1.");

            var clearMarginInRange = clearMargin >= 0 && clearMargin <= 1;
            if (!clearMarginInRange)
                throw new ArgumentOutOfRangeException(nameof(clearMargin), clearMargin, "The clear margin must be from 0 to 1.");

            services.AddSingleton(OddsOptions.Create(fade, clearMargin));
            services.AddSingleton(provider => OddsEngine.Create(
                provider.GetRequiredService<StateCatalog>(),
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<LikelihoodTable>()));
            return services;
        }

        /// <summary>
        /// Registers the <see cref="VocabularyFingerprint"/> every session is stamped with, the <see cref="HistoryReplayer"/>
        /// that rebuilds a belief from a history, and an <see cref="InMemoryHistoryStore"/> as the <see cref="IHistoryStore"/>.
        /// To keep histories somewhere else, register your own <see cref="IHistoryStore"/> before this call and it is kept.
        /// The odds must be added before this call. A missing AddOdds or a second call throws here so a wrong setup stops
        /// the app at startup.
        /// </summary>
        public IServiceCollection AddHistory()
        {
            var added = services.Any(descriptor =>
                descriptor.ServiceType == typeof(HistoryReplayer) && !descriptor.IsKeyedService);
            if (added)
                throw new InvalidOperationException("The history is added twice. Call AddHistory once.");

            var oddsAdded = services.Any(descriptor =>
                descriptor.ServiceType == typeof(OddsEngine) && !descriptor.IsKeyedService);
            if (!oddsAdded)
                throw new InvalidOperationException("The odds are not added. Add them with AddOdds before the history.");

            // Resolved when first used, so every move is in the fingerprint even when moves are added after this call.
            services.AddSingleton(provider => VocabularyFingerprint.Create(
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<StateCatalog>(),
                provider.GetRequiredService<LikelihoodTable>(),
                provider.GetService<MoveCatalog>()));
            services.AddSingleton(provider => HistoryReplayer.Create(
                provider.GetRequiredService<OddsEngine>(),
                provider.GetRequiredService<VocabularyFingerprint>()));
            services.TryAddSingleton<IHistoryStore>(_ => InMemoryHistoryStore.Create());
            return services;
        }

        /// <summary>
        /// Allows a move in a state, with how many times it worked and was tried there across all learners, and registers
        /// the <see cref="AllowedMoveCatalog"/> built from every allowed move. A learner with no score of their own starts from these counts.
        /// The catalog is built once, when it is first resolved, so moves allowed after the service provider is built are never seen.
        /// An <see cref="InMemoryScoreStore"/> is registered as the <see cref="IScoreStore"/> that keeps each learner's own scores.
        /// To keep scores somewhere else, register your own <see cref="IScoreStore"/> before the first AddMoveFor and it is kept.
        /// The state must be added with AddStates and the move with AddMove before this call. An empty or unknown id,
        /// a move allowed twice in the same state, or counts below 0 or with more worked than tried throw here so a wrong
        /// vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddMoveFor(string stateId, string moveId, int worked, int tried)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
            ArgumentException.ThrowIfNullOrWhiteSpace(moveId);

            var states = services
                .Where(descriptor => descriptor.ServiceType == typeof(StateCatalog) && !descriptor.IsKeyedService)
                .Select(descriptor => descriptor.ImplementationInstance as StateCatalog)
                .FirstOrDefault(catalog => catalog is not null);
            if (states is null)
                throw new InvalidOperationException($"The states are not added, so the move \"{moveId}\" cannot be allowed in \"{stateId}\". Add them with AddStates before AddMoveFor.");

            var knownState = states.Find(stateId).IsT0;
            if (!knownState)
                throw new InvalidOperationException($"The state \"{stateId}\" is not added. Add it with AddStates before AddMoveFor.");

            var knownMove = services.Any(descriptor =>
                descriptor.ServiceType == typeof(MoveRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is MoveRegistration registration
                && registration.Move.Id == moveId);
            if (!knownMove)
                throw new InvalidOperationException($"The move \"{moveId}\" is not added. Add it with AddMove before AddMoveFor.");

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(AllowedMoveRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is AllowedMoveRegistration registration
                && registration.StartingScore.StateId == stateId
                && registration.StartingScore.MoveId == moveId);
            if (taken)
                throw new InvalidOperationException($"The move \"{moveId}\" is allowed twice in the state \"{stateId}\".");

            if (tried < 0)
                throw new ArgumentOutOfRangeException(nameof(tried), tried, $"The move \"{moveId}\" in the state \"{stateId}\" cannot be tried fewer than 0 times.");

            var workedInRange = worked >= 0 && worked <= tried;
            if (!workedInRange)
                throw new ArgumentOutOfRangeException(nameof(worked), worked, $"The move \"{moveId}\" in the state \"{stateId}\" must have worked from 0 to {tried} times, the times it was tried.");

            services.AddSingleton(AllowedMoveRegistration.Create(MoveScore.Create(stateId, moveId, worked, tried)));
            services.TryAddSingleton(provider => AllowedMoveCatalog.Create(
                provider.GetRequiredService<StateCatalog>(),
                [.. provider.GetServices<AllowedMoveRegistration>().Select(registration => registration.StartingScore)]));
            services.TryAddSingleton<IScoreStore>(_ => InMemoryScoreStore.Create());
            return services;
        }
    }
}
