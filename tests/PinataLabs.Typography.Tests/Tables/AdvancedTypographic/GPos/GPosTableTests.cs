// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

public class GPosTableTests
{
    // LookupType1SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-1-single-adjustment-positioning-subtable
    [Test]
    public async Task SingleAdjustmentPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType1Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0012\u0014"; // XPlacement should be adjusted by minus 200 for both glyphs.
        int[] expectedGlyphIndices = { 20, 22 };
        FontRectangle[] expectedFontRectangles =
        {
            new(275, 1085.9999F, 825, 719),
            new(1622, 1084.9999F, 978, 704),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType1SubTable, Format 2
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-1-single-adjustment-positioning-subtable
    [Test]
    public async Task SingleAdjustmentPositioning_Format2_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType1Format2;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0015\u0014"; // second character XPlacement should be adjusted by minus 200
        int[] expectedGlyphIndices = { 23, 22 };
        FontRectangle[] expectedFontRectangles =
        {
            new(322, 1085.9999F, 978, 708),
            new(1522, 1084.9999F, 978, 704),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType2SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-2-pair-adjustment-positioning-subtable
    [Test]
    public async Task PairAdjustmentPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType2Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0017\u0012\u0014"; // "\u0012\u0014" first XPlacement minus 300 and second YPlacement minus 400.
        int[] expectedGlyphIndices = { 25, 20, 22 };
        FontRectangle[] expectedFontRectangles =
        {
            new(322, 1104.9999F, 978, 671),
            new(1675, 1085.9999F, 825, 719),
            new(3322, 1484.9999F, 978, 704),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType3SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-3-cursive-attachment-positioning-subtable
    [Test]
    public async Task CursiveAttachmentPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType3Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0012\u0012"; // "\u0012\u0012" characters should overlap.
        int[] expectedGlyphIndices = { 20, 20 };
        FontRectangle[] expectedFontRectangles =
        {
            new(475, 1185.9999F, 825, 719),
            new(575, 1085.9999F, 825, 719),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType4SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-4-mark-to-base-attachment-positioning-subtable
    [Test]
    public async Task MarkToBaseAttachmentPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType4Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0012\u0013"; // "\u0012\u0013" characters should overlap.
        int[] expectedGlyphIndices = { 20, 21 };
        FontRectangle[] expectedFontRectangles =
        {
            new(475, 1085.9999F, 825, 719),
            new(375, 1164.9999F, 825, 709),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType5SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-5-mark-to-ligature-attachment-positioning-subtable
    [Test]
    public async Task MarkToLigatureAttachmentPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType5Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0012\u0013"; // "\u0012\u0013" characters should overlap.
        int[] expectedGlyphIndices = { 20, 21 };
        FontRectangle[] expectedFontRectangles =
        {
            new(475, 1085.9999F, 825, 719),
            new(375, 1164.9999F, 825, 709),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType6SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-6-mark-to-mark-attachment-positioning-subtable
    [Test]
    public async Task MarkToMarkAttachmentPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType6Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0012\u0013"; // "\u0012\u0013" characters should overlap.
        int[] expectedGlyphIndices = { 20, 21 };
        FontRectangle[] expectedFontRectangles =
        {
            new(475, 1085.9999F, 825, 719),
            new(375, 1164.9999F, 825, 709),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType7SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-7-contextual-positioning-subtables
    [Test]
    public async Task ContextualPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType7Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0014\u0015\u0016"; // "\u0014\u0015\u0016" XPlacement plus 20.
        int[] expectedGlyphIndices = { 22, 23, 24 };
        FontRectangle[] expectedFontRectangles =
        {
            new(342, 1084.9999F, 978, 704),
            new(1842, 1085.9999F, 978, 708),
            new(3342, 1108.9999F, 978, 667),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType7SubTable, Format 2
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-7-contextual-positioning-subtables
    [Test]
    public async Task ContextualPositioning_Format2_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType7Format2;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0014\u0015\u0016"; // "\u0014\u0015\u0016" XPlacement plus 20.
        int[] expectedGlyphIndices = { 22, 23, 24 };
        FontRectangle[] expectedFontRectangles =
        {
            new(342, 1084.9999F, 978, 704),
            new(1842, 1085.9999F, 978, 708),
            new(3342, 1108.9999F, 978, 667),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType7SubTable, Format 3
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-7-contextual-positioning-subtables
    [Test]
    public async Task ContextualPositioning_Format3_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType7Format3;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();
        string testStr = "\u0014\u0015\u0016"; // "\u0014\u0015\u0016" XPlacement plus 20.
        int[] expectedGlyphIndices = { 22, 23, 24 };

        FontRectangle[] expectedFontRectangles =
        {
            new(342, 1084.9999F, 978, 704),
            new(1842, 1085.9999F, 978, 708),
            new(3342, 1108.9999F, 978, 667),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType8SubTable, Format 1
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookuptype-8-chained-contexts-positioning-subtable
    [Test]
    public async Task ChainedContextsPositioning_Format1_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType8Format1;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();

        // "\u0014\u0015\u0016\u0017" backtrack:\u0014, input:\u0015\u0016, lookahead:u0017 -> XPlacement plus 200.
        string testStr = "\u0014\u0015\u0016\u0017";
        int[] expectedGlyphIndices = { 22, 23, 24, 25 };
        FontRectangle[] expectedFontRectangles =
        {
            new(322, 1084.9999F, 978, 704),
            new(2022, 1085.9999F, 978, 708),
            new(3522, 1108.9999F, 978, 667),
            new(4822, 1104.9999F, 978, 671),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType8SubTable, Format 2
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookuptype-8-chained-contexts-positioning-subtable
    [Test]
    public async Task ChainedContextsPositioning_Format2_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType8Format2;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();

        // "\u0014\u0015\u0016\u0017" backtrack:\u0014, input:\u0015\u0016, lookahead:u0017 -> XPlacement plus 200.
        string testStr = "\u0014\u0015\u0016\u0017";
        int[] expectedGlyphIndices = { 22, 23, 24, 25 };
        FontRectangle[] expectedFontRectangles =
        {
            new(322, 1084.9999F, 978, 704),
            new(2022, 1085.9999F, 978, 708),
            new(3522, 1108.9999F, 978, 667),
            new(4822, 1104.9999F, 978, 671),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    // LookupType8SubTable, Format 3
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookuptype-8-chained-contexts-positioning-subtable
    [Test]
    public async Task ChainedContextsPositioning_Format3_Works()
    {
        // arrange
        string fontFile = TestFonts.GposLookupType8Format3;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();

        // "\u0014\u0015\u0016\u0017" backtrack:\u0014, input:\u0015\u0016, lookahead:u0017 -> XPlacement plus 200.
        string testStr = "\u0014\u0015\u0016\u0017";
        int[] expectedGlyphIndices = { 22, 23, 24, 25 };
        FontRectangle[] expectedFontRectangles =
        {
            new(322, 1084.9999F, 978, 704),
            new(2022, 1085.9999F, 978, 708),
            new(3522, 1108.9999F, 978, 667),
            new(4822, 1104.9999F, 978, 671),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    [Test]
    public async Task MarkAnchoring_Works()
    {
        // arrange
        string fontFile = TestFonts.TimesNewRomanFile;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();

        string testStr = "\u0644\u0651"; // /lam-arab/arabicshaddacomb
        int[] expectedGlyphIndices = { 759, 989 };
        FontRectangle[] expectedFontRectangles =
        {
            new(246, 23.5F, 363, 322),
            new(71, 84.5F, 966, 1573),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            // NET FX requires lower precision
            await CompareRectangle(expectedFontRectangles[i], renderer.GlyphRects[i], 3);
        }
    }

    [Test]
    public async Task MarkToMarkAttachment_Works()
    {
        // arrange
        string fontFile = TestFonts.MeQuranFile;
        ushort upem = ReadFontUpem(fontFile);
        Font font = new FontCollection().Add(fontFile).CreateFont(upem);
        var renderer = new ColorGlyphRenderer();

        string testStr = "\u0631\u0651\u064E";
        int[] expectedGlyphIndices = { 47, 50, 23 };
        FontRectangle[] expectedFontRectangles =
        {
            new(345, 182, 561, 461),
            new(420, 621, 447, 414),
            new(40, 1298, 1002, 986),
        };

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        await Assert.That(renderer.GlyphRects.Count).IsEqualTo(expectedFontRectangles.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }

        for (int i = 0; i < expectedFontRectangles.Length; i++)
        {
            await CompareRectangleExact(expectedFontRectangles[i], renderer.GlyphRects[i]);
        }
    }

    private static async Task CompareRectangle(FontRectangle expected, FontRectangle actual, int precision = 4)
    {
        await Assert.That(Math.Round(actual.X, precision)).IsEqualTo(Math.Round(expected.X, precision));
        await Assert.That(Math.Round(actual.Y, precision)).IsEqualTo(Math.Round(expected.Y, precision));
        await Assert.That(Math.Round(actual.Width, precision)).IsEqualTo(Math.Round(expected.Width, precision));
        await Assert.That(Math.Round(actual.Height, precision)).IsEqualTo(Math.Round(expected.Height, precision));
    }

    private static async Task CompareRectangleExact(FontRectangle expected, FontRectangle actual)
    {
        await Assert.That(actual.X).IsEqualTo(expected.X);
        await Assert.That(actual.Y).IsEqualTo(expected.Y);
        await Assert.That(actual.Width).IsEqualTo(expected.Width);
        await Assert.That(actual.Height).IsEqualTo(expected.Height);
    }

    private static ushort ReadFontUpem(string fileName)
    {
        // TODO: is there an easier way to read the UPEM? Maybe this is overcomplicated.
        FontFamily fontFamily = new FontCollection().Add(fileName);
        fontFamily.TryGetMetrics(FontStyle.Regular, out FontMetrics? metrics);

        // If no valid head table found, assume 1000, which matches typical Type1 usage.
        return metrics?.UnitsPerEm ?? 1000;
    }
}
