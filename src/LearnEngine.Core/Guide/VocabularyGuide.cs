using LearnEngine.Core.Moves;
using LearnEngine.Core.Signals;
using LearnEngine.Core.States;

namespace LearnEngine.Core.Guide;

/// <summary>
/// The guide the AI reads before it reports learner turns, written in markdown from the registered vocabulary:
/// the rules for reporting signals and acting on a turn result first, then every signal, state and move with its description.
/// </summary>
public sealed class VocabularyGuide
{
    /// <summary>
    /// The whole guide as markdown, with lines ending in a line feed.
    /// </summary>
    public string Text { get; }

    private VocabularyGuide(string text) => Text = text;

    // Internal so a guide only comes from registration, written from the catalogs the engine itself uses.
    internal static VocabularyGuide Create(SignalCatalog signals, StateCatalog states, MoveCatalog moves)
    {
        var signalLines = string.Join('\n', signals.All.Select(signal => $"- `{signal.Id}`: {signal.Description}"));
        var stateLines = string.Join('\n', states.All.Select(state => $"- `{state.Id}`: {state.Description}"));
        var moveLines = string.Join('\n', moves.All.Select(move => $"- `{move.Id}`: {move.Description}"));

        var text = $"""
            ## Reporting a learner turn

            Report the signals of every learner turn, by id only.
            - A signal is a fact about what the learner said in that turn, never a guess about why they said it.
            - Go only by the learner's words in that turn, not by your own replies. Never report the learner's words.
            - A turn with nothing that fits gets no signals. Report it anyway, with none.
            - Several signals can fit one turn. Report every one that fits.

            ## Acting on a turn result

            Make the picked move only when the state is clear. When it is not clear or no move is picked, keep going and answer what the learner asked for. Even with a move picked, answer anything the learner asks for outright.

            ## Signals

            {signalLines}

            ## States

            The engine works out from the signals which state the learner is in. You never report a state.

            {stateLines}

            ## Moves

            When the learner is clearly in a state, the engine picks one of these moves for you to make next.

            {moveLines}
            """;

        // The raw string takes the line endings of this source file, which differ between checkouts.
        return new(text.ReplaceLineEndings("\n"));
    }
}
