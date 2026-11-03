namespace LearnEngine.Core.History;

/// <summary>
/// The first event of every session: the vocabulary the session is kept under and the fade it uses.
/// A history is only replayed under the same vocabulary and always with this fade, so it gives the same belief every time.
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

    private SessionStarted(string fingerprint, double fade)
    {
        Fingerprint = fingerprint;
        Fade = fade;
    }

    /// <summary>
    /// Creates the start of a session kept under the vocabulary with the given fingerprint and with the given fade.
    /// </summary>
    public static SessionStarted Create(string fingerprint, double fade) => new(fingerprint, fade);
}
