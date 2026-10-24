#!/usr/bin/env bash
#
# Rolls the "[Unreleased]" section of CHANGELOG.md into a dated version heading and
# leaves a fresh, empty "[Unreleased]" section at the top. The headings follow
# Keep a Changelog, as the rest of the file does.
#
#   ## [Unreleased]        ->   ## [Unreleased]
#
#   ### Fixed                    ## [1.0.0] - 2026-10-24
#   - ...
#                                ### Fixed
#                                - ...
#
# Usage: roll-changelog.sh <version> [date] [changelog-path]
#
#   version         Version being released, with or without a leading "v".
#   date            Release date as YYYY-MM-DD. Defaults to today (UTC).
#   changelog-path  Defaults to CHANGELOG.md in the repository root.
#
# Exits non-zero if the changelog has no "[Unreleased]" heading, or if that section
# is empty — releasing with nothing written down is almost always a mistake.

set -euo pipefail

version=${1:?usage: roll-changelog.sh <version> [date] [changelog-path]}
date=${2:-$(date -u +%Y-%m-%d)}
changelog=${3:-"$(dirname "$0")/../../CHANGELOG.md"}

version=${version#v}

title='## [Unreleased]'
heading="## [${version}] - ${date}"

if [[ ! -f $changelog ]]; then
  echo "::error::Changelog not found: $changelog" >&2
  exit 2
fi

# Headings are compared as fixed strings, ignoring trailing whitespace, so a file
# checked out with CRLF endings still matches.
has_line() {
  awk -v want="$1" '
    { line = $0; sub(/[[:space:]]+$/, "", line) }
    line == want { found = 1; exit }
    END { exit !found }
  ' "$changelog"
}

has_prefix() {
  awk -v want="$1" '
    index($0, want) == 1 { found = 1; exit }
    END { exit !found }
  ' "$changelog"
}

if ! has_line "$title"; then
  echo "::error::No '$title' heading found in $changelog" >&2
  exit 3
fi

if has_prefix "## [${version}]"; then
  echo "::error::$changelog already contains a heading for ${version}" >&2
  exit 4
fi

# Everything between the Unreleased heading and the next "## " heading (or EOF).
unreleased_body=$(awk -v title="$title" '
  { line = $0; sub(/[[:space:]]+$/, "", line) }
  line == title { inside = 1; next }
  inside && /^## / { exit }
  inside { print }
' "$changelog")

if [[ -z ${unreleased_body//[[:space:]]/} ]]; then
  echo "::error::The Unreleased section of $changelog is empty — nothing to release" >&2
  exit 5
fi

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

awk -v title="$title" -v heading="$heading" '
  { print }
  { line = $0; sub(/[[:space:]]+$/, "", line) }
  !done && line == title {
    print ""
    print heading
    done = 1
  }
' "$changelog" >"$tmp"

mv "$tmp" "$changelog"
trap - EXIT

echo "Rolled '${title}' into '${heading}' in ${changelog}"
