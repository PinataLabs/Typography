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
        Hebrew,
        Indic,
        Myanmar,
        Thai,
        Universal,

        // The scripts that HarfBuzz gives to its universal shaper and that ArabicShaper joins here.
        UniversalJoining,
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
            ShaperKind.Arabic or ShaperKind.UniversalJoining => new ArabicShaper(script, textOptions),
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
            ShaperKind.Arabic or ShaperKind.UniversalJoining => NormalizationProfile.Arabic,
            ShaperKind.Hangul => NormalizationProfile.Hangul,
            ShaperKind.Hebrew => NormalizationProfile.Hebrew,
            ShaperKind.Indic => NormalizationProfile.Indic,
            ShaperKind.Myanmar => NormalizationProfile.Myanmar,
            ShaperKind.Universal => NormalizationProfile.Universal,
            _ => NormalizationProfile.Default,
        };

    /// <summary>
    /// Gets a value indicating whether the shaper of the script positions glyphs without GPOS: it kerns with the
    /// <c>kern</c> table of the font, and in a font without a GPOS table it places a mark on its base from the boxes
    /// of the two glyphs. The default, Arabic, Hebrew and Hangul shapers of HarfBuzz do.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <returns><see langword="true"/> if the shaper positions glyphs without GPOS.</returns>
    public static bool HasFallbackPositioning(ScriptClass script)
        => GetShaperKind(script) is ShaperKind.Default or ShaperKind.Arabic or ShaperKind.Hebrew or ShaperKind.Hangul;

    private static ShaperKind GetShaperKind(ScriptClass script)
        => script switch
        {
            // Arabic
            ScriptClass.Arabic
            or ScriptClass.Syriac => ShaperKind.Arabic,

            // HarfBuzz gives these six to its universal shaper, which also joins their letters. UniversalShaper does
            // not join, so ArabicShaper shapes them. Without GPOS their marks are still those of the universal shaper.
            ScriptClass.Mongolian
            or ScriptClass.Nko
            or ScriptClass.PhagsPa
            or ScriptClass.Mandaic
            or ScriptClass.Manichaean
            or ScriptClass.PsalterPahlavi => ShaperKind.UniversalJoining,

            // Hangul
            ScriptClass.Hangul => ShaperKind.Hangul,

            // Hebrew has the default shaper and a normalization of its own.
            ScriptClass.Hebrew => ShaperKind.Hebrew,

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

            // Thai and Lao have the default shaper, which does not place their marks without GPOS.
            ScriptClass.Thai
            or ScriptClass.Lao => ShaperKind.Thai,

            // Universal
            ScriptClass.Adlam
            or ScriptClass.Ahom
            or ScriptClass.Balinese
            or ScriptClass.Batak
            or ScriptClass.Bhaiksuki
            or ScriptClass.Brahmi
            or ScriptClass.Buginese
            or ScriptClass.Buhid
            or ScriptClass.Chakma
            or ScriptClass.Cham
            or ScriptClass.Chorasmian
            or ScriptClass.CyproMinoan
            or ScriptClass.DivesAkuru
            or ScriptClass.Dogra
            or ScriptClass.Duployan
            or ScriptClass.EgyptianHieroglyphs
            or ScriptClass.Elymaic
            or ScriptClass.Grantha
            or ScriptClass.GunjalaGondi
            or ScriptClass.HanifiRohingya
            or ScriptClass.Hanunoo
            or ScriptClass.Javanese
            or ScriptClass.Kaithi
            or ScriptClass.KayahLi
            or ScriptClass.Kharoshthi
            or ScriptClass.KhitanSmallScript
            or ScriptClass.Khojki
            or ScriptClass.Khudawadi
            or ScriptClass.Lepcha
            or ScriptClass.Limbu
            or ScriptClass.Mahajani
            or ScriptClass.Makasar
            or ScriptClass.Marchen
            or ScriptClass.MasaramGondi
            or ScriptClass.Medefaidrin
            or ScriptClass.MeeteiMayek
            or ScriptClass.Miao
            or ScriptClass.Modi
            or ScriptClass.Multani
            or ScriptClass.Nandinagari
            or ScriptClass.Newa
            or ScriptClass.NyiakengPuachueHmong
            or ScriptClass.OldSogdian
            or ScriptClass.OldUyghur
            or ScriptClass.PahawhHmong
            or ScriptClass.Rejang
            or ScriptClass.Saurashtra
            or ScriptClass.Sharada
            or ScriptClass.Siddham
            or ScriptClass.Sinhala
            or ScriptClass.Sogdian
            or ScriptClass.Soyombo
            or ScriptClass.Sundanese
            or ScriptClass.SylotiNagri
            or ScriptClass.Tagalog
            or ScriptClass.Tagbanwa
            or ScriptClass.TaiLe
            or ScriptClass.TaiTham
            or ScriptClass.TaiViet
            or ScriptClass.Takri
            or ScriptClass.Tangsa
            or ScriptClass.Tibetan
            or ScriptClass.Tifinagh
            or ScriptClass.Tirhuta
            or ScriptClass.Toto
            or ScriptClass.Vithkuqi
            or ScriptClass.Wancho
            or ScriptClass.Yezidi
            or ScriptClass.ZanabazarSquare
            => ShaperKind.Universal,
            _ => ShaperKind.Default,
        };
}
