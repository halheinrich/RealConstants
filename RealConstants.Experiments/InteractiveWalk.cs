using System.Diagnostics;
using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>What a line typed at the step prompt asks the walk to do.</summary>
internal enum InstructionKind
{
    /// <summary>End the walk - <c>q</c>, or end of input.</summary>
    Stop,

    /// <summary>Show the help screen, without spending a refinement on the question.</summary>
    Help,

    /// <summary>Take some steps. <c>r</c> is this with every step there is.</summary>
    Advance,

    /// <summary>
    /// Anything the walk does not recognise: a number it cannot honour, a near miss for a key, a
    /// stray paste. Explained, and nothing happens.
    /// </summary>
    Refused,
}

/// <summary>One instruction from the step prompt.</summary>
/// <param name="Kind">Which instruction it is.</param>
/// <param name="Steps">
/// How many steps to take when <see cref="Kind"/> is <see cref="InstructionKind.Advance"/>, and
/// zero otherwise - a refused line advances by nothing, which is the whole of the rule.
/// </param>
/// <param name="Explanation">
/// One line saying what was wrong and what to do, when <see cref="Kind"/> is
/// <see cref="InstructionKind.Refused"/>. Empty otherwise.
/// </param>
internal readonly record struct Instruction(InstructionKind Kind, int Steps, string Explanation);

/// <summary>
/// The interactive walk: one constant, one method, one step at a time, so the algorithm can be
/// felt rather than summarised.
/// </summary>
/// <remarks>
/// <para>
/// Every column is derived from <c>ErrorBoundAt</c>, <c>Refinements()</c> and an oracle. No
/// provider was given a diagnostic hook and <c>IRealConstant</c> did not change; putting an
/// experiment's convenience into a ratified contract is the edit to refuse.
/// </para>
/// <para>
/// <b>The prompt is guarded by <see cref="Console.IsInputRedirected"/>.</b> Under a pipe, a
/// redirect or any other non-terminal stdin, there is nobody to press a key, so the walk falls
/// through to the stop rules and prints a line saying the pause was skipped rather than blocking
/// forever on a read that returns end-of-file.
/// </para>
/// </remarks>
internal static class InteractiveWalk
{
    /// <summary>
    /// How many decimal places the value column will consider before giving up on the prefix.
    /// </summary>
    /// <remarks>
    /// A cap, not a width. The column shows the digits the enclosure pins and no more, so it is
    /// one character wide at step 0 of a slow method and grows as the walk converges. Declining
    /// to print an earned digit costs a reader nothing; printing an unearned one is the thing
    /// ../VISION.md forbids.
    /// </remarks>
    private const int ValuePlaces = 40;

    /// <summary>Walks one method for one constant.</summary>
    /// <param name="selectors">
    /// The selectors as typed. They must together name exactly one method: a walk is one
    /// sequence of refinements, and there is no sensible reading of two.
    /// </param>
    /// <returns>Zero on a completed walk, two on a selection that does not resolve to one.</returns>
    public static int Walk(string[] selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);

        if (selectors.Length == 0)
        {
            Console.Error.WriteLine("step needs a selector, e.g. step zeta:3/central - try 'list'.");
            return 2;
        }

        // One condition, checked wherever the selection comes from - the command line or a reply
        // at the prompt. A walk is one sequence of refinements and there is no sensible reading
        // of two, and a provider that will not build is not a walk either.
        if (!Recovery.TryResolve(selectors, Resolvable, out Choice[] choices))
        {
            return 2;
        }

        Choice choice = choices[0];
        string name = choice.Constant;
        int parameter = choice.Parameter;
        Recipe recipe = choice.Recipe;

        IRealConstant constant = Catalogue.TryCreate(recipe, parameter, out _)!;

        StopRules rules = StopRules.Default;
        (Approximation oracle, string oracleDescription) = Catalogue.Oracle(name, parameter, recipe.Method);

