---
title: Contributing
description: How to report bugs, propose changes, build and test PinataLabs.Typography, and edit this documentation.
---

Read the [contribution guide](https://github.com/PinataLabs/Typography/blob/main/.github/CONTRIBUTING.md)
before you open an issue or a pull request. This project follows the
[Code of Conduct](https://github.com/PinataLabs/Typography/blob/main/CODE_OF_CONDUCT.md).

## Report a bug

Search the [issues](https://github.com/PinataLabs/Typography/issues) first. If nobody has reported
the bug, open a new issue with:

- the version of the library and of .NET,
- the font, or a link to it,
- the text and the `TextOptions` that you used,
- what you expected, and what happened.

A small test case is the best way to show a layout bug.

## Propose a change to the public API

Open an issue that describes the change before you write the code. A maintainer must approve each
public API addition or breaking change. The build checks the public API against
`src/PinataLabs.Typography/PublicAPI.Shipped.txt` and fails if it differs.

## Build and test

You need the .NET 11 SDK, RC2 or later, and the .NET 8 and .NET 10 runtimes for the tests.

```bash
git clone https://github.com/PinataLabs/Typography
cd Typography
dotnet build PinataLabs.Typography.slnx -c Release
dotnet test PinataLabs.Typography.slnx -c Release
```

The build applies StyleCop and the `.editorconfig` rules, and treats warnings as errors.

## Edit this documentation

The site is in the `docs-website` folder of the repository, built with
[Docusaurus](https://docusaurus.io/). Each page has an **Edit this page** link at the bottom.

To run the site on your machine you need Node.js 20 or later and [pnpm](https://pnpm.io/):

```bash
cd docs-website
pnpm install
pnpm start
```

`pnpm start` serves the site at `http://localhost:3000/Typography/` and reloads it when you save a
page. `pnpm run build` makes the static site and fails on broken links.
