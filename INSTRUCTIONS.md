# RealConstants

> Collaboration contract → `../AGENTS.md`.
> Cross-cutting status & dependency graph → `../INSTRUCTIONS.md`.
> Mission, principles & repo conventions → `../VISION.md`.

The deep working reference for this submodule. Ratified design for the
investigation it serves → `../SPEC-rational-ratio.md`.

## Stack

A C# class library and its xUnit test project; language version, target
framework and namespace conventions are umbrella-wide and live in
`../VISION.md` and `Directory.Build.props`.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\Math\RealConstants\RealConstants.slnx`

## Repo

`https://github.com/halheinrich/RealConstants`, branch `main`. Public from its
first commit, which is why every bound here is proven before it is committed
rather than at a later review.

## Depends on

- **RationalApproximation** — `Approximation`, the enclosure every provider
  returns, built through its `Create` factory; and `IRealConstant`, the contract
  every provider implements. Its defaulted `StepFor` and `ApproximateTo` are
  used by the tests through an interface-typed reference.

  By `ProjectReference`, per the umbrella's ruling on intra-umbrella edges, at
  `..\..\RationalApproximation\RationalApproximation\RationalApproximation.csproj`.
  That path **escapes this repository** — see § Pitfalls.

- **BigRationalLibrary** — `HalHeinrich.Numerics.BigRational`, the exact
  rational every value and every bound is computed in. Used for its arithmetic
  and comparison operators, `Abs`, `Zero`, `Pow`, and the
  `(BigInteger, BigInteger)` constructor; and `IntegerMath.Sqrt` with
  `IntegerSqrtRounding`, which the square-root provider needs for its starting
  iterate and for its rational lower bound on the root.

  Reached **transitively**, through the reference above rather than by a second
  direct one — `IntegerMath` included. That was measured rather than inferred
  from the reference graph: a throwaway call to `IntegerMath.Sqrt` was compiled
  here before any second reference was considered, and it resolved. A checkout
  therefore needs all three repositories as siblings even though only one
  reference is written here.

## Layout

- **`RealConstants`** — the library. One public type per constant-and-**method**,
  with the *constant* a parameter where one method covers a family:
  `NewtonSquareRoot` takes its radicand, the three zeta types take their order.
  The method is never a parameter, which is § 4's ruling — see § Why a
  cross-check pair shares no code.
- **`RealConstants.Tests`** — xUnit. Also holds the oracles the providers are
  checked against, which are part of the design rather than scaffolding:
  `PiReference` (an external value, itself checked), `SquareRootReference` (a
  computed one, self-verified), `ZetaReference` (typed digits for four orders,
  checked from two sides), `DisplacedConstant` (the negative control that makes
  a cross-check falsifiable, for any constant), `EulerMaclaurin` (the same
  formula at an explicitly chosen truncation, so a test can enter the region the
  provider refuses to) and `Enclosures` (the predicates on pairs of enclosures
  that `Approximation` does not carry).
- **`RealConstants.Experiments`** — a runnable project, not a test one. It
  prints tables, has no pass or fail, and depends on wall-clock time, all of
  which `../AGENTS.md` § Exactness discipline forbids a test. See
  § The experiments project.

## Architecture

A provider is a value **and a proven bound on that value's distance from the
truth**. The bound is derived analytically, in closed form in the step index,
and stated in the type's XML documentation in a form a reader can check. It is
never measured from a run and never estimated from observed convergence. A
provider that cannot bound its truncation does not ship.

### Every provider, one shape

Each implements `IRealConstant` with two members. `ErrorBoundAt(step)` is pure,
non-increasing, tends to zero, and — the point of the member — is computable
without doing the step's work, so a run can be planned before it is paid for.
`Refinements()` is lazy, endless, strictly improving and incremental.

No type re-declares `StepFor` or `ApproximateTo`. Those are default interface
members, which C# does not surface on an implementing type, so a caller holding
a `MachinPi` cannot see them and must hold the interface. That is a consequence
of the contract's wording, not an omission to be worked around.

