using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// What a line typed at the step prompt means.
/// </summary>
/// <remarks>
/// <para>
/// The prompt itself is terminal-gated and cannot be exercised from a scripted session, which is
/// how <c>0</c> came to advance one step and reach a user. What a line <i>means</i> is a pure
/// function of the line, needs no keypress, and is what these hold. The reading and the
/// interpreting were one method until that defect; separating them is what made this file
/// possible.
/// </para>
/// <para>
/// What is still not covered here is the loop around it: that <c>h</c> and a refused count return
/// to the prompt rather than costing a refinement is visible only at a terminal.
/// </para>
/// </remarks>
public class InteractiveWalkTests
{
    [Theory]
    [InlineData("q")]
    [InlineData("Q")]
    [InlineData("  q  ")]
    public void QuitStops(string typed) =>
        Assert.Equal(InstructionKind.Stop, InteractiveWalk.Interpret(typed).Kind);

    [Fact]
    public void EndOfInputStops() =>
        Assert.Equal(InstructionKind.Stop, InteractiveWalk.Interpret(null).Kind);

    [Theory]
    [InlineData("h")]
    [InlineData("H")]
    public void HelpAsksForHelp(string typed) =>
        Assert.Equal(InstructionKind.Help, InteractiveWalk.Interpret(typed).Kind);

    [Fact]
    public void RunOnAdvancesWithoutBound()
    {
        Instruction instruction = InteractiveWalk.Interpret("r");

        Assert.Equal(InstructionKind.Advance, instruction.Kind);
        Assert.Equal(int.MaxValue, instruction.Steps);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("5", 5)]
    [InlineData(" 12 ", 12)]
    public void ACountAdvancesThatMany(string typed, int expected)
    {
        Instruction instruction = InteractiveWalk.Interpret(typed);

        Assert.Equal(InstructionKind.Advance, instruction.Kind);
        Assert.Equal(expected, instruction.Steps);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void EnterAndWhitespaceOnlyBothAdvanceOneStep(string typed)
    {
        // Enter is the sole thing that advances without a count, and whitespace-only is judged to
        // be Enter: once submitted, a line of spaces looks exactly like an empty one, so refusing
        // it would refuse a gesture the user cannot tell apart from the one that works.
        Instruction instruction = InteractiveWalk.Interpret(typed);

        Assert.Equal(InstructionKind.Advance, instruction.Kind);
        Assert.Equal(1, instruction.Steps);
    }

    /// <summary>Every shape of line the walk does not recognise.</summary>
    /// <remarks>
    /// One list, walked by the invariant test below, because the ruling is one rule rather than a
    /// set of cases: rejected, explained, state unchanged. A number below one and a stray paste
    /// differ only in what the explanation says.
    /// </remarks>
    public static TheoryData<string> Unrecognised =>
    [
        "0",            // a count of nothing
        "-1",
        "-3",
        "5x",           // a near miss for a count
        "1 2",
        "2.5",
        "x",            // a bare stray token
        "step",
        "q q",          // junk around a valid key
        " h h ",
        "rr",
        ". zeta:2/central",   // the paste that produced the ruling
        "zeta:2/central",
    ];

    [Theory]
    [MemberData(nameof(Unrecognised))]
    public void UnrecognisedInputIsRefusedExplainedAndChangesNothing(string typed)
    {
        // The invariant, stated once and checked over everything: nothing the walk fails to
        // recognise may advance it. The overturned position was that a typo should cost one step
        // rather than a lecture, and what it produced at a terminal was "> . zeta:2/central"
        // followed by a row - a selector pasted at the wrong prompt, answered as though it had
        // been Enter, and indistinguishable in the transcript from input being dropped.
        Instruction instruction = InteractiveWalk.Interpret(typed);

        Assert.Equal(InstructionKind.Refused, instruction.Kind);
        Assert.Equal(0, instruction.Steps);
        Assert.NotEmpty(instruction.Explanation);

        // That nothing happened is the half of the ruling a user reads rather than infers, so it
        // is stated in the same words every time and is not left to be inferred from the prompt
        // coming back.
        Assert.StartsWith("nothing done - ", instruction.Explanation, StringComparison.Ordinal);

        // One line, and it does not reprint the key list - h exists for that.
        Assert.DoesNotContain("\n", instruction.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("run to a stop rule", instruction.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0", "0 is not a step count")]
    [InlineData("-3", "-3 is not a step count")]
    [InlineData("5x", "'5x' is not a key or a count")]
    [InlineData(". zeta:2/central", "'. zeta:2/central' is not a key or a count")]
    public void ARefusalNamesWhatWasTypedAndWhatToDo(string typed, string expected)
    {
        // Says what was wrong and says what to do, which is what made 0's wording the model.
        string explanation = InteractiveWalk.Interpret(typed).Explanation;

        Assert.Contains(expected, explanation, StringComparison.Ordinal);
        Assert.Contains("Enter takes one step", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ALongPasteIsElidedRatherThanEchoedEntire()
    {
        // A paste is one of the cases this rule exists for, and answering a mistyped line with a
        // worse-looking one helps nobody.
        string explanation = InteractiveWalk.Interpret(new string('z', 200)).Explanation;

        Assert.Contains("...", explanation, StringComparison.Ordinal);
        Assert.True(explanation.Length < 120, $"a refusal ran to {explanation.Length} characters");
    }
}
