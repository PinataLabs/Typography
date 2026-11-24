// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.AdvancedTypographic.GSub;
using PinataLabs.Typography.Unicode;
using static PinataLabs.Typography.Tables.AdvancedTypographic.GSub.LookupType4Format1SubTable;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Shapes Arabic in a font whose GSUB table has none of the joining form features for Arabic, as HarfBuzz does. Each
/// letter takes the presentation form that the font has for its place in the word, and then the forms that make one
/// of the ligatures of <see cref="ArabicFallbackTable"/> ligate, as do a shadda and a vowel mark.
/// </summary>
internal static class ArabicFallbackShaper
{
    private const int FormCount = 4;

    // The features whose forms the table has, in the order of its forms.
    private static readonly Tag[] FormTags = [Tag.Parse("isol"), Tag.Parse("fina"), Tag.Parse("init"), Tag.Parse("medi")];

    private static readonly Tag[] JoiningFormTags =
        [Tag.Parse("isol"), Tag.Parse("fina"), Tag.Parse("fin2"), Tag.Parse("fin3"), Tag.Parse("medi"), Tag.Parse("med2"), Tag.Parse("init")];

    /// <summary>
    /// Shapes the run after the <c>rlig</c> feature of the font, when the font needs it.
    /// </summary>
    /// <param name="collection">The glyphs.</param>
    /// <param name="index">The index of the first glyph of the run.</param>
    /// <param name="count">The number of glyphs in the run.</param>
    public static void Apply(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitution || substitution.FontMetrics is not FontMetrics fontMetrics)
        {
            return;
        }

        GSubTable gsub = fontMetrics.GetGSubTable();
        if (gsub.TryGetFeatureLookups(JoiningFormTags, ScriptClass.Arabic, out _))
        {
            return;
        }

        // A form replaces the glyph of its letter, so a glyph that an earlier lookup gave the letter is replaced too.
        Dictionary<ushort, int> letters = GetLetters(fontMetrics);
        for (int i = index; i < index + count; i++)
        {
            GlyphShapingData data = substitution[i];
            int form = GetForm(data);
            if (form >= 0
                && letters.TryGetValue(data.GlyphId, out int letter)
                && TryGetForm(letter, form, out ushort formCodePoint)
                && TryGetGlyph(fontMetrics, formCodePoint, out ushort glyphId))
            {
                substitution.Replace(i, glyphId);
            }
        }

