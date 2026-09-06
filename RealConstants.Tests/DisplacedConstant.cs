namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// A knowingly wrong provider: another provider's refinements shifted off the truth by a fixed
/// amount, keeping that provider's bounds. It exists so a cross-check can be shown to fail.
/// </summary>
/// <param name="truthful">The provider to displace.</param>
/// <param name="offset">How far every value is moved. The bounds are left untouched.</param>
/// <remarks>
/// <para>
/// This is not the stub-instead-of-the-real-thing case the contract warns about, and it is not
/// a provider under test. The behaviour under test is the cross-check <i>assertion</i> - two
/// enclosures of the same constant must overlap - and an assertion nothing can fail is not
/// evidence. Feeding it a provider that is wrong by a stated amount is how that assertion earns
/// the passes it collects from the real pairs.
/// </para>
/// <para>
/// Its bound is a false claim, deliberately: the values are displaced while the bounds keep
/// saying what they said. That is exactly the defect a cross-check is meant to catch, so it is
/// what a negative control has to be. Nothing outside the cross-check tests should use this
/// type, and nothing should ever treat it as a provider of anything.
/// </para>
/// <para>
/// One type rather than one per constant. Displacing a provider while leaving its bound alone is
/// a single decision, and <c>../AGENTS.md</c> section Writing code makes the same rule in two
/// places a defect - the pi pair and the zeta(3) pair want the identical mechanism, not two that
/// happen to look alike. This is the opposite case from the providers themselves, whose
/// duplication encodes independent decisions and must be kept.
/// </para>
/// </remarks>
internal sealed class DisplacedConstant(IRealConstant truthful, BigRational offset) : IRealConstant
{
    /// <summary>Gets the bound the honest provider claims, unchanged.</summary>
    /// <param name="step">The zero-based step index.</param>
    /// <returns>The wrapped provider's bound for that step.</returns>
    public BigRational ErrorBoundAt(int step) => truthful.ErrorBoundAt(step);

    /// <summary>Gets the honest refinements, each displaced by the offset.</summary>
    /// <returns>
    /// A lazy, endless sequence of enclosures that do not contain the constant once the offset
    /// exceeds the bound.
    /// </returns>
    public IEnumerable<Approximation> Refinements() =>
        truthful.Refinements().Select(r => Approximation.Create(r.Value + offset, r.MaxError));
}
