// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
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

    /// <summary>
    /// Gets the name of the files of a subset. The name has the number of glyphs, the first glyph and a hash of all the glyph IDs,
    /// so two subsets of one font with the same number of glyphs and the same first glyph do not share a name.
    /// </summary>
    public static string GetName(string fontFile, ushort[] glyphIds)
    {
        byte[] bytes = new byte[2 * glyphIds.Length];
        for (int i = 0; i < glyphIds.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2 * i), glyphIds[i]);
        }

        string hash = Convert.ToHexString(SHA256.HashData(bytes), 0, 4).ToLowerInvariant();
        return fontFile.Replace('/', '_') + "." + glyphIds.Length + "-glyphs-from-" + (glyphIds.Length > 0 ? glyphIds[0] : 0) + "-" + hash;
    }

    public static void Write(string fontFile, ushort[] glyphIds, byte[] subset)
    {
        string? directory = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        string name = GetName(fontFile, glyphIds);
        string extension = ".subset" + Path.GetExtension(fontFile);
        File.WriteAllBytes(Path.Combine(directory, name + extension), subset);
        File.WriteAllText(
            Path.Combine(directory, name + ".json"),
            JsonSerializer.Serialize(new { original = TestFonts.GetPath(fontFile), subset = name + extension, glyphs = glyphIds }));
    }
}
