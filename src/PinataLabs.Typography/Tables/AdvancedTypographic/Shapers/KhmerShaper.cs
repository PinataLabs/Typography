// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;
using UnicodeTrieGenerator.StateAutomation;
using static PinataLabs.Typography.Unicode.Resources.IndicShapingData;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Shaper for the Khmer script, as the Khmer shaper of HarfBuzz shapes it. The syllables come from the Khmer grammar
/// that the Unicode trie generator compiles. Before any lookup, a broken cluster gets a dotted circle as its base,
/// a COENG RO and a vowel sign that goes in front of the base move before it, and the basic features are enabled for
/// the glyphs after the base.
/// </summary>
internal sealed class KhmerShaper : DefaultShaper
{
    private const string ConsonantSyllable = "consonant_syllable";
    private const string BrokenCluster = "broken_cluster";
    private const string NonKhmerCluster = "non_khmer_cluster";

    private const int DottedCircle = 0x25CC;

    private static readonly StateMachine StateMachine =
        new(KhmerShapingData.StateTable, KhmerShapingData.AcceptingStates, KhmerShapingData.Tags);

    private static readonly Tag PrefTag = Tag.Parse("pref");
    private static readonly Tag BlwfTag = Tag.Parse("blwf");
    private static readonly Tag AbvfTag = Tag.Parse("abvf");
    private static readonly Tag PstfTag = Tag.Parse("pstf");
    private static readonly Tag CfarTag = Tag.Parse("cfar");
    private static readonly Tag PresTag = Tag.Parse("pres");
    private static readonly Tag AbvsTag = Tag.Parse("abvs");
    private static readonly Tag BlwsTag = Tag.Parse("blws");
    private static readonly Tag PstsTag = Tag.Parse("psts");

    // As in HarfBuzz, the basic and the presentation features keep both joiners.
    private static readonly IReadOnlyDictionary<Tag, ManualJoiners> ManualJoinerFeatures = WithCommonManualJoiners(
        ManualJoiners.Both, PrefTag, BlwfTag, AbvfTag, PstfTag, CfarTag, PresTag, AbvsTag, BlwsTag, PstsTag);

    // The two-part vowel signs that have no Unicode decomposition. The part in front of the base comes first.
    private static readonly Dictionary<int, int[]> Decompositions = new()
    {
        { 0x17BE, [0x17C1, 0x17BE] },
        { 0x17BF, [0x17C1, 0x17BF] },
        { 0x17C0, [0x17C1, 0x17C0] },
        { 0x17C4, [0x17C1, 0x17C4] },
        { 0x17C5, [0x17C1, 0x17C5] }
    };

    public KhmerShaper(ScriptClass script, TextOptions textOptions)
        : base(script, MarkZeroingMode.None, textOptions, ManualJoinerFeatures)
    {
    }

    protected override void PlanFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // HarfBuzz applies locl, ccmp and the basic features in one stage, so their lookups run in the order of
        // the lookup list. The syllables are found and reordered before the first lookup.
        this.AddFeature(collection, index, count, LoclTag, preAction: SetupSyllables);
        this.AddFeature(collection, index, count, CcmpTag);
        this.AddFeature(collection, index, count, PrefTag, false);
        this.AddFeature(collection, index, count, BlwfTag, false);
        this.AddFeature(collection, index, count, AbvfTag, false);
        this.AddFeature(collection, index, count, PstfTag, false);
        this.AddFeature(collection, index, count, CfarTag, false);
        this.EndStage();