All are stateless once constructed, so an instance is shareable and
thread-safe, and each call to `Refinements()` returns an independent sequence.
The pi pair's constructors are the implicit parameterless one; the other four
take the constant they serve — a radicand or an order — and validate it there.

**Incremental is a per-scheme claim, not a blanket one.** Most refinements build
on the last in full: a running partial sum, running powers, a Newton iterate, a
running binomial coefficient. Two cannot, and both say so. `BorweinZetaThree`'s
Chebyshev weights depend on the depth, and `EulerMaclaurinZeta`'s correction
count grows with `N`, so in each the recombination is redone every step and
reaching step *n* is quadratic. That is a property of acceleration rather than
of an implementation, and it is stated at the type rather than left in a
profile.

### The alternating-series bound

Both series alternate with terms that strictly decrease to zero, which is what
licenses the whole bound: the distance from a partial sum to the limit is at
most the first omitted term. The grouping argument for that is written out in
full in `LeibnizPi`'s documentation — the tail read one way is a sum of positive
brackets, read the other way it is the first omitted term less a sum of positive
brackets — and `MachinPi` refers to it rather than restating it.

`LeibnizPi` omits `1/(2n+3)` at step `n`, so pi's error is at most `4/(2n+3)`.

`MachinPi` combines two arctangent series carried to the same depth. With
`m = 2n+3`, each remainder is at most `x^m/m` for its own `x`, and the triangle
inequality over `4*(4*A - B)` gives `16/(5^m * m) + 4/(239^m * m)`. Both series
and both scalings are in there; see § Pitfalls for the two ways that goes wrong.

### The Newton bound

Newton's method supplies no remainder term, so `NewtonSquareRoot` constructs
its bounds rather than quoting one — and constructs two, by different routes.
The argument is written out in the type's XML documentation; what matters here
is its shape and the one substitution it turns on.

From `x₀ = ⌈√c⌉` the iterates stay above the root and strictly descend, because
completing the square gives `x_(n+1) − √c = (x_n − √c)² / (2·x_n)` and a
non-square `c` keeps `x_n² > c` at every step. Both bounds divide by something
no larger than the root, so both need a rational `r` with `0 < r ≤ √c`. It is
`√c` truncated to 32 fractional bits, from `IntegerMath.Sqrt` on a scaled
integer: at or below `√c` because a floor is, and within `2⁻³²` of it because
that floor is above `√c·2³² − 1`. Both directions are load-bearing.

Each refinement then carries the **realised** bound `(x_n² − c)/(x_n + r)`,
read off the iterate it already holds, so it costs no part of the next step.
`ErrorBoundAt` returns the **planned** one, the closed form `2r·W^(2ⁿ)` with
`W = (x₀ − r)/(2r)`, which never looks at an iterate — that is the member's
whole point, and `W < 1` for every radicand the type accepts. The realised
bound never exceeds the planned one, so a step chosen from `ErrorBoundAt`
delivers at least what it promised.

### The two zeta(3) bounds, which are not each other's shape

`CentralBinomialZeta(3)` is alternating, so it reuses the same remainder
estimate the pi pair does — but the estimate needs its hypothesis. From
`C(2k+2,k+1) = C(2k,k)·2(2k+1)/(k+1)`, consecutive terms are in the ratio
`k³/(2(k+1)²(2k+1))`, whose denominator expands to `4k³ + 10k² + 8k + 2`. That
exceeds `4k³` for every `k ≥ 1`, so the ratio is strictly below `1/4` and one
inequality settles both obligations: the terms strictly decrease, and they tend
to zero at least geometrically. Step *n* omits the term at `k = n+2`, and the
`5/2` in front scales the bound as well as the value.

(That inequality is stated for `s = 3` here because it is where the zeta(3)
pair needs it. It holds for the whole central-binomial family and is proved in
§ The three zeta methods once, not twice.)

