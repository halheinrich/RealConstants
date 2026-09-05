namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// A knowingly wrong provider: <see cref="MachinPi"/>'s refinements shifted off pi by a fixed
/// amount, keeping <see cref="MachinPi"/>'s bounds. It exists so the cross-check can be shown to
/// fail.
/// </summary>
/// <param name="offset">How far every value is displaced. The bounds are left untouched.</param>
/// <remarks>
/// <para>
/// This is not the stub-instead-of-the-real-thing case the contract warns about, and it is not a
/// provider under test. The behaviour under test is the cross-check <i>assertion</i> - two
/// enclosures of the same constant must overlap - and an assertion nothing can fail is not
/// evidence. Feeding it a provider that is wrong by a stated amount is how the assertion earns
/// the passes it collects from the real pair.
/// </para>
/// <para>
/// Its bound is a false claim, deliberately: the values are displaced while the bounds keep
/// saying what they said. That is exactly the defect the cross-check is meant to catch, so it is
/// what a negative control has to be. Nothing outside <c>PiCrossCheckTests</c> should use this
/// type, and nothing should ever treat it as a provider of pi.
/// </para>
/// </remarks>
internal sealed class SkewedPi(BigRational offset) : IRealConstant
{
    private static readonly MachinPi Truthful = new();

    /// <summary>Gets the bound the honest provider claims, unchanged.</summary>
    /// <param name="step">The zero-based step index.</param>
    /// <returns><see cref="MachinPi"/>'s bound for that step.</returns>
    public BigRational ErrorBoundAt(int step) => Truthful.ErrorBoundAt(step);

    /// <summary>Gets the honest refinements, each displaced by the offset.</summary>
    /// <returns>A lazy, endless sequence of enclosures that do not contain pi once the offset exceeds the bound.</returns>
    public IEnumerable<Approximation> Refinements() =>
        Truthful.Refinements().Select(r => Approximation.Create(r.Value + offset, r.MaxError));
}
