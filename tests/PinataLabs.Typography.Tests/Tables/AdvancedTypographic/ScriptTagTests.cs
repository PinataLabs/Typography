// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// Each Unicode script looks for the OpenType script tags that HarfBuzz looks for, in the same order. Some of
/// these tags differ from the ISO 15924 code of the script, such as <c>nko </c> for Nkoo and <c>kana</c> for Hira.
/// </summary>
public class ScriptTagTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ScriptTagsHarfBuzzReference),
        Options)!;

    [Test]
    public async Task ReferenceHasEveryScript()
    {
        string[] scripts = Enum.GetNames<ScriptClass>();

        await Assert.That(HarfBuzz.Scripts.Select(s => s.Name)).IsEquivalentTo(scripts);
    }

    [Test]
    public async Task EveryScriptLooksForTheTagsThatHarfBuzzLooksFor()
    {
        string[] mismatches = HarfBuzz.Scripts
            .Select(s => (s.Name, Expected: string.Join(",", s.Tags), Actual: string.Join(
                ",",
                UnicodeScriptTagMap.Instance[Enum.Parse<ScriptClass>(s.Name)].Select(t => t.ToString()))))
            .Where(s => s.Expected != s.Actual)
            .Select(s => $"{s.Name}: expected [{s.Expected}], got [{s.Actual}]")
            .ToArray();

        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    [Arguments("Nko")]
    [Arguments("Lao")]
    [Arguments("Vai")]
    [Arguments("Yi")]
    [Arguments("Katakana")]
    [Arguments("Hiragana")]
    [Arguments("KannadaVersion3")]
    public async Task GlyphsAreShapedAsHarfBuzzShapesThem(string name)
    {
        ReferenceCase expected = HarfBuzz.Cases.Single(c => c.Name == name);
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(expected.Font));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(expected.Text.AsSpan(), new TextOptions(font));

        (int Id, int X, int Y)[] actual = layout
            .Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected.Glyphs.Select(g => (g.Id, g.X, g.Y)), CollectionOrdering.Matching);
    }

    private sealed class Reference
    {
        public ReferenceScript[] Scripts { get; set; } = [];

        public ReferenceCase[] Cases { get; set; } = [];
    }

    private sealed class ReferenceScript
    {
        public string Name { get; set; } = string.Empty;

        public string[] Tags { get; set; } = [];
    }

    private sealed class ReferenceCase
    {
        public string Name { get; set; } = string.Empty;

        public string Font { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public ReferenceGlyph[] Glyphs { get; set; } = [];
    }

    private sealed class ReferenceGlyph
    {
        public int Id { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