`BorweinZetaThree` gets no tail estimate at all, and its bound comes from
approximation theory. The full derivation is at the type; the load-bearing step
is that `1/(k+1)³` is a moment of a **non-negative** measure on `[0,1]`, which
is what lets `max|P|` come out of an integral and leaves
`|error| ≤ η(3)·max_[0,1]|P| / |P(−1)|` for any polynomial `P`. Chebyshev is
then the optimal choice rather than a clever one: `T_m(2x−1)` has maximum 1 on
`[0,1]` and `|T_m(−3)| = T_m(3)`, the integers 3, 17, 99, 577, … from
`T_(m+1) = 6·T_m − T_(m−1)`.

**That bound is loose, and the looseness grows.** Pulling `max|P|` out of an
oscillating integral discards the cancellation that is most of why the scheme
converges: measured against an independent value, the realised error is about
`0.70` of the claim at step 0 and about `0.009` of it by step 39. Rounding a
bound up is always permitted, so this is sound — but it decides what a
falsification test can honestly assert, and § The three kinds of test says what
that turns out to be.

### Why a cross-check pair shares no code

`LeibnizPi` is the arctangent series at `x = 1`, so one internal helper could
serve both, and it would look like the right refactor. It is not. The pair
exists to cross-check each other, and a cross-check is worth what the two
implementations' independence is worth: a defect in a shared series engine would
move both values and could cancel in the comparison. This is duplicated *shape*
encoding two independent decisions, which `../AGENTS.md` § Writing code
distinguishes from the same rule encoded twice.

`LeibnizPi` is additionally the control in the sense of `../VISION.md`
§ Guiding principles: its product is trust, so it stays short enough to audit by
reading and is not to be made faster.

The same reasoning governs the zeta(3) pair, and more strongly, because the two
schemes there are not even superficially alike: a binomial-denominator series
against a Chebyshev recombination. There is no shared helper anyone would be
tempted to extract, which is a happy accident rather than the protection — the
protection is the rule.

`NewtonSquareRoot` is the other side of that same rule and looks at first like
a breach of it: one type serves both `√2` and `√3`, differing only in the
radicand. Those are two *constants*, though, not two providers of one constant,
and § 4's ruling forbids only the latter sharing an engine. Splitting the type
in two would encode a single decision twice, which `../AGENTS.md` § Writing
code forbids just as squarely. Neither root has a cross-check partner, so there
is no independence here for a shared engine to compromise.

`DisplacedConstant` is a third case and lands the opposite way. Displacing a
provider's values while leaving its bound alone is a *single* decision serving
both pairs, so one type is right and a copy per constant would be the defect.
The test is why the code would be the same, not whether it looks the same.

### The three zeta methods, and the trap they exist to avoid

**zeta(2n) is a rational multiple of pi^(2n).** So a provider computing zeta(2)
as `π²/6` makes `π²/ζ(2)` exactly 6 by construction, and § 4's positive control
tests arithmetic rather than the bench. **No even-zeta provider here refers to
π, directly or transitively** — that is the constraint the three methods below
were chosen under, not a property they happen to have.

**`CentralBinomialZeta(s)`, s ∈ {2, 3, 4}.** `ζ(s) = c_s·Σ (±)1/(kˢ·C(2k,k))`
with `c_s` of 3, 5/2 and 36/17, the middle one alternating. One inequality
carries the family: consecutive terms are in the ratio
`kˢ/((k+1)^(s−1)·2(2k+1))`, and `(k+1)^(s−1) ≥ k^(s−1)` puts the denominator at
or above `4kˢ + 2k^(s−1)`, so the ratio is below `1/4` for every `k ≥ 1` and
every `s ≥ 2`. Positive, strictly decreasing and geometric, all three from that
one line. The alternating member's bound is the first omitted term; the two
positive members' is that times `4/3`, the geometric tail they dominate. Each
keeps the tighter shape it has earned rather than both being flattened for
symmetry. **The family stops at s = 4** — `ζ(6)` over that sum is `2.02385…`,
no rational coefficient, measured — and the guard carries that fact.

