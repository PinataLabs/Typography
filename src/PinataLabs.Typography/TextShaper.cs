// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Shapes a run of text with the GSUB and GPOS tables of a font and returns its glyphs, without measuring, rendering or
/// breaking the text into lines. A caller that writes its own output, such as a PDF writer, reads the glyphs, the
/// place of their characters in the text, and their advances and offsets.
/// </summary>
public static class TextShaper
{
    private static readonly Tag Kern = Tag.Parse("kern");

    /// <summary>
    /// Shapes a run of text.
    /// </summary>
    /// <remarks>
    /// The call takes the steps that <see cref="TextRenderer"/> and <see cref="TextMeasurer"/> take for a text run: the
    /// glyphs of the characters, the normalization that the script needs, GSUB, GPOS, the <c>kern</c> table and the
    /// placement of marks. The clusters are those of HarfBuzz at its default cluster level. A ligature is one glyph at the
    /// cluster of its first character. A mark shares the cluster of its base. A decomposition keeps the cluster of its
    /// character. A reordering gives the glyphs that it joins the lowest cluster among them, as HarfBuzz does: in an Indic
    /// syllable, a vowel sign written before its consonant joins the glyphs from its new place to the consonant, and the
    /// glyphs before that place keep their clusters. The font is not scaled, so its size does not matter.
    /// </remarks>
    /// <param name="font">The font. A variable font shapes with the variations that it was created with.</param>
    /// <param name="text">The text of the run. It is not split into lines, and a line break is not special.</param>
    /// <param name="options">The script, language and direction of the run, and its features. <see langword="null"/> for the defaults.</param>
    /// <returns>
    /// The glyphs in visual order: from left to right, so a run from right to left starts with the glyph of its last
    /// character. The advances and offsets are in font design units, and <see cref="FontMetrics.UnitsPerEm"/> of the
    /// font gives their scale. An empty text gives no glyphs.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction of <paramref name="options"/> is not a value of <see cref="TextDirection"/>.</exception>
    public static ShapedGlyph[] Shape(Font font, ReadOnlySpan<char> text, ShapingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        options ??= new();
        if (options.Direction is not (TextDirection.LeftToRight or TextDirection.RightToLeft or TextDirection.Auto))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Direction, "The direction is not left to right, right to left or automatic.");
        }

        if (text.IsEmpty)
        {
            return [];
        }

        ScriptClass? script = options.Script is ScriptClass.Common or ScriptClass.Inherited or ScriptClass.Unknown ? null : options.Script;
        TextDirection direction = options.Direction == TextDirection.Auto ? GetAutoDirection(text, script) : options.Direction;
        GlyphPositioningCollection positionings = FontRunShaper.Shape(font, text, CreateTextOptions(font, options, script), direction);

        ShapedGlyph[] glyphs = new ShapedGlyph[positionings.Count];
        for (int i = 0; i < glyphs.Length; i++)
        {
            GlyphShapingData data = positionings[i];
            GlyphMetrics metrics = positionings.GetMetrics(i);
            glyphs[i] = new(data.GlyphId, data.Cluster, metrics.AdvanceWidth, (int)MathF.Round(metrics.Offset.X), (int)MathF.Round(metrics.Offset.Y));
        }

        // The glyphs are in the order of the characters. A run from right to left draws the last character first.
        if (direction == TextDirection.RightToLeft)
        {
            Array.Reverse(glyphs);
        }

        return glyphs;
    }

    // The first strong character decides, as the Unicode Bidirectional Algorithm has it. A run with none, such as digits and
    // brackets, has the direction that HarfBuzz gives a buffer of its script, and runs from left to right without a script.
    private static TextDirection GetAutoDirection(ReadOnlySpan<char> text, ScriptClass? script)
    {
        BidiAlgorithm bidi = BidiAlgorithm.Instance.Value!;
        BidiData bidiData = new();
        bidiData.Init(text, (sbyte)TextDirection.Auto);
        bidi.Process(bidiData);
        if ((bidi.ResolvedParagraphEmbeddingLevel & 1) != 0)
        {
            return TextDirection.RightToLeft;
        }

        return script is ScriptClass named && IsRightToLeft(named) && !HasStrongCharacter(text)
            ? TextDirection.RightToLeft
            : TextDirection.LeftToRight;
    }

    private static bool HasStrongCharacter(ReadOnlySpan<char> text)
    {
        foreach (CodePoint codePoint in new SpanCodePointEnumerator(text))
        {
            if (CodePoint.GetBidiClass(codePoint).CharacterType is BidiCharacterType.LeftToRight or BidiCharacterType.RightToLeft or BidiCharacterType.ArabicLetter)
            {
                return true;
            }
        }

        return false;
    }

    // The scripts that HarfBuzz sets from right to left. It leaves the direction of Old Hungarian, Old Italic, Runic and
    // Tifinagh open, and a buffer of them runs from left to right.
    private static bool IsRightToLeft(ScriptClass script)
        => script is ScriptClass.Adlam or ScriptClass.Arabic or ScriptClass.Avestan or ScriptClass.Chorasmian or ScriptClass.Cypriot
            or ScriptClass.Elymaic or ScriptClass.Garay or ScriptClass.HanifiRohingya or ScriptClass.Hatran or ScriptClass.Hebrew
            or ScriptClass.ImperialAramaic or ScriptClass.InscriptionalPahlavi or ScriptClass.InscriptionalParthian
            or ScriptClass.Kharoshthi or ScriptClass.Lydian or ScriptClass.Mandaic or ScriptClass.Manichaean
            or ScriptClass.MendeKikakui or ScriptClass.MeroiticCursive or ScriptClass.MeroiticHieroglyphs or ScriptClass.Nabataean
            or ScriptClass.Nko or ScriptClass.OldNorthArabian or ScriptClass.OldSogdian or ScriptClass.OldSouthArabian
            or ScriptClass.OldTurkic or ScriptClass.OldUyghur or ScriptClass.Palmyrene or ScriptClass.Phoenician
            or ScriptClass.PsalterPahlavi or ScriptClass.Samaritan or ScriptClass.Sidetic or ScriptClass.Sogdian
            or ScriptClass.Syriac or ScriptClass.Thaana or ScriptClass.Yezidi;

    private static TextOptions CreateTextOptions(Font font, ShapingOptions options, ScriptClass? script)
    {
        // The last entry of a feature wins, and the features keep the order of their first entry.
        List<Tag> tags = [];
        Dictionary<Tag, bool> enabled = [];
        foreach (ShapingFeature feature in options.Features)
        {
            if (enabled.TryAdd(feature.Tag, feature.Enabled))
            {
                tags.Add(feature.Tag);
            }
            else
            {
                enabled[feature.Tag] = feature.Enabled;
            }
        }

        List<Tag> on = [];
        List<Tag> off = [];
        foreach (Tag tag in tags)
        {
            (enabled[tag] ? on : off).Add(tag);
        }

        return new TextOptions(font)
        {
            // Layers of a color glyph would each be a glyph of the run.
            ColorFontSupport = ColorFontSupport.None,
            KerningMode = off.Contains(Kern) ? KerningMode.None : KerningMode.Standard,
            FeatureTags = on,
            Script = script,
            Language = options.Language,
            DisabledFeatureTags = off,
        };
    }
}
