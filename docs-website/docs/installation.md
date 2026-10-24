---
title: Installation
description: Add PinataLabs.Typography to a .NET project, from a project reference or from a local NuGet package.
---

## Requirements

- Your project must target **.NET 8**, **.NET 10** or **.NET 11**. The package contains builds for
  `net8.0`, `net10.0` and `net11.0`.
- The library has no native dependencies and no package dependencies. It runs on Windows, Linux,
  macOS and any other platform that .NET supports.

## Get the package

The package is not yet published to NuGet. Until it is, build it from source.

1. Clone the repository and build it:

   ```bash
   git clone https://github.com/PinataLabs/Typography
   cd Typography
   dotnet build PinataLabs.Typography.slnx -c Release
   ```

   To build the repository you must install the **.NET 11 SDK, RC2 or later**
   (`11.0.100-rc.2`). The file `global.json` pins this SDK. If it is not installed, `dotnet build`
   stops with an SDK-not-found error. This applies only to building the library. Your own project
   can use any SDK that supports its target framework.

2. Choose one of these ways to use the library from your project.

### Option 1: project reference

Add a reference to the library project:

```bash
dotnet add reference path/to/Typography/src/PinataLabs.Typography/PinataLabs.Typography.csproj
```

### Option 2: local NuGet package

1. Make a package in a local folder:

   ```bash
   dotnet pack src/PinataLabs.Typography -c Release -o ./artifacts
   ```

2. In your project, install the package from that folder:

   ```bash
   dotnet add package PinataLabs.Typography --source path/to/Typography/artifacts
   ```

   A local build has the version `0.0.1`. Official builds take their version from the Git tag.

## Check the installation

Add this code to a console application. It lists the font families installed on the machine:

```csharp
using PinataLabs.Typography;

foreach (FontFamily family in SystemFonts.Families)
{
    Console.WriteLine(family.Name);
}
```

If the program prints font names, the library works. On a container or a server with no fonts
installed, the list is empty. [System fonts](./fonts/system-fonts.md) tells you where the library
looks for fonts.

## Next step

Go to [Getting started](./getting-started.md) to load a font and measure text.
