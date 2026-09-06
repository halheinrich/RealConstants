using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="Selector"/>, the command line's one grammar:
/// <c>&lt;constant&gt;[:&lt;param&gt;][/&lt;method&gt;]</c>.
/// </summary>
/// <remarks>
/// <para>
/// A parser in a runnable project is still ordinary code with a known answer, so it is tested
/// here rather than left to be exercised by hand. <c>../AGENTS.md</c> § Exactness discipline
/// separates a test from an experiment by whether the answer is known and whether the run
/// depends on wall-clock time; this has one and does not.
/// </para>
/// <para>
/// The assertions below deliberately name no constant or method that these tests invent. They
/// read the catalogue, exactly as the parser does, so that adding a provider cannot leave a test
/// asserting a stale list.
/// </para>
/// </remarks>
public class SelectorTests
{
    // ---------- The shapes that parse ----------

    [Fact]
    public void AConstantWithNoParameterAndNoMethodNamesEveryMethodForIt()
    {
        Assert.True(Selector.TryParse("pi", out Choice[] choices, out string error));
        Assert.Empty(error);

        Assert.Equal(Catalogue.For("pi").Length, choices.Length);
        Assert.All(choices, c => Assert.Equal("pi", c.Constant));
        Assert.All(choices, c => Assert.Equal(0, c.Parameter));
    }

    [Fact]
    public void AConstantWithAParameterNamesEveryMethodForIt()
    {
        Assert.True(Selector.TryParse("zeta:3", out Choice[] choices, out string error));
        Assert.Empty(error);

        Assert.Equal(Catalogue.For("zeta").Length, choices.Length);
        Assert.All(choices, c => Assert.Equal(3, c.Parameter));
    }

    [Fact]
    public void AMethodNarrowsTheSelectionToOne()
    {
        Assert.True(Selector.TryParse("zeta:3/central", out Choice[] choices, out string error));
        Assert.Empty(error);

        Choice only = Assert.Single(choices);
        Assert.Equal("zeta", only.Constant);
        Assert.Equal(3, only.Parameter);
        Assert.Equal("central", only.Recipe.Method);
    }

    [Fact]
    public void AMethodOnAConstantThatTakesNoParameterParses()
    {
        Assert.True(Selector.TryParse("pi/machin", out Choice[] choices, out string error));
        Assert.Empty(error);

        Choice only = Assert.Single(choices);
        Assert.Equal("pi", only.Constant);
        Assert.Equal("machin", only.Recipe.Method);
    }

    [Fact]
    public void SeveralSelectorsConcatenateInTheOrderGiven()
    {
        Assert.True(
            Selector.TryParseAll(["pi/machin", "sqrt:2/newton", "zeta:3/borwein"],
                out Choice[] choices, out string error));
        Assert.Empty(error);

        Assert.Equal(
            ["pi/machin", "sqrt:2/newton", "zeta:3/borwein"],
            choices.Select(Selector.Spell));
    }

    [Fact]
    public void SelectorsThatOverlapAreNotDeduplicated()
    {
        // Two selectors naming the same pairing produce it twice, and that is left alone: a
        // caller asking for the same walk twice gets it twice, which is what they typed. The
        // alternative - silently collapsing - would make a repeated run look like a dropped one.
        Assert.True(
            Selector.TryParseAll(["zeta:3/central", "zeta:3/central"], out Choice[] choices, out _));

        Assert.Equal(2, choices.Length);
    }

    [Fact]
    public void NoSelectorsIsNotAnError()
    {
        Assert.True(Selector.TryParseAll([], out Choice[] choices, out string error));

        Assert.Empty(choices);
        Assert.Empty(error);
    }

    // ---------- The shapes that do not ----------

