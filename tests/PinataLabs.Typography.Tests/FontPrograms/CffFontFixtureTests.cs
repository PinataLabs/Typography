// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Compares what <see cref="CffFont"/> reads from a font with the fontTools fixture of that font.
/// </summary>
public class CffFontFixtureTests
{
    [Test]
    [Arguments("cff-isoadobe")]
    [Arguments("cff-expert")]
    [Arguments("cff-expertsubset")]
    [Arguments("cff-encoding-format0")]
    [Arguments("cff-cid-fdselect0")]
    [Arguments("cff-cid-fdselect3")]
    [Arguments("SourceSansPro-Regular")]
    [Arguments("FDArrayTest257")]
    public async Task GlyphNamesAndCidsMatchFontTools(string fixtureName)
    {
        FontProgramFixture fixture = FontProgramFixture.Load(fixtureName);
        CffFont font = LoadCff(fixture.FontPath);
        List<string> differences = [];

        if (font.GlyphCount != fixture.Glyphs.Length || font.IsCidKeyed != fixture.IsCidKeyed)
        {
            differences.Add($"glyph count {font.GlyphCount}, CID-keyed {font.IsCidKeyed}");
        }

        Dictionary<string, int> firstByName = [];
        Dictionary<int, int> firstByCid = [];
        for (int i = 0; i < fixture.Glyphs.Length; i++)
        {
            FontProgramFixture.FixtureGlyph glyph = fixture.Glyphs[i];
            if (glyph.Name is not null)
            {
                firstByName.TryAdd(glyph.Name, i);
            }

            if (glyph.Cid is int cid)
            {
                firstByCid.TryAdd(cid, i);
            }

            string? name = font.GetGlyphName((ushort)i);
            if (name != glyph.Name)
            {
                differences.Add($"glyph {i}: name '{name}', expected '{glyph.Name}'");
            }
        }

        foreach ((string name, int glyphId) in firstByName)
        {
            if (!font.TryGetGlyphId(name, out ushort actual) || actual != glyphId)
            {
                differences.Add($"name '{name}': glyph {actual}, expected {glyphId}");
            }
        }

        foreach ((int cid, int glyphId) in firstByCid)
        {
            if (!font.TryGetGlyphIdForCid(cid, out ushort actual) || actual != glyphId)
            {
                differences.Add($"CID {cid}: glyph {actual}, expected {glyphId}");
            }
        }

        await Assert.That(differences).IsEmpty();
        await Assert.That(font.TryGetGlyphId("no.such.glyph", out _)).IsFalse();
        await Assert.That(font.GetGlyphName(ushort.MaxValue)).IsNull();
    }

    [Test]
    public async Task CidLookupIsIdentityWhenTheFontIsNotCidKeyed()
    {
        CffFont font = LoadCff(FontProgramFixture.Load("cff-isoadobe").FontPath);

        await Assert.That(font.TryGetGlyphIdForCid(7, out ushort glyphId)).IsTrue();
        await Assert.That(glyphId).IsEqualTo((ushort)7);
        await Assert.That(font.TryGetGlyphIdForCid(font.GlyphCount, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdForCid(-1, out _)).IsFalse();
    }

    [Test]
    public async Task CidLookupFailsForACidTheFontLacks()
    {
        CffFont font = LoadCff(FontProgramFixture.Load("cff-cid-fdselect0").FontPath);

        await Assert.That(font.TryGetGlyphIdForCid(1, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdForCid(70000, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphId("cid00005", out _)).IsFalse();
    }

    private static CffFont LoadCff(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return new FontReader(stream).GetTable<Cff1Table>().Font;
    }
}
