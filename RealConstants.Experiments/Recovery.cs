namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Turns a selector that did not resolve into another try, where there is somebody to ask.
/// </summary>
/// <remarks>
/// <para>
/// A bad selector used to end the run. That is the wrong moment to be least helpful: the grammar
/// is exactly what the user has just demonstrated they do not know, and refusing sends them to
/// <c>list</c> to read all of it rather than the part that would fix this line.
/// </para>
/// <para>
/// So the diagnosis is printed and a replacement is asked for, and the replacement goes through
/// <see cref="Selector"/> like everything else - there is no second parser and no shim. A form
/// the grammar retired is named, not accepted; translating it would restore the two grammars the
/// single-grammar change removed.
/// </para>
/// <para>
/// <b>Guarded by <see cref="Console.IsInputRedirected"/>, exactly as the stepper's pause is.</b>
/// Piped or scripted there is nobody to answer, so the diagnosis prints and the run ends
/// non-zero as before - no prompt, and nothing that can hang on a read returning end-of-file.
/// This is the third thing in this project gated on a terminal, and like the other two the
/// prompt itself is not verifiable from a scripted session.
/// </para>
/// </remarks>
internal static class Recovery
{
    /// <summary>What a caller needs to be satisfied with a selection, beyond it parsing.</summary>
    /// <param name="choices">The choices the selectors resolved to.</param>
    /// <returns><see langword="null"/> if the selection is usable, otherwise what is wrong with it.</returns>
    internal delegate string? Check(Choice[] choices);

    /// <summary>
    /// Resolves selectors, offering another try at a terminal until one works or the user stops.
    /// </summary>
    /// <param name="tokens">The selectors as typed on the command line.</param>
    /// <param name="check">
    /// An extra condition the selection must meet - <c>step</c> requires exactly one method, and
    /// requires it to build. Pass <see langword="null"/> where parsing is enough.
    /// </param>
    /// <param name="choices">The resolved selection, when this returns true.</param>
    /// <returns><see langword="true"/> if a selection was resolved; otherwise the caller exits 2.</returns>
    public static bool TryResolve(string[] tokens, Check? check, out Choice[] choices)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        string[] attempt = tokens;

        while (true)
        {
            string? problem = Selector.TryParseAll(attempt, out choices, out string error)
                ? check?.Invoke(choices)
                : error;

            if (problem is null)
            {
                return true;
            }

            Console.Error.WriteLine(problem);

            if (Console.IsInputRedirected)
            {
                choices = [];
                return false;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine($"  {Selector.Shape}");
            Console.Error.WriteLine("  enter a selector, or blank to give up");
            Console.Error.Write("selector> ");

            string? typed = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(typed) ||
                typed.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                choices = [];
                return false;
            }

            // Split on whitespace so a reply can name several selectors, exactly as the command
            // line does. The reply is not treated as a special case anywhere below this point.
            attempt = typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
