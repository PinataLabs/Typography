# PinataLabs.Typography documentation site

The PinataLabs.Typography documentation site, built with [Docusaurus](https://docusaurus.io/).
Published to GitHub Pages at <https://pinatalabs.github.io/Typography/>.

## Local development

This project uses [pnpm](https://pnpm.io/). The version is pinned by the
`packageManager` field in [`package.json`](package.json), so `corepack enable` is
enough to get the right one.

```sh
pnpm install
pnpm start
```

`pnpm start` serves the site at <http://localhost:3000/Typography/> with hot reload.

## Build

```sh
pnpm run build      # static output in ./build
pnpm run serve      # serve the built output locally
pnpm run typecheck  # type check the config and sidebars
```

The build fails on a broken link or a broken anchor, so run it before you open a pull request.

## Content

Documentation pages live in [`docs/`](docs) as Markdown. Ordering is controlled by
[`sidebars.ts`](sidebars.ts). A new page must be added there to appear in the sidebar.

The site runs in **docs-only mode** — `docs/overview.md` has `slug: /` and is served
as the site root, so there is no separate landing page under `src/pages`.

| Folder | Contents |
| --- | --- |
| `docs/` (root) | Overview, installation, getting started, concepts. |
| `docs/fonts/` | Loading fonts, system fonts, descriptions and metrics. |
| `docs/text/` | Measuring, wrapping, rich text, international text, fallback, vertical text, OpenType features. |
| `docs/rendering/` | Glyph renderers, color fonts, hinting. |
| `docs/unicode/` | Code points and graphemes. |
| `docs/api/` | The API reference, one page per group of types. |
| `docs/reference/` | Platforms, migration, troubleshooting, FAQ, licensing, contributing. |

The API reference is written by hand from
[`PublicAPI.Shipped.txt`](../src/PinataLabs.Typography/PublicAPI.Shipped.txt). When a pull request
changes the public API, update the matching page in `docs/api/` in the same pull request.

## Publishing

[`.github/workflows/docs.yml`](../.github/workflows/docs.yml) builds the site (and type checks it)
on pull requests that touch `docs-website/`, and builds and deploys it on every push to `main` that
touches `docs-website/`. Pull requests build the site without deploying.

One-time repository setup: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
