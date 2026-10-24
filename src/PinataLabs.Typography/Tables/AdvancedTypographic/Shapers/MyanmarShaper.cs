// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Shaper for the Myanmar script. It follows the Microsoft OpenType specification
/// "Developing OpenType Fonts for Myanmar Script":
/// <see href="https://learn.microsoft.com/en-us/typography/script-development/myanmar"/>.
/// Text is split into syllables, broken syllables get a dotted circle as their base, and each syllable
/// is reordered before the basic shaping features are applied.
/// </summary>
internal sealed class MyanmarShaper : DefaultShaper
{
    private const string ConsonantSyllable = "consonant_syllable";
    private const string BrokenCluster = "broken_cluster";
    private const string NonMyanmarCluster = "non_myanmar_cluster";

    private const int DottedCircle = 0x25CC;

    private static readonly Tag RphfTag = Tag.Parse("rphf");
    private static readonly Tag PrefTag = Tag.Parse("pref");
    private static readonly Tag BlwfTag = Tag.Parse("blwf");
    private static readonly Tag PstfTag = Tag.Parse("pstf");
    private static readonly Tag PresTag = Tag.Parse("pres");
    private static readonly Tag AbvsTag = Tag.Parse("abvs");
    private static readonly Tag BlwsTag = Tag.Parse("blws");
    private static readonly Tag PstsTag = Tag.Parse("psts");
    private static readonly Tag DistTag = Tag.Parse("dist");
    private static readonly Tag AbvmTag = Tag.Parse("abvm");
    private static readonly Tag BlwmTag = Tag.Parse("blwm");

    public MyanmarShaper(ScriptClass script, TextOptions textOptions)
       : base(script, MarkZeroingMode.PreGPos, textOptions)
    {
    }

    /// <summary>
    /// The shaping categories of Myanmar characters.
    /// </summary>
    internal enum Categories
    {
        /// <summary>Anything that is not part of a Myanmar syllable.</summary>
        Other,

        /// <summary>Consonant.</summary>
        C,

        /// <summary>Consonants that can form a kinzi: NGA, RA and MON NGA.</summary>
        Ra,

        /// <summary>Independent vowel.</summary>
        IV,

        /// <summary>Dotted circle, the base of a broken cluster.</summary>
        GB,

        /// <summary>Virama (invisible stacker).</summary>
        H,

        /// <summary>Asat (killer).</summary>
        As,

        /// <summary>Medial YA.</summary>
        MY,

        /// <summary>Medial RA.</summary>
        MR,

        /// <summary>Medial WA.</summary>
        MW,

        /// <summary>Medial HA.</summary>
        MH,

        /// <summary>Pre-base vowel sign.</summary>
        VPre,

        /// <summary>Above-base vowel sign.</summary>
        VAbv,

        /// <summary>Below-base vowel sign.</summary>
        VBlw,

        /// <summary>Post-base vowel sign.</summary>
        VPst,

        /// <summary>Anusvara.</summary>
        A,

        /// <summary>Dot below.</summary>
        DB,

        /// <summary>Tone mark.</summary>
        PT,

        /// <summary>Visarga.</summary>
        SM,

        /// <summary>Joiner or non-joiner.</summary>
        J,

        /// <summary>Variation selector.</summary>
        VS,
    }

    /// <summary>
    /// The position of a glyph within a syllable. The order of the members is the order of
    /// the glyphs after the initial reordering.
    /// </summary>
    internal enum Positions
    {
        Start,
        PreM,
        PreC,
        BaseC,
        AfterMain,
        AboveC,
        BeforeSub,
        BelowC,
        AfterSub,
        BeforePost,
        PostC,
        AfterPost,
        FinalC,
        End,
    }

    /// <inheritdoc/>
    protected override void PlanFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // Localised forms. The syllables are found before the first feature is applied.
        this.AddFeature(collection, index, count, LoclTag, preAction: SetupSyllables);
        this.AddFeature(collection, index, count, CcmpTag);

        // Basic shaping. The syllables are reordered once the glyph sequence is final
        // and before the first positional form is chosen.
        // Only the kinzi takes part in rphf, which is enabled for it when the syllables are found.
        this.AddFeature(collection, index, count, RphfTag, false, preAction: Reorder);
        this.AddFeature(collection, index, count, PrefTag);
        this.AddFeature(collection, index, count, BlwfTag);
        this.AddFeature(collection, index, count, PstfTag);

