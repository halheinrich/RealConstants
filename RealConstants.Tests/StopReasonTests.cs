using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// That every way a walk can end has a phrase of its own.
/// </summary>
/// <remarks>
/// <see cref="WalkResult.Describe"/> ends in a <c>_ =&gt; "unknown"</c> arm, which is what a stop
/// reason added without a phrase would silently print - in the outcome column of a CSV, and in the
/// sentence a walk ends on. Adding <see cref="StopReason.OracleLimit"/> is exactly that edit, so
/// this closes the arm from outside.
/// </remarks>
public class StopReasonTests
{
    [Fact]
    public void EveryStopReasonHasItsOwnPhrase()
    {
        string[] phrases =
        [
            .. Enum.GetValues<StopReason>()
                .Select(reason => new WalkResult(0, default, TimeSpan.Zero, reason)
                    .Describe(StopRules.Default)),
        ];

        Assert.DoesNotContain("unknown", phrases);
        Assert.Equal(phrases.Length, phrases.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void OutrunningTheOracleSaysHowToGoDeeper()
    {
        // A reader told only that there is nothing further to read is being left where they
        // started. The oracle depth is the knob, and it is in the catalogue.
        string phrase = new WalkResult(0, default, TimeSpan.Zero, StopReason.OracleLimit)
            .Describe(StopRules.Default);

        Assert.Contains("oracle", phrase, StringComparison.Ordinal);
        Assert.Contains(nameof(Catalogue), phrase, StringComparison.Ordinal);
    }
}
