using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>One constant, one method, and the parameter that fixes which constant.</summary>
/// <param name="Constant">The constant's catalogue name.</param>
/// <param name="Parameter">The parameter, or zero where the constant takes none.</param>
/// <param name="Recipe">The method chosen for it.</param>
internal sealed record Choice(string Constant, int Parameter, Recipe Recipe);

/// <summary>
/// The command line's one grammar: <c>&lt;constant&gt;[:&lt;param&gt;][/&lt;method&gt;]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>One grammar, owned by one type.</b> <c>step</c> and <c>compare</c> previously parsed their
/// arguments differently while meaning the same things, so "which method" had two spellings and
/// neither command could express a subset. Everything either command knows about argument shape
/// now comes from here.
/// </para>
/// <para>
/// <b>This type holds no list of constants or methods.</b> It splits a token and asks
/// <see cref="Catalogue"/> — <c>FindConstant</c>, <c>Find</c>, <c>For</c>. Adding a provider is a
/// row in the catalogue and the command line gains it with no edit here. A <c>switch</c> over
/// method names in this file would be the design failing.
/// </para>
/// <para>
/// <b>It does not validate domains either.</b> <c>sqrt:4/newton</c> parses, because <c>sqrt</c>
/// is a constant and <c>newton</c> is a method for it; that 4 is a perfect square is a fact
/// <see cref="NewtonSquareRoot"/> owns, and it is discovered by asking that type to build one.
/// The user then sees the provider's own words rather than a guess made here. Teaching this
/// parser which radicands are acceptable would put that rule in two places, and the copy here
/// would be the one that goes stale.
/// </para>
/// </remarks>
internal static class Selector
{
    /// <summary>Parses one selector into the choices it names.</summary>
    /// <param name="token">The selector as typed.</param>
    /// <param name="choices">The choices it resolves to, in catalogue order.</param>
    /// <param name="error">What was wrong, when it does not parse.</param>
    /// <returns><see langword="true"/> if the token names a constant, and a method if it gave one.</returns>
    public static bool TryParse(string token, out Choice[] choices, out string error)
    {
        choices = [];
        error = string.Empty;

        string[] halves = (token ?? string.Empty).Split('/');
        if (halves.Length > 2)
        {
            error = $"'{token}' has more than one '/' - a selector names at most one method";
            return false;
        }

        if (!TryParseConstant(halves[0], out string constant, out int parameter, out error))
        {
            return false;
        }

        Recipe[] recipes;

        if (halves.Length == 2)
        {
            Recipe? one = Catalogue.Find(constant, halves[1]);
            if (one is null)
            {
                string available = string.Join(", ", Catalogue.For(constant).Select(r => r.Method));
                error = $"no method '{halves[1]}' for {constant} - available: {available}";
                return false;
            }

            recipes = [one];
        }
        else
        {
            recipes = Catalogue.For(constant);
        }

        choices = [.. recipes.Select(r => new Choice(constant, parameter, r))];
        return true;
    }

    /// <summary>Parses every selector on a command line, in the order given.</summary>
    /// <param name="tokens">The selectors as typed.</param>
    /// <param name="choices">Everything they resolve to, concatenated.</param>
    /// <param name="error">What was wrong, when one of them does not parse.</param>
    /// <returns><see langword="true"/> if every token parsed.</returns>
    /// <remarks>
    /// The first bad token stops the parse. A command line is short enough that reporting one
    /// problem and stopping reads better than a list, and the second complaint is often a
    /// consequence of the first.
    /// </remarks>
    public static bool TryParseAll(IEnumerable<string> tokens, out Choice[] choices, out string error)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        List<Choice> all = [];
        error = string.Empty;

        string[] asArray = [.. tokens];

        foreach (string token in asArray)
        {
            if (!TryParse(token, out Choice[] some, out error))
            {
                // A whole-line explanation beats a per-token one where the line has a shape we
                // recognise: "central is a method" is true but unhelpful when the real problem is
                // that the user wrote the argument order this grammar replaced.
                error = DiagnoseRetiredForm(asArray) ?? error;
                choices = [];
                return false;
            }

            all.AddRange(some);
        }