        // Presentation forms.
        this.AddFeature(collection, index, count, PresTag);
        this.AddFeature(collection, index, count, AbvsTag);
        this.AddFeature(collection, index, count, BlwsTag);
        this.AddFeature(collection, index, count, PstsTag);

        // Positioning.
        this.AddFeature(collection, index, count, KernTag);
        this.AddFeature(collection, index, count, DistTag);
        this.AddFeature(collection, index, count, AbvmTag);
        this.AddFeature(collection, index, count, BlwmTag);
        this.AddFeature(collection, index, count, MarkTag);
        this.AddFeature(collection, index, count, MkmkTag);
    }

    private static void SetupSyllables(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        Categories[] categories = new Categories[count];
        for (int i = 0; i < count; i++)
        {
            categories[i] = GetCategory(substitutionCollection[index + i].CodePoint.Value);
        }

        // Find the syllables first. Inserting dotted circles changes the indices, so we then go backwards.
        List<(int Start, int End, string Type)> syllables = [];
        for (int start = 0; start < count;)
        {
            int end = MatchSyllable(categories, start, out string type);
            syllables.Add((start, end, type));
            start = end;
        }

        for (int s = syllables.Count - 1; s >= 0; s--)
        {
            (int start, int end, string type) = syllables[s];
            for (int i = start; i < end; i++)
            {
                substitutionCollection[index + i].MyanmarShapingEngineInfo = new(categories[i], Positions.Start, type, s + 1);
            }

            // The kinzi is the only part that takes part in rphf.
            if (HasKinzi(categories, start, end))
            {
                for (int i = start; i < start + 3; i++)
                {
                    substitutionCollection.EnableShapingFeature(index + i, RphfTag);
                }
            }

            if (type == BrokenCluster)
            {
                GlyphShapingData first = substitutionCollection[index + start];
                FontMetrics fontMetrics = substitutionCollection.GetFontMetrics(first);
                if (fontMetrics.TryGetGlyphId(new(DottedCircle), out ushort id))
                {
                    // The dotted circle is the base, so it goes after any kinzi.
                    int at = index + start + (HasKinzi(categories, start, end) ? 3 : 0);
                    substitutionCollection.InsertGlyph(at, id, new(DottedCircle));
                    substitutionCollection[at].MyanmarShapingEngineInfo = new(Categories.GB, Positions.Start, type, s + 1);
                }
            }
        }
    }

    private static void Reorder(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        int max = index + count;
        int start = index;
        while (start < max)
        {
            int end = NextSyllable(substitutionCollection, start, max);
            string? type = substitutionCollection[start].MyanmarShapingEngineInfo?.SyllableType;
            if (type is ConsonantSyllable or BrokenCluster)
            {
                ReorderSyllable(substitutionCollection, start, end);
            }

            start = end;
        }
    }

    private static void ReorderSyllable(GlyphSubstitutionCollection collection, int start, int end)
    {
        // The kinzi is moved to after the base. Its glyphs are the first three, if present.
        int limit = start;
        bool hasKinzi = end - start >= 3
            && CategoryOf(collection[start]) == Categories.Ra
            && CategoryOf(collection[start + 1]) == Categories.As
            && CategoryOf(collection[start + 2]) == Categories.H;
        if (hasKinzi)
        {
            limit += 3;
        }

        // The base is the first consonant after the kinzi.
        int baseIndex = end;
        for (int i = limit; i < end; i++)
        {
            if (IsBase(CategoryOf(collection[i])))
            {
                baseIndex = i;
                break;
            }
        }

        int current = start;
        for (; current < limit; current++)
        {
            SetPosition(collection[current], Positions.AfterMain);
        }

        for (; current < baseIndex; current++)
        {
            SetPosition(collection[current], Positions.PreC);
        }

        if (current < end)
        {
            SetPosition(collection[current], Positions.BaseC);
            current++;
        }

        Positions position = Positions.AfterMain;
        for (; current < end; current++)
        {
            GlyphShapingData data = collection[current];
            Categories category = CategoryOf(data);

            if (category == Categories.MR)
            {
                // Medial RA goes before the base.
                SetPosition(data, Positions.PreC);
            }
            else if (category == Categories.VPre)
            {
                // Pre-base vowels go before everything else, including medial RA.
                SetPosition(data, Positions.PreM);
            }
            else if (category == Categories.VS)
            {
                // A variation selector stays with the character before it.
                Positions previous = collection[current - 1].MyanmarShapingEngineInfo?.Position ?? position;
                SetPosition(data, previous);
            }
            else if (position == Positions.AfterMain && category == Categories.VBlw)
            {
                position = Positions.BelowC;
                SetPosition(data, position);
            }
            else if (position == Positions.BelowC && category == Categories.A)
            {
                SetPosition(data, Positions.BeforeSub);
            }
            else if (position == Positions.BelowC && category == Categories.VBlw)
            {
                SetPosition(data, position);
            }
            else if (position == Positions.BelowC)
            {
                position = Positions.AfterSub;
                SetPosition(data, position);
            }
            else
            {
                SetPosition(data, position);
            }
        }

        collection.Sort(
            start,
            end,
            static (a, b) => (a.MyanmarShapingEngineInfo?.Position ?? Positions.Start).CompareTo(b.MyanmarShapingEngineInfo?.Position ?? Positions.Start));
    }

    private static void SetPosition(GlyphShapingData data, Positions position)
    {
        if (data.MyanmarShapingEngineInfo is { } info)
        {
            info.Position = position;
        }
    }

    private static Categories CategoryOf(GlyphShapingData data)
        => data.MyanmarShapingEngineInfo?.Category ?? Categories.Other;

    private static bool IsBase(Categories category)
        => category is Categories.C or Categories.Ra or Categories.IV or Categories.GB;

    private static bool HasKinzi(Categories[] categories, int start, int end)
        => end - start >= 3
        && categories[start] == Categories.Ra
        && categories[start + 1] == Categories.As
        && categories[start + 2] == Categories.H;

    /// <summary>
    /// Maps a code point to its Myanmar shaping category. The Indic syllabic and positional
    /// categories supply most of the mapping; the medials and a few signs need to be told apart by code point.
    /// </summary>
    private static Categories GetCategory(int codePoint)
    {
        switch (codePoint)
        {
            case 0x200C:
            case 0x200D:
                return Categories.J;
            case DottedCircle:
                return Categories.GB;
            case 0x1004: // NGA
            case 0x101B: // RA
            case 0x105A: // MON NGA
                return Categories.Ra;
            case 0x1036:
                return Categories.A;
            case 0x1037:
                return Categories.DB;
            case 0x1038:
                return Categories.SM;
            case 0x1039:
                return Categories.H;
            case 0x103A:
                return Categories.As;
            case 0x103B:
                return Categories.MY;
            case 0x103C:
                return Categories.MR;
            case 0x103D:
                return Categories.MW;
            case 0x103E:
                return Categories.MH;
        }

        if (codePoint is >= 0xFE00 and <= 0xFE0F)
        {
            return Categories.VS;
        }

        // Only the Myanmar block and its extensions are syllable material.
        if (codePoint is not ((>= 0x1000 and <= 0x109F) or (>= 0xA9E0 and <= 0xA9FF) or (>= 0xAA60 and <= 0xAA7F)))
        {
            return Categories.Other;
        }

        uint cp = (uint)codePoint;
        switch (UnicodeData.GetIndicSyllabicCategory(cp))
        {
            case IndicSyllabicCategory.Consonant:
                return Categories.C;
            case IndicSyllabicCategory.VowelIndependent:
                return Categories.IV;
            case IndicSyllabicCategory.ConsonantMedial:
                return Categories.MW;
            case IndicSyllabicCategory.Bindu:
                return Categories.A;
            case IndicSyllabicCategory.Visarga:
                return Categories.SM;
            case IndicSyllabicCategory.ToneMark:
                return Categories.PT;
            case IndicSyllabicCategory.VowelDependent:
                return UnicodeData.GetIndicPositionalCategory(cp) switch
                {
                    IndicPositionalCategory.Left => Categories.VPre,
                    IndicPositionalCategory.Right => Categories.VPst,
                    IndicPositionalCategory.Bottom => Categories.VBlw,
                    _ => Categories.VAbv,
                };
            default:
                return Categories.Other;
        }
    }

    /// <summary>
    /// Finds the end of the syllable that starts at <paramref name="start"/>.
    /// The grammar is the Myanmar syllable grammar from the Microsoft specification:
    /// <code>
    /// consonant_syllable = kinzi? (C | Ra | IV | GB) VS? syllable_tail
    /// broken_cluster     = kinzi? VS? syllable_tail
    /// syllable_tail      = (H (C | Ra | IV) VS?)* (H | complex_tail)
    /// complex_tail       = As* medial vowel_group post_vowel_group* tone_group* SM* J?
    /// </code>
    /// </summary>
    private static int MatchSyllable(Categories[] c, int start, out string type)
    {
        int p = HasKinzi(c, start, c.Length) ? start + 3 : start;
        if (p < c.Length && IsBase(c[p]))
        {
            p++;
            if (p < c.Length && c[p] == Categories.VS)
            {
                p++;
            }

            type = ConsonantSyllable;
            return MatchTail(c, p);
        }

        if (p < c.Length && c[p] == Categories.VS)
        {
            p++;
        }

        int end = MatchTail(c, p);
        if (end > start)
        {
            type = BrokenCluster;
            return end;
        }

        type = NonMyanmarCluster;
        return start + 1;
    }

    private static int MatchTail(Categories[] c, int p)
    {
        // (H (C | Ra | IV) VS?)*
        while (p + 1 < c.Length && c[p] == Categories.H && c[p + 1] is Categories.C or Categories.Ra or Categories.IV)
        {
            p += 2;
            if (p < c.Length && c[p] == Categories.VS)
            {
                p++;
            }
        }

        if (p < c.Length && c[p] == Categories.H)
        {
            return p + 1;
        }

        return MatchComplexTail(c, p);
    }

    private static int MatchComplexTail(Categories[] c, int p)
    {
        p = Skip(c, p, Categories.As);

        // Medials: MY? As? MR? ((MW MH? | MH) As?)? As?
        p = SkipOne(c, p, Categories.MY);
        p = SkipOne(c, p, Categories.As);
        p = SkipOne(c, p, Categories.MR);
        if (p < c.Length && c[p] == Categories.MW)
        {
            p = SkipOne(c, p + 1, Categories.MH);
            p = SkipOne(c, p, Categories.As);
        }
        else if (p < c.Length && c[p] == Categories.MH)
        {
            p = SkipOne(c, p + 1, Categories.As);
        }

        p = SkipOne(c, p, Categories.As);

        // Main vowel group: (VPre VS?)* VAbv* VBlw* A* (DB As?)?
        while (p < c.Length && c[p] == Categories.VPre)
        {
            p = SkipOne(c, p + 1, Categories.VS);
        }

        p = Skip(c, p, Categories.VAbv);
        p = Skip(c, p, Categories.VBlw);
        p = Skip(c, p, Categories.A);
        p = SkipDotBelow(c, p);

        // Post vowel groups: (VPst MH? As* VAbv* A* (DB As?)?)*
        while (p < c.Length && c[p] == Categories.VPst)
        {
            p = SkipOne(c, p + 1, Categories.MH);
            p = Skip(c, p, Categories.As);
            p = Skip(c, p, Categories.VAbv);
            p = Skip(c, p, Categories.A);
            p = SkipDotBelow(c, p);
        }

        // Tone groups: (PT A* DB? As?)*
        while (p < c.Length && c[p] == Categories.PT)
        {
            p = Skip(c, p + 1, Categories.A);
            p = SkipOne(c, p, Categories.DB);
            p = SkipOne(c, p, Categories.As);
        }

        p = Skip(c, p, Categories.SM);
        return SkipOne(c, p, Categories.J);
    }

    private static int SkipDotBelow(Categories[] c, int p)
    {
        if (p < c.Length && c[p] == Categories.DB)
        {
            return SkipOne(c, p + 1, Categories.As);
        }

        return p;
    }

    private static int SkipOne(Categories[] c, int p, Categories category)
        => p < c.Length && c[p] == category ? p + 1 : p;

    private static int Skip(Categories[] c, int p, Categories category)
    {
        while (p < c.Length && c[p] == category)
        {
            p++;
        }

        return p;
    }

    private static int NextSyllable(IGlyphShapingCollection collection, int index, int max)
    {
        int? syllable = collection[index].MyanmarShapingEngineInfo?.Syllable;
        while (++index < max)
        {
            if (collection[index].MyanmarShapingEngineInfo?.Syllable != syllable)
            {
                break;
            }
        }

        return index;
    }
}
