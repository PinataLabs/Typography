// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Text.Json;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// Writes the subsets that the tests make, with a JSON file for each that names the font and the glyphs, when the environment
/// variable EXPORT_FONT_SUBSETS names a directory. tests/tools/verify_font_subsets.py reads that directory and checks every subset with fontTools.
/// With the variable unset, it does nothing.
/// </summary>
internal static class SubsetExport
{
    private const string Variable = "EXPORT_FONT_SUBSETS";

    public static void Write(string fontFile, ushort[] glyphIds, byte[] subset)
    {
        string? directory = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        string name = fontFile.Replace('/', '_') + "." + glyphIds.Length + "-glyphs-from-" + (glyphIds.Length > 0 ? glyphIds[0] : 0);
        string extension = ".subset" + Path.GetExtension(fontFile);
        File.WriteAllBytes(Path.Combine(directory, name + extension), subset);
        File.WriteAllText(
            Path.Combine(directory, name + ".json"),
            JsonSerializer.Serialize(new { original = TestFonts.GetPath(fontFile), subset = name + extension, glyphs = glyphIds }));
    }
}
