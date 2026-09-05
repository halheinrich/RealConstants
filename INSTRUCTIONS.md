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
  and comparison operators, `Abs`, `Zero`, and the `(BigInteger, BigInteger)`
  constructor.

  Reached **transitively**, through the reference above rather than by a second
  direct one. A checkout therefore needs all three repositories as siblings even
  though only one reference is written here.

## Layout

- **`RealConstants`** — the library. One public type per constant-and-method
  pair; no shared series machinery between them, deliberately (see
  § Architecture).
- **`RealConstants.Tests`** — xUnit. Also holds the oracles the providers are
  checked against, which are part of the design rather than scaffolding:
  `PiReference` (an external value, itself checked), `SkewedPi` (the negative
  control that makes the cross-check falsifiable) and `Enclosures` (the
  predicates on pairs of enclosures that `Approximation` does not carry).

## Architecture

A provider is a value **and a proven bound on that value's distance from the
truth**. The bound is derived analytically, in closed form in the step index,
and stated in the type's XML documentation in a form a reader can check. It is
never measured from a run and never estimated from observed convergence. A
provider that cannot bound its truncation does not ship.

### Both providers, one shape

Each implements `IRealConstant` with two members. `ErrorBoundAt(step)` is pure,
non-increasing, tends to zero, and — the point of the member — is computable
without doing the step's work, so a run can be planned before it is paid for.
`Refinements()` is lazy, endless, strictly improving and incremental.

Neither type re-declares `StepFor` or `ApproximateTo`. Those are default
interface members, which C# does not surface on an implementing type, so a
caller holding a `MachinPi` cannot see them and must hold the interface. That is
a consequence of the contract's wording, not an omission to be worked around.

Both are stateless, so an instance is shareable and thread-safe, and each call
to `Refinements()` returns an independent sequence.

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

### Why the two providers share no code

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

`SkewedPi` is the negative control on the cross-check itself: Machin's
refinements displaced by a stated amount with the bounds left untouched. It is a
knowingly wrong provider and exists so that a passing cross-check means the
predicate could have failed.

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
```

Both constructors are the implicit parameterless one. Both `ErrorBoundAt`
overloads throw `ArgumentOutOfRangeException` on a negative step;
`MachinPi.ErrorBoundAt` additionally throws when `2*step+3` would overflow
`int`, which no reachable target error can provoke.

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

- **`Refinements()` is endless.** Every consumer and every test takes a finite
  prefix. A `foreach` without a `Take` does not terminate.

- **Leibniz's denominators grow fast.** The running partial sum's denominator is
  the least common multiple of the odd numbers used, so a few hundred steps is
  cheap and a few hundred thousand is not. `StepFor` is the way to ask how deep
  a target would be without paying for it.

## Subproject-internal next steps

- **The remaining providers.** Newton for the square roots, on
  `IntegerMath.Sqrt`; Apéry and Borwein for the target. Each is a cross-check
  pair or a negative control in `../SPEC-rational-ratio.md` § 4, and each
  follows the shape set here.

- **No `Experiments` project, deliberately.** Runs with no known answer are not
  tests and do not belong in this repository at all; § 4 places them in `Zeta`.

- **A provider whose bound is only conditional** would have to say so in its XML
  documentation, at the member. None here is conditional; the first one that is
  sets the precedent.
