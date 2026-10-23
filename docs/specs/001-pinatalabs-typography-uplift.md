# Spec 001: PinataLabs.Typography Project Uplift

**Status:** Approved (2026-10-23)
**Date:** 2026-10-23

## Problem

This repository is a fork of SixLabors.Fonts cut at the last Apache-2.0 commit
(PR #344, `32bef429`). Later upstream code is under a different license and must
never be reintroduced. The fork still carries SixLabors branding and identity:
namespaces, the package ID, the strong-name key, trademarked icons, and shared
build infrastructure pulled from a SixLabors-owned submodule. It also runs on an
obsolete toolchain: netstandard/netcoreapp3.1 targets, `.sln`, xUnit and C# 9.
The goal is an independently branded, modern library: **PinataLabs.Typography**.

## Scope

### In

1. **Vendor and remove the `shared-infrastructure` submodule.** Copy only the
   parts in use (Apache-2.0 at submodule commit `33cb12ca`) into the repo,
   rebrand them, and remove the submodule entry and `.gitmodules`. Do not copy
   `branding/` (trademarks) or `SixLabors.snk`.
2. **Convert the solution** from `SixLabors.Fonts.sln` to `PinataLabs.Typography.slnx`.
3. **Retarget** the library to `net8.0;net10.0;net11.0`. Remove every other target
   and the things that only existed for them: the `System.Memory` and
   `System.Runtime.CompilerServices.Unsafe` polyfills, `NULLABLE_ATTRIBUTES`,
   `SUPPORTS_*` and `NETSTANDARD2_0` branches, and the vendored `HashCode`,
   `MathF` and `ExcludeFromCodeCoverageAttribute` shims.
4. **Pin the SDK** with `global.json` to `11.0.100-rc.2`
   (`rollForward: latestFeature`, `allowPrerelease: true`).
5. **Rebrand** the projects, folders, assemblies, NuGet package ID, root
   namespaces, every `namespace`/`using`, the AppContext switch, the docs and
   the package metadata (see Domain Model).
6. **Licensing and attribution:** add the new file headers and a `NOTICE` file
   (see License & Attribution).
7. **Remove strong-name signing:** no `.snk`, and no public-key
   `InternalsVisibleTo`.
8. **Record the public API** with `Microsoft.CodeAnalysis.PublicApiAnalyzers`
   (`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`), captured straight
   after the rename.
9. **Migrate the tests to TUnit**, using native async assertions
   (`await Assert.That(...)`). Replace Moq with NSubstitute.
10. **Upgrade the language:** `LangVersion` latest, file-scoped namespaces,
    Nullable enabled, and warnings as errors in **every** project.
11. **Rewrite internals for net8+:** use BCL APIs instead of shims and modern
    APIs internally (Span, `ArgumentNullException.ThrowIfNull`, collection
    expressions, etc.), with **no public API change**.
12. **Move package versions to Central Package Management**
    (`Directory.Packages.props`), all at their latest stable versions.
13. **Move code style to .editorconfig:** the StyleCop rules move from
    `sixlabors.ruleset` into `.editorconfig` severities at the same strength.
    `stylecop.json` gets the new header template.
14. **Benchmarks:** rebrand them, multi-target `net8.0;net10.0`, and remove any
    comparison against SixLabors.Fonts packages.
15. **Samples:** rebrand and retarget `ListFonts` to `net10.0`. Delete
    `DrawWithImageSharp`.
16. **Docs and community files:** rewrite the README; rebrand CONTRIBUTING,
    the issue/PR templates and CODE_OF_CONDUCT (contact points to the
    PinataLabs/Typography GitHub repo, no email); delete `FUNDING.yml`.
    `dependabot.yml` is kept and updated for the new layout.
17. **CI:** rewrite `.github/workflows/build-and-test.yml` to build and test on
    ubuntu, windows and macos for net8/10/11. No pack, publish or codecov.
18. **Mechanism for additive/breaking API changes:** the work in scope is the
    API baseline, a `CHANGELOG.md`, and the approval rule (see API Contract).
    No specific API change is pre-approved by this spec.

### Out

- Any code from upstream SixLabors/Fonts after `32bef429`, or any
  SixLabors package dependency (e.g. ImageSharp.Drawing).
- SixLabors branding assets. The package ships without an icon until PinataLabs
  supplies one.
- NuGet pack/publish, codecov, release automation.
- Specific public API additions or removals. Each one is proposed and approved
  individually after this spec lands.
- Behaviour changes to font loading, shaping, layout or rendering.

## License & Attribution (fixed requirement)

Apache-2.0 §4 and §6 apply:

- `LICENSE` (Apache-2.0) stays unchanged.
- Every existing source file carries:
  ```
  // Copyright (c) Six Labors.
  // Copyright (c) PinataLabs.
  // Licensed under the Apache License, Version 2.0.
  ```
  New files carry only `// Copyright (c) PinataLabs.` plus the license line.
- `NOTICE` states: PinataLabs.Typography is derived from SixLabors.Fonts
  (Copyright (c) Six Labors, Apache-2.0), forked at commit `32bef429`, and
  modified by PinataLabs. It also lists the vendored SharedInfrastructure
  files (commit `33cb12ca`).
- No SixLabors trademarks (names used as branding, logos, icons) appear in the
  product identity. Factual attribution in `NOTICE` and the README is allowed.
- Package metadata: `Authors`/`Company` = `PinataLabs`,
  `PackageLicenseExpression` = `Apache-2.0`,
  `RepositoryUrl` = `https://github.com/PinataLabs/Typography`.

## Domain Model (naming map)

| Before | After |
|---|---|
| `SixLabors.Fonts.sln` | `PinataLabs.Typography.slnx` |
| `src/SixLabors.Fonts/` | `src/PinataLabs.Typography/` |
| `src/UnicodeTrieGenerator/` | `src/PinataLabs.Typography.UnicodeTrieGenerator/` |
| `tests/SixLabors.Fonts.Tests/` | `tests/PinataLabs.Typography.Tests/` |
| `tests/SixLabors.Fonts.Benchmarks/SixLabors.Fonts.Benchmarks/` | `tests/PinataLabs.Typography.Benchmarks/` (flattened) |
| `samples/ListFonts/` | `samples/ListFonts/` (contents rebranded) |
| `samples/DrawWithImageSharp/`, `ConsoleApp1/` | deleted |
| `namespace SixLabors.Fonts[.X]` | `namespace PinataLabs.Typography[.X]` (1:1) |
| `namespace SixLabors` (Guard, ThrowHelper, DebugGuard) | `PinataLabs.Typography`, `internal` |
| `SixLabors.*.props/targets`, `SixLaborsSolutionDirectory`, `SixLaborsPublicKey` | `PinataLabs.*` equivalents; the public key is removed |
| `Switch.SixLabors.Fonts.DoNotUseNativeSystemFontsEnumeration` | `Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration` (old name not honoured) |
| `UnicodeTrieGenerator` constants pointing at `SixLabors.Fonts.sln` / `src\SixLabors.Fonts\...` | point at the new solution/paths |
| `restore source https://f.feedz.io/sixlabors/...` | removed |

Renames use `git mv` so history is kept.

**Not rebranded (test data):** font fixtures whose embedded family name is
`SixLaborsSampleAB`, and the assertions on that name.

## API Contract

- **Versioning:** MinVer with tag prefix `v`. The first release tag is
  `v1.0.0-alpha.1`.
- **Public API baseline:** after the rename, `PublicAPI.Shipped.txt` records the
  full public surface under `PinataLabs.Typography.*`. It must equal the original
  SixLabors.Fonts surface with only namespaces changed, except for members that
  existed only for the removed targets.
- **Changes after the baseline:** an additive or breaking public API change
  requires (a) a proposal approved by the developer, (b) its own commit,
  (c) a matching `PublicAPI.Unshipped.txt` change, and (d) a `CHANGELOG.md`
  entry. The internal rewrites (scope item 11) must not touch the PublicAPI files.
- `CHANGELOG.md` starts with an "Unreleased" section listing the rename, the
  retargeting, the AppContext switch rename, and the dropped strong naming.

## Authorization

_N/A (library). Repository permissions are unchanged._

## Edge Cases & Failure Modes

- **No .NET 11 SDK installed:** the build fails with the SDK-not-found error
  from `global.json`. This is intended and documented in the README.
- **Test fixture names:** `SixLaborsSampleAB` stays (see Domain Model).
- **Generated Unicode resources:** after the rename, the generator must still
  find the solution root (`.slnx`) and write into `src/PinataLabs.Typography/Unicode/Resources`.
- **InternalsVisibleTo** without strong naming: `PinataLabs.Typography.Tests`
  and `DynamicProxyGenAssembly2` (needed by NSubstitute's Castle proxies)
  without `Key`.
- **Platform-specific code** (Mac/Windows/Linux system font enumeration) keeps
  working on all three CI OSes.
- **Leftover branding:** after the rebrand, a case-insensitive search for
  `sixlabors` / `six labors` finds matches only in: the copyright lines,
  `NOTICE`, the README attribution, `LICENSE`, the `SixLaborsSampleAB` test
  data, and this spec.

## Non-Functional Requirements

- **Test parity:** record the xUnit discovered test-case count (theory rows
  expanded) on net8.0 before migrating. TUnit must discover the same count on
  net8.0, net10.0 and net11.0, with all tests passing and no new skips. Any
  intentional difference is listed and explained in the PR.
- **Performance gate:** run the benchmarks on net8.0 at the commit before the
  internal rewrites (scope item 11) and again after. No benchmark may regress
  by more than 5% in mean time or allocated bytes. Results are attached to the
  PR; net10.0 results are informational.
- **Build hygiene:** zero warnings (warnings as errors everywhere), on all
  three TFMs, on all three CI OSes.

## Integrations

- **GitHub Actions:** the `build-and-test.yml` matrix (ubuntu-latest,
  windows-latest, macos-latest) runs `dotnet test` against the `.slnx`, using
  `actions/setup-dotnet` with `global-json-file`.
- **Issue tracker:** GitHub Issues, PinataLabs/Typography.
- **Package feeds:** nuget.org restore only. Nothing is published.

## Delivery

One branch with ordered commits. **Every commit builds, and the tests pass on
it** (xUnit until commit 7):

1. Vendor shared-infrastructure, remove the submodule, add CPM
2. `.sln` → `.slnx`
3. Retarget to net8/10/11 + `global.json`; remove polyfills and `#if` branches
4. Rebrand: `git mv` the folders and projects, rename namespaces, the AppContext
   switch, MSBuild properties and the generator paths; remove strong naming
5. Headers, `NOTICE`, README rewrite, community files, delete `FUNDING.yml`
6. PublicApiAnalyzers baseline + `CHANGELOG.md`
7. Record the xUnit count → migrate to TUnit + NSubstitute
8. Language upgrade (LangVersion, file-scoped namespaces, nullable errors
   everywhere) + StyleCop → `.editorconfig`
9. Record the benchmark baseline → internal net8+ rewrites → re-run the benchmarks
10. CI workflow rewrite

## Acceptance Criteria

- **AC-1** Given a clean clone, when `git submodule status` runs, then nothing is
  listed and `.gitmodules` does not exist.
- **AC-2** Given the .NET 11 RC2 SDK, when `dotnet build PinataLabs.Typography.slnx -c Release`
  runs, then it succeeds with zero warnings, and the library is produced for
  net8.0, net10.0 and net11.0 only.
- **AC-3** Given the repo root, when the solution files are listed, then only
  `PinataLabs.Typography.slnx` exists (no `.sln`).
- **AC-4** Given the source tree, when the files are searched for
  `namespace SixLabors` or `using SixLabors`, then nothing is found.
- **AC-5** Given the leftover-branding search (Edge Cases), when it runs, then
  matches appear only in the allowed locations.
- **AC-6** Given any `.cs` file that existed at `32bef429`, when its header is
  read, then it has the three-line two-owner header. Given any new `.cs` file,
  then it has the PinataLabs-only header.
- **AC-7** Given the repo, when `NOTICE` is read, then it names SixLabors.Fonts,
  Six Labors' copyright, Apache-2.0, fork commit `32bef429`, and the vendored
  SharedInfrastructure commit `33cb12ca`. `LICENSE` is byte-identical to the original.
- **AC-8** Given the packed library (`dotnet pack`, local only), when the nuspec is
  inspected, then the id is `PinataLabs.Typography`, authors are `PinataLabs`,
  the license is `Apache-2.0`, the repository URL is `https://github.com/PinataLabs/Typography`,
  there is no icon, and the assembly is not strong-named.
- **AC-9** Given the library project, when the project and package references
  are inspected, then there is no `System.Memory`, no `System.Runtime.CompilerServices.Unsafe`,
  no reference to any `SixLabors.*` package, and no `#if` on `NETSTANDARD*`,
  `NETCOREAPP*`, `SUPPORTS_*` or `NULLABLE_ATTRIBUTES`.
- **AC-10** Given the baseline commit (delivery step 6), when `PublicAPI.Shipped.txt`
  is compared with the original public API with namespaces mapped 1:1, then
  the only differences are members that existed only for the removed targets,
  and each one is listed in `CHANGELOG.md`.
- **AC-11** Given the internal-rewrite commits (delivery step 9), when their diff
  is inspected, then neither PublicAPI file changes.
- **AC-12** Given the recorded xUnit test-case count N on net8.0, when
  `dotnet test` runs on the TUnit project, then N tests are discovered on each of
  net8.0, net10.0 and net11.0, all pass, and none are newly skipped.
- **AC-13** Given the test project, when its package references are inspected,
  then xUnit, `xunit.runner.visualstudio` and Moq are absent, TUnit and
  NSubstitute are present, and no test uses `Assert.Equal`-style xUnit
  assertions.
- **AC-14** Given the benchmark baseline and post-rewrite runs on net8.0, when
  they are compared, then no benchmark's mean time or allocated bytes is more
  than 5% worse.
- **AC-15** Given every project, when its effective properties are evaluated, then
  `Nullable=enable`, `TreatWarningsAsErrors=true` and `LangVersion=latest` are
  set, and every `.cs` file uses a file-scoped namespace.
- **AC-16** Given the repo, when `Directory.Packages.props` is inspected, then every
  package version is declared there, none appears in a project file, and each is
  the latest stable at the time of the change.
- **AC-17** Given `.editorconfig`, when the build runs, then the StyleCop rules
  that `sixlabors.ruleset` enforced are enforced at the same severity, and no
  `.ruleset` file remains.
- **AC-18** Given `AppContext.SetSwitch("Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration", true)`,
  when the system fonts load, then directory enumeration is used. The old
  SixLabors switch name has no effect.
- **AC-19** Given the Unicode trie generator, when it runs from the build
  output, then it finds the `.slnx` root and regenerates the resources under
  `src/PinataLabs.Typography/Unicode/Resources` with no diff.
- **AC-20** Given a push to the branch, when CI runs, then it builds and tests on
  ubuntu, windows and macos for net8.0/net10.0/net11.0 and passes. It has no
  pack, publish or codecov steps.
- **AC-21** Given the repo, when the samples and community files are inspected,
  then `DrawWithImageSharp`, `ConsoleApp1` and `FUNDING.yml` are gone,
  `ListFonts` builds and runs on net10.0, and README/CONTRIBUTING/templates/CoC
  reference PinataLabs/Typography, with the CoC contact being the GitHub repo.
- **AC-22** Given `git log --follow` on a renamed source file, when it runs,
  then the pre-rename history is shown.

## Deferred Decisions

| Decision | Fallback until decided | Revisit trigger |
|---|---|---|
| .NET 11 SDK pin after GA | Stay on `11.0.100-rc.2` with `rollForward: latestFeature` | .NET 11 GA (expected Nov 2026): bump the pin to the GA SDK |
| Package icon | No `PackageIcon` | PinataLabs supplies an icon |
| Publishing / codecov / release automation | CI is build + test only | First intended package release |
| Specific additive/breaking API changes | None. Each is proposed and approved individually under the API Contract rule | Any API proposal after this spec lands |

## Open Questions

_None._
