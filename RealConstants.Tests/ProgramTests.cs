using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// That every example the usage screen offers is a command that still runs.
/// </summary>
/// <remarks>
/// <para>
/// The third round-trip in this project, after the catalogue's and the suggested example's, and it
/// is the same shape for the same reason: a hand-written example rots the moment a provider is
/// renamed or an order is dropped from a family, and an example that no longer runs is worse than
/// no example at all. The command half is checkable and is checked here; the reason half is
/// editorial and is deliberately not derived.
/// </para>
/// <para>
/// Nothing here runs an experiment. These assert that the selectors resolve and that a walk's
/// selection would build - a walk itself depends on wall-clock time, which <c>../AGENTS.md</c>
/// section Exactness discipline puts outside what a test may do.
/// </para>
/// </remarks>
public class ProgramTests
{
    public static TheoryData<string, string[], string> Examples
    {
        get
        {
            TheoryData<string, string[], string> data = [];

            foreach ((string command, string[] selectors, string reason) in Program.Examples)
            {
                data.Add(command, selectors, reason);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void EveryExampleParsesThroughTheOneGrammar(string command, string[] selectors, string reason)
    {
        Assert.True(Selector.TryParseAll(selectors, out Choice[] choices, out string error), error);

        // A selector that named nothing would render as a plausible example and do nothing.
        if (selectors.Length > 0)
        {
            Assert.NotEmpty(choices);
        }

        // The reason is not derived, but it must exist: an unannotated example is the thing this
        // screen already had.
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.False(string.IsNullOrWhiteSpace(command));
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void EveryWalkExampleNamesOneMethodThatBuilds(string command, string[] selectors, string reason)
    {
        _ = reason;

        if (!string.Equals(command, Program.StepCommand, StringComparison.Ordinal))
        {
            return;
        }

        Assert.True(Selector.TryParseAll(selectors, out Choice[] choices, out string error), error);

        // The same two conditions the walk applies to a selection it is handed: exactly one
        // method, and a provider that accepts the parameter. sqrt:4/newton would parse and fail
        // here, which is exactly the kind of example that must not reach the screen.
        Choice only = Assert.Single(choices);
        Assert.NotNull(Catalogue.TryCreate(only.Recipe, only.Parameter, out string refusal));
        Assert.Empty(refusal);
    }

    [Fact]
    public void TheExamplesCoverTheShapesTheGrammarExistsFor()
    {
        // The multi-selector case especially: it is what one grammar over both commands bought,
        // and it is the least guessable from the shape line alone.
        Assert.Contains(Program.Examples, e => e.Command == Program.CompareCommand && e.Selectors.Length == 0);
        Assert.Contains(Program.Examples, e => e.Command == Program.CompareCommand && e.Selectors.Length > 1);
        Assert.Contains(Program.Examples, e => e.Command == Program.StepCommand);

        // And one selector naming a constant without a method, which is the other thing the
        // grammar does that a reader would not guess.
        Assert.Contains(
            Program.Examples,
            e => e.Selectors.Any(s => !s.Contains('/', StringComparison.Ordinal)));
    }

    [Fact]
    public void TheRatifiedExamplesSayWhatTheyAre()
    {
        // A reader's route from this tool into SPEC-rational-ratio.md section 4 is the reason
        // those examples are in the list at all. If the reasons stopped naming it the examples
        // would still run and would have lost their point.
        Assert.True(
            Program.Examples.Count(e => e.Reason.Contains("section 4", StringComparison.Ordinal)) >= 3,
            "the cross-check pairs and the negative control should each name what ratifies them");
    }
}
