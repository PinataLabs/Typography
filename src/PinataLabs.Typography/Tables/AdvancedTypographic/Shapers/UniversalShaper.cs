// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;
using UnicodeTrieGenerator.StateAutomation;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// This shaper is an implementation of the Universal Shaping Engine, which
/// uses Unicode data to shape a number of scripts without a dedicated shaping engine.
/// <see href="https://www.microsoft.com/typography/OpenTypeDev/USE/intro.htm"/>.
/// </summary>
internal sealed class UniversalShaper : DefaultShaper
{
    private static readonly StateMachine StateMachine =
        new(UniversalShapingData.StateTable, UniversalShapingData.AcceptingStates, UniversalShapingData.Tags);

    private static readonly Tag RphfTag = Tag.Parse("rphf");
    private static readonly Tag NuktTag = Tag.Parse("nukt");
    private static readonly Tag AkhnTag = Tag.Parse("akhn");
    private static readonly Tag PrefTag = Tag.Parse("pref");

    private static readonly Tag RkrfTag = Tag.Parse("rkrf");
    private static readonly Tag AbvfTag = Tag.Parse("abvf");
    private static readonly Tag BlwfTag = Tag.Parse("blwf");
    private static readonly Tag HalfTag = Tag.Parse("half");
    private static readonly Tag PstfTag = Tag.Parse("pstf");
    private static readonly Tag VatuTag = Tag.Parse("vatu");
    private static readonly Tag CjctTag = Tag.Parse("cjct");

    private static readonly Tag IsolTag = Tag.Parse("isol");
    private static readonly Tag InitTag = Tag.Parse("init");
    private static readonly Tag MediTag = Tag.Parse("medi");
    private static readonly Tag FinaTag = Tag.Parse("fina");

    private static readonly Tag AbvsTag = Tag.Parse("abvs");
    private static readonly Tag BlwsTag = Tag.Parse("blws");
    private static readonly Tag HalnTag = Tag.Parse("haln");
    private static readonly Tag PresTag = Tag.Parse("pres");
    private static readonly Tag PstsTag = Tag.Parse("psts");

    private const int DottedCircle = 0x25cc;

    private static readonly int OtherCategory = Array.IndexOf(UniversalShapingData.Categories, "O");

    private static readonly int IndependentCategory = Array.IndexOf(UniversalShapingData.Categories, "IND");

    private static readonly int SymbolCategory = Array.IndexOf(UniversalShapingData.Categories, "S");

    private static readonly int GenericBaseCategory = Array.IndexOf(UniversalShapingData.Categories, "GB");

    private static readonly int HalantCategory = Array.IndexOf(UniversalShapingData.Categories, "H");

    private static readonly int RephaCategory = Array.IndexOf(UniversalShapingData.Categories, "R");

    private readonly bool joinsLetters;

    /// <summary>
    /// Initializes a new instance of the <see cref="UniversalShaper"/> class.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="joinsLetters">Whether the letters of the script join as Arabic letters do.</param>
    /// <param name="textOptions">The text options.</param>
    public UniversalShaper(ScriptClass script, bool joinsLetters, TextOptions textOptions)
       : base(script, MarkZeroingMode.PreGPos, textOptions)
        => this.joinsLetters = joinsLetters;

    /// <inheritdoc/>
    protected override void PlanFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // Default glyph pre-processing group
        this.AddFeature(collection, index, count, LoclTag, preAction: this.SetupSyllables);
        this.AddFeature(collection, index, count, CcmpTag);
        this.AddFeature(collection, index, count, NuktTag);
        this.AddFeature(collection, index, count, AkhnTag);

        // Reordering group
        this.AddFeature(collection, index, count, RphfTag, true, ClearSubstitutionFlags, RecordRhpf);
        this.AddFeature(collection, index, count, PrefTag, true, ClearSubstitutionFlags, RecordPref);

        // Orthographic unit shaping group
        this.AddFeature(collection, index, count, RkrfTag);
        this.AddFeature(collection, index, count, AbvfTag);
        this.AddFeature(collection, index, count, BlwfTag);
        this.AddFeature(collection, index, count, HalfTag);
        this.AddFeature(collection, index, count, PstfTag);
        this.AddFeature(collection, index, count, VatuTag);
        this.AddFeature(collection, index, count, CjctTag, postAction: Reorder);