**`EulerMaclaurinZeta(s)`, s ≥ 2.** The only route to `ζ(6)` and the second
route for 2 and 4, which is what makes the even controls genuine pairs. See
§ The Euler-Maclaurin bound.

**`DirectSumZeta(s)`, s ≥ 2.** `Σ 1/kˢ` with the tail bracketed between
integrals of `x^-s`: `(N+1)^(1−s)/(s−1) < tail < N^(1−s)/(s−1)`, two lines from
the monotonicity of `x^-s` and nothing cited. It is the obviously-correct slow
implementation `../AGENTS.md` § Exactness discipline asks to sit beside the
fast ones — **its only product is trust and it is not to be optimised**, which
its own documentation says. Its measured cost is in § Pitfalls.

### The Euler-Maclaurin bound, and what a step had to be

The formula with its integral remainder is cited; the bound on that remainder
is derived here. The remainder integrates the periodic Bernoulli function
against `f^(2M)`; that function is bounded by `|B_2M|`, because replacing every
cosine in its Fourier series by one gives its value at zero; pulling the maximum
out leaves an integral that evaluates exactly, to
`(|B_2M|/(2M)!)·(s)_(2M−1)·N^(1−s−2M)` — **the magnitude of the last term
included**. The bound is a term already computed, and estimates nothing.

**The series in M is asymptotic and turns.** At `N = 10` the realised error
improves to about `1.0e−27` at `M = 30` and worsens to `1.8e−15` by `M = 65`.
So a step may **not** grow M: it would report a shrinking bound over a growing
error, which is the one failure this bench exists to make impossible. A test
pins the turn rather than trusting the constraint, and a second pins the other
half — growing N at fixed M never turns.

**A step grows N, and M is chosen rather than scheduled.** At each `N` the type
takes the `M` minimising the proven bound. That is not a heuristic, and it makes
the contract's monotonicity obligation two lines instead of a measurement:
`E(N+1, M*(N+1)) ≤ E(N+1, M*(N)) < E(N, M*(N))`, the first because `M*(N+1)`
minimises at `N+1`, the second because `E ∝ N^(1−s−2M)` falls in `N` at fixed
`M`. Tending to zero follows from the same second fact.

About **2.75 decimal digits a step**, measured 2.746 at s = 2, 4 and 6 alike,
which is `log₁₀(e^2π)` — the classical accuracy of an optimally truncated
Euler-Maclaurin, and the fastest provider here.

### The three kinds of test, and why none is sufficient alone

- **Cross-check.** The two providers' enclosures must overlap, and where Machin
  is the finer of the two its enclosure must sit inside Leibniz's. This is the
  strongest correctness test available here, and it is the only one that needs
  no external value. It cannot detect the two providers agreeing on something
  that is not pi.
- **Enclosure of an external value.** `PiReference` is the published expansion
  truncated at sixty places, held as the exact interval truncation licenses.
  This grounds the pair on pi. It is only as good as the digits typed, so a deep
  Machin enclosure — proven bound far finer than the last place — is asserted to
  lie inside it, which fails on any mistyped digit.
- **Attempted violation.** A bound is tested by trying to break it. Halving
  either claimed bound is refuted at step 0; quartering Machin's is refuted at
  every step checked. Where a mutation does *not* break, no falsification test
  is claimed — see § Pitfalls.

`DisplacedConstant` is the negative control on the cross-check itself: a
provider's refinements displaced by a stated amount with the bounds left
untouched. It is a knowingly wrong provider and exists so that a passing
cross-check means the predicate could have failed.

**The zeta(3) pair inverts which assertion carries the weight.** Apéry gains
about 0.64 decimal digits a step and Borwein about 0.77, so at equal steps their
bounds stay within a couple of orders of each other. Overlap is therefore a sharp
test at every pairing, and containment only becomes available deep in the grid —
where against pi, Machin buries Leibniz within two steps and containment is the
assertion that matters. A matched pair is worth more: a cross-check can only
refute a disagreement larger than the two bounds together.

