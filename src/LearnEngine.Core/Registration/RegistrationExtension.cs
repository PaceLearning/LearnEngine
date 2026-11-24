using System.Collections.Immutable;
using LearnEngine.Core.Bandit;
using LearnEngine.Core.Effects;
using LearnEngine.Core.Guide;
using LearnEngine.Core.History;
using LearnEngine.Core.Judging;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Likelihoods;
using LearnEngine.Core.Moves;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Prediction;
using LearnEngine.Core.Sessions;
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
        /// Registers the <see cref="OddsEngine"/>, the <see cref="OddsOptions"/> it is used with and the <see cref="MoveEffectCatalog"/>.
        /// The fade is how far the belief moves back to the priors before a turn when no move was made, and the clear margin is how far
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
            // The effects and the rate weight are read when first resolved, so AddMoveEffect and AddLearning may come after this call.
            services.AddSingleton(provider => MoveEffectCatalog.Create(
                provider.GetRequiredService<StateCatalog>(),
                fade,
                [.. provider.GetServices<MoveEffectRegistration>().Select(registration => registration.Effect)]));
            services.AddSingleton(provider => OddsEngine.Create(
                provider.GetRequiredService<StateCatalog>(),
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<LikelihoodTable>(),
                provider.GetRequiredService<MoveEffectCatalog>(),
                provider.GetService<LearningOptions>()?.RateWeight ?? LearningOptions.DefaultRateWeight));
            return services;
        }

        /// <summary>
        /// Makes a state a goal state, one the engine wants to bring the learner to, such as "following", and registers
        /// the <see cref="GoalStates"/> built from every goal state. They are built once, when first resolved, so goal
        /// states added after the service provider is built are never seen.
        /// The state must be added with AddStates before this call. An empty or unknown id, or a state added twice as a
        /// goal state, throws here so a wrong vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddGoalState(string stateId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

            var states = services
                .Where(descriptor => descriptor.ServiceType == typeof(StateCatalog) && !descriptor.IsKeyedService)
                .Select(descriptor => descriptor.ImplementationInstance as StateCatalog)
                .FirstOrDefault(catalog => catalog is not null);
            if (states is null)
                throw new InvalidOperationException($"The states are not added, so \"{stateId}\" cannot be a goal state. Add them with AddStates before AddGoalState.");

            var knownState = states.Find(stateId).IsT0;
            if (!knownState)
                throw new InvalidOperationException($"The state \"{stateId}\" is not added. Add it with AddStates before AddGoalState.");

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(GoalStateRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is GoalStateRegistration registration
                && registration.StateId == stateId);
            if (taken)
                throw new InvalidOperationException($"The state \"{stateId}\" is added twice as a goal state.");

            services.AddSingleton(GoalStateRegistration.Create(stateId));
            services.TryAddSingleton(provider => GoalStates.Create(
                provider.GetServices<GoalStateRegistration>().Select(registration => registration.StateId)));
            return services;
        }

        /// <summary>
        /// Adds where a move made in a state leads by the next learner turn, shared by every learner: the chance of each
        /// state after it, written as a state id and the chance, such as ("following", 0.55). A state not given has a
        /// chance of 0, and the chances must add up to 1. The weight is how many turns of evidence the chances count as
        /// before a learner's own counts move them, so a higher weight makes them move more slowly.
        /// A move made in a state with no effect added is read as no move there, which fades back to the priors with
        /// the fade given to AddOdds, with a weight of <see cref="MoveEffectCatalog.NoMoveWeight"/>.
        /// The effects are read when the <see cref="MoveEffectCatalog"/> is first resolved, so effects added after the
        /// service provider is built are never seen.
        /// The state must be added with AddStates and the move with AddMove before this call. An empty or unknown id,
        /// an effect added twice for the same state and move, a weight not above 0 and finite, a state given twice, a
        /// chance outside 0 to 1, or chances that do not add up to 1 throw here so a wrong vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddMoveEffect(string fromStateId, string moveId, double weight, params ReadOnlySpan<(string ToStateId, double Chance)> chances)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fromStateId);
            ArgumentException.ThrowIfNullOrWhiteSpace(moveId);

            var states = services
                .Where(descriptor => descriptor.ServiceType == typeof(StateCatalog) && !descriptor.IsKeyedService)
                .Select(descriptor => descriptor.ImplementationInstance as StateCatalog)
                .FirstOrDefault(catalog => catalog is not null);
            if (states is null)
                throw new InvalidOperationException($"The states are not added, so the effect of \"{moveId}\" in \"{fromStateId}\" cannot be added. Add them with AddStates before AddMoveEffect.");

            var knownState = states.Find(fromStateId).IsT0;
            if (!knownState)
                throw new InvalidOperationException($"The state \"{fromStateId}\" is not added. Add it with AddStates before AddMoveEffect.");

            var knownMove = services.Any(descriptor =>
                descriptor.ServiceType == typeof(MoveRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is MoveRegistration registration
                && registration.Move.Id == moveId);
            if (!knownMove)
                throw new InvalidOperationException($"The move \"{moveId}\" is not added. Add it with AddMove before AddMoveEffect.");

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(MoveEffectRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is MoveEffectRegistration registration
                && registration.Effect.FromStateId == fromStateId
                && registration.Effect.MoveId == moveId);
            if (taken)
                throw new InvalidOperationException($"The effect of the move \"{moveId}\" in the state \"{fromStateId}\" is added twice.");

            // Written so a NaN fails too, which a plain bound check would let through.
            var weightInRange = weight > 0 && double.IsFinite(weight);
            if (!weightInRange)
                throw new ArgumentOutOfRangeException(nameof(weight), weight, $"The weight of the effect of \"{moveId}\" in \"{fromStateId}\" must be above 0 and finite.");

            var given = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (toStateId, chance) in chances)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(toStateId);

                var knownToState = states.Find(toStateId).IsT0;
                if (!knownToState)
                    throw new InvalidOperationException($"The state \"{toStateId}\" in the effect of \"{moveId}\" in \"{fromStateId}\" is not added. Add it with AddStates before AddMoveEffect.");

                // Written so a NaN chance fails too, which a pair of separate bound checks would let through.
                var chanceInRange = chance >= 0 && chance <= 1;
                if (!chanceInRange)
                    throw new ArgumentOutOfRangeException(nameof(chances), chance, $"The chance of \"{toStateId}\" in the effect of \"{moveId}\" in \"{fromStateId}\" must be from 0 to 1.");

                if (!given.TryAdd(toStateId, chance))
                    throw new InvalidOperationException($"The effect of \"{moveId}\" in \"{fromStateId}\" gives the state \"{toStateId}\" twice.");
            }

            // A small tolerance, because chances written as decimals rarely add up to exactly 1 in floating point.
            var total = given.Values.Sum();
            var addsUpToOne = Math.Abs(total - 1) <= 1e-9;
            if (!addsUpToOne)
                throw new InvalidOperationException($"The chances of the effect of \"{moveId}\" in \"{fromStateId}\" add up to {total}, not 1.");

            services.AddSingleton(MoveEffectRegistration.Create(MoveEffect.Create(
                fromStateId,
                moveId,
                weight,
                [.. states.All.Select(state => StateOdds.Create(state.Id, given.GetValueOrDefault(state.Id)))])));
            return services;
        }

        /// <summary>
        /// Lets the engine learn how each learner learns: after every turn, the chance the learner was in each state is
        /// added to their own counts of every signal in that state, as shown or not shown, so their rates move away
        /// from the shared ones. The rate weight is how many turns the shared rate of a signal counts as, so a higher
        /// weight makes a learner's own counts move it more slowly.
        /// It registers the <see cref="LearningOptions"/> and an <see cref="InMemoryLearnerModelStore"/> as the
        /// <see cref="ILearnerModelStore"/>. To keep learner models somewhere else, register your own
        /// <see cref="ILearnerModelStore"/> before this call and it is kept.
        /// A rate weight that is not above 0 and finite, or a second call, throws here so a wrong setup stops the app at startup.
        /// </summary>
        public IServiceCollection AddLearning(double rateWeight = LearningOptions.DefaultRateWeight)
        {
            var added = services.Any(descriptor =>
                descriptor.ServiceType == typeof(LearningOptions) && !descriptor.IsKeyedService);
            if (added)
                throw new InvalidOperationException("The learning is added twice. Call AddLearning once.");

            // Written so a NaN fails too, which a plain bound check would let through.
            var weightInRange = rateWeight > 0 && double.IsFinite(rateWeight);
            if (!weightInRange)
                throw new ArgumentOutOfRangeException(nameof(rateWeight), rateWeight, "The rate weight must be above 0 and finite.");

            services.AddSingleton(LearningOptions.Create(rateWeight));
            services.TryAddSingleton<ILearnerModelStore>(_ => InMemoryLearnerModelStore.Create());
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

            // Resolved when first used, so every move and outcome signal is in the fingerprint even when they are added after this call.
            services.AddSingleton(provider => VocabularyFingerprint.Create(
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<StateCatalog>(),
                provider.GetRequiredService<LikelihoodTable>(),
                provider.GetService<MoveCatalog>(),
                provider.GetService<OutcomeSignals>(),
                provider.GetRequiredService<MoveEffectCatalog>(),
                provider.GetService<GoalStates>(),
                provider.GetService<LearningOptions>()?.RateWeight ?? LearningOptions.DefaultRateWeight));
            services.AddSingleton(provider => HistoryReplayer.Create(
                provider.GetRequiredService<OddsEngine>(),
                provider.GetRequiredService<VocabularyFingerprint>(),
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetService<MoveCatalog>()));
            services.TryAddSingleton<IHistoryStore>(_ => InMemoryHistoryStore.Create());
            return services;
        }

        /// <summary>
        /// Allows a move in a state, with how many times it worked and was tried there across all learners, and registers
        /// the <see cref="AllowedMoveCatalog"/> built from every allowed move. A learner with no score of their own starts from these counts.
        /// It also registers the <see cref="MovePicker"/> that picks a move in a clear state.
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
            services.TryAddSingleton(provider => MovePicker.Create(
                provider.GetRequiredService<AllowedMoveCatalog>(),
                provider.GetRequiredService<MoveCatalog>(),
                provider.GetRequiredService<IScoreStore>()));
            return services;
        }

        /// <summary>
        /// Adds a signal that shows a picked move worked, such as "offers.own_answer", and registers the
        /// <see cref="OutcomeSignals"/> built from every success and failure signal.
        /// The outcome signals are built once, when first resolved, so signals added after the service provider is built are never seen.
        /// It also registers the <see cref="MoveJudge"/> that judges a picked move by the learner turns after it.
        /// The signal must be added with AddSignal, the history with AddHistory and at least one move with AddMoveFor
        /// before this call. An empty or unknown id, a missing AddHistory or AddMoveFor, a signal added twice as a
        /// success signal, or a signal that is a failure signal too throws here so a wrong vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddSuccessSignal(string signalId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(signalId);

            var knownSignal = services.Any(descriptor =>
                descriptor.ServiceType == typeof(SignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is SignalRegistration registration
                && registration.Signal.Id == signalId);
            if (!knownSignal)
                throw new InvalidOperationException($"The signal \"{signalId}\" is not added. Add it with AddSignal before AddSuccessSignal.");

            var historyAdded = services.Any(descriptor =>
                descriptor.ServiceType == typeof(HistoryReplayer) && !descriptor.IsKeyedService);
            if (!historyAdded)
                throw new InvalidOperationException($"The history is not added, so the signal \"{signalId}\" cannot judge a move. Add it with AddHistory before AddSuccessSignal.");

            var movesAllowed = services.Any(descriptor =>
                descriptor.ServiceType == typeof(AllowedMoveRegistration) && !descriptor.IsKeyedService);
            if (!movesAllowed)
                throw new InvalidOperationException($"No moves are allowed, so the signal \"{signalId}\" has no move to judge. Allow them with AddMoveFor before AddSuccessSignal.");

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(SuccessSignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is SuccessSignalRegistration registration
                && registration.SignalId == signalId);
            if (taken)
                throw new InvalidOperationException($"The signal \"{signalId}\" is added twice as a success signal.");

            var failure = services.Any(descriptor =>
                descriptor.ServiceType == typeof(FailureSignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is FailureSignalRegistration registration
                && registration.SignalId == signalId);
            if (failure)
                throw new InvalidOperationException($"The signal \"{signalId}\" is added as a failure signal too. A signal can show a move worked or failed, not both.");

            services.AddSingleton(SuccessSignalRegistration.Create(signalId));
            services.TryAddSingleton(provider => OutcomeSignals.Create(
                [.. provider.GetServices<SuccessSignalRegistration>().Select(registration => registration.SignalId)],
                [.. provider.GetServices<FailureSignalRegistration>().Select(registration => registration.SignalId)]));
            services.TryAddSingleton(provider => MoveJudge.Create(
                provider.GetRequiredService<HistoryReplayer>(),
                provider.GetRequiredService<OutcomeSignals>(),
                provider.GetRequiredService<AllowedMoveCatalog>(),
                provider.GetRequiredService<IHistoryStore>(),
                provider.GetRequiredService<IScoreStore>()));
            return services;
        }

        /// <summary>
        /// Adds a signal that shows a picked move failed, such as "says.overwhelmed", and registers the
        /// <see cref="OutcomeSignals"/> built from every success and failure signal.
        /// The outcome signals are built once, when first resolved, so signals added after the service provider is built are never seen.
        /// It also registers the <see cref="MoveJudge"/> that judges a picked move by the learner turns after it.
        /// The signal must be added with AddSignal, the history with AddHistory and at least one move with AddMoveFor
        /// before this call. An empty or unknown id, a missing AddHistory or AddMoveFor, a signal added twice as a
        /// failure signal, or a signal that is a success signal too throws here so a wrong vocabulary stops the app at startup.
        /// </summary>
        public IServiceCollection AddFailureSignal(string signalId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(signalId);

            var knownSignal = services.Any(descriptor =>
                descriptor.ServiceType == typeof(SignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is SignalRegistration registration
                && registration.Signal.Id == signalId);
            if (!knownSignal)
                throw new InvalidOperationException($"The signal \"{signalId}\" is not added. Add it with AddSignal before AddFailureSignal.");

            var historyAdded = services.Any(descriptor =>
                descriptor.ServiceType == typeof(HistoryReplayer) && !descriptor.IsKeyedService);
            if (!historyAdded)
                throw new InvalidOperationException($"The history is not added, so the signal \"{signalId}\" cannot judge a move. Add it with AddHistory before AddFailureSignal.");

            var movesAllowed = services.Any(descriptor =>
                descriptor.ServiceType == typeof(AllowedMoveRegistration) && !descriptor.IsKeyedService);
            if (!movesAllowed)
                throw new InvalidOperationException($"No moves are allowed, so the signal \"{signalId}\" has no move to judge. Allow them with AddMoveFor before AddFailureSignal.");

            var taken = services.Any(descriptor =>
                descriptor.ServiceType == typeof(FailureSignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is FailureSignalRegistration registration
                && registration.SignalId == signalId);
            if (taken)
                throw new InvalidOperationException($"The signal \"{signalId}\" is added twice as a failure signal.");

            var success = services.Any(descriptor =>
                descriptor.ServiceType == typeof(SuccessSignalRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is SuccessSignalRegistration registration
                && registration.SignalId == signalId);
            if (success)
                throw new InvalidOperationException($"The signal \"{signalId}\" is added as a success signal too. A signal can show a move worked or failed, not both.");

            services.AddSingleton(FailureSignalRegistration.Create(signalId));
            services.TryAddSingleton(provider => OutcomeSignals.Create(
                [.. provider.GetServices<SuccessSignalRegistration>().Select(registration => registration.SignalId)],
                [.. provider.GetServices<FailureSignalRegistration>().Select(registration => registration.SignalId)]));
            services.TryAddSingleton(provider => MoveJudge.Create(
                provider.GetRequiredService<HistoryReplayer>(),
                provider.GetRequiredService<OutcomeSignals>(),
                provider.GetRequiredService<AllowedMoveCatalog>(),
                provider.GetRequiredService<IHistoryStore>(),
                provider.GetRequiredService<IScoreStore>()));
            return services;
        }

        /// <summary>
        /// Registers the <see cref="SessionEngine"/>, the one service the app or an MCP host calls to start a session,
        /// report each learner turn and read the belief. Call it last, after the vocabulary: the signals, states,
        /// likelihoods, odds, history, moves allowed in each state, the success and failure signals and the learning.
        /// It also registers the <see cref="VocabularyGuide"/> the AI reads, written once from the vocabulary when first
        /// resolved, and the <see cref="Predictor"/> that says what is expected of the next turn.
        /// It takes no options of its own, because the fade and clear margin are given to AddOdds, the rate weight to
        /// AddLearning, and the stores are the ones registered by AddHistory, AddMoveFor and AddLearning.
        /// No success or failure signal or no AddLearning before this call, or a second call, throws here so a wrong
        /// setup stops the app at startup.
        /// </summary>
        public IServiceCollection AddLearnEngine()
        {
            var added = services.Any(descriptor =>
                descriptor.ServiceType == typeof(SessionEngine) && !descriptor.IsKeyedService);
            if (added)
                throw new InvalidOperationException("The engine is added twice. Call AddLearnEngine once.");

            // The judge is only registered once the history, an allowed move and an outcome signal are added, so its
            // presence means the whole vocabulary came first.
            var judgeAdded = services.Any(descriptor =>
                descriptor.ServiceType == typeof(MoveJudge) && !descriptor.IsKeyedService);
            if (!judgeAdded)
                throw new InvalidOperationException("No success or failure signal is added, so no picked move can be judged. Add them with AddSuccessSignal or AddFailureSignal before AddLearnEngine.");

            var learningAdded = services.Any(descriptor =>
                descriptor.ServiceType == typeof(LearningOptions) && !descriptor.IsKeyedService);
            if (!learningAdded)
                throw new InvalidOperationException("The learning is not added, so the engine cannot learn how the learner learns. Add it with AddLearning before AddLearnEngine.");

            services.AddSingleton(provider => SessionEngine.Create(
                provider.GetRequiredService<OddsEngine>(),
                provider.GetRequiredService<OddsOptions>(),
                provider.GetRequiredService<MoveEffectCatalog>(),
                provider.GetRequiredService<VocabularyFingerprint>(),
                provider.GetRequiredService<IHistoryStore>(),
                provider.GetRequiredService<HistoryReplayer>(),
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<MoveCatalog>(),
                provider.GetRequiredService<MoveJudge>(),
                provider.GetRequiredService<MovePicker>(),
                provider.GetRequiredService<ILearnerModelStore>(),
                provider.GetRequiredService<Predictor>()));
            services.AddSingleton(provider => Predictor.Create(
                provider.GetRequiredService<OddsEngine>(),
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<LikelihoodTable>(),
                provider.GetService<GoalStates>(),
                provider.GetRequiredService<LearningOptions>().RateWeight));
            services.AddSingleton(provider => VocabularyGuide.Create(
                provider.GetRequiredService<SignalCatalog>(),
                provider.GetRequiredService<StateCatalog>(),
                provider.GetRequiredService<MoveCatalog>()));
            return services;
        }
    }
}
