using System.Globalization;

namespace LearnEngine.Web.Sessions;

/// <summary>
/// Names a session for people, from the time its id starts with, such as "Today 14:02" or "Nov 20, 09:15".
/// </summary>
public static class SessionLabels
{
    /// <summary>
    /// When the session started, in this computer's time zone, read from an id that start_session gave, or null when
    /// the id does not start with a time.
    /// </summary>
    public static DateTimeOffset? Started(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (sessionId.Length < 15)
            return null;

        var stamped = DateTime.TryParseExact(
            sessionId[..15],
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var utc);
        if (!stamped)
            return null;

        return new DateTimeOffset(utc, TimeSpan.Zero).ToLocalTime();
    }

    /// <summary>
    /// The name of the session as of the given time: "Today 14:02", "Yesterday 19:40", or the date and time for an
    /// older one. An id that does not start with a time is its own name.
    /// </summary>
    public static string For(string sessionId, DateTimeOffset now)
    {
        var started = Started(sessionId);
        if (started is null)
            return sessionId;

        var day = started.Value.Date;
        var today = now.ToLocalTime().Date;
        var time = started.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (day == today)
            return $"Today {time}";

        if (day == today.AddDays(-1))
            return $"Yesterday {time}";

        var date = started.Value.ToString(day.Year == today.Year ? "MMM d" : "MMM d yyyy", CultureInfo.InvariantCulture);
        return $"{date}, {time}";
    }
}
