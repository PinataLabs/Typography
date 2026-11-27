// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Unicode;

public class AdobeGlyphListTests
{
    [Test]
    public async Task EveryGlyphListNameHasItsListedText()
    {
        List<string> different = [];
        foreach ((string name, string expected) in GlyphNameData.GlyphList)
        {
            if (!AdobeGlyphList.TryGetUnicode(name, out string actual) || actual != expected)
            {
                different.Add(name);
            }
        }

        await Assert.That(GlyphNameData.GlyphList.Count).IsEqualTo(4281);
        await Assert.That(different).IsEmpty();
    }

    [Test]
    public async Task EveryZapfDingbatsNameHasItsListedText()
    {
        List<string> different = [];
        foreach ((string name, string expected) in GlyphNameData.ZapfDingbats)
        {
            if (!AdobeGlyphList.TryGetZapfDingbatsUnicode(name, out string actual) || actual != expected)
            {
                different.Add(name);
            }

            // The names a1 to a206 are not Adobe Glyph List names.
            if (AdobeGlyphList.TryGetUnicode(name, out _))
            {
                different.Add(name + " in the Adobe Glyph List");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The preferred name of a code point is the name that aglfn.txt gives it, so name, text and name again is the identity.
    [Test]
    public async Task EveryPreferredNameRoundTrips()
    {
        List<string> different = [];
        foreach ((int codePoint, string name) in GlyphNameData.Aglfn)
        {
            if (!AdobeGlyphList.TryGetUnicode(name, out string text)
                || text != char.ConvertFromUtf32(codePoint)
                || AdobeGlyphList.GetGlyphName(codePoint) != name)
            {
                different.Add(name);
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The Adobe Glyph List gives some code points many names, so name, text and name again need not be the identity.
    // It gives text and then name and then text again, which is the identity: the writer's name reads back as the same text.
    [Test]
    public async Task EveryGlyphListNameWithOneCodePointReadsBackFromItsPreferredName()
    {
        List<string> different = [];
        foreach ((string name, string text) in GlyphNameData.GlyphList.Where(e => !e.Value.Any(char.IsSurrogate) && e.Value.Length == 1))
        {
            string preferred = AdobeGlyphList.GetGlyphName(text[0]);
            if (!AdobeGlyphList.TryGetUnicode(preferred, out string readBack) || readBack != text)
            {
                different.Add($"{name} {preferred}");
            }

            // A name that is not the preferred one is a listed name or the uni form.
            if (GlyphNameData.Aglfn.TryGetValue(text[0], out string? aglfn) ? preferred != aglfn : preferred != $"uni{(int)text[0]:X4}")
            {
                different.Add($"{name} has the preferred name {preferred}");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The list maps Omega, Delta and mu to the compatibility characters, as aglfn.txt does, and omegagreek, deltagreek and
    // mugreek to the Greek letters. A caller that wants the letters normalizes the result (NFC for Omega only, NFKC for mu).
    [Test]
    [Arguments("Omega", "Ω")]
    [Arguments("Delta", "∆")]
    [Arguments("mu", "µ")]
    [Arguments("Omegagreek", "Ω")]
    [Arguments("Deltagreek", "Δ")]
    [Arguments("mugreek", "μ")]
    public async Task MapsADoubleMappedNameToItsListedCharacter(string glyphName, string expected)
    {
        await Assert.That(AdobeGlyphList.TryGetUnicode(glyphName, out string text)).IsTrue();
        await Assert.That(text).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0x2126, "Omega")]
    [Arguments(0x2206, "Delta")]
    [Arguments(0x00B5, "mu")]
    [Arguments(0x03A9, "uni03A9")]
    public async Task NamesTheDoubleMappedCharactersAsAglfnDoes(int codePoint, string expected)
        => await Assert.That(AdobeGlyphList.GetGlyphName(codePoint)).IsEqualTo(expected);

    [Test]
    [Arguments("A", "A")]
    [Arguments("Euro", "€")]
    [Arguments("dalethatafpatah", "דֲ")]
    [Arguments("uni0041", "A")]
    [Arguments("uni20AC", "€")]
    [Arguments("uni20AC0041", "€A")]
    [Arguments("uniD7FF", "퟿")]
    [Arguments("uniE000", "")]
    [Arguments("u0041", "A")]
    [Arguments("u1F600", "\U0001F600")]
    [Arguments("u10FFFF", "\U0010FFFF")]
    [Arguments("u0000", "\0")]
    [Arguments("f_f_i", "ffi")]
    [Arguments("f_i", "fi")]
    [Arguments("T_h.alt", "Th")]
    [Arguments("uni0041_u1F600_Euro", "A\U0001F600€")]
    [Arguments("A.swash", "A")]
    [Arguments("uni0041.alt", "A")]
    [Arguments("fi.liga", "ﬁ")]
    [Arguments("A.B.C", "A")]
    public async Task MapsAGlyphNameByTheSpecification(string glyphName, string expected)
    {
        await Assert.That(AdobeGlyphList.TryGetUnicode(glyphName, out string text)).IsTrue();
        await Assert.That(text).IsEqualTo(expected);
    }

    [Test]
    [Arguments("")]
    [Arguments(".notdef")]
    [Arguments(".")]
    [Arguments("_")]
    [Arguments("A_")]
    [Arguments("_A")]
    [Arguments("A__B")]
    [Arguments("a1")]
    [Arguments("notaglyph")]
    [Arguments("f_notaglyph")]
    [Arguments("uni")]
    [Arguments("uni004")]
    [Arguments("uni00410")]
    [Arguments("uni004a")]
    [Arguments("uni00e9")]
    [Arguments("uniD800")]
    [Arguments("uniDFFF")]
    [Arguments("uni0041D800")]
    [Arguments("uni004G")]
    [Arguments("u004")]
    [Arguments("u0041a")]
    [Arguments("u00e9")]
    [Arguments("u1F60g")]
    [Arguments("uD800")]
    [Arguments("uDFFF")]
    [Arguments("u110000")]
    [Arguments("u1234567")]
    [Arguments("u+0041")]
    [Arguments("a b")]
    [Arguments("é")]
    public async Task RejectsANameWithoutText(string glyphName)
    {
        await Assert.That(AdobeGlyphList.TryGetUnicode(glyphName, out string text)).IsFalse();
        await Assert.That(text).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task IsCaseSensitive()
    {
        await Assert.That(AdobeGlyphList.TryGetUnicode("euRo", out _)).IsFalse();
        await Assert.That(AdobeGlyphList.TryGetUnicode("EURO", out _)).IsFalse();
        await Assert.That(AdobeGlyphList.TryGetUnicode("UNI0041", out _)).IsFalse();
        await Assert.That(AdobeGlyphList.TryGetUnicode("U0041", out _)).IsFalse();
    }

    [Test]
    public async Task ZapfDingbatsNamesMapFirstAndOtherNamesMapAsUsual()
    {
        await Assert.That(AdobeGlyphList.TryGetZapfDingbatsUnicode("a1", out string a1)).IsTrue();
        await Assert.That(a1).IsEqualTo("✁");
        await Assert.That(AdobeGlyphList.TryGetZapfDingbatsUnicode("space", out string space)).IsTrue();
        await Assert.That(space).IsEqualTo(" ");
        await Assert.That(AdobeGlyphList.TryGetZapfDingbatsUnicode("uni2701.alt", out string uni)).IsTrue();
        await Assert.That(uni).IsEqualTo("✁");
        await Assert.That(AdobeGlyphList.TryGetZapfDingbatsUnicode("a1_a2", out string pair)).IsTrue();
        await Assert.That(pair).IsEqualTo("✁✂");
        await Assert.That(AdobeGlyphList.TryGetZapfDingbatsUnicode("a207", out _)).IsFalse();
    }

    [Test]
    [Arguments(0x20, "space")]
    [Arguments(0x41, "A")]
    [Arguments(0x20AC, "Euro")]
    [Arguments(0xFB01, "uniFB01")]
    [Arguments(0x0394, "uni0394")]
    [Arguments(0x00A0, "uni00A0")]
    [Arguments(0x0000, "uni0000")]
    [Arguments(0xD7FF, "uniD7FF")]
    [Arguments(0xE000, "uniE000")]
    [Arguments(0xFFFF, "uniFFFF")]
    [Arguments(0x10000, "u10000")]
    [Arguments(0x1F600, "u1F600")]
    [Arguments(0x10FFFF, "u10FFFF")]
    public async Task GetGlyphNameGivesThePreferredName(int codePoint, string expected)
        => await Assert.That(AdobeGlyphList.GetGlyphName(codePoint)).IsEqualTo(expected);

    [Test]
    [Arguments(-1)]
    [Arguments(0xD800)]
    [Arguments(0xDFFF)]
    [Arguments(0x110000)]
    [Arguments(int.MaxValue)]
    [Arguments(int.MinValue)]
    public async Task GetGlyphNameRejectsAValueThatIsNotAScalarValue(int codePoint)
        => await Assert.That(() => AdobeGlyphList.GetGlyphName(codePoint)).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task EveryScalarValueNameReadsBackAsItsCodePoint()
    {
        List<int> different = [];
        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint += codePoint < 0x20000 ? 1 : 0x101)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            if (!AdobeGlyphList.TryGetUnicode(AdobeGlyphList.GetGlyphName(codePoint), out string text) || text != char.ConvertFromUtf32(codePoint))
            {
                different.Add(codePoint);
            }
        }

        await Assert.That(different).IsEmpty();
    }
}