The threshold for containment is measured, not derived from which bound is
numerically finer. Borwein's bound undercuts Apéry's from step 23, but
containment against the whole grid holds only from step 29, because containment
also needs the gap between the two *values* to fit inside the difference of the
bounds.

**And its falsifications are not uniform, which the tests state rather than
flatten.** Halving Apéry's bound is refuted at every step checked. Halving
Borwein's is refuted at step 0 and nowhere deeper, because that bound is loose
by more than a factor of two from step 1 on. Dropping Apéry's `5/2` from the
bound fails at once; dropping Borwein's `4/3` from the *value* fails at once;
dropping Borwein's `4/3` from the *bound* does not fail at all, so that test
asserts the factor is present and says why it must be — see § Pitfalls.

**The zeta oracle is checked from two sides because neither alone is enough.**
`ZetaReference` carries the expansion truncated at sixty places for `s` in 2, 3,
4 and 6, checked by two independent deep enclosures the way `PiReference` is
checked by one. Beneath it sits `DirectSumZeta`, which encloses ζ(s) from the
definition with a proven tail bracket and no identity, coefficient or
acceleration anywhere in it. That reaches only nine or ten places, but it shares
nothing with any fast provider, so it fixes the leading digits without the mild
circularity of a string and a provider vouching for each other. It was a test
fixture until this arc; a provider is its right home, and the fixture is gone.

**The even controls are pairs, and ζ(6) is not.** `CentralBinomialZeta` and
`EulerMaclaurinZeta` cross-check at `s = 2` and `s = 4`, which is what makes
§ 4's positive controls genuine pairs rather than lone providers. The
central-binomial family has no `s = 6` member, so what stands behind ζ(6) is
Euler-Maclaurin against direct summation — a third opinion of very different
depth, and a test says so out loud rather than letting the even controls read as
uniformly paired.

**Their tolerances are lopsided by an order or two**, more than the zeta(3)
pair's. The central-binomial series at even `s` is all-positive, so its partial
sum sits below ζ(s) by nearly its whole claimed bound while Euler-Maclaurin sits
close to centred: at the tightest pairing the two thresholds differ by a factor
of 131 at `s = 2` and 58 at `s = 4`. Both are asserted, along with a
displacement caught in one direction only.

**And the controls' own premise is a test here**, not in `Zeta`:
`π²/ζ(2) = 6`, `π⁴/ζ(4) = 90` and `π⁶/ζ(6) = 945`, through `MachinPi`,
`Approximation.Pow` and the propagated division of § 2 — the same path the
pipeline will take. If it failed there instead, that session would audit correct
wiring. The propagated half-width comes out under `1e-55`, so each control also
refutes its neighbours, and a further test swaps the three targets between
orders to show the assertions can fail together.

**`NewtonSquareRoot` has only the last two of the three**, and nothing about it
should be read as a cross-check. There is no second square-root provider and
§ 4 asks for none. `SquareRootReference` is an oracle rather than a partner: it
takes `IntegerMath.Sqrt` on a radicand scaled by `2^2048`, and it is grounded
by asserting that the integer it returns brackets that scaled radicand between
two consecutive squares — two multiplications a reader can check, rather than
trust in `IntegerMath`. The attempted violations are halving the claimed bound,
refuted at every step checked, and building the bound from a value *above* the
root rather than below, refuted at every step by a margin the reference sees.

## Public API

