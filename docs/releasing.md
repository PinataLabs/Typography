# Releasing

PinataLabs.Typography is published to nuget.org from GitHub Actions. There are two
buttons in the Actions tab. Both call one workflow that does the publishing.

## Two packages, one version

Each release publishes two packages:

| Package | Project | Contents |
|---|---|---|
| `PinataLabs.Typography.Unicode` | `src/PinataLabs.Typography.Unicode` | The Unicode algorithms and character properties. No font code. |
| `PinataLabs.Typography` | `src/PinataLabs.Typography` | Fonts and text layout. Depends on the package above. |

They always have the same version. `PinataLabs.Typography` reads internals of the Unicode
assembly, so its package depends on exactly the Unicode package packed with it, `[x.y.z]`
and not `x.y.z` or newer. The publish workflow stops when the two packed versions differ.
It pushes the Unicode package first.

Each project has its own `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and a stable
release ships both.

| Workflow | File | What it does |
|---|---|---|
| Release | `release-prepare.yml` | Stable release. Rolls the changelog, ships the public API, tags `vX.Y.Z`, then publishes. |
| Pre-release | `release-prerelease.yml` | Publishes an `alpha`, `beta` or `rc` package from the current commit. No tag, no GitHub Release. |
| Release Publish | `release-publish.yml` | Called by the two above. Builds, tests, packs and pushes to NuGet. You do not run it directly. |

Both buttons have **dry-run** on by default. Run a dry run first, read the job log and the
summary, then run again with dry-run off.

## Stable release

1. Make sure `CHANGELOG.md` has entries under `## [Unreleased]`. The release fails if that
   section is empty.
2. Run **Release** from `main`. Leave **version** empty to use the version Release Drafter
   has drafted, or enter one, for example `1.0.0`.
   - Release Drafter drafts `v0.0.1` until the first release is published. For the first
     release, enter the version.
3. The workflow then:
   - rolls `## [Unreleased]` into `## [1.0.0] - <date>`,
   - moves the entries in `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt`, in both
     projects,
   - commits both to `main`, tags the commit `v1.0.0` and pushes them,
   - builds and tests the tag, packs both projects, checks that both packages have the
     version of the tag,
   - pushes the packages and symbols to NuGet, the Unicode package first,
   - publishes the drafted GitHub Release and attaches the packages.

If a stable release fails after the tag is pushed, do not run **Release** again: the tag
already exists, so it stops. Open the failed run and use **Re-run failed jobs**. That runs
the publish part again from the tag, and the NuGet push skips a version that is already
there.

## Pre-release

Run **Pre-release** from `main` and pick a label. MinVer numbers the package from the last
`v` tag and the number of commits since it, for example `0.1.0-beta.12`.

The run fails if that version is already on nuget.org for either package. That happens
when nothing has been committed since the last pre-release with the same label.

From a branch other than `main`, a dry run works but publishing does not. The `nuget`
environment allows deployments from `main` only.

## Unicode version

`UnicodeInfo.Version` reports the Unicode version of the property data. It comes from the
rule files in `src/PinataLabs.Typography.UnicodeTrieGenerator/Rules`, through the generator.
A caller can pin its own conformance suites to it, so a release that changes it must say so:
put a line in the `CHANGELOG.md` entry, for example "The Unicode data is version 18.0.0, up
from 17.0.0". Read the [rule files' README](../src/PinataLabs.Typography.UnicodeTrieGenerator/Rules/README.md)
for how to move to a new version.

## Versioning

- Versions come from git tags through MinVer, with the tag prefix `v`.
- `MinVerMinimumMajorMinor` is `0.1`, so before the first stable tag every build is
  `0.1.0-alpha.0.N`.
- Release Drafter picks the next stable version from the labels on merged pull requests:
  `breaking` bumps the major version, `enhancement` the minor version, anything else the
  patch version.
- The **PR Autolabel** workflow sets those labels from the pull request title, which follows
  Conventional Commits (`feat:`, `fix:`, `feat!:` and so on).

> [!WARNING]
> A local clone of this repository can have the upstream `v1.0.0-betaN` tags. They are not
> on `origin`, and they must stay off it. Do not run `git push --tags`. MinVer would read
> them, and the release workflows compare new versions with the highest `v` tag.

## Setup

These are set once. They are recorded here so they can be checked or rebuilt.

### nuget.org trusted publishing

The policy on nuget.org, for the `jafin` account:

| Field | Value |
|---|---|
| Repository owner | `PinataLabs` |
| Repository | `Typography` |
| Workflow file | `release-publish.yml` |
| Environment | `nuget` |

The policy has to let this workflow push both package IDs, `PinataLabs.Typography` and
`PinataLabs.Typography.Unicode`. A dry run does not push, so it does not show whether
nuget.org accepts the first push of the Unicode ID. The owner of the `jafin` account should
check the policy, and the ownership of the new ID, before the first release that carries it.
If the first push is rejected, push that package once by hand with an API key, then run the
release again: the push skips a version that is already there.

nuget.org checks the token's `job_workflow_ref` claim. That claim names the workflow file
that runs the NuGet login step, even when another workflow calls it. That is why the policy
names `release-publish.yml` and not the two workflows you run.

### GitHub environment

The `nuget` environment, under **Settings → Environments**, allows deployments from the
`main` branch only. nuget.org has no branch restriction of its own, so this is what stops a
workflow on another branch from publishing.

### Labels

The autolabeler cannot create labels, so these must exist in the repository: `breaking`,
`enhancement`, `bug`, `performance`, `documentation`, `chore`, `dependencies`.

### Branch rules

The release commit and tag are pushed to `main` with `GITHUB_TOKEN`. The **Main** ruleset is
disabled. If it is enabled, add GitHub Actions to its bypass list, or stable releases fail at
the push.
