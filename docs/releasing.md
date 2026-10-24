# Releasing

PinataLabs.Typography is published to nuget.org from GitHub Actions. There are two
buttons in the Actions tab. Both call one workflow that does the publishing.

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
   - moves the entries in `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt`,
   - commits both to `main`, tags the commit `v1.0.0` and pushes them,
   - builds and tests the tag, packs it, checks the package version equals the tag,
   - pushes the package and symbols to NuGet,
   - publishes the drafted GitHub Release and attaches the packages.

If a stable release fails after the tag is pushed, do not run **Release** again: the tag
already exists, so it stops. Open the failed run and use **Re-run failed jobs**. That runs
the publish part again from the tag, and the NuGet push skips a version that is already
there.

## Pre-release

Run **Pre-release** from `main` and pick a label. MinVer numbers the package from the last
`v` tag and the number of commits since it, for example `1.0.0-beta.12`.

The run fails if that version is already on nuget.org. That happens when nothing has been
committed since the last pre-release with the same label.

From a branch other than `main`, a dry run works but publishing does not. The `nuget`
environment allows deployments from `main` only.

## Versioning

- Versions come from git tags through MinVer, with the tag prefix `v`.
- `MinVerMinimumMajorMinor` is `1.0`, so before the first stable tag every build is
  `1.0.0-alpha.0.N`.
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