```csharp
namespace HalHeinrich.Numerics;

public sealed class LeibnizPi : IRealConstant
{
    public BigRational ErrorBoundAt(int step);          // 4/(2*step+3)
    public IEnumerable<Approximation> Refinements();
}

public sealed class MachinPi : IRealConstant
{
    public BigRational ErrorBoundAt(int step);          // 16/(5^m*m) + 4/(239^m*m)
    public IEnumerable<Approximation> Refinements();    //   where m = 2*step+3
}

public sealed class NewtonSquareRoot : IRealConstant
{
    public NewtonSquareRoot(BigInteger radicand);       // >= 2, and not a square
    public BigInteger Radicand { get; }
    public BigRational ErrorBoundAt(int step);          // 2r * W^(2^step)
    public IEnumerable<Approximation> Refinements();
}

public sealed class BorweinZetaThree : IRealConstant
{
    public BigRational ErrorBoundAt(int step);          // (4/3)/T_m(3),
    public IEnumerable<Approximation> Refinements();    //   where m = step + 1
}

public sealed class CentralBinomialZeta : IRealConstant
{
    public CentralBinomialZeta(int order);              // s in {2, 3, 4}
    public int Order { get; }
    public BigRational ErrorBoundAt(int step);          // c_s/(k^s*C(2k,k)) at
    public IEnumerable<Approximation> Refinements();    //   k = step+2, times
}                                                       //   4/3 unless alternating

public sealed class EulerMaclaurinZeta : IRealConstant
{
    public EulerMaclaurinZeta(int order);               // s >= 2
    public int Order { get; }
    public BigRational ErrorBoundAt(int step);          // least over M of
    public IEnumerable<Approximation> Refinements();    //   |B_2M/(2M)! *
}                                                       //   (s)_(2M-1) *
                                                        //   N^(1-s-2M)|, N=step+2

public sealed class DirectSumZeta : IRealConstant
{
    public DirectSumZeta(int order);                    // s >= 2
    public int Order { get; }
    public BigRational ErrorBoundAt(int step);          // half the tail bracket
    public IEnumerable<Approximation> Refinements();    //   at N = step + 1
}
```

The pi pair's constructors are the implicit parameterless one. The other four
throw `ArgumentOutOfRangeException` on a constant they cannot serve:
`NewtonSquareRoot` on a radicand below two or a perfect square,
`CentralBinomialZeta` on any order but 2, 3 and 4, and the other two on an order
below two.

Every `ErrorBoundAt` throws `ArgumentOutOfRangeException` on a negative step.
Two add an upper guard and four do not. `MachinPi`'s throws when `2*step+3`
would overflow `int`, which no reachable target error can provoke;
`NewtonSquareRoot`'s throws above step 30, where the closed form's exponent
`2^step` stops fitting an `int`. The four zeta providers need neither, because
the step index enters only as `step + 1` or `step + 2`, carried in a `long` —
which is deliberate, since a guard is a step a bracketing `StepFor` can
overshoot.

`StepFor(BigRational)` and `ApproximateTo(BigRational)` come from
`IRealConstant` as default interface members and are reachable only through an
interface-typed reference.

## Pitfalls

- **This repository does not build standalone**, and the reference chain is two
  hops. `RealConstants` needs `RationalApproximation` beside it, which needs
  `BigRationalLibrary` beside *it*. A checkout of this repo alone cannot
  restore. `.github/workflows/build-and-test.yml` reconstructs all three sibling
  checkouts and verifies both hops before restoring, because the failure this
  gate exists to prevent is a green run that compiled nothing.

- **Machin's bound has two available mistakes and neither announces itself.**
  Applying the coefficient `4` while forgetting that the identity yields `pi/4`
  understates the bound fourfold — caught immediately by the tests. Bounding only
  the `arctan(1/5)` series and neglecting the other **does not fail
  numerically**, because the neglected term is around seven orders of magnitude
  smaller. That is precisely why it must not be done: a bound that holds because
  an unbounded quantity happened to be small is a measurement wearing a proof's
  clothes. The test asserts the second series is *present* in the bound, and
  deliberately does not assert that removing it would be caught, because that
  would be a false claim.

- **The cross-check's tolerance is not symmetric.** It is the sum of the two
  bounds minus the distance between the values in one direction, and plus it in
  the other. A Leibniz step of odd index sits below pi, so a displacement can be
  detected upward and missed downward at the same magnitude. Any new
  cross-check assertion needs both thresholds computed, not one averaged.

- **A cross-check between a deep provider and a shallow one proves very little**
  — it can only refute a disagreement larger than both bounds together. Pair
  comparable depths, or state what the coarser one costs.

