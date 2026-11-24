// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

internal static class ShaperFactory
{
    private static readonly Tag DfltTag = Tag.Parse("DFLT");

    private static readonly Tag LatnTag = Tag.Parse("latn");

    private static readonly Tag MymrTag = Tag.Parse("mymr");

    private enum ShaperKind
    {
        Default,
        Arabic,
        Hangul,
        Hebrew,
        Indic,

        // HarfBuzz has a Khmer shaper. IndicShaper shapes Khmer here.
        Khmer,
        Myanmar,
        Thai,
        Universal,

        // The scripts of the universal shaper whose letters join as Arabic letters do.
        UniversalJoining,
    }

    /// <summary>
    /// Creates the shaper of a text. As in HarfBuzz, it depends on the script of the text and on the script that
    /// the GSUB table of the font has for it.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="substitutionScript">The tag that <see cref="GSubTable.GetScriptTag"/> gives for the script.</param>
    /// <param name="textOptions">The text options.</param>
    /// <returns>A shaper for the text.</returns>
    public static BaseShaper Create(ScriptClass script, Tag substitutionScript, TextOptions textOptions)
        => GetShaperKind(script, substitutionScript) switch
        {
            ShaperKind.Arabic => new ArabicShaper(script, textOptions),
            ShaperKind.Hangul => new HangulShaper(script, textOptions),
            ShaperKind.Indic or ShaperKind.Khmer => new IndicShaper(script, substitutionScript, textOptions),
            ShaperKind.Myanmar => new MyanmarShaper(script, textOptions),
            ShaperKind.Universal => new UniversalShaper(script, false, textOptions),
            ShaperKind.UniversalJoining => new UniversalShaper(script, true, textOptions),
            _ => new DefaultShaper(script, textOptions),
        };

    /// <summary>
    /// Gets how the shaper of the script wants its text normalized before GSUB.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="substitutionScript">The tag that <see cref="GSubTable.GetScriptTag"/> gives for the script.</param>
    /// <returns>The profile of the shaper that <see cref="Create"/> returns for the text.</returns>
    public static NormalizationProfile GetNormalizationProfile(ScriptClass script, Tag substitutionScript)
        => GetShaperKind(script, substitutionScript) switch
        {
            ShaperKind.Arabic => NormalizationProfile.Arabic,
            ShaperKind.Hangul => NormalizationProfile.Hangul,
            ShaperKind.Hebrew => NormalizationProfile.Hebrew,
            ShaperKind.Indic or ShaperKind.Khmer => NormalizationProfile.Indic,
            ShaperKind.Myanmar => NormalizationProfile.Myanmar,
            ShaperKind.Universal or ShaperKind.UniversalJoining => NormalizationProfile.Universal,
            _ => NormalizationProfile.Default,
        };

    /// <summary>
    /// Gets a value indicating whether the shaper of the script positions glyphs without GPOS: it kerns with the
    /// <c>kern</c> table of the font, and in a font without a GPOS table it places a mark on its base from the boxes
    /// of the two glyphs. The default, Arabic, Hebrew and Hangul shapers of HarfBuzz do.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="substitutionScript">The tag that <see cref="GSubTable.GetScriptTag"/> gives for the script.</param>
    /// <returns><see langword="true"/> if the shaper positions glyphs without GPOS.</returns>
    public static bool HasFallbackPositioning(ScriptClass script, Tag substitutionScript)
        => GetShaperKind(script, substitutionScript) is ShaperKind.Default or ShaperKind.Arabic or ShaperKind.Hebrew or ShaperKind.Hangul;

    // HarfBuzz gives a script to another shaper when the GSUB table of the font has one of these scripts for it.
    // A font that has no script at all for the text keeps the shaper of the script.
    private static ShaperKind GetShaperKind(ScriptClass script, Tag substitutionScript)
    {
        ShaperKind kind = GetShaperKind(script);
        bool isDflt = substitutionScript == DfltTag;
        bool isDfltOrLatn = isDflt || substitutionScript == LatnTag;
        return kind switch
        {
            ShaperKind.Arabic when script == ScriptClass.Syriac && isDflt => ShaperKind.Default,
            ShaperKind.Indic or ShaperKind.Universal or ShaperKind.UniversalJoining when isDfltOrLatn => ShaperKind.Default,

            // A version 3 tag such as dev3.
            ShaperKind.Indic when (substitutionScript.Value & 0xFF) == '3' => ShaperKind.Universal,
            ShaperKind.Myanmar when isDfltOrLatn || substitutionScript == MymrTag => ShaperKind.Default,
            _ => kind,
        };
    }

    private static ShaperKind GetShaperKind(ScriptClass script)
        => script switch
        {
            // Arabic
            ScriptClass.Arabic
            or ScriptClass.Syriac => ShaperKind.Arabic,

            // Universal, with letters that join
            ScriptClass.Adlam
            or ScriptClass.Chorasmian
            or ScriptClass.HanifiRohingya
            or ScriptClass.Mandaic
            or ScriptClass.Manichaean
            or ScriptClass.Mongolian
            or ScriptClass.Nko
            or ScriptClass.OldUyghur
            or ScriptClass.PhagsPa
            or ScriptClass.PsalterPahlavi
            or ScriptClass.Sogdian => ShaperKind.UniversalJoining,

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
            or ScriptClass.Telugu => ShaperKind.Indic,

            // Khmer
            ScriptClass.Khmer => ShaperKind.Khmer,

            // Myanmar
            ScriptClass.Myanmar => ShaperKind.Myanmar,

            // Thai and Lao have the default shaper, which does not place their marks without GPOS.
            ScriptClass.Thai
            or ScriptClass.Lao => ShaperKind.Thai,

            // Universal
            ScriptClass.Ahom
            or ScriptClass.Balinese
            or ScriptClass.Batak
            or ScriptClass.Bhaiksuki
            or ScriptClass.Brahmi
            or ScriptClass.Buginese
            or ScriptClass.Buhid
            or ScriptClass.Chakma
            or ScriptClass.Cham
            or ScriptClass.CyproMinoan
            or ScriptClass.DivesAkuru
            or ScriptClass.Dogra
            or ScriptClass.Duployan
            or ScriptClass.EgyptianHieroglyphs
            or ScriptClass.Elymaic
            or ScriptClass.Grantha
            or ScriptClass.GunjalaGondi
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
            or ScriptClass.PahawhHmong
            or ScriptClass.Rejang
            or ScriptClass.Saurashtra
            or ScriptClass.Sharada
            or ScriptClass.Siddham
            or ScriptClass.Sinhala
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
