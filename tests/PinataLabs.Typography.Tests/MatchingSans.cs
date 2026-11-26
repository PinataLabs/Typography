// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The Matching Sans family that tests/tools/generate_font_matching_fonts.py builds.
/// A test picks the faces that its family has by naming the files to add.
/// </summary>
internal static class MatchingSans
{
    private const string FilePrefix = "MatchingSans-";

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
    /// The nine upright faces of normal width, from Thin to Black.
    /// </summary>
    public static readonly string[] Weights =
        ["Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black"];

    /// <summary>
    /// Gets the style that the generator gives to a face, in OS/2 and post.
    /// </summary>
    /// <param name="face">The face, such as "Light" or "CondensedBold".</param>
    /// <returns>The style of the face.</returns>
    public static FontStyle StyleOf(string face) => face switch
    {
        "Thin" => new(new FontWeight(100), FontWidth.Normal, FontSlope.Normal),
        "ExtraLight" => new(new FontWeight(200), FontWidth.Normal, FontSlope.Normal),
        "Light" => new(new FontWeight(300), FontWidth.Normal, FontSlope.Normal),
        "Regular" => FontStyle.Regular,
        "Medium" => new(new FontWeight(500), FontWidth.Normal, FontSlope.Normal),
        "SemiBold" => new(new FontWeight(600), FontWidth.Normal, FontSlope.Normal),
        "Bold" => FontStyle.Bold,
        "ExtraBold" => new(new FontWeight(800), FontWidth.Normal, FontSlope.Normal),
        "Black" => new(new FontWeight(900), FontWidth.Normal, FontSlope.Normal),
        "LightItalic" => new(new FontWeight(300), FontWidth.Normal, FontSlope.Italic),
        "Italic" => FontStyle.Italic,
        "BoldItalic" => FontStyle.BoldItalic,
        "Oblique" => new(FontWeight.Normal, FontWidth.Normal, FontSlope.Oblique(10F)),
        "SteepOblique" => new(FontWeight.Normal, FontWidth.Normal, FontSlope.Oblique(20F)),
        "ExtraCondensed" => new(FontWeight.Normal, new FontWidth(62.5F), FontSlope.Normal),
        "Condensed" => new(FontWeight.Normal, new FontWidth(75F), FontSlope.Normal),
        "CondensedBold" => new(FontWeight.Bold, new FontWidth(75F), FontSlope.Normal),
        "Expanded" => new(FontWeight.Normal, new FontWidth(125F), FontSlope.Normal),
        "ExtraExpanded" => new(FontWeight.Normal, new FontWidth(150F), FontSlope.Normal),
        _ => throw new KeyNotFoundException(face),
    };

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

    /// <summary>
    /// Gives the name of a face, such as "Light", from its file.
    /// </summary>
    /// <param name="face">The metrics of a face that a family of <see cref="Family(string[])"/> holds.</param>
    /// <returns>The name of the face.</returns>
    public static string FaceName(FontMetrics face)
        => Path.GetFileNameWithoutExtension(((FileFontMetrics)face).Path)[FilePrefix.Length..];

    /// <summary>
    /// Gives the order in which a request reaches the faces: the face that matches, then the face that
    /// matches once that one is gone, and so on until every face has had its turn.
    /// </summary>
    /// <param name="faces">The faces of the family.</param>
    /// <param name="request">The requested style.</param>
    /// <returns>The names of the faces, best match first.</returns>
    public static string[] SearchOrder(string[] faces, FontStyle request)
    {
        List<string> remaining = [.. faces];
        List<string> order = [];
        while (remaining.Count > 0)
        {
            string match = FaceName(Family([.. remaining]).MatchFace(request));
            order.Add(match);
            remaining.Remove(match);
        }

        return [.. order];
    }
}