        choices = [.. all];
        return true;
    }

    /// <summary>How a constant is written on the command line.</summary>
    /// <param name="constant">The constant's catalogue name.</param>
    /// <param name="parameter">The parameter, or zero where it takes none.</param>
    /// <returns><c>pi</c>, or <c>zeta:3</c>.</returns>
    public static string Spell(string constant, int parameter)
    {
        ConstantNote? note = Catalogue.FindConstant(constant);
        return note is null || note.Parameter.Length == 0
            ? constant
            : string.Create(CultureInfo.InvariantCulture, $"{constant}:{parameter}");
    }

    /// <summary>How a whole choice is written on the command line.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns><c>zeta:3/central</c>.</returns>
    public static string Spell(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return $"{Spell(choice.Constant, choice.Parameter)}/{choice.Recipe.Method}";
    }

    /// <summary>The shape line: the one spelling of the grammar anything is allowed to print.</summary>
    /// <remarks>
    /// <para>
    /// Read by the help screen and by the recovery prompt, and by nothing else. It was written out
    /// by hand in three places before - here, in the "more than one '/'" error, and in
    /// <c>Program</c>'s usage, where the copy had already drifted to <c>&lt;parameter&gt;</c> with a
    /// double space. A rule stated in three places and corrected in one is the defect this project
    /// keeps meeting; there is now nothing to correct twice.
    /// </para>
    /// <para>
    /// <b>A refusal does not print it.</b> Every diagnosis below ends with something concrete - a
    /// selector that would have worked, or the names available at the position that was wrong - and
    /// the grammar then arrives once, at the prompt, which is what the user is about to answer.
    /// Printing it at the end of the diagnosis as well put it on screen twice, four lines apart.
    /// </para>
    /// </remarks>
    public const string Shape = "a selector is <constant>[:<param>][/<method>]";

    /// <summary>
    /// Explains a token that names no constant, saying what it <i>is</i> where the catalogue
    /// knows.
    /// </summary>
    /// <param name="token">The token as typed.</param>
    /// <returns>The diagnosis, over one or more lines.</returns>
    /// <remarks>
    /// A method name reaching this branch was the original complaint: <c>central</c> is a token
    /// the catalogue knows perfectly well, and answering "no constant named 'central'" treats it
    /// as an unrecognised noun and lists the wrong set. Every branch here resolves through
    /// <see cref="Catalogue"/>; none carries a list of its own, and the round-trip test in
    /// <c>SelectorTests</c> is what keeps that true.
    /// </remarks>
    private static string DiagnoseUnknownConstant(string token)
    {
        Recipe[] asMethod = Array.FindAll(
            Catalogue.Recipes,
            r => string.Equals(r.Method, token, StringComparison.OrdinalIgnoreCase));

        if (asMethod.Length == 0)
        {
            string known = string.Join(", ", Catalogue.Constants.Select(c => c.Name));
            return $"no constant named '{token}' - known: {known}";
        }

        string[] families = [.. asMethod.Select(r => r.Constant).Distinct(StringComparer.OrdinalIgnoreCase)];

        // One family is the common case and lets the suggestion be concrete; several would make
        // any single example arbitrary, so the constants are named instead.
        string suggestion = families.Length == 1
            ? $"try  {Example(families[0], asMethod[0].Method)}"
            : $"it belongs to: {string.Join(", ", families)}";

        return $"'{token}' is a method, not a constant. Methods attach with '/'."
             + Environment.NewLine + $"  {suggestion}";
    }

    /// <summary>Builds a concrete selector naming a constant and one of its methods.</summary>
    /// <param name="constant">The constant's catalogue name.</param>
    /// <param name="method">The method's catalogue name.</param>
    /// <returns>A selector such as <c>zeta:2/central</c>.</returns>
    /// <remarks>
    /// The parameter is taken from the bench set rather than invented, so the example is a
    /// pairing this bench actually runs and the catalogue still owns every value in it.
    /// </remarks>
    public static string Example(string constant, string method)
    {
        (string Constant, int Parameter) entry = Array.Find(
            Catalogue.BenchSet,
            b => string.Equals(b.Constant, constant, StringComparison.OrdinalIgnoreCase));

        return $"{Spell(constant, entry.Parameter)}/{method}";
    }

    /// <summary>
    /// Recognises the retired two-token form and says what it would have meant.
    /// </summary>
    /// <param name="tokens">The arguments as typed.</param>
    /// <returns>The diagnosis, or <see langword="null"/> if this is not that shape.</returns>
    /// <remarks>
    /// <c>step central 2</c> and <c>step zeta:3 central</c> both named a method and a constant in
    /// two tokens, which is the grammar removed when the two commands were given one. Naming the
    /// selector that means it is help; accepting it would restore the second grammar, so it is
    /// diagnosed and refused rather than translated.
    /// </remarks>
    public static string? DiagnoseRetiredForm(string[] tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        if (tokens.Length != 2)
        {
            return null;
        }

        string asTyped = string.Join(" ", tokens);

        // step <method> <param>
        Recipe? leading = Array.Find(
            Catalogue.Recipes,
            r => string.Equals(r.Method, tokens[0], StringComparison.OrdinalIgnoreCase));

        if (leading is not null &&
            int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parameter))
        {
            return Retired(asTyped, $"{Spell(leading.Constant, parameter)}/{leading.Method}");
        }

        // step <constant>[:<param>] <method>
        if (TryParseConstant(tokens[0], out string constant, out _, out _) &&
            Catalogue.Find(constant, tokens[1]) is not null)
        {
            return Retired(asTyped, $"{tokens[0]}/{tokens[1]}");
        }

        return null;
    }

    /// <summary>Wraps a suggested selector in the retired-form explanation.</summary>
    private static string Retired(string asTyped, string selector) =>
        $"'{asTyped}' is the retired two-token form, where a method followed its constant."
        + Environment.NewLine + $"  the selector that means it is  {selector}";

    /// <summary>Parses the constant half of a selector, with its parameter.</summary>
    private static bool TryParseConstant(
        string text, out string constant, out int parameter, out string error)
    {
        constant = string.Empty;
        parameter = 0;
        error = string.Empty;

        string[] parts = text.Split(':');
        if (parts.Length > 2)
        {
            error = $"'{text}' has more than one ':' - a constant is <name> or <name>:<param>";
            return false;
        }

        ConstantNote? note = Catalogue.FindConstant(parts[0]);
        if (note is null)
        {
            error = DiagnoseUnknownConstant(parts[0]);
            return false;
        }

        constant = note.Name;

        if (note.Parameter.Length == 0)
        {
            if (parts.Length == 2)
            {
                error = $"{note.Name} takes no parameter";
                return false;
            }

            return true;
        }

        if (parts.Length != 2)
        {
            error = $"{note.Name} needs a parameter - {note.Parameter}, as in {note.Name}:2";
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out parameter))
        {
            error = $"'{parts[1]}' is not an integer";
            return false;
        }

        return true;
    }
}
