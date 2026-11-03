using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

/// <summary>
/// The first event of every session: the vocabulary the session is kept under, the fade it uses and the clear margin
/// it uses. A history is only replayed under the same vocabulary and always with this fade and clear margin, so it
/// gives the same belief and the same judgments every time.
/// </summary>
public sealed class SessionStarted : HistoryEvent
{
    /// <summary>
    /// The fingerprint of the vocabulary the session is kept under, from <see cref="VocabularyFingerprint.Value"/>.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// How far the belief moves back to the priors before every learner turn of the session, from 0 to 1.
    /// </summary>
    public double Fade { get; }

    /// <summary>
    /// How far the leading state must be ahead of the runner up to be clear in the session, from 0 to 1.
    /// Only a clear state lets a move be picked, and a picked move is only judged while its state stays clear.
    /// </summary>
    public double ClearMargin { get; }

    // Internal, not private, so the source generated JSON reader can call it.
    [JsonConstructor]
    internal SessionStarted(string fingerprint, double fade, double clearMargin)
    {
        Fingerprint = fingerprint;
        Fade = fade;
        ClearMargin = clearMargin;
    }

    /// <summary>
    /// Creates the start of a session kept under the vocabulary with the given fingerprint, with the given fade and clear margin.
    /// </summary>
    public static SessionStarted Create(string fingerprint, double fade, double clearMargin) => new(fingerprint, fade, clearMargin);
}
