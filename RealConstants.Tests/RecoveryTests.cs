using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// What a user is told when a selector does not resolve, and what happens when there is nobody to
/// ask.
/// </summary>
/// <remarks>
/// <para>
/// The complaint these answer was <c>step central 2</c> reporting "no constant named 'central'".
/// Two failures in one line: <c>central</c> is a token the catalogue knows, and the arguments
/// were the retired two-token order, so the grammar was least discoverable at the moment it was
/// most needed.
/// </para>
/// <para>
/// Every assertion below reads the catalogue for what it expects, so a message that started
/// carrying a list of its own would fail here rather than drift quietly.
/// </para>
/// <para>
/// <b>The prompt itself is not tested and cannot be from here.</b> Under a test host stdin is
/// redirected, which is the branch that must never prompt - so what these establish is that the
/// non-interactive path diagnoses and gives up. A person at a terminal is the only way to
/// exercise the loop.
/// </para>
/// </remarks>
public class RecoveryTests
{
    /// <summary>Runs a resolution with standard error captured, as a scripted run would see it.</summary>
    private static (bool Resolved, string Written) Resolve(string[] tokens, Recovery.Check? check = null)
    {
        TextWriter original = Console.Error;
        using StringWriter captured = new();

        try
        {
            Console.SetError(captured);
            bool resolved = Recovery.TryResolve(tokens, check, out _);
            return (resolved, captured.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public void AMethodNamedWhereAConstantBelongsIsDiagnosedAsAMethod()
    {
        (bool resolved, string written) = Resolve(["central"]);

        Assert.False(resolved);
        Assert.Contains("'central' is a method, not a constant", written, StringComparison.Ordinal);
        Assert.Contains("Methods attach with '/'", written, StringComparison.Ordinal);

        // And the suggestion is a selector that actually resolves, not a shape to fill in.
        Assert.Contains("zeta:2/central", written, StringComparison.Ordinal);
        Assert.True(Selector.TryParse("zeta:2/central", out _, out _));
    }

    [Fact]
    public void TheSuggestionForAMethodComesFromTheCatalogue()
    {
        // Every method the catalogue holds must produce a suggestion the parser accepts, so the
        // branch cannot start naming pairings that do not exist.
        foreach (Recipe recipe in Catalogue.Recipes)
        {
            string example = Selector.Example(recipe.Constant, recipe.Method);

            Assert.True(Selector.TryParse(example, out Choice[] choices, out string error), error);
            Assert.Same(recipe, Assert.Single(choices).Recipe);
        }
    }

    [Fact]
    public void AnUnknownMethodListsThatConstantsMethodsAndNotTheConstants()
    {
        (bool resolved, string written) = Resolve(["zeta:3/machin"]);

        Assert.False(resolved);

        // Every method zeta has, and no constant name that is not also a method name.
        Assert.All(
            Catalogue.For("zeta"),
            r => Assert.Contains(r.Method, written, StringComparison.Ordinal));
        Assert.DoesNotContain("sqrt", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTokenStillListsTheConstants()
    {
        (bool resolved, string written) = Resolve(["tau"]);

        Assert.False(resolved);
        Assert.All(
            Catalogue.Constants,
            c => Assert.Contains(c.Name, written, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("central", "2", "zeta:2/central")]
    [InlineData("newton", "3", "sqrt:3/newton")]
    [InlineData("zeta:3", "borwein", "zeta:3/borwein")]
    [InlineData("sqrt:2", "newton", "sqrt:2/newton")]
    public void TheRetiredTwoTokenFormIsNamedRatherThanAccepted(string first, string second, string meant)
    {
        (bool resolved, string written) = Resolve([first, second]);

        // Named, and refused: accepting it would restore the second grammar that the
        // single-grammar change removed.
        Assert.False(resolved);
        Assert.Contains("retired two-token form", written, StringComparison.Ordinal);
        Assert.Contains(meant, written, StringComparison.Ordinal);
        Assert.Contains($"'{first} {second}'", written, StringComparison.Ordinal);

        // The selector it names must itself resolve, or the advice is worse than none.
        Assert.True(Selector.TryParse(meant, out _, out _));
    }

    [Fact]
    public void TheWholeLineDiagnosisWinsOverThePerTokenOne()
    {
        // 'central' alone is diagnosed as a method; 'central 2' is diagnosed as the retired form,
        // because that is the mistake actually being made.
        (_, string alone) = Resolve(["central"]);
        (_, string pair) = Resolve(["central", "2"]);

        Assert.Contains("is a method, not a constant", alone, StringComparison.Ordinal);
        Assert.Contains("retired two-token form", pair, StringComparison.Ordinal);
    }

    [Fact]
    public void ADiagnosisDoesNotCarryTheGrammarBecauseThePromptDoes()
    {
        // It used to end every diagnosis with the shape line, and the prompt preamble opens with
        // it, so a user at a terminal read the same sentence twice four lines apart. The prompt is
        // where it belongs - it is what they are about to answer - so the diagnosis says something
        // concrete instead and stops.
        foreach (string[] tokens in (string[][])[["central"], ["central", "2"], ["zeta:3", "central"], ["tau"]])
        {
            (_, string written) = Resolve(tokens);
            Assert.DoesNotContain(Selector.Shape, written, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AScriptedRunDiagnosesAndGivesUpWithoutPrompting()
    {
        // Under a test host stdin is redirected, which is the guarded branch. If the guard were
        // ever removed this test would block on a read rather than fail, so its passing at all is
        // part of what it establishes.
        Assert.True(Console.IsInputRedirected, "this test only means anything with stdin redirected");

        (bool resolved, string written) = Resolve(["central", "2"]);

        Assert.False(resolved);
        Assert.DoesNotContain("selector>", written, StringComparison.Ordinal);
        Assert.DoesNotContain("blank to give up", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtraConditionIsCheckedTheSameWayAParseFailureIs()
    {
        // step's resolve-to-one goes through the same path, so it gets the same treatment: a
        // selection that parses but names four methods is diagnosed, not silently taken.
        (bool resolved, string written) = Resolve(
            ["zeta:3"],
            choices => choices.Length == 1 ? null : $"that names {choices.Length} methods");

        Assert.False(resolved);
        Assert.Contains($"that names {Catalogue.For("zeta").Length} methods", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AGoodSelectorNeedsNoDiagnosisAtAll()
    {
        (bool resolved, string written) = Resolve(["zeta:3/central"]);

        Assert.True(resolved);
        Assert.Empty(written);
    }
}
