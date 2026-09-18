using System.Reflection;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="Catalogue"/>'s claims about its own rows, and in particular the one duplication the
/// design accepted rather than removed.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue restates each method's identity and cadence as runtime strings, duplicating what
/// the providers' XML documentation says. <see cref="Catalogue"/>'s remarks hold the ruling that
/// accepted that, with its reason and the alternative it rejected; what can be gated is gated
/// here.
/// </para>
/// <para>
/// <b>What is decidable is the domain.</b> Each row states in prose which parameters its provider
/// accepts, and the provider is the authority. A row claiming <c>s = 2, 3 or 4</c> for something
/// that refuses 4, or accepts 5, is a claim the user reads and acts on. That is checked below by
/// asking the provider.
/// </para>
/// <para>
/// <b>The cadence is half decidable, and only that half is checked.</b> "About 0.6 decimal digits
/// per step" is a quantitative claim, and <c>../AGENTS.md</c> § Reliability clause (f) requires
/// one to carry the basis that produced it. Each row therefore names its basis in a field of its
/// own - a step-count test pinning what a target costs - and whether that names a test which
/// exists, in the provider's own test class, is decidable and asserted below. A renamed or
/// deleted test turns it red rather than leaving the row citing nothing.
/// </para>
/// <para>
/// <b>Whether the test bears the figure out is not checked, and cannot be here.</b> Tying the
/// prose figure to the test's assertions would be a text match, which that same clause says
/// never settles a semantic property. That half stays a reader's check: run the named test and
/// read what it pins.
/// </para>
/// </remarks>
public class CatalogueTests
{
    /// <summary>Parameters a domain test will try, spanning every provider's acceptable range.</summary>
    /// <remarks>
    /// Deliberately wider than any provider accepts, and including the values each is known to
    /// refuse: 1 is below every order, 4 is a perfect square, 5 is past the central-binomial
    /// family and is not Borwein's order.
    /// </remarks>
    private static readonly int[] Probes = [0, 1, 2, 3, 4, 5, 6, 7, 9, 12];

    [Fact]
    public void EveryRowsStatedDomainMatchesWhatItsProviderAccepts()
    {
        foreach (Recipe recipe in Catalogue.Recipes)
        {
            ConstantNote note = Catalogue.FindConstant(recipe.Constant)!;

            if (note.Parameter.Length == 0)
            {
                // A constant with no parameter has nothing to disagree about, but its row must
                // still say so rather than describing a range it cannot have.
                Assert.Equal("no parameter", recipe.Domain);
                Assert.NotNull(Catalogue.TryCreate(recipe, 0, out _));
                continue;
            }

            foreach (int probe in Probes)
            {
                bool accepted = Catalogue.TryCreate(recipe, probe, out string refusal) is not null;

                Assert.True(
                    accepted == DomainAdmits(recipe.Domain, probe),
                    $"{recipe.Constant}/{recipe.Method} states \"{recipe.Domain}\" but "
                    + (accepted ? $"accepted {probe}" : $"refused {probe}: {refusal}"));
            }
        }
    }

