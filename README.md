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
