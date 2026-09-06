# RealConstants

A .NET 10 class library of **real-constant providers that prove their own
truncation bound**. Each provider hands back a value together with an upper
bound on that value's distance from the truth, and the bound is derived
analytically — never measured from observed convergence, never estimated from
how fast the digits appear to settle.

Everything is exact rational arithmetic (`BigRational`). There is no `double`
anywhere in a computational path; floating point is excluded for being
*untrackably* inaccurate, not merely inaccurate.

## What lives here, and what does not

This layer holds the constants. The general machinery it plugs into — the
`Approximation` enclosure, the `IRealConstant` contract these types implement,
and the rational-approximation search — lives one layer down in
`RationalApproximation`, which knows nothing about which reals are interesting.
The investigation that consumes these providers lives one layer up, in `Zeta`.

Design: `SPEC-rational-ratio.md` in the
[umbrella repository](https://github.com/halheinrich/Math).

### π, from two independent series

Two providers, not one, and that is the design rather than an accident of
ordering. Two independent computations of the same constant must agree inside
their own bounds, which is a correctness test no single provider can give
itself:

- **`LeibnizPi`** — `π/4 = 1 − 1/3 + 1/5 − 1/7 + …`. It converges appallingly
  slowly, and that is fine: it is the control, kept trivially auditable rather
  than made fast.
- **`MachinPi`** — `π/4 = 4·arctan(1/5) − arctan(1/239)`. The workhorse. Its
  error bound has to account for both arctan series and for the scaling in
  front of each; bounding one series and forgetting the factor is a wrong
  bound, not a loose one.

Both are alternating series with strictly decreasing terms, so the remainder is
bounded in absolute value by the first omitted term. That is the whole proof,
and each provider's XML documentation states it in a form a reader can check.

### √2 and √3, by Newton's method

One provider, **`NewtonSquareRoot`**, taking the radicand — not a type per
root. Two types differing only in a constant would be one rule written twice,
and the independence ruling that keeps `LeibnizPi` and `MachinPi` apart governs
two providers of the *same* constant, which these are not. The iteration is
`x ↦ (x + c/x)/2` from `x₀ = ⌈√c⌉`, in exact rationals; the iterates stay above
the root and descend to it, roughly squaring the accuracy at every step.

Newton hands you no remainder term the way an alternating series does, so the
bound is built rather than quoted — and two are, by different routes. Each
refinement carries `(xₙ² − c)/(xₙ + r)`, read off the iterate it already holds.
`ErrorBoundAt(n)` instead returns the closed form `2r·W^(2ⁿ)`, where
`W = (x₀ − r)/(2r)`, and never looks at an iterate at all; that is what lets a
run be planned before it is paid for. Both need a rational **lower** bound
`r ≤ √c`, because both divide by something no larger than the root, and `r` is
`√c` truncated to 32 fractional bits by `IntegerMath.Sqrt` on a scaled integer.
Take `r` from the wrong side and the bound stops holding at step 0, which is a
test here rather than a remark.

There is **no cross-check partner** for this provider, and none is implied. The
design names cross-check pairs for π and for ζ(3) and names none for square
roots. The tests ground it on an oracle instead — `IntegerMath.Sqrt` at about
three hundred decimal places, self-verified by two integer multiplications —
which is weaker evidence than a second provider would give, and is described
that way rather than dressed up.

### ζ(3), from two unrelated schemes

The target, and the second cross-check pair. Neither bound is the π pair's
alternating-series remainder, and the two are not the same shape as each other:

- **`AperyZetaThree`** — `ζ(3) = (5/2)·Σ (−1)^(k−1)/(k³·C(2k,k))`, published by
  Hjortnaes in 1953 and the series Apéry used in 1978. This one *is*
  alternating, so the remainder estimate is available — but only once the terms
  are shown positive and strictly decreasing, which is the part that gets done
  rather than assumed. About 0.64 decimal digits per step.
- **`BorweinZetaThree`** — not a truncated series at all. A weighted
  recombination of `1/1³ … 1/m³` whose weights come from the Chebyshev
  polynomial `T_m(2x−1)`, with a bound from approximation theory rather than
  from a tail. About 0.77 digits per step.

Borwein's derivation is elementary end to end and is written out at the type,
because a bound of an unfamiliar shape that is merely cited is not one a reader
can check. In outline: `1/(k+1)³` is a moment of the **non-negative** measure
`(ln(1/x))²/2 dx` on `[0,1]`, so `η(3)` is one integral; any polynomial `P` with
`P(−1) ≠ 0` splits that integral into a finite combination of moments plus an
error carrying `P`; non-negativity lets `max|P|` come out of the error integral,
leaving `|error| ≤ η(3)·max_[0,1]|P| / |P(−1)|`. That asks for a polynomial small
on `[0,1]` and large at `−1`, which is the extremal problem Chebyshev solves —
`max|T_m(2x−1)| = 1` there, while `|T_m(−3)| = T_m(3)` is the integer sequence
3, 17, 99, 577, … growing like `(3+√8)^m`. With `η(3) < 1` and
`ζ(3) = (4/3)·η(3)`, the bound is `(4/3)/T_m(3)`.

The two rates are close on purpose. A cross-check between a deep provider and a
shallow one refutes very little, so a **matched** pair is worth more than a fast
one paired with a slow one — the opposite balance from the π pair, where Machin
buries Leibniz within two steps.

Borwein's bound is proven but **not tight**, and the repository says so: pulling
`max|P|` out of an oscillating integral discards the cancellation that is most of
why the scheme works, so the realised error falls from about 0.70 of the claim at
step 0 to about 0.009 by step 39. Loose is permitted and short is not — but it
limits what a falsification test may claim, and the tests assert the limit rather
than papering over it.

## Projects

- `RealConstants` — main library
- `RealConstants.Tests` — xUnit tests

## Building

**This repository does not build standalone.** It references
`RationalApproximation` by `ProjectReference`, that reference escapes the repo,
and the referenced project in turn reaches out to `BigRationalLibrary`:

```
..\..\RationalApproximation\RationalApproximation\RationalApproximation.csproj
..\..\BigRationalLibrary\BigRationalLibrary\BigRationalLibrary.csproj
```

Those resolve only when all three checkouts sit as siblings, as they do inside
the umbrella:

```
Math/
  BigRationalLibrary/
  RationalApproximation/
  RealConstants/             <- here
```

A clone of this repository alone cannot restore. This is the accepted price of
the umbrella's `ProjectReference` ruling, not an oversight; the build-and-test
workflow reconstructs that layout rather than pretending otherwise.

```powershell
dotnet build
```

## Test

```powershell
dotnet test
```

## What a result from this bench means

Numerics refute and bound; they do not establish. A provider here says "the
truth is within this distance of this rational", and nothing stronger. Where
these providers feed a search for a rational relation, the output is a
conjecture with a stated bound attached, not a proof.
