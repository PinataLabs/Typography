// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Unicode;

public class GlyphNameEncodingsTests
{
    public static IEnumerable<SimpleFontEncoding> Encodings() => Enum.GetValues<SimpleFontEncoding>();

    // The entries of the data files: Annex D, with its notes, and the AFM files of the core fonts.
    [Test]
    [MethodDataSource(nameof(Encodings))]
    public async Task EveryCodeHasTheGlyphNameOfItsSource(SimpleFontEncoding encoding)
    {
        Dictionary<int, string> expected = GlyphNameData.ReadEncoding(encoding);
        List<string> different = [];
        for (int code = 0; code < 256; code++)
        {
            string? actual = GlyphNameEncodings.GetGlyphName(encoding, (byte)code);
            if (actual != expected.GetValueOrDefault(code))
            {
                different.Add($"{code:X2} expected {expected.GetValueOrDefault(code)} actual {actual}");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    [Test]
    [Arguments(SimpleFontEncoding.Standard, 149)]
    [Arguments(SimpleFontEncoding.WinAnsi, 224)]
    [Arguments(SimpleFontEncoding.MacRoman, 223)]
    [Arguments(SimpleFontEncoding.MacExpert, 165)]
    [Arguments(SimpleFontEncoding.Symbol, 189)]
    [Arguments(SimpleFontEncoding.ZapfDingbats, 202)]
    public async Task HasTheNumberOfCodesOfTheEncoding(SimpleFontEncoding encoding, int expected)
    {
        int count = 0;
        for (int code = 0; code < 256; code++)
        {
            count += GlyphNameEncodings.GetGlyphName(encoding, (byte)code) is null ? 0 : 1;
        }

        await Assert.That(count).IsEqualTo(expected);
    }

    // Each glyph name finds a code that has the name. Where more than one code has it, the lowest is found.
    [Test]
    [MethodDataSource(nameof(Encodings))]
    public async Task EveryGlyphNameRoundTripsToACodeWithThatName(SimpleFontEncoding encoding)
    {
        List<string> different = [];
        for (int code = 0; code < 256; code++)
        {
            string? name = GlyphNameEncodings.GetGlyphName(encoding, (byte)code);
            if (name is null)
            {
                continue;
            }

            if (!GlyphNameEncodings.TryGetCode(encoding, name, out byte found)
                || found > code
                || GlyphNameEncodings.GetGlyphName(encoding, found) != name)
            {
                different.Add($"{code:X2} {name}");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The names are for the Adobe Glyph List, so each one has text there, and the ZapfDingbats names in its own list.
    [Test]
    [MethodDataSource(nameof(Encodings))]
    public async Task EveryGlyphNameHasText(SimpleFontEncoding encoding)
    {
        List<string> without = [];
        for (int code = 0; code < 256; code++)
        {
            string? name = GlyphNameEncodings.GetGlyphName(encoding, (byte)code);
            if (name is not null
                && !(encoding == SimpleFontEncoding.ZapfDingbats
                    ? AdobeGlyphList.TryGetZapfDingbatsUnicode(name, out _)
                    : AdobeGlyphList.TryGetUnicode(name, out _)))
            {
                without.Add($"{code:X2} {name}");
            }
        }

        await Assert.That(without).IsEmpty();
    }

    [Test]
    [Arguments(SimpleFontEncoding.Standard, 0x27, "quoteright")]
    [Arguments(SimpleFontEncoding.Standard, 0x60, "quoteleft")]
    [Arguments(SimpleFontEncoding.Standard, 0xAE, "fi")]
    [Arguments(SimpleFontEncoding.Standard, 0xE1, "AE")]
    [Arguments(SimpleFontEncoding.Standard, 0xFB, "germandbls")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x27, "quotesingle")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x60, "grave")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x80, "Euro")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x95, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x7F, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x81, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x8D, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x8F, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x90, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x9D, "bullet")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x9F, "Ydieresis")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0xA0, "space")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0xAD, "hyphen")]
    [Arguments(SimpleFontEncoding.WinAnsi, 0xFF, "ydieresis")]
    [Arguments(SimpleFontEncoding.MacRoman, 0x27, "quotesingle")]
    [Arguments(SimpleFontEncoding.MacRoman, 0x80, "Adieresis")]
    [Arguments(SimpleFontEncoding.MacRoman, 0xCA, "space")]
    [Arguments(SimpleFontEncoding.MacRoman, 0xDB, "currency")]
    [Arguments(SimpleFontEncoding.MacRoman, 0xFF, "caron")]
    [Arguments(SimpleFontEncoding.MacExpert, 0x21, "exclamsmall")]
    [Arguments(SimpleFontEncoding.MacExpert, 0x30, "zerooldstyle")]
    [Arguments(SimpleFontEncoding.Symbol, 0x61, "alpha")]
    [Arguments(SimpleFontEncoding.Symbol, 0xA0, "Euro")]
    [Arguments(SimpleFontEncoding.ZapfDingbats, 0x21, "a1")]
    [Arguments(SimpleFontEncoding.ZapfDingbats, 0x80, "a89")]
    [Arguments(SimpleFontEncoding.ZapfDingbats, 0xFE, "a191")]
    public async Task HasTheGlyphNameOfAnnexD(SimpleFontEncoding encoding, byte code, string expected)
        => await Assert.That(GlyphNameEncodings.GetGlyphName(encoding, code)).IsEqualTo(expected);

    [Test]
    [Arguments(SimpleFontEncoding.Standard, 0x00)]
    [Arguments(SimpleFontEncoding.Standard, 0x7F)]
    [Arguments(SimpleFontEncoding.Standard, 0xA0)]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x00)]
    [Arguments(SimpleFontEncoding.WinAnsi, 0x1F)]
    [Arguments(SimpleFontEncoding.MacRoman, 0x7F)]
    [Arguments(SimpleFontEncoding.MacExpert, 0x1F)]
    [Arguments(SimpleFontEncoding.Symbol, 0x7F)]
    [Arguments(SimpleFontEncoding.ZapfDingbats, 0x7F)]
    public async Task GivesNoGlyphNameToACodeWithoutOne(SimpleFontEncoding encoding, byte code)
        => await Assert.That(GlyphNameEncodings.GetGlyphName(encoding, code)).IsNull();

    [Test]
    [Arguments(SimpleFontEncoding.Standard, "space", 0x20)]
    [Arguments(SimpleFontEncoding.WinAnsi, "space", 0x20)]
    [Arguments(SimpleFontEncoding.WinAnsi, "hyphen", 0x2D)]
    [Arguments(SimpleFontEncoding.WinAnsi, "bullet", 0x7F)]
    [Arguments(SimpleFontEncoding.MacRoman, "space", 0x20)]
    [Arguments(SimpleFontEncoding.Symbol, "Euro", 0xA0)]
    [Arguments(SimpleFontEncoding.ZapfDingbats, "a1", 0x21)]
    public async Task FindsTheLowestCodeOfAGlyphName(SimpleFontEncoding encoding, string glyphName, byte expected)
    {
        await Assert.That(GlyphNameEncodings.TryGetCode(encoding, glyphName, out byte code)).IsTrue();
        await Assert.That(code).IsEqualTo(expected);
    }

    [Test]
    [Arguments(SimpleFontEncoding.Standard, "")]
    [Arguments(SimpleFontEncoding.Standard, "Euro")]
    [Arguments(SimpleFontEncoding.Standard, "SPACE")]
    [Arguments(SimpleFontEncoding.Standard, "space.alt")]
    [Arguments(SimpleFontEncoding.Standard, "uni0020")]
    [Arguments(SimpleFontEncoding.WinAnsi, "fi")]
    [Arguments(SimpleFontEncoding.ZapfDingbats, "a207")]
    public async Task FindsNoCodeForAGlyphNameThatTheEncodingLacks(SimpleFontEncoding encoding, string glyphName)
    {
        await Assert.That(GlyphNameEncodings.TryGetCode(encoding, glyphName, out byte code)).IsFalse();
        await Assert.That(code).IsEqualTo((byte)0);
    }

    [Test]
    public async Task RejectsAnEncodingThatIsNotDefined()
    {
        await Assert.That(() => GlyphNameEncodings.GetGlyphName((SimpleFontEncoding)6, 0x41)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => GlyphNameEncodings.TryGetCode((SimpleFontEncoding)(-1), "A", out _)).Throws<ArgumentOutOfRangeException>();
    }
}
