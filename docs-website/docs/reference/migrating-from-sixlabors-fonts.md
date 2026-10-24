---
title: Migrating from SixLabors.Fonts
description: Move code from SixLabors.Fonts to PinataLabs.Typography. The types and members keep their names; the namespaces, targets and AppContext switch change.
---

PinataLabs.Typography is a fork of SixLabors.Fonts, taken at commit `32bef429`. The type and member
names are the same, so most code needs only new `using` directives.

## What changes

| | SixLabors.Fonts | PinataLabs.Typography |
| --- | --- | --- |
| Package and assembly | `SixLabors.Fonts` | `PinataLabs.Typography` |
| Root namespace | `SixLabors.Fonts` | `PinataLabs.Typography` |
| Other namespaces | `SixLabors.Fonts.X` | `PinataLabs.Typography.X` |
| Target frameworks | `netcoreapp3.1`, `netstandard2.0`, `netstandard2.1` and others | `net8.0`, `net10.0`, `net11.0` |
| Package dependencies | `System.Memory` and others on older targets | None |
| Strong name | Strong-named | Not strong-named |
| AppContext switch to stop native font enumeration on macOS | `Switch.SixLabors.Fonts.DoNotUseNativeSystemFontsEnumeration` | `Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration` |

## Steps

1. Remove the `SixLabors.Fonts` package and add `PinataLabs.Typography`. See
   [Installation](../installation.md).
2. Replace the namespaces. Every namespace maps one to one:

   ```csharp
   // Before
   using SixLabors.Fonts;
   using SixLabors.Fonts.Unicode;
   using SixLabors.Fonts.Tables.AdvancedTypographic;

   // After
   using PinataLabs.Typography;
   using PinataLabs.Typography.Unicode;
   using PinataLabs.Typography.Tables.AdvancedTypographic;
   ```

3. If you set the AppContext switch for macOS font enumeration, rename it. The old name has no
   effect.
4. Make sure that your project targets .NET 8 or later.
5. If you referenced the assembly by its strong name, for example in a binding redirect or with
   `InternalsVisibleTo`, rebuild without it. The public key token is now null.

## Libraries that depend on SixLabors.Fonts

Libraries such as SixLabors.ImageSharp.Drawing take `SixLabors.Fonts` types in their APIs. The
types of the two packages have the same names but are different types, so you cannot pass a
`PinataLabs.Typography.Font` to them. Keep SixLabors.Fonts for code that talks to those libraries.

## Removed members

The fork removed some type forwarders that existed only for the old target frameworks
(`System.HashCode`, `System.MathF` and others). Your code still gets those types from the .NET
runtime. The [changelog](https://github.com/PinataLabs/Typography/blob/main/CHANGELOG.md) lists
them.