        Console.Error.WriteLine($"computing {Catalogue.Title(name, parameter)} by {recipe.Summary}");
        Console.Error.WriteLine($"  {recipe.Identity(parameter)}");
        Console.Error.WriteLine($"  {recipe.StepMeaning}; {recipe.Cadence}   [{recipe.Provider}]");
        Console.Error.WriteLine(
            $"  oracle: {oracleDescription}, half-width {Presentation.Magnitude(oracle.MaxError)}");
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  stop rules: {rules.MaxSteps} steps, {rules.MaxSeconds:F0} s, " +
            $"{rules.MaxDenominatorBits} denominator bits, or a bound finer than that oracle"));

        bool interactive = !Console.IsInputRedirected;
        if (interactive)
        {
            Console.Error.WriteLine(
                "  " + string.Join(", ", Keys.Select(k => k.Key + " = " + k.Meaning)));
        }
        else
        {
            Console.Error.WriteLine("  stdin is not a terminal, so the pause is skipped and this runs to a stop rule");
        }

        Console.Error.WriteLine();
        Console.WriteLine("step | value | claimed | realised | realised/claimed | gained | den_bits | us");

        // Two clocks, because they answer different questions. `computing` is the one the
        // wall-clock stop rule reads, and it is PAUSED while the prompt waits: a rule meant to
        // bound how long a method takes must not count how long a person took to press a key.
        // `session` is the whole elapsed time and is reported alongside it.
        Stopwatch computing = Stopwatch.StartNew();
        Stopwatch session = Stopwatch.StartNew();
        Stopwatch perStep = new();
        int pending = interactive ? 1 : int.MaxValue;
        double previousDigits = 0;
        int step = 0;
        StopReason reason = StopReason.StepLimit;

        // The enumerator is driven by hand rather than by `foreach`, so that `perStep` can
        // bracket MoveNext() and nothing else. Under `foreach` the refinement is computed
        // before the body is entered, so a timer started in the body measures the oracle
        // subtraction and the decimal rendering instead of the step - measured 2026-09-05 on a
        // redirected central s=2 run, where the column summed to 6.80 s of a 10.00 s walk and
        // the 3.20 s it omitted was the stepping the column exists to show.
        using IEnumerator<Approximation> refinements = constant.Refinements().GetEnumerator();

        while (true)
        {
            perStep.Restart();
            if (!refinements.MoveNext())
            {
                throw new InvalidOperationException("Refinements() ended, which the contract forbids.");
            }

            Approximation refinement = refinements.Current;
            TimeSpan stepCost = perStep.Elapsed;

            // The realised error is only known to within the oracle's own half-width, so it is
            // reported as the largest it could be. Once the walk is finer than the oracle that
            // number stops meaning anything - it would settle at the oracle's half-width and the
            // ratio would climb past one, reading exactly like a violated bound - so both
            // columns say so instead of printing a figure that invites the wrong conclusion.
            bool resolved = refinement.MaxError > oracle.MaxError;
            BigRational realised = BigRational.Abs(refinement.Value - oracle.Value) + oracle.MaxError;

            double digits = -Presentation.DecimalExponent(refinement.MaxError);

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{step} | {Presentation.Earned(refinement, ValuePlaces)} | " +
                $"{Presentation.Magnitude(refinement.MaxError)} | " +
                $"{(resolved ? Presentation.Magnitude(realised) : "past oracle")} | " +
                $"{(resolved ? Presentation.Ratio(realised, refinement.MaxError) : "-")} | " +
                $"{digits - previousDigits:F2} | {Runner.DenominatorBits(refinement)} | " +
                $"{stepCost.TotalMicroseconds:F0}"));

            previousDigits = digits;
            step++;

            // The fifth stop rule, and the one this walk hits first on a converging method. Past
            // here every remaining column is fixed: realised and its ratio read "past oracle", and
            // the value column has already reached its cap, so the rows are identical and endless.
            // A run of zeta:3/central produced them to step 4565 before this existed. Nothing was
            // wrong with any of them; there was just nothing left to read.
            if (!resolved)
            {
                reason = StopReason.OracleLimit;
                break;
            }

            if (step >= rules.MaxSteps)
            {
                reason = StopReason.StepLimit;
                break;
            }

            if (computing.Elapsed.TotalSeconds >= rules.MaxSeconds)
            {
                reason = StopReason.TimeLimit;
                break;
            }

            if (Runner.DenominatorBits(refinement) >= rules.MaxDenominatorBits)
            {
                reason = StopReason.SizeLimit;
                break;
            }

            if (!interactive)
            {
                continue;
            }

            pending--;
            if (pending > 0)
            {
                continue;
            }

            // The pause is not the method's cost. Measured 2026-09-05 against the first
            // version of this file, which ran one clock straight through the read and then
            // reported "did not finish within 10 s" after two instant steps and twenty-five
            // seconds of somebody thinking - a false statement about the method, which is the
            // one kind of output this bench may not produce.
            computing.Stop();
            bool stopping = ReadInstruction(rules, ref pending);
            computing.Start();

            if (stopping)
            {
                reason = StopReason.UserQuit;
                break;
            }
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"stopped after {step} steps, {computing.Elapsed.TotalSeconds:F2} s computing " +
            $"({session.Elapsed.TotalSeconds:F2} s including the pause): " +
            $"{new WalkResult(step, default, computing.Elapsed, reason).Describe(rules)}"));

        return 0;
    }

    /// <summary>Says whether a selection is one this walk can run, and why not when it is not.</summary>
    /// <param name="choices">The resolved selection.</param>
    /// <returns><see langword="null"/> when the walk can proceed, otherwise the diagnosis.</returns>
    private static string? Resolvable(Choice[] choices)
    {
        if (choices.Length != 1)
        {
            // The failure the grammar introduces: a constant with no method names all of them,
            // which is right for compare and meaningless here. Naming what was selected, and one
            // way to narrow it, beats reprinting a usage line the reader has already read.
            string named = string.Join(", ", choices.Select(Selector.Spell));

            return choices.Length == 0
                ? "that names no method"
                : $"that names {choices.Length} methods; pick one, e.g. {Selector.Spell(choices[0])}"
                  + Environment.NewLine + $"  selected: {named}";
        }

        Choice only = choices[0];

        return Catalogue.TryCreate(only.Recipe, only.Parameter, out string refusal) is null
            ? $"{Selector.Spell(only)}: {refusal}"
            : null;
    }

    /// <summary>
    /// Reads one actionable instruction from the prompt, answering help requests first.
    /// </summary>
    /// <param name="rules">The stop rules, which the help screen quotes.</param>
    /// <param name="pending">Set to how many steps to take before prompting again.</param>
    /// <returns><see langword="true"/> if the walker asked to stop.</returns>
    /// <remarks>
    /// A loop rather than a single read, because two instructions must not consume a step:
    /// <c>h</c>, since a reader who has to spend a refinement to find out what the columns mean is
    /// being charged for the question, and a refusal, since spending one on being told no is the
    /// same charge. Reading a line is all this does; what a line means is <see cref="Interpret"/>,
    /// which is testable and is where that reasoning lives.
    /// </remarks>
    private static bool ReadInstruction(StopRules rules, ref int pending)
    {
        while (true)
        {
            Console.Error.Write("> ");
            Instruction instruction = Interpret(Console.ReadLine());

            if (instruction.Kind == InstructionKind.Stop)
            {
                return true;
            }

            if (instruction.Kind == InstructionKind.Help)
            {
                WriteHelp(rules);
                continue;
            }

            if (instruction.Kind == InstructionKind.Refused)
            {
                Console.Error.WriteLine($"  {instruction.Explanation}");
                continue;
            }

            pending = instruction.Steps;
            return false;
        }
    }

    /// <summary>Reads one line typed at the step prompt as the instruction it gives.</summary>
    /// <param name="typed">The line, or <see langword="null"/> at end of input.</param>
    /// <returns>What it asks for.</returns>
    /// <remarks>
    /// <para>
    /// Separated from the read so that it can be tested, which is the point: the prompt itself is
    /// terminal-gated and unverifiable from a scripted session, but what a line <i>means</i> is a
    /// pure function of the line and nothing about it needs a keypress. The defect this was pulled
    /// out for - <c>0</c> advancing one step - was found by a person in ten seconds and was not
    /// reachable by any test in the suite.
    /// </para>
    /// <para>
    /// <b>One rule for everything it does not recognise: rejected, explained, state unchanged.</b>
    /// A number below one, a near miss for a key, a stray paste - these were three special cases
    /// and a fallthrough, and they are one branch now.
    /// </para>
    /// <para>
    /// This file argued the other way and was wrong. The position was that at a prompt whose
    /// commonest answer is "go on", a typo should cost one refinement and not a lecture, so
    /// anything unrecognised advanced one step. What that produced at a terminal was
    /// <c>&gt; . zeta:2/central</c> followed by a row, which is a selector pasted at the wrong
    /// prompt being answered as though it had been Enter. The argument against it is the one that
    /// carried the refusal of <c>0</c> one commit earlier and should have carried this at the same
    /// time: a line that silently becomes one step is indistinguishable, in the transcript, from
    /// input being dropped. It is worse than <c>0</c> was, since <c>0</c> at least did nothing
    /// surprising. And a refusal is not a lecture - it is one line, and it does not reprint the
    /// keys, because <c>h</c> exists.
    /// </para>
    /// <para>
    /// <b><c>Enter</c> is the sole thing that advances without a count</b>, and is untouched.
    /// Whitespace-only is treated as <c>Enter</c>: submitted, a line of spaces looks exactly like
    /// an empty one, so refusing it would refuse a gesture the user cannot tell apart from the one
    /// that works.
    /// </para>
    /// <para>
    /// End of input counts as a request to stop - under a terminal that is Ctrl+Z or Ctrl+D, and
    /// the redirected path never reaches here at all.
    /// </para>
    /// </remarks>
    public static Instruction Interpret(string? typed)
    {
        if (typed is null)
        {
            return new Instruction(InstructionKind.Stop, 0, string.Empty);
        }

        string trimmed = typed.Trim();

        // Empty is Enter, and whitespace-only is Enter too. A line of spaces looks exactly like an
        // empty one once it has been submitted, so refusing it would refuse a gesture the user
        // cannot tell apart from the one that works.
        if (trimmed.Length == 0)
        {
            return new Instruction(InstructionKind.Advance, 1, string.Empty);
        }

        if (trimmed.Equals(QuitKey, StringComparison.OrdinalIgnoreCase))
        {
            return new Instruction(InstructionKind.Stop, 0, string.Empty);
        }

        if (trimmed.Equals(HelpKey, StringComparison.OrdinalIgnoreCase))
        {
            return new Instruction(InstructionKind.Help, 0, string.Empty);
        }

        if (trimmed.Equals(RunKey, StringComparison.OrdinalIgnoreCase))
        {
            return new Instruction(InstructionKind.Advance, int.MaxValue, string.Empty);
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int many))
        {
            return many >= 1
                ? new Instruction(InstructionKind.Advance, many, string.Empty)
                : Refuse(string.Create(CultureInfo.InvariantCulture,
                    $"{many} is not a step count; it must be at least 1, and Enter takes one step"));
        }

        return Refuse($"'{Echo(trimmed)}' is not a key or a count; " +
                      $"Enter takes one step, {HelpKey} lists the keys");
    }

    /// <summary>Builds a refusal: no advance, and one line saying why.</summary>
    /// <param name="because">What was wrong and what to do, without the leading clause.</param>
    /// <returns>The instruction.</returns>
    /// <remarks>
    /// <b>The leading clause is the same words every time and is not optional.</b> That nothing
    /// happened is the half of this rule a user reads rather than infers, and it is the half that
    /// separates a refusal from input being dropped - which is the whole complaint that produced
    /// the rule.
    /// </remarks>
    private static Instruction Refuse(string because) =>
        new(InstructionKind.Refused, 0, "nothing done - " + because);

    /// <summary>Quotes typed input back at a length a prompt can carry.</summary>
    /// <param name="trimmed">The line as typed, trimmed.</param>
    /// <returns>The line, shortened with an ellipsis past <see cref="EchoLimit"/> characters.</returns>
    /// <remarks>
    /// A stray paste is one of the cases this rule exists for, and a paste can be a whole
    /// selector, a path, or a screenful. Echoing it entire would answer a mistyped line with a
    /// worse-looking one.
    /// </remarks>
    private static string Echo(string trimmed) =>
        trimmed.Length <= EchoLimit ? trimmed : trimmed[..EchoLimit] + "...";

    /// <summary>What each key does. The single source for both the header line and the help.</summary>
    /// <remarks>
    /// One table rather than a legend in the header and a list in the help. Two renderings of
    /// one fact is what <c>../AGENTS.md</c> section Writing code calls the same rule in two
    /// places, and a key added to one and not the other is exactly how it goes wrong.
    /// </remarks>
    private static readonly (string Key, string Meaning)[] Keys =
    [
        ("Enter", "one step"),
        ("<n>", "that many steps, n at least 1"),
        (RunKey, "run to a stop rule"),
        (HelpKey, "help"),
        (QuitKey, "quit"),
    ];

    /// <summary>Run to a stop rule.</summary>
    private const string RunKey = "r";

    /// <summary>Show the help screen.</summary>
    private const string HelpKey = "h";

    /// <summary>End the walk.</summary>
    private const string QuitKey = "q";

    /// <summary>How much of a mistyped line a refusal quotes back before eliding it.</summary>
    private const int EchoLimit = 30;

    /// <summary>What each column of the walk means.</summary>
    private static readonly (string Column, string Meaning)[] Columns =
    [
        ("step", "zero-based index into Refinements()"),
        ("value", "only the digits Lower and Upper agree on; ? when they agree on none"),
        ("claimed", "MaxError - the proven bound this enclosure carries, not its error"),
        ("realised", "|value - oracle| widened by the oracle's own half-width"),
        ("realised/claimed", "upper bound on how much of the claimed bound the error uses"),
        ("gained", "decimal places won by this step alone, from -log10(claimed)"),
        ("den_bits", "bit-length of the larger denominator the enclosure carries"),
        ("us", "microseconds to compute this refinement, rendering excluded - noisy per row"),
    ];

    /// <summary>Writes the help screen to standard error, so redirected data stays clean.</summary>
    /// <param name="rules">The stop rules in force.</param>
    private static void WriteHelp(StopRules rules)
    {
        int keyWidth = Keys.Max(k => k.Key.Length);
        int columnWidth = Columns.Max(c => c.Column.Length);

        Console.Error.WriteLine();
        Console.Error.WriteLine("  keys");
        foreach ((string key, string meaning) in Keys)
        {
            Console.Error.WriteLine($"    {key.PadRight(keyWidth)}  {meaning}");
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("  columns");
        foreach ((string column, string meaning) in Columns)
        {
            Console.Error.WriteLine($"    {column.PadRight(columnWidth)}  {meaning}");
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("    realised and its ratio read \"past oracle\" on the last row of a walk that");
        Console.Error.WriteLine("    outran its oracle, because from there the oracle cannot resolve the error");
        Console.Error.WriteLine();
        Console.Error.WriteLine("    a single us reading is noisy at this scale - read the trend, not the row");
        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  stop rules: {rules.MaxSteps} steps, {rules.MaxSeconds:F0} s computing, " +
            $"{rules.MaxDenominatorBits} denominator bits, or a bound finer than the oracle"));
        Console.Error.WriteLine("  the pause does not count toward the time rule, and h costs no step");
        Console.Error.WriteLine();
    }
}
