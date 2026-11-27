// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Tables.AdvancedTypographic.GSub;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Shapes Arabic in a font whose GSUB table has none of the joining form features for Arabic in the language of the
/// run, as HarfBuzz does. Each letter takes the presentation form that the font has for its place in the word, and
/// then the forms that make one of the ligatures of <see cref="ArabicFallbackTable"/> ligate, as do a shadda and a vowel
/// mark.
/// </summary>
internal static class ArabicFallbackShaper
{
    // The features whose forms the table has, in the order of its forms.
    private static readonly Tag[] FormTags = [Tag.Parse("isol"), Tag.Parse("fina"), Tag.Parse("init"), Tag.Parse("medi")];

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
        if (!NeedsFallback(gsub, substitution.TextOptions))
        {
            return;
        }

        ArabicFallbackPlan plan = fontMetrics.GetArabicFallbackPlan();

        // A form replaces the glyph of its letter, so a glyph that an earlier lookup gave the letter is replaced too.
        for (int i = index; i < index + count; i++)
        {
            GlyphShapingData data = substitution[i];
            int form = GetForm(data);
            if (form >= 0 && plan.TryGetFormGlyph(data.GlyphId, form, out ushort glyphId))
            {
                substitution.Replace(i, glyphId);
            }
        }

        LookupFeatures features = LookupFeatures.ForShaperSubstitution();
        count = Ligate(fontMetrics, gsub, substitution, features, index, count, plan.LetterLigatures);
        Ligate(fontMetrics, gsub, substitution, features, index, count, plan.MarkLigatures);
    }

    // As in HarfBuzz, a run falls back only when the font lacks every one of the four joining forms for the language of the
    // run. HarfBuzz plans a form for the fallback only while the caller leaves it on, so a form that the caller turned off
    // ends the fallback, and the letters keep their glyphs.
    private static bool NeedsFallback(GSubTable gsub, TextOptions options)
    {
        foreach (Tag tag in FormTags)
        {
            if (LookupFeatures.IsDisabled(options.DisabledFeatureTags, tag))
            {
                return false;
            }
        }

        return !gsub.TryGetFeatureLookups(FormTags, ScriptClass.Arabic, options.Language, options.DisabledFeatureTags, out _);
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
}
