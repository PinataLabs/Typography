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

namespace PinataLabs.Typography.Tests;

/// <summary>
/// <see cref="TextShaper.Shape"/> gives the glyphs that HarfBuzz gives: the same glyph IDs, clusters, advances and
/// offsets, in font units. tests/tools/generate_text_shaper_reference.py writes TextShaper.ttf and the reference.
/// </summary>
public class TextShaperTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("TextShaper.harfbuzz.json")),
        Options)!;

    private static readonly Dictionary<string, Font> Fonts = [];

    // The runs that the library shapes with other glyphs than HarfBuzz, whatever calls it: the layout gives the same glyphs.
    // It is a Gujarati ra with a virama and a letter after it. Each entry has the font and the code points of the run.
    private static readonly HashSet<string> KnownDifferences =
    [
        "NotoSansGujarati-Regular.ttf 0A95 0AB0 0ACD 0AAE",
    ];

    private static readonly Dictionary<string, ScriptClass> Scripts = new()
    {
        ["Arab"] = ScriptClass.Arabic,
        ["Bali"] = ScriptClass.Balinese,
        ["Beng"] = ScriptClass.Bengali,
        ["Cyrl"] = ScriptClass.Cyrillic,
        ["Deva"] = ScriptClass.Devanagari,
        ["Grek"] = ScriptClass.Greek,
        ["Gujr"] = ScriptClass.Gujarati,
        ["Guru"] = ScriptClass.Gurmukhi,
        ["Hang"] = ScriptClass.Hangul,
        ["Hebr"] = ScriptClass.Hebrew,
        ["Khmr"] = ScriptClass.Khmer,
        ["Knda"] = ScriptClass.Kannada,
        ["Latn"] = ScriptClass.Latin,
        ["Mlym"] = ScriptClass.Malayalam,
        ["Mymr"] = ScriptClass.Myanmar,
        ["Orya"] = ScriptClass.Oriya,
        ["Taml"] = ScriptClass.Tamil,
        ["Telu"] = ScriptClass.Telugu,
    };

    /// <summary>
    /// Gets the real faces of the reference. The runs of each have marks on letters of its script.
    /// </summary>
    public static IEnumerable<object[]> Faces
        => HarfBuzz.Rows.Select(r => r.Font).Where(f => f != "TextShaper.ttf").Distinct().Select(f => new object[] { f });

    [Test]
    public async Task LigatureOfTwoLettersIsOneGlyphWhoseClusterCoversBoth()
    {
        Font font = GetFont("EBGaramond-Regular.ttf");

        // The run "office" has the ligature ffi at the characters 1 to 3.
        ShapedGlyph[] glyphs = TextShaper.Shape(font, "office");
        ShapedGlyph[] parts = TextShaper.Shape(font, "office", new() { Features = [new(Tag.Parse("liga"), false)] });

        await Assert.That(glyphs.Select(g => g.Cluster)).IsEquivalentTo(new[] { 0, 1, 4, 5 });
        await Assert.That(parts.Select(g => g.Cluster)).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
        await Assert.That(glyphs.Select(g => g.GlyphId)).DoesNotContain(parts[1].GlyphId);
    }

    [Test]
    public async Task LamAlefIsOneGlyphWhoseClusterCoversBoth()
    {
        Font font = GetFont("Dubai-Regular.ttf");

        // A word of two letters, lam and alef, and a space and the letter beh after it. The glyphs are in visual order,
        // so the glyph of the last character is the first.
        ShapedGlyph[] glyphs = TextShaper.Shape(font, "\u0644\u0627 \u0628", new() { Direction = TextDirection.RightToLeft });

        await Assert.That(glyphs.Select(g => g.Cluster)).IsEquivalentTo(new[] { 3, 2, 0 });
    }

    [Test]
    public async Task AdvancesAndOffsetsAreInFontUnits()
    {
        Font font = GetFont("TextShaper.ttf");

        ShapedGlyph[] glyphs = TextShaper.Shape(font, "e\u0301");

        await Assert.That(font.FontMetrics.UnitsPerEm).IsEqualTo((ushort)1000);
        await Assert.That(glyphs.Select(g => (g.XAdvance, g.XOffset, g.YOffset))).IsEquivalentTo(new[] { (600, 0, 0), (0, -300, 100) });
    }

    [Test]
    public async Task AdvanceAbove32767IsNotNegative()
    {
        // TextShaperLarge.ttf has 16384 units to the em, and its o is 20000 wide. The pair o i adds 30000.
        ShapedGlyph[] glyphs = TextShaper.Shape(GetFont("TextShaperLarge.ttf"), "oi");

        await Assert.That(glyphs.Select(g => g.XAdvance)).IsEquivalentTo(new[] { 50000, 8192 });
    }

    [Test]
    public async Task AdvanceAbove65535IsNotWrapped()
    {
        // The W is 40960 wide, and the pair W W adds 32000.
        ShapedGlyph[] glyphs = TextShaper.Shape(GetFont("TextShaperLarge.ttf"), "WW");

        await Assert.That(glyphs.Select(g => g.XAdvance)).IsEquivalentTo(new[] { 72960, 40960 });
    }

    [Test]
    public async Task AdvanceBelowZeroIsNegative()
    {
        // The i is 8192 wide, and the pair i o takes 12000 from it.
        ShapedGlyph[] glyphs = TextShaper.Shape(GetFont("TextShaperLarge.ttf"), "io");

        await Assert.That(glyphs.Select(g => g.XAdvance)).IsEquivalentTo(new[] { -3808, 20000 });
    }

    [Test]
    public async Task OffsetBeyond32767IsNotWrapped()
    {
        // The mark moves back by the 40960 units of the W that it sits on, and up by the anchor of the W.
        ShapedGlyph[] glyphs = TextShaper.Shape(GetFont("TextShaperLarge.ttf"), "Ẃ");

        await Assert.That(glyphs.Select(g => (g.XOffset, g.YOffset))).IsEquivalentTo(new[] { (0, 0), (-40960, 20000) });
    }

    [Test]
    public async Task SizeOfTheFontDoesNotChangeTheGlyphs()
    {
        Font font = GetFont("TextShaper.ttf");

        ShapedGlyph[] small = TextShaper.Shape(font.WithSize(7), "AVA");
        ShapedGlyph[] large = TextShaper.Shape(font.WithSize(300), "AVA");

        await Assert.That(large).IsEquivalentTo(small);
    }

    [Test]
    public async Task EmptyTextHasNoGlyphs()
        => await Assert.That(TextShaper.Shape(GetFont("TextShaper.ttf"), string.Empty)).IsEmpty();

    [Test]
    public async Task FontIsRequired()
        => await Assert.That(() => TextShaper.Shape(null!, "a")).Throws<ArgumentNullException>();

    [Test]
    public async Task DirectionOfTheFirstStrongCharacterIsTheDefault()
    {
        Font font = GetFont("DejaVuSans.ttf");

        ShapedGlyph[] automatic = TextShaper.Shape(font, "\u05D0\u05D1");
        ShapedGlyph[] rightToLeft = TextShaper.Shape(font, "\u05D0\u05D1", new() { Direction = TextDirection.RightToLeft });

        await Assert.That(automatic).IsEquivalentTo(rightToLeft);
        await Assert.That(automatic.Select(g => g.Cluster)).IsEquivalentTo(new[] { 1, 0 });
    }

    [Test]
    public async Task DirectionThatIsNoValueOfTheEnumThrows()
        => await Assert.That(() => TextShaper.Shape(GetFont("TextShaper.ttf"), "a", new() { Direction = (TextDirection)7 })).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task DirectionThatIsNoValueOfTheEnumThrowsForEmptyText()
        => await Assert.That(() => TextShaper.Shape(GetFont("TextShaper.ttf"), string.Empty, new() { Direction = (TextDirection)(-1) })).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task FeaturesAreNotNull()
        => await Assert.That(() => new ShapingOptions { Features = null! }).Throws<ArgumentNullException>();

    [Test]
    public async Task RunWithNoStrongCharacterHasTheDirectionOfItsScript()
    {
        Font font = GetFont("DejaVuSans.ttf");

        // Digits and brackets have no direction of their own. The glyphs of a run from right to left start with the last character.
        ShapedGlyph[] arabic = TextShaper.Shape(font, "123 ()", new() { Script = ScriptClass.Arabic });
        ShapedGlyph[] hebrew = TextShaper.Shape(font, "123 ()", new() { Script = ScriptClass.Hebrew });
        ShapedGlyph[] latin = TextShaper.Shape(font, "123 ()", new() { Script = ScriptClass.Latin });
        ShapedGlyph[] unset = TextShaper.Shape(font, "123 ()");

        await Assert.That(arabic.Select(g => g.Cluster)).IsEquivalentTo(new[] { 5, 4, 3, 2, 1, 0 });
        await Assert.That(hebrew.Select(g => g.Cluster)).IsEquivalentTo(new[] { 5, 4, 3, 2, 1, 0 });
        await Assert.That(latin.Select(g => g.Cluster)).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
        await Assert.That(unset.Select(g => g.Cluster)).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
    }

    [Test]
    public async Task StrongCharacterDecidesTheDirectionOverTheScript()
    {
        // The caller names Arabic for Latin letters. The letters decide, where HarfBuzz would follow the script.
        ShapedGlyph[] glyphs = TextShaper.Shape(GetFont("DejaVuSans.ttf"), "abc", new() { Script = ScriptClass.Arabic });

        await Assert.That(glyphs.Select(g => g.Cluster)).IsEquivalentTo(new[] { 0, 1, 2 });
    }

    [Test]
    public async Task CommonScriptCountsAsNoScript()
    {
        Font font = GetFont("TextShaper.ttf");

        ShapedGlyph[] unset = TextShaper.Shape(font, "fi");
        ShapedGlyph[] common = TextShaper.Shape(font, "fi", new() { Script = ScriptClass.Common });

        await Assert.That(common).IsEquivalentTo(unset);
    }

    [Test]
    public async Task LastEntryOfAFeatureWins()
    {
        Font font = GetFont("TextShaper.ttf");
        Tag liga = Tag.Parse("liga");

        ShapedGlyph[] off = TextShaper.Shape(font, "fi", new() { Features = [new(liga, true), new(liga, false)] });
        ShapedGlyph[] on = TextShaper.Shape(font, "fi", new() { Features = [new(liga, false), new(liga, true)] });

        await Assert.That(off).Count().IsEqualTo(2);
        await Assert.That(on).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ShapingOnSeveralThreadsGivesTheSameGlyphs()
    {
        Font font = GetFont("Dubai-Regular.ttf");
        ShapingOptions options = new() { Direction = TextDirection.RightToLeft };
        const string text = "\u0628\u0650\u0633\u0652\u0645\u0650 \u0627\u0644\u0644\u0651\u064E\u0647\u0650";
        ShapedGlyph[] expected = TextShaper.Shape(font, text, options);
        ShapedGlyph[][] results = new ShapedGlyph[64][];

        await Task.WhenAll(Enumerable.Range(0, results.Length).Select(i => Task.Run(() => results[i] = TextShaper.Shape(font, text, options))));

        await Assert.That(results.All(r => r.AsSpan().SequenceEqual(expected))).IsTrue();
    }

    [Test]
    public async Task NoOptionsAreTheDefaultOptions()
    {
        Font font = GetFont("EBGaramond-Regular.ttf");

        await Assert.That(TextShaper.Shape(font, "office")).IsEquivalentTo(TextShaper.Shape(font, "office", new()));
    }

    [Test]
    public async Task UnpairedSurrogateHasAGlyphAndACluster()
    {
        Font font = GetFont("TextShaper.ttf");

        ShapedGlyph[] glyphs = TextShaper.Shape(font, "e\uD800e");

        await Assert.That(glyphs.Select(g => g.Cluster)).IsEquivalentTo(new[] { 0, 1, 2 });
    }

    [Test]
    public async Task LeftMatraAfterARunOfAnotherScriptStartsAWord()
    {
        Font font = GetFont("NotoSansBengali-Regular.ttf");

        // Ka and the vowel sign e, which goes before ka and has the init form 446 at the start of a word. A Latin letter
        // before them is a run of its own, which HarfBuzz shapes in a buffer of its own, so the sign still starts a word.
        ShapedGlyph[] syllable = TextShaper.Shape(font, "কে", new() { Script = ScriptClass.Bengali });
        ShapedGlyph[] afterLetter = TextShaper.Shape(font, "aকে");

        await Assert.That(syllable.Select(g => g.GlyphId).SequenceEqual(new ushort[] { 446, 19 })).IsTrue();
        await Assert.That(afterLetter.Skip(1).Select(g => g.GlyphId).SequenceEqual(new ushort[] { 446, 19 })).IsTrue();
    }

    [Test]
    public async Task KnownDifferencesAreStillDifferences()
    {
        // A run that starts to match HarfBuzz leaves the list.
        Row[] known = HarfBuzz.Rows.Where(IsKnownDifference).ToArray();
        List<string> matching = known
            .Where(r => Shape(r, withScript: true).Select(g => g.GlyphId).SequenceEqual(r.Glyphs.Select(g => (ushort)g[0])))
            .Select(r => $"{r.Font} {Describe(r.Text)}")
            .ToList();

        await Assert.That(known.Length).IsEqualTo(KnownDifferences.Count);
        await Assert.That(matching).IsEmpty();
    }

    [Test]
    public async Task SyntheticCasesGiveTheGlyphsThatHarfBuzzGives()
        => await Assert.That(Differences(HarfBuzz.Rows.Where(r => r.Font == "TextShaper.ttf"), withScript: true, positions: true)).IsEmpty();

    [Test]
    [MethodDataSource(nameof(Faces))]
    public async Task RunsOfMarksGiveTheGlyphIdsAndClustersThatHarfBuzzGives(string fontName)
        => await Assert.That(Differences(HarfBuzz.Rows.Where(r => r.Font == fontName), withScript: true, positions: false)).IsEmpty();

    [Test]
    [MethodDataSource(nameof(Faces))]
    public async Task RunsOfMarksGiveTheAdvancesAndOffsetsThatHarfBuzzGives(string fontName)
        => await Assert.That(PositionDifferences(HarfBuzz.Rows.Where(r => r.Font == fontName), withScript: true)).IsEmpty();

    [Test]
    [MethodDataSource(nameof(Faces))]
    public async Task ScriptOfTheCharactersGivesTheSameGlyphsAsTheScriptOfTheRun(string fontName)
    {
        // A run that has no character of a script of its own, such as a tatweel and its marks, has the script of the run
        // only when the caller names it.
        IEnumerable<Row> rows = HarfBuzz.Rows.Where(r => r.Font == fontName && r.Text.EnumerateRunes().Any(HasScriptOfItsOwn));

        await Assert.That(Differences(rows, withScript: false, positions: true)).IsEmpty();
    }

    private static Font GetFont(string name, Dictionary<string, float>? variations = null)
    {
        lock (Fonts)
        {
            string key = name + string.Concat(variations?.Select(v => $" {v.Key}={v.Value}") ?? []);
            if (!Fonts.TryGetValue(key, out Font? font))
            {
                // The shaper does not scale, so the size does not matter.
                FontVariation[] axes = variations?.Select(v => new FontVariation(Tag.Parse(v.Key), v.Value)).ToArray() ?? [];
                font = new FontCollection().Add(TestFonts.GetPath(name)).CreateFont(12, FontStyle.Regular, axes);
                Fonts.Add(key, font);
            }

            return font;
        }
    }

    private static ShapedGlyph[] Shape(Row row, bool withScript)
    {
        ShapingOptions options = new()
        {
            Script = withScript && row.Script is not null ? Scripts[row.Script] : null,
            Language = row.Language is null ? null : Tag.Parse(row.Language),
            Direction = row.Auto ? TextDirection.Auto : row.Direction == "rtl" ? TextDirection.RightToLeft : TextDirection.LeftToRight,
            Features = row.Features?.Select(f => new ShapingFeature(Tag.Parse(f.Key), f.Value)).ToArray() ?? [],
        };

        return TextShaper.Shape(GetFont(row.Font, row.Variations), row.Text, options);
    }

    // The glyph IDs and clusters, and with positions the advances and offsets too, of each row that the library does not
    // give as HarfBuzz does.
    private static List<string> Differences(IEnumerable<Row> rows, bool withScript, bool positions)
    {
        List<string> different = [];
        foreach (Row row in rows.Where(r => !IsKnownDifference(r)))
        {
            ShapedGlyph[] actual = Shape(row, withScript);
            string expected = string.Join(' ', row.Glyphs.Select(g => positions ? $"{g[0]}@{g[1]}:{g[2]}:{g[3]},{g[4]}" : $"{g[0]}@{g[1]}"));
            string result = string.Join(' ', actual.Select(g => positions ? $"{g.GlyphId}@{g.Cluster}:{g.XAdvance}:{g.XOffset},{g.YOffset}" : $"{g.GlyphId}@{g.Cluster}"));
            if (expected != result)
            {
                different.Add($"{row.Name} [{Describe(row.Text)}] expected [{expected}] actual [{result}]");
            }
        }

        return different;
    }

    private static List<string> PositionDifferences(IEnumerable<Row> rows, bool withScript)
    {
        List<string> different = [];
        foreach (Row row in rows.Where(r => !IsKnownDifference(r)))
        {
            ShapedGlyph[] actual = Shape(row, withScript);
            if (actual.Length != row.Glyphs.Length)
            {
                continue;
            }

            string expected = string.Join(' ', row.Glyphs.Select(g => $"{g[2]}:{g[3]},{g[4]}"));
            string result = string.Join(' ', actual.Select(g => $"{g.XAdvance}:{g.XOffset},{g.YOffset}"));
            if (expected != result)
            {
                different.Add($"{row.Name} [{Describe(row.Text)}] expected [{expected}] actual [{result}]");
            }
        }

        return different;
    }

    private static bool HasScriptOfItsOwn(System.Text.Rune rune)
        => CodePoint.GetScriptClass(new CodePoint(rune.Value)) is not (ScriptClass.Common or ScriptClass.Inherited or ScriptClass.Unknown);

    private static bool IsKnownDifference(Row row) => KnownDifferences.Contains($"{row.Font} {Describe(row.Text)}");

    private static string Describe(string text) => string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));

    private sealed class Reference
    {
        public Row[] Rows { get; set; } = [];
    }

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;

        public string Font { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public string? Script { get; set; }

        public string? Language { get; set; }

        public string Direction { get; set; } = "ltr";

        public bool Auto { get; set; }

        public Dictionary<string, bool>? Features { get; set; }

        public Dictionary<string, float>? Variations { get; set; }

        public int[][] Glyphs { get; set; } = [];
    }
}
