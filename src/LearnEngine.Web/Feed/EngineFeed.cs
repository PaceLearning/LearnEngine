using System.Collections.Immutable;

namespace LearnEngine.Web.Feed;

/// <summary>
/// What the AI just did with the engine, in memory, so the pages move while it talks: the tools publish every turn they
/// keep, and the pages listen. It keeps the last <see cref="Kept"/> turns and is lost when the app stops; the histories
/// on disk are what a page reads back.
/// </summary>
public sealed class EngineFeed
{
    /// <summary>
    /// How many of the last turns the feed keeps.
    /// </summary>
    public const int Kept = 200;

    private readonly Lock writing = new();
    private ImmutableArray<TurnShown> recent = [];

    /// <summary>
    /// Raised after a turn is published, on the thread of the tool that published it. A listener must not throw or
    /// wait, so a page hands the work to its own renderer and returns.
    /// </summary>
    public event Action<TurnShown>? Shown;

    /// <summary>
    /// The last turns published, oldest first, at most <see cref="Kept"/>.
    /// </summary>
    public ImmutableArray<TurnShown> Recent => recent;

    private EngineFeed() { }

    /// <summary>
    /// Creates an empty feed.
    /// </summary>
    public static EngineFeed Create() => new();

    /// <summary>
    /// Puts the turn on the feed and tells every listener.
    /// </summary>
    public void Publish(TurnShown shown)
    {
        ArgumentNullException.ThrowIfNull(shown);

        lock (writing)
            recent = recent.Length < Kept ? recent.Add(shown) : recent.RemoveAt(0).Add(shown);

        Shown?.Invoke(shown);
    }
}