        LookupFeatures features = new([], 0, 0, true);
        count = Ligate(fontMetrics, gsub, substitution, features, index, count, CreateLetterLigatures(fontMetrics));
        Ligate(fontMetrics, gsub, substitution, features, index, count, CreateMarkLigatures(fontMetrics));
    }

    private static Dictionary<ushort, int> GetLetters(FontMetrics fontMetrics)
    {
        Dictionary<ushort, int> letters = [];
        for (int letter = ArabicFallbackTable.FirstLetter; letter <= ArabicFallbackTable.LastLetter; letter++)
        {
            if (TryGetGlyph(fontMetrics, letter, out ushort glyphId))
            {
                letters.TryAdd(glyphId, letter);
            }
        }

        return letters;
    }

    private static int GetForm(GlyphShapingData data)
    {
        foreach (TagEntry feature in data.Features)
        {
            if (feature.Enabled)
            {
                int form = Array.IndexOf(FormTags, feature.Tag);
                if (form >= 0)
                {
                    return form;
                }
            }
        }

        return -1;
    }

    private static bool TryGetForm(int letter, int form, out ushort codePoint)
    {
        codePoint = letter is >= ArabicFallbackTable.FirstLetter and <= ArabicFallbackTable.LastLetter
            ? ArabicFallbackTable.Forms[((letter - ArabicFallbackTable.FirstLetter) * FormCount) + form]
            : (ushort)0;

        return codePoint != 0;
    }

    private static bool TryGetGlyph(FontMetrics fontMetrics, int codePoint, out ushort glyphId)
        => fontMetrics.TryGetGlyphId(new CodePoint(codePoint), out glyphId) && glyphId != 0;

    private static int Ligate(
        FontMetrics fontMetrics,
        GSubTable gsub,
        GlyphSubstitutionCollection collection,
        LookupFeatures features,
        int index,
        int count,
        LookupType4Format1SubTable? ligatures)
    {
        if (ligatures is null)
        {
            return count;
        }

        for (int i = index; i < index + count; i++)
        {
            int before = collection.Count;
            ligatures.TrySubstitution(fontMetrics, gsub, collection, features, i, index + count - i);
            count += collection.Count - before;
        }

        return count;
    }

    // The letters of a ligature take the forms that join them: the first one joins only forward unless the ligature
    // joins backward, the last one only backward unless the ligature joins forward.
    private static LookupType4Format1SubTable? CreateLetterLigatures(FontMetrics fontMetrics)
    {
        const int isolated = 0, final = 1, initial = 2, medial = 3;
        List<(ushort First, LigatureTable Ligature)> ligatures = [];
        ReadOnlySpan<ushort> table = ArabicFallbackTable.LetterLigatures;
        for (int i = 0; i < table.Length; i += 3 + table[i + 2])
        {
            int form = table[i + 1];
            ReadOnlySpan<ushort> letters = table.Slice(i + 3, table[i + 2]);
            bool joinsBackward = form is final or medial;
            bool joinsForward = form is initial or medial;
            if (!TryGetGlyph(fontMetrics, table[i], out ushort ligatureGlyph))
            {
                continue;
            }

            ushort[] glyphs = new ushort[letters.Length];
            bool hasGlyphs = true;
            for (int j = 0; j < letters.Length && hasGlyphs; j++)
            {
                bool backward = j > 0 || joinsBackward;
                bool forward = j < letters.Length - 1 || joinsForward;
                int letterForm = (backward, forward) switch
                {
                    (true, true) => medial,
                    (true, false) => final,
                    (false, true) => initial,
                    _ => isolated,
                };

                hasGlyphs = TryGetForm(letters[j], letterForm, out ushort codePoint) && TryGetGlyph(fontMetrics, codePoint, out glyphs[j]);
            }

            if (hasGlyphs)
            {
                ligatures.Add((glyphs[0], new LigatureTable(ligatureGlyph, glyphs.AsSpan(1).ToArray())));
            }
        }

        return CreateLookup(ligatures, LookupFlags.IgnoreMarks);
    }

    private static LookupType4Format1SubTable? CreateMarkLigatures(FontMetrics fontMetrics)
    {
        List<(ushort First, LigatureTable Ligature)> ligatures = [];
        ReadOnlySpan<ushort> table = ArabicFallbackTable.MarkLigatures;
        for (int i = 0; i < table.Length; i += 2 + table[i + 1])
        {
            ReadOnlySpan<ushort> marks = table.Slice(i + 2, table[i + 1]);
            ushort[] glyphs = new ushort[marks.Length];
            bool hasGlyphs = TryGetGlyph(fontMetrics, table[i], out ushort ligatureGlyph);
            for (int j = 0; j < marks.Length && hasGlyphs; j++)
            {
                hasGlyphs = TryGetGlyph(fontMetrics, marks[j], out glyphs[j]);
            }

            if (hasGlyphs)
            {
                ligatures.Add((glyphs[0], new LigatureTable(ligatureGlyph, glyphs.AsSpan(1).ToArray())));
            }
        }

        return CreateLookup(ligatures, 0);
    }

    // The ligatures of one first glyph keep the order of the table, which has the longer ones first.
    private static LookupType4Format1SubTable? CreateLookup(List<(ushort First, LigatureTable Ligature)> ligatures, LookupFlags lookupFlags)
    {
        if (ligatures.Count == 0)
        {
            return null;
        }

        SortedDictionary<ushort, List<LigatureTable>> sets = [];
        foreach ((ushort first, LigatureTable ligature) in ligatures)
        {
            if (!sets.TryGetValue(first, out List<LigatureTable>? set))
            {
                set = [];
                sets.Add(first, set);
            }

            set.Add(ligature);
        }

        ushort[] coverage = new ushort[sets.Count];
        LigatureSetTable[] ligatureSets = new LigatureSetTable[sets.Count];
        int index = 0;
        foreach (KeyValuePair<ushort, List<LigatureTable>> set in sets)
        {
            coverage[index] = set.Key;
            ligatureSets[index++] = new LigatureSetTable(set.Value.ToArray());
        }

        return new LookupType4Format1SubTable(ligatureSets, new CoverageFormat1Table(coverage), lookupFlags);
    }
}