        // The presentation features share one stage with the common features.
        this.AddFeature(collection, index, count, PresTag);
        this.AddFeature(collection, index, count, AbvsTag);
        this.AddFeature(collection, index, count, BlwsTag);
        this.AddFeature(collection, index, count, PstsTag);
    }

    protected override void AssignFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        // Split the two-part vowel signs.
        Span<ushort> ids = stackalloc ushort[2];
        for (int i = index + count - 1; i >= index; i--)
        {
            GlyphShapingData data = substitutionCollection[i];
            if (!Decompositions.TryGetValue(data.CodePoint.Value, out int[]? parts))
            {
                continue;
            }

            FontMetrics fontMetrics = substitutionCollection.GetFontMetrics(data);
            for (int j = 0; j < parts.Length; j++)
            {
                fontMetrics.TryGetGlyphId(new CodePoint(parts[j]), out ids[j]);
            }

            substitutionCollection.Replace(i, ids);
            for (int j = 0; j < parts.Length; j++)
            {
                substitutionCollection[i + j].CodePoint = new(parts[j]);
            }
        }
    }

    private static void SetupSyllables(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        Span<int> values = count <= 64 ? stackalloc int[count] : new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = UnicodeData.GetIndicShapingProperties((uint)substitutionCollection[index + i].CodePoint.Value) >> 8;
        }

        // A glyph that no syllable holds is a syllable of its own.
        int syllable = 0;
        int last = 0;
        foreach (StateMatch match in StateMachine.Match(values))
        {
            for (int i = last; i < match.StartIndex; i++)
            {
                SetInfo(substitutionCollection, index + i, values[i], NonKhmerCluster, ++syllable);
            }

            ++syllable;
            for (int i = match.StartIndex; i <= match.EndIndex; i++)
            {
                SetInfo(substitutionCollection, index + i, values[i], match.Tags[0], syllable);
            }

            last = match.EndIndex + 1;
        }

        for (int i = last; i < count; i++)
        {
            SetInfo(substitutionCollection, index + i, values[i], NonKhmerCluster, ++syllable);
        }

        int max = index + count;
        for (int start = index; start < max;)
        {
            int end = NextSyllable(substitutionCollection, start, max);
            string type = substitutionCollection[start].IndicShapingEngineInfo!.SyllableType;
            if (type == BrokenCluster && InsertDottedCircle(substitutionCollection, start))
            {
                end++;
                max++;
            }

            if (type is ConsonantSyllable or BrokenCluster)
            {
                ReorderSyllable(substitutionCollection, start, end);
            }

            start = end;
        }
    }

    private static void SetInfo(GlyphSubstitutionCollection collection, int index, int category, string type, int syllable)
        => collection[index].IndicShapingEngineInfo = new((Categories)(1 << category), Positions.End, type, syllable);

    // The dotted circle is the base of a broken cluster. It goes before the first glyph.
    private static bool InsertDottedCircle(GlyphSubstitutionCollection collection, int start)
    {
        GlyphShapingData first = collection[start];
        if (!collection.GetFontMetrics(first).TryGetGlyphId(new(DottedCircle), out ushort id))
        {
            return false;
        }

        IndicShapingEngineInfo info = first.IndicShapingEngineInfo!;
        collection.InsertGlyph(start, start, id, new(DottedCircle));
        collection[start].IndicShapingEngineInfo = new(Categories.Dotted_Circle, Positions.End, info.SyllableType, info.Syllable);
        return true;
    }

    private static void ReorderSyllable(GlyphSubstitutionCollection collection, int start, int end)
    {
        // The glyphs after the base can take their below, above and post-base forms.
        for (int i = start + 1; i < end; i++)
        {
            collection.EnableShapingFeature(i, BlwfTag);
            collection.EnableShapingFeature(i, AbvfTag);
            collection.EnableShapingFeature(i, PstfTag);
        }

        int coengs = 0;
        for (int i = start + 1; i < end; i++)
        {
            Categories category = CategoryOf(collection[i]);
            if (category == Categories.Coeng && coengs <= 2 && i + 1 < end)
            {
                coengs++;
                if (CategoryOf(collection[i + 1]) != Categories.Ra)
                {
                    continue;
                }

                // COENG RO takes its pre-base form in front of the base, and what follows it takes cfar.
                collection.EnableShapingFeature(i, PrefTag);
                collection.EnableShapingFeature(i + 1, PrefTag);
                collection.MoveGlyph(i, start);
                collection.MoveGlyph(i + 1, start + 1);
                for (int j = i + 2; j < end; j++)
                {
                    collection.EnableShapingFeature(j, CfarTag);
                }

                coengs = 2;
            }
            else if (category == Categories.VPre)
            {
                // The vowel sign in front of the base goes before everything else.
                collection.MoveGlyph(i, start);
            }
        }
    }

    private static Categories CategoryOf(GlyphShapingData data)
        => data.IndicShapingEngineInfo?.Category ?? Categories.X;

    private static int NextSyllable(GlyphSubstitutionCollection collection, int index, int max)
    {
        int syllable = collection[index].IndicShapingEngineInfo!.Syllable;
        int next = index + 1;
        while (next < max && collection[next].IndicShapingEngineInfo?.Syllable == syllable)
        {
            next++;
        }

        return next;
    }
}