    [Fact]
    public void ARefusalCarriesTheProvidersOwnWords()
    {
        // The domain prose is for reading; the refusal a user actually sees must come from the
        // guard that owns the rule, so that the reason travels with it.
        Recipe newton = Catalogue.Find("sqrt", "newton")!;

        Assert.Null(Catalogue.TryCreate(newton, 4, out string square));
        Assert.Contains("perfect square", square, StringComparison.Ordinal);

        Assert.Null(Catalogue.TryCreate(newton, 1, out string small));
        Assert.Contains("at least two", small, StringComparison.Ordinal);

        Recipe central = Catalogue.Find("zeta", "central")!;
        Assert.Null(Catalogue.TryCreate(central, 6, out string family));
        Assert.Contains("2.02385", family, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRowsCadenceNamesARunnableTestInItsProvidersTestClass()
    {
        // The decidable half of the cadence's citation: the name resolves, to a method the runner
        // will actually run. Whether that test bears the figure out is the half that is not
        // decidable here - see the remarks above.
        Assembly tests = typeof(CatalogueTests).Assembly;

        foreach (Recipe recipe in Catalogue.Recipes)
        {
            string row = $"{recipe.Constant}/{recipe.Method}";
            string cited = recipe.CadenceTest;
            int dot = cited.LastIndexOf('.');

            Assert.True(dot > 0 && dot < cited.Length - 1, $"{row} cites '{cited}', which is not Class.Method");

            string className = cited[..dot];
            string methodName = cited[(dot + 1)..];

            // In the provider's own test class, because a figure about one provider measured in
            // another's tests is the likeliest way a citation is wrong while still resolving -
            // a name pasted from the neighbouring row.
            Assert.Equal(recipe.Provider + "Tests", className);

            Type? type = tests.GetType($"{typeof(CatalogueTests).Namespace}.{className}");
            Assert.True(type is not null, $"{row} cites class {className}, which the test assembly does not hold");

            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(method is not null, $"{row} cites {cited}, which does not exist");

            // A test, and one that runs: a skipped test is no basis for anything. Theory derives
            // from Fact, so this admits both.
            FactAttribute? fact = method.GetCustomAttribute<FactAttribute>(inherit: true);
            Assert.True(fact is not null, $"{row} cites {cited}, which is not a test");
            Assert.True(fact.Skip is null, $"{row} cites {cited}, which is skipped");
        }
    }

    [Fact]
    public void EveryRowIsReachableAndDistinct()
    {
        Assert.Equal(
            Catalogue.Recipes.Length,
            Catalogue.Recipes.Select(r => $"{r.Constant}/{r.Method}").Distinct(StringComparer.Ordinal).Count());

        foreach (Recipe recipe in Catalogue.Recipes)
        {
            Assert.Same(recipe, Catalogue.Find(recipe.Constant, recipe.Method));
            Assert.Contains(recipe, Catalogue.For(recipe.Constant));
        }
    }

    [Fact]
    public void EveryConstantHasAtLeastOneMethodAndAnOracleAmongThem()
    {
        foreach (ConstantNote note in Catalogue.Constants)
        {
            Recipe[] recipes = Catalogue.For(note.Name);

            Assert.NotEmpty(recipes);
            Assert.NotNull(Catalogue.Find(note.Name, note.OracleMethod));
            Assert.True(
                note.SelfOracleDepth > note.OracleDepth,
                $"{note.Name}'s self-oracle must be deeper than its ordinary one");
        }
    }

    [Fact]
    public void EveryBenchSetEntryNamesAConstantThatAcceptsItsParameter()
    {
        foreach ((string constant, int parameter) in Catalogue.BenchSet)
        {
            Assert.NotNull(Catalogue.FindConstant(constant));

            // At least one method must actually build there, or the bench set contains a row
            // that can only ever print a refusal.
            Assert.Contains(
                Catalogue.For(constant),
                r => Catalogue.TryCreate(r, parameter, out _) is not null);
        }
    }

    /// <summary>Reads a row's stated domain and says whether it admits a parameter.</summary>
    /// <param name="domain">The prose from the catalogue row.</param>
    /// <param name="probe">The parameter to test.</param>
    /// <returns>Whether the prose claims that parameter is accepted.</returns>
    /// <remarks>
    /// The prose is written in a handful of fixed shapes precisely so that this can be decided
    /// rather than guessed at. A row whose domain does not match one of them fails loudly here,
    /// which is the intended outcome: an unparseable domain is one nobody can check.
    /// </remarks>
    private static bool DomainAdmits(string domain, int probe)
    {
        if (domain == "any non-square integer of at least 2")
        {
            int root = (int)Math.Sqrt(probe);
            return probe >= 2 && root * root != probe && (root + 1) * (root + 1) != probe;
        }

        if (domain == "s = 2, 3 or 4")
        {
            return probe is 2 or 3 or 4;
        }

        if (domain == "s = 3 only")
        {
            return probe == 3;
        }

        if (domain == "s >= 2")
        {
            return probe >= 2;
        }

        throw new InvalidOperationException(
            $"The domain \"{domain}\" is not in a shape this test can decide. Either write it in "
            + "one of the shapes above, or this gate stops covering the row.");
    }
}