        // As in HarfBuzz, the joining forms are a stage of their own, after the reordering.
        this.AddFeature(collection, index, count, IsolTag, false);
        this.AddFeature(collection, index, count, InitTag, false);
        this.AddFeature(collection, index, count, MediTag, false);
        this.AddFeature(collection, index, count, FinaTag, false);
        this.EndStage();

        // Standard topographic presentation and positional feature application
        this.AddFeature(collection, index, count, AbvsTag);
        this.AddFeature(collection, index, count, BlwsTag);
        this.AddFeature(collection, index, count, HalnTag);
        this.AddFeature(collection, index, count, PresTag);
        this.AddFeature(collection, index, count, PstsTag);
        this.AddHorizontalFeature(collection, index, count, DistTag);
        this.AddFeature(collection, index, count, AbvmTag);
        this.AddFeature(collection, index, count, BlwmTag);
    }

    /// <inheritdoc/>
    protected override void AssignFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        if (this.joinsLetters)
        {
            ArabicShaper.AssignJoiningForms(collection, index, count);
        }

        DecomposeSplitVowels(collection, index, count);
    }

    private static void DecomposeSplitVowels(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        Span<ushort> buffer = stackalloc ushort[16];
        int end = index + count;
        for (int i = end - 1; i >= 0; i--)
        {
            GlyphShapingData data = substitutionCollection[i];
            FontMetrics fontMetrics = substitutionCollection.GetFontMetrics(data);
            if (UniversalShapingData.Decompositions.TryGetValue(data.CodePoint.Value, out int[]? decompositions) && decompositions != null)
            {
                Span<ushort> ids = buffer.Slice(0, decompositions.Length);
                bool hasParts = true;
                for (int j = 0; j < decompositions.Length; j++)
                {
                    hasParts &= fontMetrics.TryGetGlyphId(new CodePoint(decompositions[j]), out ushort id);
                    ids[j] = id;
                }

                // As in HarfBuzz, a vowel sign stays whole when the font lacks one of its parts.
                if (!hasParts)
                {
                    continue;
                }

                substitutionCollection.Replace(i, ids);
                for (int j = 0; j < decompositions.Length; j++)
                {
                    substitutionCollection[i + j].CodePoint = new(decompositions[j]);
                }
            }
        }
    }

    private void SetupSyllables(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        FindSyllables(substitutionCollection, index, count);
        if (!this.joinsLetters)
        {
            AssignSyllableForms(substitutionCollection, index, count);
        }
    }

    private static void FindSyllables(GlyphSubstitutionCollection substitutionCollection, int index, int count)
    {
        Span<int> values = count <= 64 ? stackalloc int[count] : new int[count];
        for (int i = index; i < index + count; i++)
        {
            values[i - index] = GetCategory(substitutionCollection[i].CodePoint);
        }

        // A symbol is a base too. It stays a symbol before a symbol modifier, which only the symbol cluster takes.
        for (int i = 0; i < count; i++)
        {
            if (values[i] == SymbolCategory
                && (i + 1 == count || UniversalShapingData.Categories[values[i + 1]] is not "SMAbv" and not "SMBlw"))
            {
                values[i] = GenericBaseCategory;
            }
        }

        int syllable = 0;
        int start = 0;
        while (start < count)
        {
            // As in HarfBuzz, a repha does not go with a character that is a base only because it is not of a
            // script. The repha is a broken cluster, and the character starts the next cluster.
            int end = start + 1;
            while (end < count
                && !(values[end - 1] == RephaCategory && IsBaseOutsideScripts(substitutionCollection[end + index].CodePoint)))
            {
                end++;
            }

            foreach (StateMatch match in StateMachine.Match(values[start..end]))
            {
                ++syllable;
                int matchStart = match.StartIndex + start;
                int matchEnd = match.EndIndex + start;

                // Create shaper info
                for (int i = matchStart; i <= matchEnd; i++)
                {
                    GlyphShapingData data = substitutionCollection[i + index];
                    string category = UniversalShapingData.Categories[values[i]];

                    data.UniversalShapingEngineInfo = new(category, match.Tags[0], syllable);
                }

                // Assign rphf feature
                int limit = values[matchStart] == RephaCategory
                    ? 1
                    : Math.Min(3, matchEnd - matchStart);

                for (int i = matchStart; i < matchStart + limit; i++)
                {
                    substitutionCollection.AddShapingFeature(i + index, new TagEntry(RcltTag, true));
                }
            }

            start = end;
        }

        // The state machine has no cluster for a halant that no base comes before or after.
        // As in HarfBuzz, it is a broken cluster.
        for (int i = 0; i < count; i++)
        {
            GlyphShapingData data = substitutionCollection[i + index];
            if (values[i] == HalantCategory && data.UniversalShapingEngineInfo is null)
            {
                data.UniversalShapingEngineInfo = new(UniversalShapingData.Categories[HalantCategory], "broken_cluster", ++syllable);
            }
        }
    }

    /// <summary>
    /// Enables isol, init, medi or fina on each syllable of a script whose letters do not join, as HarfBuzz does: a
    /// run of syllables takes init on the first, medi on the middle ones and fina on the last, and a syllable alone
    /// takes isol. A character outside every syllable ends the run.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the first glyph.</param>
    /// <param name="count">The number of glyphs.</param>
    private static void AssignSyllableForms(GlyphSubstitutionCollection collection, int index, int count)
    {
        int end = index + count;
        int lastStart = index;
        Tag? lastForm = null;
        for (int start = index; start < end;)
        {
            UniversalShapingEngineInfo? info = collection[start].UniversalShapingEngineInfo;
            int syllableEnd = start + 1;
            while (info is not null && syllableEnd < end && collection[syllableEnd].UniversalShapingEngineInfo?.Syllable == info.Syllable)
            {
                syllableEnd++;
            }

            if (info is null || info.SyllableType is "independent_cluster" or "hieroglyph_cluster" or "non_cluster")
            {
                lastForm = null;
            }
            else
            {
                bool joins = lastForm == FinaTag || lastForm == IsolTag;
                if (joins)
                {
                    lastForm = lastForm == FinaTag ? MediTag : InitTag;
                    SetForm(collection, lastStart, start, lastForm.Value);
                }

                lastForm = joins ? FinaTag : IsolTag;
                SetForm(collection, start, syllableEnd, lastForm.Value);
            }

            lastStart = start;
            start = syllableEnd;
        }
    }

    private static void SetForm(GlyphSubstitutionCollection collection, int start, int end, Tag form)
    {
        for (int i = start; i < end; i++)
        {
            collection.DisableShapingFeature(i, IsolTag);
            collection.DisableShapingFeature(i, InitTag);
            collection.DisableShapingFeature(i, MediTag);
            collection.DisableShapingFeature(i, FinaTag);
            collection.EnableShapingFeature(i, form);
        }
    }

    // As in HarfBuzz, a character that is not a letter or a sign of a script is a base, so a sign after a space,
    // a digit or a full stop is not a broken cluster. HarfBuzz 15.0.0 leaves out the characters that it does not
    // draw, and six right brackets.
    private static int GetCategory(CodePoint codePoint)
        => IsBaseOutsideScripts(codePoint)
            ? GenericBaseCategory
            : UnicodeData.GetUniversalShapingSymbolCount((uint)codePoint.Value);

    private static bool IsBaseOutsideScripts(CodePoint codePoint)
    {
        int category = UnicodeData.GetUniversalShapingSymbolCount((uint)codePoint.Value);
        return (category == OtherCategory || category == IndependentCategory)
            && !UnicodeUtility.IsDefaultIgnorableCodePoint((uint)codePoint.Value)
            && codePoint.Value is not (0x005D or 0x007D or 0x27E7 or 0x27E9 or 0x2E23 or 0x2E25);
    }

    private static void ClearSubstitutionFlags(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        int end = index + count;
        for (int i = index; i < end; i++)
        {
            GlyphShapingData data = substitutionCollection[i];
            data.IsSubstituted = false;
        }
    }

    private static void RecordRhpf(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        int end = index + count;
        for (int i = index; i < end; i++)
        {
            GlyphShapingData data = substitutionCollection[i];
            if (data.IsSubstituted && data.Features.Any(x => x.Tag == RphfTag))
            {
                // Mark a substituted repha.
                if (data.UniversalShapingEngineInfo != null)
                {
                    data.UniversalShapingEngineInfo.Category = "R";
                }
            }
        }
    }

    private static void RecordPref(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        int end = index + count;
        for (int i = index; i < end; i++)
        {
            GlyphShapingData data = substitutionCollection[i];
            if (data.IsSubstituted)
            {
                // Mark a substituted pref as VPre, as they behave the same way.
                if (data.UniversalShapingEngineInfo != null)
                {
                    data.UniversalShapingEngineInfo.Category = "VPre";
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
        int end = NextSyllable(substitutionCollection, index, max);
        while (start < max)
        {
            GlyphShapingData data = substitutionCollection[start];
            UniversalShapingEngineInfo? info = data.UniversalShapingEngineInfo;
            string? type = info?.SyllableType;

            // Only a few syllable types need reordering.
            if (type is not "virama_terminated_cluster" and not "standard_cluster" and not "broken_cluster")
            {
                goto Increment;
            }

            FontMetrics fontMetrics = substitutionCollection.GetFontMetrics(data);
            if (type == "broken_cluster" && fontMetrics.TryGetGlyphId(new(DottedCircle), out ushort id))
            {
                // As in HarfBuzz, the dotted circle is the base of the cluster. It goes after a repha and before
                // every other glyph. The glyphs that go in front of the base are then moved in front of it.
                int i = start;
                while (i < end && substitutionCollection[i].UniversalShapingEngineInfo?.Category == "R")
                {
                    i++;
                }

                // After a syllable of only repha, the glyph at the place of the circle is of the next syllable.
                substitutionCollection.InsertGlyph(i, i < end ? i : end - 1, id, new(DottedCircle));
                substitutionCollection[i].UniversalShapingEngineInfo = new(
                    UniversalShapingData.Categories[GetCategory(new(DottedCircle))],
                    type,
                    info!.Syllable);

                // The first glyph of the syllable may have changed.
                data = substitutionCollection[start];
                info = data.UniversalShapingEngineInfo;
                end++;
                max++;
            }

            // Move things forward
            if (info?.Category == "R" && end - start > 1)
            {
                // Got a repha. Reorder it to after first base, before first halant.
                for (int i = start + 1; i < end; i++)
                {
                    GlyphShapingData current = substitutionCollection[i];
                    info = current.UniversalShapingEngineInfo;
                    if (IsBase(info) || IsHalant(current))
                    {
                        // If we hit a halant, move before it; otherwise it's a base: move to it's
                        // place, and shift things in between backward.
                        if (IsHalant(current))
                        {
                            i--;
                        }

                        substitutionCollection.MoveGlyph(start, i);
                        break;
                    }
                }
            }

            // Move things back
            for (int i = start, j = end; i < end; i++)
            {
                GlyphShapingData current = substitutionCollection[i];
                info = current.UniversalShapingEngineInfo;

                if (IsBase(info) || IsHalant(current))
                {
                    // If we hit a halant, move after it; otherwise move to the beginning, and
                    // shift things in between forward.
                    if (IsHalant(current))
                    {
                        j = i + 1;
                    }
                    else
                    {
                        j = i;
                    }
                }
                else if ((info?.Category == "VPre" || info?.Category == "VMPre")
                    && current.LigatureComponent <= 0 // Only move the first component of a MultipleSubst
                    && j < i)
                {
                    substitutionCollection.MoveGlyph(i, j);
                }
            }

            Increment:
            start = end;
            end = NextSyllable(substitutionCollection, start, max);
        }
    }

    private static int NextSyllable(IGlyphShapingCollection collection, int index, int count)
    {
        if (index >= count)
        {
            return index;
        }

        int? syllable = collection[index].UniversalShapingEngineInfo?.Syllable;
        while (++index < count)
        {
            if (collection[index].UniversalShapingEngineInfo?.Syllable != syllable)
            {
                break;
            }
        }

        return index;
    }

    private static bool IsHalant(GlyphShapingData data)
        => (data.UniversalShapingEngineInfo?.Category is "H" or "HVM" or "IS") && !data.IsLigated;

    private static bool IsBase(UniversalShapingEngineInfo? info)
        => info?.Category is "B" or "GB";
}
