// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using TUnit.Core.Interfaces;

namespace PinataLabs.Typography.Tests.Unicode;

public class BidiAlgorithmTests
{
    private ITestOutput Output => TestContext.Current!.Output;

    [Test]
    public async Task RendersKurdishTextCorrect()
    {
        // arrange
        Font arabicFont = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(8);
        ColorGlyphRenderer renderer = new();
        const string testStr = "نەما خانی هەتا چیڕۆکی عیشقی ئێمە داڕێژێ";
        int[] expectedGlyphIndices =
        {
            0, 214, 0, 0, 139, 197, 3, 0, 289, 0, 333, 3, 336, 266, 221, 337, 242, 3, 336, 276, 0, 0, 337,
            188, 3, 140, 170, 0, 301, 3, 336, 294, 140, 196, 3, 140, 290, 0, 294
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    public async Task RendersFarsiTextCorrect()
    {
        // arrange
        Font arabicFont = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(8);
        var renderer = new ColorGlyphRenderer();
        const string testStr = "زناب فارسی را تک کمی سخت است";
        int[] expectedGlyphIndices =
        {
            168, 218, 139, 3, 168, 195, 218, 3, 336, 289, 276, 3, 274, 170, 3, 139,
            203, 3, 336, 218, 203, 140, 250, 3, 157, 140, 294, 207
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    public async Task RendersArabicTextWithPunctuationCorrectly()
    {
        // arrange
        Font arabicFont = new FontCollection().Add(TestFonts.TimesNewRomanFile).CreateFont(12);
        ColorGlyphRenderer renderer = new();
        const string testStr = "زۆرمان باس کرد؛ باسی ئاو، کەش، هەوای کوردستان.";
        int[] expectedGlyphIndices =
        {
            17, 997, 910, 920, 947, 937, 941, 1006, 815, 3, 821, 909, 1005, 1002, 1003, 3, 748,
            949, 1002, 815, 3, 748, 1005, 910, 907, 3, 822, 947, 910, 913, 3, 749, 937, 942, 815,
            3, 945, 910, 913, 3, 997, 910, 995, 941, 1574, 943
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    public async Task RendersArabicNumbersFromLeftToRight()
    {
        // arrange
        Font arabicFont = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(12);
        var renderer = new ColorGlyphRenderer();
        string testStr = "٠١٢٣٤٥٦٧٨٩";
        int[] expectedGlyphIndices = { 403, 405, 407, 409, 411, 413, 415, 417, 419, 421 };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    public async Task MixingArabicWordsWithNumbers_Works()
    {
        // arrange
        Font arabicFont = new FontCollection().Add(TestFonts.SegeouiFontFile).CreateFont(12);
        ColorGlyphRenderer renderer = new();
        const string testStr = "لە ساڵی ١٢٣٤ ڕوویدا";
        int[] expectedGlyphIndices = { 2317, 3631, 2380, 2345, 2345, 2485, 3, 2264, 2265, 2266, 2267, 3, 2379, 2540, 2247, 2260, 3, 2842, 2286 };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    public async Task MathematicalFormulasWithArabicText_Works()
    {
        // arrange
        Font arabicFont = new FontCollection().Add(TestFonts.SegeouiFontFile).CreateFont(12);
        ColorGlyphRenderer renderer = new();
        const string testStr = "١١س + ٨ج = ٨٥١";
        int[] expectedGlyphIndices = { 2271, 2268, 2264, 3, 32, 3, 2322, 2271, 3, 14, 3, 2329, 2264, 2264 };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    public async Task ICUTests()
    {
        await Assert.That(this.ICUTestsImpl()).IsTrue();
    }

    [Test]
    public async Task BoundaryNeutralIsRemovedWhenTheTextHasNoEmbeddingControls()
    {
        // The mark takes no type from the soft hyphen before it, so it follows the right to left paragraph.
        var data = new BidiData();
        data.Init("\U000000AD\U00000300", 1);
        var bidi = new BidiAlgorithm();

        bidi.Process(data);

        await Assert.That((int)bidi.ResolvedLevels[1]).IsEqualTo(1);
    }

    [Test]
    [Arguments("ab\u00AD", new sbyte[] { 2, 2, 1 })]
    [Arguments("ab \u00AD", new sbyte[] { 2, 2, 1, 1 })]
    [Arguments("ab\u00AD ", new sbyte[] { 2, 2, 1, 1 })]
    [Arguments("ab\u00AD\tcd", new sbyte[] { 2, 2, 1, 1, 2, 2 })]
    [Arguments("ab\t\u00ADcd", new sbyte[] { 2, 2, 1, 1, 2, 2 })]
    [Arguments("ab\u00ADcd", new sbyte[] { 2, 2, 2, 2, 2 })]
    [Arguments("ab\u202C", new sbyte[] { 2, 2, 1 })]
    [Arguments("ab\u202A\tcd", new sbyte[] { 2, 2, 1, 1, 2, 2 })]
    [Arguments("ab\u202C\u202A \tcd", new sbyte[] { 2, 2, 1, 1, 1, 1, 2, 2 })]
    [Arguments("ab\u202Ccd", new sbyte[] { 2, 2, 2, 2, 2 })]
    public async Task RuleL1ResetsABoundaryNeutralInTheSequenceItResets(string text, sbyte[] expected)
    {
        // UAX #9 section 5.2: rule L1 resets BN together with the whitespace before a separator or the end of the line.
        var data = new BidiData();
        data.Init(text, 1);
        var bidi = new BidiAlgorithm();

        bidi.Process(data);

        await Assert.That(bidi.ResolvedLevels.ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("\u202Babc", new sbyte[] { 0, 2, 2, 2 })]
    [Arguments("\u202Aabc", new sbyte[] { 0, 2, 2, 2 })]
    [Arguments("\u202Eabc", new sbyte[] { 0, 1, 1, 1 })]
    [Arguments("\u202Dabc", new sbyte[] { 0, 2, 2, 2 })]
    public async Task ARemovedCharacterAtTheStartTakesTheParagraphLevel(string text, sbyte[] expected)
    {
        // UAX #9 section 5.2: a removed character takes the level of the character before it, or the paragraph level
        // when no character is before it. Rules X2 to X5 give an embedding control the level that it pushes.
        var data = new BidiData();
        data.Init(text, 0);
        var bidi = new BidiAlgorithm();

        bidi.Process(data);

        await Assert.That(bidi.ResolvedLevels.ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("\u2068\n\u2069", new sbyte[] { 0, 0, 0 })]
    [Arguments("\u2066a\n\u2069", new sbyte[] { 0, 2, 0, 0 })]
    [Arguments("\u2067a\n\u2069", new sbyte[] { 0, 2, 0, 0 })]
    [Arguments("\u2066a\nb\u2069", new sbyte[] { 0, 2, 0, 0, 0 })]
    [Arguments("\u2066\na\u2069", new sbyte[] { 0, 0, 0, 0 })]
    [Arguments("\u202B\na", new sbyte[] { 0, 0, 0 })]
    [Arguments("\u2068!\n\u05D0", new sbyte[] { 0, 2, 0, 1 })]
    public async Task AParagraphSeparatorEndsEveryIsolateAndEmbedding(string text, sbyte[] expected)
    {
        // UAX #9 BD9 and X8: a paragraph separator ends every embedding, override and isolate, so no isolate initiator
        // matches a PDI after it, and the text after it is at the paragraph level. The text that rule X5c reads for an
        // FSI with no matching PDI ends at the separator.
        var data = new BidiData();
        data.Init(text, 0);
        var bidi = new BidiAlgorithm();

        bidi.Process(data);

        await Assert.That(bidi.ResolvedLevels.ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // UAX #9 P1: each paragraph separator ends a paragraph, and the rules after it see one paragraph at a time. Rule W2
    // does not read the AL before the line feed, so the EN stays EN, and W7 makes it L at sos. Rule N1 does not read the
    // R before the line feed, so the ON takes sos. With Auto, P2 and P3 give each paragraph its own level from its own
    // text, skipping its own isolates, and L1 resets its whitespace to that level. CR LF is one separator. ICU 72
    // (ubidi, as Windows ships it) gives the same levels.
    [Test]
    [Arguments("\u0627\n1", 0, new sbyte[] { 1, 0, 0 })]
    [Arguments("\u05D0\n!\u05D1", 0, new sbyte[] { 1, 0, 0, 1 })]
    [Arguments("\n\u05D0", 2, new sbyte[] { 0, 1 })]
    [Arguments("abc\n\u05D0!a", 2, new sbyte[] { 0, 0, 0, 0, 1, 1, 2 })]
    [Arguments("abc\n\u05D0 ", 2, new sbyte[] { 0, 0, 0, 0, 1, 1 })]
    [Arguments("\u2067\u05D0\u2069a\n\u2066a\u2069\u05D0", 2, new sbyte[] { 0, 1, 0, 0, 0, 1, 2, 1, 1 })]
    [Arguments("\u05D0\r\nb", 2, new sbyte[] { 1, 1, 1, 0 })]
    public async Task EachParagraphIsResolvedOnItsOwn(string text, int paragraphEmbeddingLevel, sbyte[] expected)
    {
        var data = new BidiData();
        data.Init(text, (sbyte)paragraphEmbeddingLevel);
        var bidi = new BidiAlgorithm();

        bidi.Process(data);

        await Assert.That(bidi.ResolvedLevels.ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // Rule X5c reads the text of an FSI and rule P2 the text of the paragraph, and both skip each isolate pair in it. The
    // FSI holds the pair LRI a PDI, then an R, so it resolves right to left. With paragraph level 2, P2 skips the pair
    // and finds the R. Types that are a slice of a longer array resolve to the levels of the same types in an array of
    // their own.
    [Test]
    [Arguments(0, new[] { BidiCharacterType.FirstStrongIsolate, BidiCharacterType.LeftToRightIsolate, BidiCharacterType.LeftToRight, BidiCharacterType.PopDirectionalIsolate, BidiCharacterType.RightToLeft, BidiCharacterType.PopDirectionalIsolate })]
    [Arguments(2, new[] { BidiCharacterType.LeftToRightIsolate, BidiCharacterType.LeftToRight, BidiCharacterType.PopDirectionalIsolate, BidiCharacterType.RightToLeft })]
    public async Task TypesThatDoNotStartTheArray_ResolveAsTheTypesOnTheirOwn(int paragraphEmbeddingLevel, BidiCharacterType[] types)
    {
        BidiCharacterType[] prefixed = [BidiCharacterType.LeftToRight, BidiCharacterType.LeftToRight, .. types];

        sbyte[] alone = Resolve(types);
        sbyte[] sliced = Resolve(new ReadOnlyArraySlice<BidiCharacterType>(prefixed, 2, types.Length));

        await Assert.That(sliced).IsEquivalentTo(alone, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(alone[^2]).IsEqualTo((sbyte)1);

        sbyte[] Resolve(ReadOnlyArraySlice<BidiCharacterType> slice)
        {
            var bidi = new BidiAlgorithm();
            bidi.Process(slice, ArraySlice<BidiPairedBracketType>.Empty, ArraySlice<int>.Empty, (sbyte)paragraphEmbeddingLevel, false, null, null, null);
            return bidi.ResolvedLevels.ToArray();
        }
    }

    private bool ICUTestsImpl()
    {
        this.Output.WriteLine("Bidi Class Tests");
        this.Output.WriteLine("----------------");

        // Read the test file
        string[] lines = File.ReadAllLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "BidiTest.txt"));

        var bidi = new BidiAlgorithm();

        var tests = new List<Test>();

        // Process each line
        int[]? levels = null;
        for (int lineNumber = 1; lineNumber < lines.Length + 1; lineNumber++)
        {
            // Get the line, remove comments
            string line = lines[lineNumber - 1].Split('#')[0].Trim();

            // Ignore blank/comment only lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Directive?
            if (line.StartsWith("@"))
            {
                if (line.StartsWith("@Levels:"))
                {
                    levels = line.Substring(8).Trim().Split(' ').Where(x => x.Length > 0).Select(x =>
                    {
                        if (x == "x")
                        {
                            return -1;
                        }
                        else
                        {
                            return int.Parse(x);
                        }
                    }).ToArray();
                }

                continue;
            }

            // Split data line
            string[] parts = line.Split(';');

            // Get the directions
            BidiCharacterType[] directions = parts[0].Split(' ').Select(x =>
            {
                UnicodeTypeMaps.BidiCharacterTypeMap.TryGetValue(x, out BidiCharacterType cls);
                return cls;
            }).ToArray();

            // Get the bit set
            int bitset = Convert.ToInt32(parts[1].Trim(), 16);

            BidiPairedBracketType[] pairTypes = Enumerable.Repeat(BidiPairedBracketType.None, directions.Length).ToArray();
            int[] pairValues = Enumerable.Repeat(0, directions.Length).ToArray();

            for (int bit = 1; bit < 8; bit <<= 1)
            {
                if ((bitset & bit) == 0)
                {
                    continue;
                }

                sbyte paragraphEmbeddingLevel;
                switch (bit)
                {
                    case 1:
                        paragraphEmbeddingLevel = 2;        // Auto
                        break;

                    case 2:
                        paragraphEmbeddingLevel = 0;        // LTR
                        break;

                    case 4:
                        paragraphEmbeddingLevel = 1;        // RTL
                        break;

                    default:
                        throw new NotSupportedException();
                }

                tests.Add(new Test(directions, paragraphEmbeddingLevel, levels!, lineNumber));
            }
        }

        this.Output.WriteLine($"Test data loaded: {tests.Count} test cases");

        for (int testNumber = 0; testNumber < tests.Count; testNumber++)
        {
            Test t = tests[testNumber];

            // Run the algorithm...
            ReadOnlyArraySlice<sbyte> resultLevels;

            bidi.Process(
                t.Types,
                ArraySlice<BidiPairedBracketType>.Empty,
                ArraySlice<int>.Empty,
                t.ParagraphEmbeddingLevel,
                false,
                null,
                null,
                null);

            resultLevels = bidi.ResolvedLevels;

            // Check the results match
            bool pass = true;
            if (resultLevels.Length == t.ExpectedLevels.Length)
            {
                for (int i = 0; i < t.ExpectedLevels.Length; i++)
                {
                    if (t.ExpectedLevels[i] == -1)
                    {
                        continue;
                    }

                    if (resultLevels[i] != t.ExpectedLevels[i])
                    {
                        pass = false;
                        break;
                    }
                }
            }
            else
            {
                pass = false;
            }

            if (!pass)
            {
                this.Output.WriteLine($"Failed line {t.LineNumber}");
                this.Output.WriteLine($"        Data: {string.Join(" ", t.Types)}");
                this.Output.WriteLine($" Embed Level: {t.ParagraphEmbeddingLevel}");
                this.Output.WriteLine($"    Expected: {string.Join(" ", t.ExpectedLevels)}");
                this.Output.WriteLine($"      Actual: {string.Join(" ", resultLevels)}");
                return false;
            }
        }

        return true;
    }

    private readonly struct Test
    {
        public Test(
            BidiCharacterType[] types,
            sbyte paragraphEmbeddingLevel,
            int[] expectedLevels,
            int lineNumber)
        {
            this.Types = types;
            this.ParagraphEmbeddingLevel = paragraphEmbeddingLevel;
            this.ExpectedLevels = expectedLevels;
            this.LineNumber = lineNumber;
        }

        public BidiCharacterType[] Types { get; }

        public sbyte ParagraphEmbeddingLevel { get; }

        public int[] ExpectedLevels { get; }

        public int LineNumber { get; }
    }
}
