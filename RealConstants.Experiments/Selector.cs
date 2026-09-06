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
            error = $"'{token}' has more than one '/' - a selector is <constant>[:<param>][/<method>]";
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

        foreach (string token in tokens)
        {
            if (!TryParse(token, out Choice[] some, out error))
            {
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
            string known = string.Join(", ", Catalogue.Constants.Select(c => c.Name));
            error = $"no constant named '{parts[0]}' - known: {known}";
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
