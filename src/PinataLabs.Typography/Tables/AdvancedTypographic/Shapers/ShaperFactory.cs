// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

internal static class ShaperFactory
{
    private enum ShaperKind
    {
        Default,
        Arabic,
        Hangul,
        Indic,
        Myanmar,
        Universal,
    }

    /// <summary>
    /// Creates a Shaper based on the given script language.
    /// </summary>
    /// <param name="script">The script language.</param>
    /// <param name="unicodeScriptTag">The unicode script tag found in the font matching the script.</param>
    /// <param name="textOptions">The text options.</param>
    /// <returns>A shaper for the given script.</returns>
    public static BaseShaper Create(ScriptClass script, Tag unicodeScriptTag, TextOptions textOptions)
        => GetShaperKind(script) switch
        {
            ShaperKind.Arabic => new ArabicShaper(script, textOptions),
            ShaperKind.Hangul => new HangulShaper(script, textOptions),
            ShaperKind.Indic => new IndicShaper(script, unicodeScriptTag, textOptions),
            ShaperKind.Myanmar => new MyanmarShaper(script, textOptions),
            ShaperKind.Universal => new UniversalShaper(script, textOptions),
            _ => new DefaultShaper(script, textOptions),
        };

    /// <summary>
    /// Gets how the shaper of the script wants its text normalized before GSUB.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <returns>The profile of the shaper that <see cref="Create"/> returns for the script.</returns>
    public static NormalizationProfile GetNormalizationProfile(ScriptClass script)
        => GetShaperKind(script) switch
        {
            ShaperKind.Arabic => NormalizationProfile.Arabic,
            ShaperKind.Hangul => NormalizationProfile.Hangul,
            ShaperKind.Indic => NormalizationProfile.Indic,
            ShaperKind.Myanmar => NormalizationProfile.Myanmar,
            ShaperKind.Universal => NormalizationProfile.Universal,
            _ => NormalizationProfile.Default,
        };

    private static ShaperKind GetShaperKind(ScriptClass script)
        => script switch
        {
            // Arabic
            ScriptClass.Arabic
            or ScriptClass.Mongolian
            or ScriptClass.Syriac
            or ScriptClass.Nko
            or ScriptClass.PhagsPa
            or ScriptClass.Mandaic
            or ScriptClass.Manichaean
            or ScriptClass.PsalterPahlavi => ShaperKind.Arabic,

            // Hangul
            ScriptClass.Hangul => ShaperKind.Hangul,

            // Indic
            ScriptClass.Bengali
            or ScriptClass.Devanagari
            or ScriptClass.Gujarati
            or ScriptClass.Gurmukhi
            or ScriptClass.Kannada
            or ScriptClass.Malayalam
            or ScriptClass.Oriya
            or ScriptClass.Tamil
            or ScriptClass.Telugu
            or ScriptClass.Khmer => ShaperKind.Indic,

            // Myanmar
            ScriptClass.Myanmar => ShaperKind.Myanmar,

            // Universal
            ScriptClass.Balinese
            or ScriptClass.Batak
            or ScriptClass.Brahmi
            or ScriptClass.Buginese
            or ScriptClass.Buhid
            or ScriptClass.Chakma
            or ScriptClass.Cham
            or ScriptClass.Duployan
            or ScriptClass.EgyptianHieroglyphs
            or ScriptClass.Grantha
            or ScriptClass.Hanunoo
            or ScriptClass.Javanese
            or ScriptClass.Kaithi
            or ScriptClass.KayahLi
            or ScriptClass.Kharoshthi
            or ScriptClass.Khojki
            or ScriptClass.Khudawadi
            or ScriptClass.Lepcha
            or ScriptClass.Limbu
            or ScriptClass.Mahajani
            or ScriptClass.MeeteiMayek
            or ScriptClass.Modi
            or ScriptClass.PahawhHmong
            or ScriptClass.Rejang
            or ScriptClass.Saurashtra
            or ScriptClass.Sharada
            or ScriptClass.Siddham
            or ScriptClass.Sinhala
            or ScriptClass.Sundanese
            or ScriptClass.SylotiNagri
            or ScriptClass.Tagalog
            or ScriptClass.Tagbanwa
            or ScriptClass.TaiLe
            or ScriptClass.TaiTham
            or ScriptClass.TaiViet
            or ScriptClass.Takri
            or ScriptClass.Tibetan
            or ScriptClass.Tifinagh
            or ScriptClass.Tirhuta
            => ShaperKind.Universal,
            _ => ShaperKind.Default,
        };
}