- **`ErrorBoundAt` must not compute `2*step+3` in `int`.** It wraps at
  `int.MaxValue` to a bound larger than step 0's, which breaks the
  non-increasing obligation exactly where a bracketing `StepFor` looks. `Leibniz`
  computes it in `long`; `MachinPi` guards and throws, because its exponent has
  to reach `BigInteger.Pow`.

- **No floating point anywhere in a computational path**, including in a test
  expectation. Every literal here is an exact rational. A `double` would be
  banned for being untrackably inaccurate even where it would be accurate
  enough.

- **A square-root bound's rational stand-in for `√c` must come from below.**
  Both of `NewtonSquareRoot`'s bounds divide by it, so a value *above* the root
  shrinks the quotient and the bound stops holding — at step 0, by about
  `2e-12` for `√2`. `IntegerMath.Sqrt`'s `Floor` mode is the correct one and
  `Ceiling` is a silent defect: it builds a plausible-looking bound that fails
  only against a reference finer than `2⁻³²`. A test constructs the wrong-side
  bound explicitly and asserts it fails.

- **`NewtonSquareRoot.ErrorBoundAt` is cheap only in relative terms.** Its
  closed form squares a rational `step` times, so it is a *factor* cheaper than
  the step — which does that and a division and an addition at every one — not
  an order cheaper. Nothing could do better: a bound of magnitude `10^(-k·2ⁿ)`
  needs about `2ⁿ` bits however it is reached. The guard at step 30 is where the
  exponent stops fitting an `int`; memory runs out well before it. And because
  the defaulted `StepFor` brackets by doubling, a target needing step `n`
  evaluates the bound at a step below `2n`, so it can hit the guard while the
  answer sits well inside it. Targets to about `1e-44000` are unaffected.

- **A loose bound is sound but it limits what a test may claim.**
  `BorweinZetaThree`'s bound discards the cancellation in an oscillating
  integral, so it overstates the realised error by a factor that grows with the
  degree. Halving it is refuted at step 0 and nowhere deeper, and dropping the
  `4/3` from it is never refuted, because the slack already exceeds the factor.
  Both are asserted the way they actually behave. Writing "halving the bound
  fails" as a blanket claim, or asserting a falsification that does not happen,
  would each be worse than asserting nothing.

- **`StepFor`'s bracket assumes a bound *shape*, and the assumption is about
  bit-length rather than magnitude.** It doubles the step to bracket, so
  answering "step *n*" evaluates the bound at a step below `2n`. That is a
  bounded overhead exactly when the bound's bit-length grows at most linearly in
  the step — measured here at 1.24–1.63× the answer's bits for both zeta(3)
  providers, across targets from `1e-30` to `1e-2000`. It is *not* bounded for
  `NewtonSquareRoot`, whose bound needs about `2^step` bits, so the probe costs
  the square. That is halheinrich/Math#53, and the zeta(3) pair is evidence the
  default is right for ordinary shapes rather than evidence it needs changing.

- **No even-zeta provider may reach ζ(s) through π.** `ζ(2n)` is a rational
  multiple of `π^(2n)`, so a provider defined that way makes `π^(2n)/ζ(2n)`
  exact by construction and turns § 4's positive controls into tautologies —
  they would pass on any value of π whatever. This is the single constraint the
  three zeta methods were chosen under. It is easy to violate in good faith,
  because `π²/6` is the first thing anyone writes for ζ(2).

- **A step may not grow Euler-Maclaurin's correction count.** That series is
  asymptotic: at `N = 10` the error improves to about `1e−27` by `M = 30` and
  worsens to `1.8e−15` by `M = 65`. A step growing `M` would report a shrinking
  bound over a growing error. The step grows `N`, `M` is chosen as the bound's
  minimiser, and a test pins the turn — the constraint is mechanical rather than
  remembered.

