#!/usr/bin/env bash
#
# Writes one CycloneDX software bill of materials per packed package, next to the package:
#
#   <packages-dir>/PinataLabs.Typography.<version>.cdx.json
#   <packages-dir>/PinataLabs.Typography.Unicode.<version>.cdx.json
#
# The version is read from the .nupkg that is already in <packages-dir>, so a bill of materials
# cannot name a version that was not packed. The components come from each project's restored
# dependencies (artifacts/obj/<project>/project.assets.json), so run it after the restore and the pack. It does not
# restore, because the tool's own restore would run without GenerateSBOM and rewrite that file.
# Packages that only build the project, such as MinVer, are listed with scope "excluded".
#
# The CycloneDX tool is pinned in .config/dotnet-tools.json. Run `dotnet tool restore` first.
#
# Usage: generate-sbom.sh [packages-dir]
#
#   packages-dir  Defaults to ./packages, where the release workflow packs both projects.

set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
packages=${1:-"$root/packages"}
packages="$(cd "$packages" && pwd)"

# PinataLabs.Typography.<digit> cannot match PinataLabs.Typography.Unicode.<digit>, whose name has a
# letter after the dot.
for id in PinataLabs.Typography.Unicode PinataLabs.Typography; do
  nupkg=("$packages/$id".[0-9]*.nupkg)
  if [[ ${#nupkg[@]} -ne 1 || ! -f ${nupkg[0]} ]]; then
    echo "::error::Expected one $id package in $packages" >&2
    exit 1
  fi

  version=$(basename "${nupkg[0]}" .nupkg)
  version=${version#"$id".}
  bom="$packages/$id.$version.cdx.json"

  dotnet tool run dotnet-CycloneDX "$root/src/$id/$id.csproj" \
    --disable-package-restore \
    --include-project-references \
    --set-nuget-purl \
    --set-name "$id" \
    --set-version "$version" \
    --set-type Library \
    --output-format Json \
    --output "$packages" \
    --filename "$(basename "$bom")"

  # The restore runs before MinVer, so project.assets.json holds PinataLabs.Typography.Unicode at the
  # default 0.0.1, and the tool copies that into the bill of materials. The two packages are always
  # packed at one version, which the release workflow checks, so correct it to the packed version.
  jq --arg v "$version" '
    walk(if type == "string" then sub("^pkg:nuget/PinataLabs\\.Typography\\.Unicode@.*$"; "pkg:nuget/PinataLabs.Typography.Unicode@" + $v) else . end)
    | (.components[]? | select(.name == "PinataLabs.Typography.Unicode") | .version) = $v
  ' "$bom" > "$bom.tmp"
  mv "$bom.tmp" "$bom"
done

ls -la "$packages"/*.cdx.json
