// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.AdvancedTypographic.GSub;
using PinataLabs.Typography.Unicode;
using static PinataLabs.Typography.Tables.AdvancedTypographic.GSub.LookupType4Format1SubTable;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// What <see cref="ArabicFallbackShaper"/> needs from one font: the glyphs of the presentation forms of
/// <see cref="ArabicFallbackTable"/> that the font has, and the ligatures of those forms. It depends on the font
/// alone and does not change, so a font builds it once and every run of Arabic text shares it. Whether a run needs it
/// depends on the language of the run and its features, which <see cref="ArabicFallbackShaper"/> decides.
/// </summary>
internal sealed class ArabicFallbackPlan
{
    private const int FormCount = 4;
    private const int Isolated = 0;
    private const int Final = 1;
    private const int Initial = 2;
    private const int Medial = 3;

    // The index in formGlyphs of the first form of a letter, by the glyph of the letter.
    private readonly Dictionary<ushort, int> letters;

    // The glyph of the isolated, final, initial and medial form of each letter, or 0 when the font has none.
    private readonly ushort[] formGlyphs;

    private ArabicFallbackPlan(FontMetrics fontMetrics)
    {
        ReadOnlySpan<ushort> forms = ArabicFallbackTable.Forms;
        this.formGlyphs = new ushort[forms.Length];
        for (int i = 0; i < forms.Length; i++)
        {
            if (forms[i] != 0)
            {
                TryGetGlyph(fontMetrics, forms[i], out this.formGlyphs[i]);
            }
        }

        this.letters = [];
        for (int letter = ArabicFallbackTable.FirstLetter; letter <= ArabicFallbackTable.LastLetter; letter++)
        {
            if (TryGetGlyph(fontMetrics, letter, out ushort glyphId))
            {
                this.letters.TryAdd(glyphId, (letter - ArabicFallbackTable.FirstLetter) * FormCount);
            }
        }

        this.LetterLigatures = this.CreateLetterLigatures(fontMetrics);
        this.MarkLigatures = CreateMarkLigatures(fontMetrics);
    }

    /// <summary>
    /// Gets the ligatures of two or three letters, or <see langword="null"/> when the font has none of them.
    /// </summary>
    public LookupType4Format1SubTable? LetterLigatures { get; }

    /// <summary>
    /// Gets the ligatures of a shadda and a vowel mark, or <see langword="null"/> when the font has none of them.
    /// </summary>
    public LookupType4Format1SubTable? MarkLigatures { get; }

    /// <summary>
    /// Builds the plan of a font.
    /// </summary>
    /// <param name="fontMetrics">The font.</param>
    /// <returns>The plan.</returns>
    public static ArabicFallbackPlan Create(FontMetrics fontMetrics) => new(fontMetrics);

    /// <summary>
    /// Gets the glyph of a form of the letter that a glyph stands for.
    /// </summary>
    /// <param name="glyphId">The glyph of the letter.</param>
    /// <param name="form">The form: 0 for isolated, 1 for final, 2 for initial and 3 for medial.</param>
    /// <param name="formGlyphId">The glyph of the form.</param>
    /// <returns><see langword="true"/> if the font has the form.</returns>
    public bool TryGetFormGlyph(ushort glyphId, int form, out ushort formGlyphId)
    {
        if (this.letters.TryGetValue(glyphId, out int first))
        {
            formGlyphId = this.formGlyphs[first + form];
            return formGlyphId != 0;
        }

        formGlyphId = 0;
        return false;
    }

    private static bool TryGetGlyph(FontMetrics fontMetrics, int codePoint, out ushort glyphId)
        => fontMetrics.TryGetGlyphId(new CodePoint(codePoint), out glyphId) && glyphId != 0;

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

    // The letters of a ligature take the forms that join them: the first one joins only forward unless the ligature
    // joins backward, the last one only backward unless the ligature joins forward.
    private LookupType4Format1SubTable? CreateLetterLigatures(FontMetrics fontMetrics)
    {
        List<(ushort First, LigatureTable Ligature)> ligatures = [];
        ReadOnlySpan<ushort> table = ArabicFallbackTable.LetterLigatures;
        for (int i = 0; i < table.Length; i += 3 + table[i + 2])
        {
            int form = table[i + 1];
            ReadOnlySpan<ushort> ligatureLetters = table.Slice(i + 3, table[i + 2]);
            bool joinsBackward = form is Final or Medial;
            bool joinsForward = form is Initial or Medial;
            if (!TryGetGlyph(fontMetrics, table[i], out ushort ligatureGlyph))
            {
                continue;
            }

            ushort[] glyphs = new ushort[ligatureLetters.Length];
            bool hasGlyphs = true;
            for (int j = 0; j < ligatureLetters.Length && hasGlyphs; j++)
            {
                bool backward = j > 0 || joinsBackward;
                bool forward = j < ligatureLetters.Length - 1 || joinsForward;
                int letterForm = (backward, forward) switch
                {
                    (true, true) => Medial,
                    (true, false) => Final,
                    (false, true) => Initial,
                    _ => Isolated,
                };

                glyphs[j] = this.GetFormGlyph(ligatureLetters[j], letterForm);
                hasGlyphs = glyphs[j] != 0;
            }

            if (hasGlyphs)
            {
                ligatures.Add((glyphs[0], new LigatureTable(ligatureGlyph, glyphs.AsSpan(1).ToArray())));
            }
        }

        return CreateLookup(ligatures, LookupFlags.IgnoreMarks);
    }

    private ushort GetFormGlyph(int letter, int form)
        => letter is >= ArabicFallbackTable.FirstLetter and <= ArabicFallbackTable.LastLetter
            ? this.formGlyphs[((letter - ArabicFallbackTable.FirstLetter) * FormCount) + form]
            : (ushort)0;
}