    [Fact]
    public void AnUnknownConstantIsRefusedAndTheKnownOnesAreListed()
    {
        Assert.False(Selector.TryParse("tau", out Choice[] choices, out string error));

        Assert.Empty(choices);
        Assert.Contains("tau", error, StringComparison.Ordinal);
        Assert.All(
            Catalogue.Constants,
            c => Assert.Contains(c.Name, error, StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownMethodForAKnownConstantIsRefusedAndTheRealOnesAreListed()
    {
        Assert.False(Selector.TryParse("zeta:3/machin", out Choice[] choices, out string error));

        Assert.Empty(choices);
        Assert.Contains("machin", error, StringComparison.Ordinal);
        Assert.All(
            Catalogue.For("zeta"),
            r => Assert.Contains(r.Method, error, StringComparison.Ordinal));
    }

    [Fact]
    public void AParameterOnAConstantThatTakesNoneIsRefused()
    {
        Assert.False(Selector.TryParse("pi:2", out Choice[] choices, out string error));

        Assert.Empty(choices);
        Assert.Contains("no parameter", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingParameterOnAConstantThatNeedsOneIsRefused()
    {
        Assert.False(Selector.TryParse("zeta", out Choice[] choices, out string error));

        Assert.Empty(choices);
        Assert.Contains("needs a parameter", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AParameterThatIsNotAnIntegerIsRefused()
    {
        Assert.False(Selector.TryParse("zeta:three", out _, out string error));

        Assert.Contains("three", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zeta:3/central/euler")]
    [InlineData("zeta:2:3/central")]
    public void AMalformedSelectorIsRefused(string token)
    {
        Assert.False(Selector.TryParse(token, out Choice[] choices, out _));

        Assert.Empty(choices);
    }

    [Fact]
    public void TheFirstBadSelectorStopsTheParse()
    {
        Assert.False(
            Selector.TryParseAll(["pi/machin", "tau", "zeta:3/central"], out Choice[] choices, out string error));

        Assert.Empty(choices);
        Assert.Contains("tau", error, StringComparison.Ordinal);
    }

    // ---------- What the parser deliberately does not do ----------

    [Fact]
    public void ADomainRejectionIsNotTheParsersBusiness()
    {
        // sqrt:4 parses. That 4 is a perfect square is a fact NewtonSquareRoot owns and states in
        // a guard with its reasoning attached, and it is discovered by asking that type to build
        // one - not by teaching this parser which radicands are acceptable. Putting the rule here
        // too would be the copy that goes stale.
        Assert.True(Selector.TryParse("sqrt:4/newton", out Choice[] choices, out string error));
        Assert.Empty(error);

        Choice only = Assert.Single(choices);
        Assert.Null(Catalogue.TryCreate(only.Recipe, only.Parameter, out string refusal));

        // And the refusal the user sees is the provider's own words.
        Assert.Contains("perfect square", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void TheParserResolvesThroughTheCatalogueRatherThanAListOfItsOwn()
    {
        // The property that matters for adding a provider later: every name the parser accepts
        // comes back as a catalogue row, and every catalogue row is reachable by naming it.
        foreach (Recipe recipe in Catalogue.Recipes)
        {
            int parameter = Catalogue.FindConstant(recipe.Constant)!.Parameter.Length == 0 ? 0 : 3;
            string token = Selector.Spell(new Choice(recipe.Constant, parameter, recipe));

            Assert.True(Selector.TryParse(token, out Choice[] choices, out string error), error);
            Assert.Same(recipe, Assert.Single(choices).Recipe);
        }
    }

    // ---------- Spelling ----------

    [Fact]
    public void SpellingRoundTripsThroughTheParser()
    {
        foreach (string token in (string[])["pi/leibniz", "sqrt:2/newton", "zeta:6/euler"])
        {
            Assert.True(Selector.TryParse(token, out Choice[] choices, out _));
            Assert.Equal(token, Selector.Spell(Assert.Single(choices)));
        }
    }

    [Fact]
    public void SpellingOmitsTheParameterWhereAConstantTakesNone() =>
        Assert.Equal("pi", Selector.Spell("pi", 0));
}
