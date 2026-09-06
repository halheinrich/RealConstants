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
    [InlineData("", 1)]
    [InlineData("   ", 1)]
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
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-3")]
    public void ANumberBelowOneIsRefusedRatherThanReinterpreted(string typed)
    {
        // The defect: 0 parsed, failed a `many > 0` guard, and fell through to the branch that
        // advances one step on unrecognised input. 0 is recognised and means zero, so advancing
        // was the walk doing something other than what it had just understood. -3 is the same
        // fault and reached a user by the same route.
        Instruction instruction = InteractiveWalk.Interpret(typed);

        Assert.Equal(InstructionKind.NotACount, instruction.Kind);

        // The number is carried so the refusal can name it back.
        Assert.Equal(int.Parse(typed, System.Globalization.CultureInfo.InvariantCulture), instruction.Steps);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("step")]
    [InlineData("3 4")]
    [InlineData("2.5")]
    public void UnrecognisedInputStillAdvancesOneStep(string typed)
    {
        // Kept deliberately. At a prompt whose commonest answer is "go on", a typo should cost one
        // refinement and not a lecture - the change above narrows this branch to input the walk
        // genuinely did not understand, and does not remove it.
        Instruction instruction = InteractiveWalk.Interpret(typed);

        Assert.Equal(InstructionKind.Advance, instruction.Kind);
        Assert.Equal(1, instruction.Steps);
    }
}
