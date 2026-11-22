// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

/// <summary>
/// The Type 1 fonts and fontTools fixtures under Fonts/Type1. See the README there for sources and licences.
/// </summary>
internal static class Type1TestFonts
{
    public static byte[] Read(string fileName) => File.ReadAllBytes(GetPath(fileName));

    public static string GetPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Fonts", "Type1", fileName);
}
