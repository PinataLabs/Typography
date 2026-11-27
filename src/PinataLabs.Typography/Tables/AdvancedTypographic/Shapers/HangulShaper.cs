// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// This is a shaper for the Hangul script, used by the Korean language. It composes and decomposes syllables, sets the
/// jamo features and moves tone marks as <c>preprocess_text_hangul</c> of HarfBuzz 15.0.0 does.
/// </summary>
internal sealed class HangulShaper : DefaultShaper
{
    private static readonly Tag LjmoTag = Tag.Parse("ljmo");

    private static readonly Tag VjmoTag = Tag.Parse("vjmo");

    private static readonly Tag TjmoTag = Tag.Parse("tjmo");

    private const int HangulBase = 0xac00;
    private const int LBase = 0x1100; // lead
    private const int VBase = 0x1161; // vowel
    private const int TBase = 0x11a7; // trail
    private const int LCount = 19;
    private const int VCount = 21;
    private const int TCount = 28;
    private const int NCount = VCount * TCount;
    private const int LEnd = LBase + LCount - 1;
    private const int VEnd = VBase + VCount - 1;
    private const int TEnd = TBase + TCount - 1;
    private const int DottedCircle = 0x25cc;

    // Character categories
    private const byte X = 0; // Other character
    private const byte L = 1; // Leading consonant
    private const byte V = 2; // Medial vowel
    private const byte T = 3; // Trailing consonant
    private const byte LV = 4; // Composed <LV> syllable
    private const byte LVT = 5; // Composed <LVT> syllable
    private const byte M = 6; // Tone mark

    public HangulShaper(ScriptClass script, TextOptions textOptions)
        : base(script, MarkZeroingMode.None, textOptions)
    {
    }

