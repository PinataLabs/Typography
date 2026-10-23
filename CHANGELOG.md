# Changelog

All notable changes to PinataLabs.Typography are recorded in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

PinataLabs.Typography is a fork of an Apache-2.0 project, taken at commit `32bef429`.
See `NOTICE` for the original project and its copyright.
The entries below describe the changes from that commit.

## Public API changes

The public API is recorded in `src/PinataLabs.Typography/PublicAPI.Shipped.txt`.
`Microsoft.CodeAnalysis.PublicApiAnalyzers` checks it, and the build fails when the
public surface does not match the PublicAPI files.

An additive or breaking public API change needs all of these:

1. A proposal that the maintainer approves.
2. A commit of its own.
3. A matching change to `src/PinataLabs.Typography/PublicAPI.Unshipped.txt`.
4. An entry in this file.

## [Unreleased]

### Changed

- **Rename.** The package and assembly are now `PinataLabs.Typography`.
  Every namespace from the original project is mapped 1:1 to `PinataLabs.Typography[.X]`.
  The type and member names do not change.
- **Retargeting.** The library targets `net8.0`, `net10.0` and `net11.0`.
  The old targets `netcoreapp3.1`, `netstandard2.1` and `netstandard2.0` are removed.
  The package no longer depends on `System.Memory`, `System.Buffers`,
  `System.Numerics.Vectors` or `System.Runtime.CompilerServices.Unsafe`.
- **AppContext switch rename.** The switch that disables native system font enumeration
  is now `Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration`.
  The original project's switch name has no effect.
- **No strong naming.** The assembly is not strong-named. Its public key token is null,
  so code that references it by the old strong name must be rebuilt.

### Removed

These public members existed only for the removed targets. They were type forwarders
that the shared-infrastructure polyfills added to the assembly. They forwarded the
`System.*` types to the runtime, which still supplies them on every new target.
Your code can use them from the runtime as before; only the forwarders in this
assembly are gone. The names use the PublicAPI file format.

`System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute`
(forwarded on `netcoreapp3.1`, `netstandard2.1` and `netstandard2.0`):

```text
System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute
System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute.ExcludeFromCodeCoverageAttribute() -> void
```

`System.HashCode` (forwarded on `netcoreapp3.1` and `netstandard2.1`):

```text
System.HashCode
System.HashCode.Add<T>(T value) -> void
System.HashCode.Add<T>(T value, System.Collections.Generic.IEqualityComparer<T>? comparer) -> void
System.HashCode.HashCode() -> void
System.HashCode.ToHashCode() -> int
override System.HashCode.Equals(object? obj) -> bool
override System.HashCode.GetHashCode() -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8) -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7) -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6) -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5) -> int
static System.HashCode.Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4) -> int
static System.HashCode.Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3) -> int
static System.HashCode.Combine<T1, T2>(T1 value1, T2 value2) -> int
static System.HashCode.Combine<T1>(T1 value1) -> int
```

`System.MathF` (forwarded on `netcoreapp3.1` and `netstandard2.1`; `BitDecrement`, `BitIncrement`,
`CopySign`, `FusedMultiplyAdd`, `ILogB`, `Log2`, `MaxMagnitude`, `MinMagnitude` and `ScaleB`
on `netcoreapp3.1` only):

```text
System.MathF
const System.MathF.E = 2.7182817 -> float
const System.MathF.PI = 3.1415927 -> float
static System.MathF.Abs(float x) -> float
static System.MathF.Acos(float x) -> float
static System.MathF.Acosh(float x) -> float
static System.MathF.Asin(float x) -> float
static System.MathF.Asinh(float x) -> float
static System.MathF.Atan(float x) -> float
static System.MathF.Atan2(float y, float x) -> float
static System.MathF.Atanh(float x) -> float
static System.MathF.BitDecrement(float x) -> float
static System.MathF.BitIncrement(float x) -> float
static System.MathF.Cbrt(float x) -> float
static System.MathF.Ceiling(float x) -> float
static System.MathF.CopySign(float x, float y) -> float
static System.MathF.Cos(float x) -> float
static System.MathF.Cosh(float x) -> float
static System.MathF.Exp(float x) -> float
static System.MathF.Floor(float x) -> float
static System.MathF.FusedMultiplyAdd(float x, float y, float z) -> float
static System.MathF.IEEERemainder(float x, float y) -> float
static System.MathF.ILogB(float x) -> int
static System.MathF.Log(float x) -> float
static System.MathF.Log(float x, float y) -> float
static System.MathF.Log10(float x) -> float
static System.MathF.Log2(float x) -> float
static System.MathF.Max(float x, float y) -> float
static System.MathF.MaxMagnitude(float x, float y) -> float
static System.MathF.Min(float x, float y) -> float
static System.MathF.MinMagnitude(float x, float y) -> float
static System.MathF.Pow(float x, float y) -> float
static System.MathF.Round(float x) -> float
static System.MathF.Round(float x, System.MidpointRounding mode) -> float
static System.MathF.Round(float x, int digits) -> float
static System.MathF.Round(float x, int digits, System.MidpointRounding mode) -> float
static System.MathF.ScaleB(float x, int n) -> float
static System.MathF.Sign(float x) -> int
static System.MathF.Sin(float x) -> float
static System.MathF.Sinh(float x) -> float
static System.MathF.Sqrt(float x) -> float
static System.MathF.Tan(float x) -> float
static System.MathF.Tanh(float x) -> float
static System.MathF.Truncate(float x) -> float
```
