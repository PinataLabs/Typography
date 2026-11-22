// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

/// <summary>
/// Compares every glyph of the Type 1 test fonts with fontTools.
/// tests/tools/type1/generate_fixtures.py writes the fixtures.
/// </summary>
public class Type1ParityTests
{
    /// <summary>
    /// The largest accepted difference for a coordinate or an advance, in glyph units.
    /// fontTools computes in double precision and the sink takes single precision.
    /// </summary>
    private const float Tolerance = 0.001F;

    [Test]
    [Arguments("bchr8a.pfb", "bchr8a.json", 229)]
    [Arguments("cmr10.pfb", "cmr10.json", 132)]
    [Arguments("cmmi10.pfb", "cmmi10.json", 130)]
    [Arguments("qtmr.pfb", "qtmr.json", 1090)]
    [Arguments("bchr8a.pfa", "bchr8a.json", 229)]
    [Arguments("bchr8a.fontfile", "bchr8a.json", 229)]
    public async Task EveryGlyphMatchesFontTools(string fontFile, string fixtureFile, int glyphCount)
    {
        Type1Program font = Type1Program.Load(Type1TestFonts.Read(fontFile));
        Fixture fixture = Fixture.Read(fixtureFile);

        await Assert.That(fixture.Glyphs.Length).IsEqualTo(glyphCount);
        await Assert.That(Compare(font, fixture)).IsEmpty();
    }

    [Test]
    public async Task PdfFontFileWithWrongLengthsMatchesFontTools()
    {
        using JsonDocument containers = JsonDocument.Parse(Type1TestFonts.Read("containers.json"));
        JsonElement root = containers.RootElement;
        byte[] data = Type1TestFonts.Read(root.GetProperty("fontFile").GetString()!);
        Fixture fixture = Fixture.Read("bchr8a.json");

        int length1 = root.GetProperty("length1").GetInt32();
        int wrongLength1 = root.GetProperty("wrongLength1").GetInt32();
        await Assert.That(wrongLength1).IsNotEqualTo(length1);

        await Assert.That(Compare(Type1Program.Load(data, length1), fixture)).IsEmpty();
        await Assert.That(Compare(Type1Program.Load(data, wrongLength1), fixture)).IsEmpty();
        await Assert.That(Compare(Type1Program.Load(data, data.Length + 1), fixture)).IsEmpty();
    }

    private static List<string> Compare(Type1Program font, Fixture fixture)
    {
        List<string> failures = [];
        if (font.FontName != fixture.FontName)
        {
            failures.Add($"FontName: expected {fixture.FontName}, got {font.FontName}");
        }

        float[] matrix = [font.FontMatrix.M11, font.FontMatrix.M12, font.FontMatrix.M21, font.FontMatrix.M22, font.FontMatrix.M31, font.FontMatrix.M32];
        if (!matrix.SequenceEqual(fixture.FontMatrix.Select(x => (float)x)))
        {
            failures.Add($"FontMatrix: expected {string.Join(" ", fixture.FontMatrix)}, got {string.Join(" ", matrix)}");
        }

        // Glyph 0 is .notdef; the other glyphs keep the /CharStrings order that fontTools reports.
        Dictionary<string, ushort> glyphIds = [];
        ushort next = 1;
        foreach (FixtureGlyph glyph in fixture.Glyphs)
        {
            glyphIds.Add(glyph.Name, glyph.Name == ".notdef" ? (ushort)0 : next++);
        }

        if (font.GlyphCount != fixture.Glyphs.Length)
        {
            failures.Add($"GlyphCount: expected {fixture.Glyphs.Length}, got {font.GlyphCount}");
        }

        foreach (FixtureGlyph glyph in fixture.Glyphs)
        {
            ushort glyphId = glyphIds[glyph.Name];
            if (font.GetGlyphName(glyphId) != glyph.Name
                || !font.TryGetGlyphId(glyph.Name, out ushort found)
                || found != glyphId)
            {
                failures.Add($"{glyph.Name}: expected glyph ID {glyphId}, which is named {font.GetGlyphName(glyphId)}");
                continue;
            }

            OutlineRecorder recorder = new();
            if (!font.TryWriteOutline(glyphId, ref recorder))
            {
                failures.Add($"{glyph.Name}: no outline");
                continue;
            }

            string? difference = OutlineRecorder.Difference(OutlineRecorder.Parse(glyph.Outline), recorder.Commands, Tolerance);
            if (difference is not null)
            {
                failures.Add($"{glyph.Name}: {difference}");
            }

            if (!font.TryGetAdvanceWidth(glyphId, out float advance) || !(MathF.Abs(advance - (float)glyph.Advance) <= Tolerance))
            {
                failures.Add($"{glyph.Name}: expected advance {glyph.Advance}, got {advance}");
            }
        }

        ushort[] encoding = new ushort[256];
        if (!font.TryGetBuiltInEncoding(encoding))
        {
            failures.Add("no built-in encoding");
        }

        for (int code = 0; code < 256; code++)
        {
            glyphIds.TryGetValue(fixture.Encoding[code], out ushort expected);
            if (encoding[code] != expected)
            {
                failures.Add($"code {code} ({fixture.Encoding[code]}): expected glyph {expected}, got {encoding[code]}");
            }
        }

        return failures;
    }

    private sealed record FixtureGlyph(string Name, double Advance, string Outline);

    private sealed record Fixture(string FontName, double[] FontMatrix, string[] Encoding, FixtureGlyph[] Glyphs)
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static Fixture Read(string fileName)
            => JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Type1TestFonts.GetPath(fileName)), Options)
                ?? throw new InvalidDataException(fileName);
    }
}
