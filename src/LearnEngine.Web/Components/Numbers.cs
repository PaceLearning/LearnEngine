using System.Globalization;

namespace LearnEngine.Web.Components;

/// <summary>
/// Writes the engine's numbers the same way on every page, whatever the culture of the computer.
/// </summary>
public static class Numbers
{
    /// <summary>
    /// A chance from 0 to 1 as a whole percent, such as "31%".
    /// </summary>
    public static string Percent(double chance) =>
        Math.Round(chance * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// A soft count with at most one decimal, such as "6.2" or "8".
    /// </summary>
    public static string Count(double count) =>
        Math.Round(count, 1).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// A rate, chance or bound with two decimals, such as "0.17".
    /// </summary>
    public static string Two(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// A surprise with one decimal, such as "1.4".
    /// </summary>
    public static string One(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);
}