- **Direct summation's ceiling is the denominator, not the term count.** In
  exact rationals the partial sum carries `lcm(1..N)`, which grows exponentially
  in `N`. Measured: ζ(2) reaches about `1e−8` in 12951 steps and 37 kilobits
  before ten seconds are up, and never gets near `1e−10`. Steps and seconds are
  proxies for that; bits is the thing itself, which is why the experiments
  project has a bit-length stop rule.

- **What a falsification test may claim differs by provider, and the tests say
  which.** The central-binomial bound is tight enough that a *tenth* of it is
  refuted at every step; Euler-Maclaurin's is tight to a factor of two, so
  halving it is refuted at some steps and not others and the tests use two
  fifths; direct summation's half-width overstates its realised error
  increasingly, so halving is refuted only in the first few steps of the larger
  orders. Writing "halving the bound fails" as a blanket claim would be false
  three times over.

- **`Refinements()` is endless.** Every consumer and every test takes a finite
  prefix. A `foreach` without a `Take` does not terminate.

- **Leibniz's denominators grow fast.** The running partial sum's denominator is
  the least common multiple of the odd numbers used, so a few hundred steps is
  cheap and a few hundred thousand is not. `StepFor` is the way to ask how deep
  a target would be without paying for it.

## The experiments project

`RealConstants.Experiments` is runnable and has no pass or fail. It follows
`Collatz.Experiments`: `OutputType` `Exe`, `IsPackable` false, named experiments
with `list` and `--help`, exit 2 on an unknown name.

- **`compare`** walks all three zeta methods to a set of error targets, at every
  order each reaches, and records steps, seconds, the bound attained, the
  denominator bit-length and which stop rule fired.
- **`step <method> <s>`** is the interactive walk — one step at a time, so the
  algorithms can be felt rather than summarised. `Enter` takes one step, a
  number takes that many, `r` runs to a stop rule, `q` quits.

**Four stop rules, and a run reports which fired**: steps, wall-clock,
denominator bit-length, and the user quitting. All are configurable at the top
of `Runner.cs`. A run that stops is recorded as *what was observed* — "did not
finish within 10 s" — never as a verdict about the method. Direct summation
stops in most cells; that is the expected result and the reason it is in the
table.

**Nothing here may reach the library.** Every figure the stepper prints is
derivable from `ErrorBoundAt`, `Refinements()` and an oracle, so there is no
`IRealConstant` change and no diagnostic hook on any provider. Putting an
experiment's convenience into a ratified contract is the edit to refuse.
Everything downstream holds the interface and never a concrete type, which is
what keeps a comparison a comparison.

**Decimals live in `Presentation.cs` and nowhere else.** § Exactness discipline
permits formatting at presentation and bans floating point upstream of it; the
conversion is exact truncation of an exact rational in integers, and the one
`double` in the repository estimates a magnitude for a column heading.

**`Console.IsInputRedirected` guards the prompt**, so a piped or scripted run
falls through to the stop rules and prints a line saying the pause was skipped.
Both branches were exercised: under a pipe the skip path fires and runs to the
time rule; under a real pty the interactive path fires, emits one row and blocks
on its prompt. What is **not** exercised is the interpretation of what is typed
— no route was found to deliver keystrokes into a pty from a scripted session.

## Subproject-internal next steps

- **Every provider `../SPEC-rational-ratio.md` § 4 names now exists**, the even
  positive controls included. Nothing here waits on another provider; what comes
  next consumes these rather than adding to them, and lives in `Zeta`.

- **The `Experiments` project is for cost, not for answers.** The earlier note
  here said this repository would have none, on the grounds that runs with no
  known answer belong to `Zeta`. That reasoning stands and this project does not
  contradict it: nothing here searches for an unknown answer. What it measures
  is what a method *costs* to reach a known one, which is equally not a test —
  it has no pass or fail and it depends on wall-clock time. The ζ(5) and ζ(7)
  runs remain `Zeta`'s.

- **A provider whose bound is only conditional** would have to say so in its XML
  documentation, at the member. None here is conditional; the first one that is
  sets the precedent.
