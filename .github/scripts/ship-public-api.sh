#!/usr/bin/env bash
#
# Moves the entries in PublicAPI.Unshipped.txt into PublicAPI.Shipped.txt, as
# Microsoft.CodeAnalysis.PublicApiAnalyzers expects when a version ships.
#
#   - An entry is appended to Shipped.
#   - A "*REMOVED*" entry removes the matching line from Shipped.
#   - Shipped is re-sorted in ordinal (C locale) order, which is the order it is in
#     today, and Unshipped is reset to its "#nullable enable" header.
#
# The build that follows checks the result: RS0016/RS0017 fail it if the two files no
# longer describe the public surface.
#
# Usage: ship-public-api.sh [project-dir]
#
#   project-dir  Defaults to src/PinataLabs.Typography in the repository root.

set -euo pipefail

dir=${1:-"$(dirname "$0")/../../src/PinataLabs.Typography"}
shipped="$dir/PublicAPI.Shipped.txt"
unshipped="$dir/PublicAPI.Unshipped.txt"
header='#nullable enable'

for file in "$shipped" "$unshipped"; do
  if [[ ! -f $file ]]; then
    echo "::error::Not found: $file" >&2
    exit 2
  fi
done

# Entries only: no header, no blank lines, no trailing CR.
entries() {
  tr -d '\r' <"$1" | grep -vxF "$header" | grep -v '^[[:space:]]*$' || true
}

added=$(entries "$unshipped" | grep -v '^\*REMOVED\*' || true)
removed=$(entries "$unshipped" | sed -n 's/^\*REMOVED\*//p')

if [[ -z $added && -z $removed ]]; then
  echo "PublicAPI.Unshipped.txt has no entries — nothing to ship"
  exit 0
fi

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

{
  echo "$header"
  {
    if [[ -n $removed ]]; then
      entries "$shipped" | grep -vxF -f <(printf '%s\n' "$removed") || true
    else
      entries "$shipped"
    fi
    [[ -n $added ]] && printf '%s\n' "$added"
  } | LC_ALL=C sort -u
} >"$tmp"

mv "$tmp" "$shipped"
trap - EXIT
printf '%s\n' "$header" >"$unshipped"

echo "Shipped $(grep -c . <<<"$added" || true) added and $(grep -c . <<<"$removed" || true) removed public API entries"