    /// <inheritdoc/>
    protected override void PlanFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        this.AddFeature(collection, index, count, LjmoTag, false);
        this.AddFeature(collection, index, count, VjmoTag, false);
        this.AddFeature(collection, index, count, TjmoTag, false);
    }

    /// <inheritdoc/>
    protected override void AssignFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        if (collection is GlyphSubstitutionCollection substitutionCollection)
        {
            // GSub
            int end = ShapeSyllables(substitutionCollection, index, index + count);

            // As in HarfBuzz, calt stays on for the run but not for the jamo that the shaper leaves, because some
            // fonts (Noto Sans CJK, Source Han Sans) put their jamo lookups in calt. A syllable keeps it.
            for (int i = index; i < end; i++)
            {
                if (GetSyllableType(substitutionCollection[i].CodePoint) is L or V or T)
                {
                    substitutionCollection.DisableShapingFeature(i, CaltTag);
                }
            }
        }
        else
        {
            // GPos
            // Simply loop and enable based on type.
            // Glyph substitution has handled [de]composition.
            for (int i = index; i < index + count; i++)
            {
                GlyphShapingData data = collection[i];
                CodePoint codePoint = data.CodePoint;
                switch (GetSyllableType(codePoint))
                {
                    case L:
                        collection.EnableShapingFeature(i, LjmoTag);
                        break;
                    case V:
                        collection.EnableShapingFeature(i, VjmoTag);
                        break;
                    case T:
                        collection.EnableShapingFeature(i, TjmoTag);
                        break;
                    case LV:
                        collection.EnableShapingFeature(i, LjmoTag);
                        collection.EnableShapingFeature(i, VjmoTag);
                        break;
                    case LVT:
                        collection.EnableShapingFeature(i, LjmoTag);
                        collection.EnableShapingFeature(i, VjmoTag);
                        collection.EnableShapingFeature(i, TjmoTag);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Composes each syllable that the font has a glyph for and takes apart each one that it lacks into jamo with the
    /// jamo features, in one cluster. A tone mark right after a syllable moves before it, and any other tone mark gets
    /// a dotted circle.
    /// </summary>
    /// <returns>The end of the run, which composition and decomposition move.</returns>
    private static int ShapeSyllables(GlyphSubstitutionCollection collection, int index, int end)
    {
        // The glyphs after the run stay as they are, so the run ends this many glyphs before the end of the collection.
        int after = collection.Count - end;

        // The glyphs of the last syllable, if start < syllableEnd.
        int start = index;
        int syllableEnd = index;
        int i = index;
        while (i < collection.Count - after)
        {
            end = collection.Count - after;
            GlyphShapingData data = collection[i];
            CodePoint u = data.CodePoint;
            int type = GetSyllableType(u);
            FontMetrics metrics = collection.GetFontMetrics(data);

            if (type == M)
            {
                if (start < syllableEnd && syllableEnd == i)
                {
                    // The tone mark follows a syllable. It moves before it, unless it is zero width.
                    if (!IsZeroWidth(collection, data))
                    {
                        collection.MoveGlyph(i, start);
                    }

                    i++;
                }
                else
                {
                    i = InsertDottedCircle(collection, data, i) + 1;
                }

                start = syllableEnd = i;
                continue;
            }

            start = i;
            if (type == L && i + 1 < end)
            {
                if (GetSyllableType(collection[i + 1].CodePoint) == V)
                {
                    // <L,V> or <L,V,T>: compose it if the font has the syllable, or set the jamo features.
                    CodePoint v = collection[i + 1].CodePoint;
                    bool hasT = i + 2 < end && GetSyllableType(collection[i + 2].CodePoint) == T;
                    int length = hasT ? 3 : 2;
                    if (IsCombiningL(u) && IsCombiningV(v) && (!hasT || IsCombiningT(collection[i + 2].CodePoint)))
                    {
                        int tIndex = hasT ? collection[i + 2].CodePoint.Value - TBase : 0;
                        CodePoint s = new(HangulBase + ((u.Value - LBase) * NCount) + ((v.Value - VBase) * TCount) + tIndex);
                        if (collection.IsUnresolved(i, i + length) && metrics.TryGetGlyphId(s, out ushort id))
                        {
                            collection.Replace(i, length - 1, id);
                            collection[i].CodePoint = s;
                            syllableEnd = ++i;
                            continue;
                        }
                    }

                    syllableEnd = SetJamoFeatures(collection, i, length);
                    i = syllableEnd;
                    continue;
                }
            }
            else if (type is LV or LVT)
            {
                // <LV>, <LVT> or <LV,T>.
                bool hasGlyph = data.GlyphId != 0;
                int sIndex = u.Value - HangulBase;
                int tIndex = sIndex % TCount;
                bool tFollows = tIndex == 0 && i + 1 < end && GetSyllableType(collection[i + 1].CodePoint) == T;
                if (tFollows && IsCombiningT(collection[i + 1].CodePoint))
                {
                    // <LV,T>: compose it if the font has the syllable.
                    CodePoint s = new(u.Value + collection[i + 1].CodePoint.Value - TBase);
                    if (collection.IsUnresolved(i, i + 2) && metrics.TryGetGlyphId(s, out ushort id))
                    {
                        collection.Replace(i, 1, id);
                        collection[i].CodePoint = s;
                        syllableEnd = ++i;
                        continue;
                    }
                }

                // Take the syllable apart if the font lacks it, or if a trailing consonant that does not compose with it
                // follows. That consonant joins the syllable, which a consonant after a syllable the font lacks does not.
                if ((!hasGlyph || tFollows) && collection.IsUnresolved(i, hasGlyph ? i + 2 : i + 1))
                {
                    int jamo = Decompose(collection, metrics, i);
                    if (jamo > 0)
                    {
                        syllableEnd = SetJamoFeatures(collection, i, hasGlyph ? 3 : jamo);
                        i = syllableEnd;
                        continue;
                    }

                    if (!hasGlyph && tIndex != 0)
                    {
                        // HarfBuzz normalizes the text after this pass, and so gives an <LVT> it cannot take apart
                        // into jamo the glyphs of its <LV> and <T>, which are not a syllable.
                        i = DecomposeTrailingConsonant(collection, metrics, i) + 1;
                        continue;
                    }
                }

                if (hasGlyph)
                {
                    syllableEnd = i + 1;
                }
            }

            i++;
        }

        return collection.Count - after;
    }

    /// <summary>
    /// Sets the jamo features of the glyphs of a syllable that is not composed, and gives them one cluster.
    /// </summary>
    /// <returns>The end of the syllable.</returns>
    private static int SetJamoFeatures(GlyphSubstitutionCollection collection, int index, int length)
    {
        collection.EnableShapingFeature(index, LjmoTag);
        collection.EnableShapingFeature(index + 1, VjmoTag);
        if (length == 3)
        {
            collection.EnableShapingFeature(index + 2, TjmoTag);
        }

        collection.MergeClusters(index, index + length);
        return index + length;
    }

    /// <summary>
    /// Replaces the glyph of a syllable with the glyphs of its jamo, if the font has all of them.
    /// </summary>
    /// <returns>The number of jamo, or 0 if the font lacks one.</returns>
    private static int Decompose(GlyphSubstitutionCollection collection, FontMetrics metrics, int index)
    {
        int s = collection[index].CodePoint.Value - HangulBase;
        CodePoint l = new(LBase + (s / NCount));
        CodePoint v = new(VBase + ((s % NCount) / TCount));
        CodePoint t = new(TBase + (s % TCount));
        bool hasT = t.Value != TBase;
        ushort tGlyph = 0;
        if (!metrics.TryGetGlyphId(l, out ushort lGlyph)
            || !metrics.TryGetGlyphId(v, out ushort vGlyph)
            || (hasT && !metrics.TryGetGlyphId(t, out tGlyph)))
        {
            return 0;
        }

        Span<ushort> glyphs = stackalloc ushort[hasT ? 3 : 2];
        glyphs[0] = lGlyph;
        glyphs[1] = vGlyph;
        if (hasT)
        {
            glyphs[2] = tGlyph;
        }

        collection.Replace(index, glyphs);
        collection[index].CodePoint = l;
        collection[index + 1].CodePoint = v;
        if (hasT)
        {
            collection[index + 2].CodePoint = t;
        }

        return glyphs.Length;
    }

    /// <summary>
    /// Replaces the glyph of an <c>LVT</c> syllable with the glyphs of its <c>LV</c> syllable and its trailing consonant,
    /// as the canonical decomposition of the syllable gives them, if the font has both.
    /// </summary>
    /// <returns>The index of the last glyph.</returns>
    private static int DecomposeTrailingConsonant(GlyphSubstitutionCollection collection, FontMetrics metrics, int index)
    {
        int s = collection[index].CodePoint.Value - HangulBase;
        CodePoint lv = new(HangulBase + (s - (s % TCount)));
        CodePoint t = new(TBase + (s % TCount));
        if (!metrics.TryGetGlyphId(lv, out ushort lvGlyph) || !metrics.TryGetGlyphId(t, out ushort tGlyph))
        {
            return index;
        }

        Span<ushort> glyphs = stackalloc ushort[2];
        glyphs[0] = lvGlyph;
        glyphs[1] = tGlyph;
        collection.Replace(index, glyphs);
        collection[index].CodePoint = lv;
        collection[index + 1].CodePoint = t;
        return index + 1;
    }

    private static int GetSyllableType(CodePoint codePoint)
    {
        GraphemeClusterClass type = CodePoint.GetGraphemeClusterClass(codePoint);
        int value = codePoint.Value;

        return type switch
        {
            GraphemeClusterClass.HangulLead => L,
            GraphemeClusterClass.HangulVowel => V,
            GraphemeClusterClass.HangulTail => T,
            GraphemeClusterClass.HangulLeadVowel => LV,
            GraphemeClusterClass.HangulLeadVowelTail => LVT,

            // HANGUL SINGLE DOT TONE MARK
            // HANGUL DOUBLE DOT TONE MARK
            _ => value is >= 0x302E and <= 0x302F ? M : X,
        };
    }

    private static bool IsZeroWidth(GlyphSubstitutionCollection collection, GlyphShapingData data)
    {
        // We don't have access to the glyphs metrics as an array when substituting so we have to loop.
        FontMetrics fontMetrics = collection.GetFontMetrics(data);
        TextAttributes textAttributes = data.TextRun.TextAttributes;
        TextDecorations textDecorations = data.TextRun.TextDecorations;
        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        ColorFontSupport colorFontSupport = collection.TextOptions.ColorFontSupport;
        if (fontMetrics.TryGetGlyphMetrics(data.CodePoint, textAttributes, textDecorations, layoutMode, colorFontSupport, out IReadOnlyList<GlyphMetrics>? metrics))
        {
            foreach (GlyphMetrics gm in metrics)
            {
                if (gm.AdvanceWidth == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int InsertDottedCircle(GlyphSubstitutionCollection collection, GlyphShapingData data, int index)
    {
        FontMetrics fontMetrics = collection.GetFontMetrics(data);
        if (!fontMetrics.TryGetGlyphId(new(DottedCircle), out ushort id))
        {
            return index;
        }

        // If the tone mark is zero width, insert the dotted circle before, otherwise after
        Span<ushort> glyphs = stackalloc ushort[2];
        if (IsZeroWidth(collection, data))
        {
            glyphs[1] = data.GlyphId;
            glyphs[0] = id;
        }
        else
        {
            glyphs[1] = id;
            glyphs[0] = data.GlyphId;
        }

        collection.Replace(index, glyphs);
        return index + 1;
    }

    private static bool IsCombiningL(CodePoint code) => UnicodeUtility.IsInRangeInclusive((uint)code.Value, LBase, LEnd);

    private static bool IsCombiningV(CodePoint code) => UnicodeUtility.IsInRangeInclusive((uint)code.Value, VBase, VEnd);

    private static bool IsCombiningT(CodePoint code) => UnicodeUtility.IsInRangeInclusive((uint)code.Value, TBase + 1, TEnd);
}
