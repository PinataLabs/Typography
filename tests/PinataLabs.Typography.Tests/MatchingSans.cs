// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The Matching Sans family that tests/tools/generate_font_matching_fonts.py builds.
/// A test picks the faces that its family has by naming the files to add.
/// </summary>
internal static class MatchingSans
{
    /// <summary>
    /// Every face of the family.
    /// </summary>
    public static readonly string[] AllFaces =
    [
        "Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black",
        "LightItalic", "Italic", "BoldItalic",
        "Oblique", "SteepOblique",
        "ExtraCondensed", "Condensed", "CondensedBold", "Expanded", "ExtraExpanded",
    ];

    /// <summary>
    /// Creates a collection that holds the named faces, in that order, and gives its family.
    /// </summary>
    /// <param name="faces">The faces to add, such as "Light" or "CondensedBold".</param>
    /// <returns>The family of the faces.</returns>
    public static FontFamily Family(params string[] faces)
    {
        FontCollection collection = new();
        foreach (string face in faces)
        {
            collection.Add(TestFonts.MatchingSansFile(face));
        }

        return collection.Get("Matching Sans");
    }
}
