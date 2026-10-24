---
title: System fonts
description: Use the fonts installed on Windows, Linux, macOS and Android through SystemFonts, and control where the library looks for them.
---

`SystemFonts` gives you the fonts installed on the machine. It finds them the first time you use it
and keeps the result for the life of the process.

## Use an installed font

```csharp
using PinataLabs.Typography;

Font font = SystemFonts.CreateFont("Arial", 12);
Font boldItalic = SystemFonts.CreateFont("Arial", 12, FontStyle.BoldItalic);
```

`CreateFont` and `Get` throw `FontFamilyNotFoundException` when the family is not installed. The
exception message, and its `SearchDirectories` property, list the folders that the library looked
in. When you are not sure that a font is installed, use `TryGet`:

```csharp
if (!SystemFonts.TryGet("Segoe UI", out FontFamily family) &&
    !SystemFonts.TryGet("DejaVu Sans", out family))
{
    throw new InvalidOperationException("No usable UI font is installed.");
}

Font font = family.CreateFont(12);
```

## List the installed fonts

```csharp
foreach (FontFamily family in SystemFonts.Families.OrderBy(f => f.Name))
{
    string styles = string.Join(", ", family.GetAvailableStyles());
    Console.WriteLine($"{family.Name}: {styles}");
}
```

The repository has a small sample that does this, in `samples/ListFonts`.

## Where the library looks

On Windows, Linux and Android, the library looks in these folders and in all of their subfolders:

| Platform | Folders |
| --- | --- |
| Windows | `%SYSTEMROOT%\Fonts`, `%APPDATA%\Microsoft\Windows\Fonts`, `%LOCALAPPDATA%\Microsoft\Windows\Fonts` |
| Linux | `~/.fonts/`, `~/.local/share/fonts/`, `/usr/local/share/fonts/`, `/usr/share/fonts/` |
| Android | `/system/fonts/` |

In those folders it loads files with the extensions `.ttf`, `.ttc` and `.otf`. It does not load
`.woff` or `.woff2` files from system folders.

On **macOS** the library asks the operating system (Core Text) for the list of installed fonts. To
make it search the folders `~/Library/Fonts/`, `/Library/Fonts/`, `/System/Library/Fonts/` and
`/Network/Library/Fonts/` instead, turn on this switch at the start of your program, before you use
`SystemFonts`:

```csharp
AppContext.SetSwitch("Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration", true);
```

You can also set it in your project file:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration" Value="true" />
</ItemGroup>
```

`SystemFonts.Collection.SearchDirectories` tells you which folders were searched. With native
enumeration on macOS, the list is empty.

## Containers and servers

Many container images have no fonts. Then `SystemFonts.Families` is empty and every `Get` throws.
You can:

- Ship the fonts that you need with your application and load them into a
  [`FontCollection`](./loading-fonts.md). This is the most predictable choice, because the output
  does not change with the machine.
- Install fonts in the image. On Debian and Ubuntu, for example, `apt-get install fonts-dejavu-core`
  puts fonts in `/usr/share/fonts/`.

## Culture-specific names

Like a `FontCollection`, `SystemFonts` has overloads that take a `CultureInfo`, so you can find and
list families by their names in another language:

```csharp
using System.Globalization;

CultureInfo japanese = CultureInfo.GetCultureInfo("ja-JP");
IEnumerable<FontFamily> families = SystemFonts.GetByCulture(japanese);
```
