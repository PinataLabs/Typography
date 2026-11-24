// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Compares what <see cref="FontProgram"/> reads from a font with the fontTools fixture of that font,
/// for every fixture in Fonts/FontPrograms and for every glyph of each font.
/// </summary>
public class FontProgramParityTests
{
    public static IEnumerable<object[]> Fixtures
        => Directory.GetFiles(TestFonts.GetPath("FontPrograms"), "*.json")
            .Select(x => Path.GetFileNameWithoutExtension(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(x => new object[] { x });

    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task PropertiesMatchFontTools(string fixtureName)
    {
        (FontProgramFixture fixture, FontProgram font) = Load(fixtureName);

        await Assert.That(font.Format.ToString()).IsEqualTo(fixture.Format);
        await Assert.That(font.GlyphCount).IsEqualTo(fixture.Glyphs.Length);
        await Assert.That(font.IsCidKeyed).IsEqualTo(fixture.IsCidKeyed);
        await Assert.That(font.PaintType).IsEqualTo(fixture.PaintType == 2 ? GlyphPaintType.Stroked : GlyphPaintType.Filled);
        await Assert.That(font.StrokeWidth).IsEqualTo((float)fixture.StrokeWidth);

        double[] matrix = GlyphOutline.ToArray(font.FontMatrix);
        for (int i = 0; i < matrix.Length; i++)
        {
            await Assert.That(matrix[i]).IsEqualTo(fixture.FontMatrix[i]).Within(1e-9);
        }
    }

    // The outlines are compared in ems, within GlyphOutline.Tolerance: the fontTools outline through
    // the matrix of the glyph, and the FontProgram outline through the one matrix of the font.
    // The two differ only for a CID-keyed font whose Font DICTs have their own matrix.
    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task EveryOutlineMatchesFontTools(string fixtureName)
    {
        (FontProgramFixture fixture, FontProgram font) = Load(fixtureName);
        double[] fontMatrix = GlyphOutline.ToArray(font.FontMatrix);
        List<string> differences = [];

        for (int i = 0; i < fixture.Glyphs.Length && differences.Count < 10; i++)
        {
            FontProgramFixture.FixtureGlyph glyph = fixture.Glyphs[i];
            GlyphOutline actual = new();
            if (!font.TryWriteOutline((ushort)i, ref actual))
            {
                differences.Add($"glyph {i}: no outline");
                continue;
            }

            string? difference = GlyphOutline.Compare(GlyphOutline.Parse(glyph.Outline), glyph.Matrix ?? fixture.FontMatrix, actual, fontMatrix);
            if (difference is not null)
            {
                differences.Add($"glyph {i}: {difference}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task EveryAdvanceWidthMatchesFontTools(string fixtureName)
    {
        (FontProgramFixture fixture, FontProgram font) = Load(fixtureName);
        List<string> differences = [];

        for (int i = 0; i < fixture.Glyphs.Length && differences.Count < 10; i++)
        {
            double? expected = fixture.Glyphs[i].Advance;
            bool found = font.TryGetAdvanceWidth((ushort)i, out float actual);
            if (found != expected.HasValue || (found && Math.Abs(actual - expected!.Value) > 0.001))
            {
                differences.Add($"glyph {i}: advance {(found ? actual : null)}, expected {expected}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task EveryGlyphNameMatchesFontTools(string fixtureName)
    {
        (FontProgramFixture fixture, FontProgram font) = Load(fixtureName);
        List<string> differences = [];

        // More than one glyph can have the same name. A lookup by name finds the first.
        Dictionary<string, int> firstByName = [];
        for (int i = 0; i < fixture.Glyphs.Length; i++)
        {
            string? expected = fixture.Glyphs[i].Name;
            string? actual = font.GetGlyphName((ushort)i);
            if (actual != expected)
            {
                differences.Add($"glyph {i}: name '{actual}', expected '{expected}'");
            }

            if (expected is not null)
            {
                firstByName.TryAdd(expected, i);
            }
        }

        foreach ((string name, int glyphId) in firstByName)
        {
            if (!font.TryGetGlyphId(name, out ushort actual) || actual != glyphId)
            {
                differences.Add($"name '{name}': glyph {actual}, expected {glyphId}");
            }
        }

        await Assert.That(differences.Take(10)).IsEmpty();
        await Assert.That(font.TryGetGlyphId("no.such.glyph", out _)).IsFalse();
        await Assert.That(font.TryGetGlyphId(string.Empty, out _)).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task EveryCidMatchesFontTools(string fixtureName)
    {
        (FontProgramFixture fixture, FontProgram font) = Load(fixtureName);
        List<string> differences = [];

        for (int i = 0; i < fixture.Glyphs.Length; i++)
        {
            // A program that is not CID-keyed maps a CID to the glyph with the same ID.
            int cid = fixture.Glyphs[i].Cid ?? i;
            if (!font.TryGetGlyphIdForCid(cid, out ushort actual) || actual != i)
            {
                differences.Add($"CID {cid}: glyph {actual}, expected {i}");
            }
        }

        HashSet<int> cids = [.. fixture.Glyphs.Select((x, i) => x.Cid ?? i)];
        int absent = Enumerable.Range(0, cids.Count + 1).First(x => !cids.Contains(x));

        await Assert.That(differences.Take(10)).IsEmpty();
        await Assert.That(font.TryGetGlyphIdForCid(absent, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdForCid(-1, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdForCid(70000, out _)).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task BuiltInEncodingMatchesFontTools(string fixtureName)
    {
        (FontProgramFixture fixture, FontProgram font) = Load(fixtureName);
        ushort[] glyphIds = new ushort[256];
        Array.Fill(glyphIds, ushort.MaxValue);

        bool found = font.TryGetBuiltInEncoding(glyphIds);

        await Assert.That(found).IsEqualTo(fixture.Encoding is not null);

        int[] expected = fixture.Encoding ?? Enumerable.Repeat((int)ushort.MaxValue, 256).ToArray();
        List<string> differences = [];
        for (int code = 0; code < glyphIds.Length; code++)
        {
            if (glyphIds[code] != expected[code])
            {
                differences.Add($"code {code}: glyph {glyphIds[code]}, expected {expected[code]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    private static (FontProgramFixture Fixture, FontProgram Font) Load(string fixtureName)
    {
        FontProgramFixture fixture = FontProgramFixture.Load(fixtureName);
        return (fixture, FontProgram.Load(File.ReadAllBytes(fixture.FontPath)));
    }
}
