// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

public class Type1ProgramTests
{
    private const string Square = "M 10 0 L 10 100 L 110 100 Z";

    [Test]
    public async Task EveryContainerLoadsTheSameFont()
    {
        Type1FontBuilder builder = SquareFont();
        byte[][] containers =
        [
            builder.BuildPfa(hex: true),
            builder.BuildPfa(hex: true, trailer: false),
            builder.BuildPfa(hex: false),
            builder.BuildPfa(hex: false, trailer: false),
            builder.BuildPfb(),
            builder.BuildPfb(clearTextSegments: 3, binarySegmentLength: 50),
        ];

        foreach (byte[] container in containers)
        {
            Type1Program font = Type1TestFonts.Load(container);

            await Assert.That(font.GlyphCount).IsEqualTo(2);
            await Assert.That(Type1CharStringEngineTests.Outline(font, "A")).IsEqualTo(Square);
        }
    }

    [Test]
    public async Task APfbWhoseEexecSectionIsHexadecimalTextLoads()
    {
        byte[] pfa = SquareFont().BuildPfa(hex: true);
        byte[] pfb = [0x80, 1, (byte)pfa.Length, (byte)(pfa.Length >> 8), 0, 0, .. pfa, 0x80, 3];

        await Assert.That(Type1CharStringEngineTests.Outline(Type1TestFonts.Load(pfb), "A")).IsEqualTo(Square);
    }

    [Test]
    [Arguments(null)]
    [Arguments(4)]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(-1)]
    public async Task CharStringsDecryptWithAnyLenIV(int? lenIV)
    {
        Type1FontBuilder builder = SquareFont();
        builder.LenIV = lenIV;
        builder.AddSubr("100 vlineto return");
        builder.Glyphs[1] = ("A", Type1FontBuilder.CharString("10 500 hsbw 0 callsubr 100 hlineto closepath endchar"));

        await Assert.That(Type1CharStringEngineTests.Outline(builder.Load(), "A")).IsEqualTo(Square);
    }

    [Test]
    [Arguments("-|", "|-", "|")]
    [Arguments("readstring", "noaccessdef", "noaccessput")]
    public async Task TheFontChoosesTheNamesOfItsReadAndDefineProcedures(string rd, string nd, string np)
    {
        Type1FontBuilder builder = SquareFont();
        builder.RD = rd;
        builder.ND = nd;
        builder.NP = np;
        builder.AddSubr("return");

        await Assert.That(Type1CharStringEngineTests.Outline(builder.Load(), "A")).IsEqualTo(Square);
    }

    [Test]
    public async Task NotdefIsGlyph0AndTheOtherGlyphsKeepTheirOrder()
    {
        Type1Program font = new Type1FontBuilder()
            .AddGlyph("B", "0 500 hsbw endchar")
            .AddGlyph("A", "0 500 hsbw endchar")
            .AddGlyph(".notdef", "0 250 hsbw endchar")
            .AddGlyph("C", "0 500 hsbw endchar")
            .Load();

        await Assert.That(Names(font)).IsEqualTo(".notdef B A C");
        await Assert.That(font.TryGetAdvanceWidth(0, out float advance)).IsTrue();
        await Assert.That(advance).IsEqualTo(250F);
    }

    [Test]
    public async Task AFontWithoutNotdefGetsAnEmptyOne()
    {
        Type1Program font = new Type1FontBuilder()
            .AddGlyph("B", "0 500 hsbw endchar")
            .AddGlyph("A", "0 500 hsbw endchar")
            .Load();
        OutlineRecorder recorder = new();

        await Assert.That(Names(font)).IsEqualTo(".notdef B A");
        await Assert.That(font.TryGetGlyphId(".notdef", out ushort glyphId)).IsTrue();
        await Assert.That(glyphId).IsEqualTo((ushort)0);
        await Assert.That(font.TryWriteOutline(0, ref recorder)).IsTrue();
        await Assert.That(recorder.Commands).IsEmpty();
        await Assert.That(font.TryGetAdvanceWidth(0, out float advance)).IsTrue();
        await Assert.That(advance).IsEqualTo(0F);
    }

    [Test]
    public async Task GlyphLookupsRejectWhatTheFontDoesNotHave()
    {
        Type1Program font = SquareFont().Load();
        OutlineRecorder recorder = new();

        await Assert.That(font.TryGetGlyphId("B", out _)).IsFalse();
        await Assert.That(font.GetGlyphName(2)).IsNull();
        await Assert.That(font.TryWriteOutline(2, ref recorder)).IsFalse();
        await Assert.That(font.TryGetAdvanceWidth(2, out _)).IsFalse();
    }

    [Test]
    public async Task TheCleartextGivesTheFontMatrixTheBBoxAndThePaintType()
    {
        Type1FontBuilder builder = SquareFont();
        builder.ClearTextBody = "/FontName /Other def /PaintType 2 def /FontMatrix [.0005 0 1e-4 0.0005 1 -2] readonly def\n/FontBBox {-1 -2 3.5 4} readonly def\n";
        Type1Program font = builder.Load();

        await Assert.That(font.PaintType).IsEqualTo(2);
        await Assert.That(font.FontMatrix).IsEqualTo(new Matrix3x2(0.0005F, 0, 0.0001F, 0.0005F, 1, -2));
        await Assert.That(font.FontBBox).IsEqualTo(new Bounds(-1, -2, 3.5F, 4));
        await Assert.That(font.TryGetBuiltInEncoding(new ushort[256])).IsFalse();
    }

    [Test]
    public async Task AFontWithoutAFontMatrixUsesOneThousandUnitsPerEm()
    {
        Type1FontBuilder builder = SquareFont();
        builder.ClearTextBody = "/FontName /Other def\n";

        await Assert.That(builder.Load().FontMatrix).IsEqualTo(Matrix3x2.CreateScale(0.001F));
    }

    [Test]
    [Arguments("/Encoding StandardEncoding def\n", 65)]
    [Arguments("/Encoding 256 array\n0 1 255 {1 index exch /.notdef put} for\ndup 7 /A put\ndup 300 /A put\ndup 9 /missing put\nreadonly def\n", 7)]
    [Arguments("/Encoding [ /.notdef /.notdef /A ] def\n", 2)]
    public async Task TheBuiltInEncodingMapsCodesToGlyphs(string encoding, int codeOfA)
    {
        Type1FontBuilder builder = SquareFont();
        builder.ClearTextBody = encoding;
        ushort[] glyphIds = new ushort[256];

        await Assert.That(builder.Load().TryGetBuiltInEncoding(glyphIds)).IsTrue();
        await Assert.That(glyphIds[codeOfA]).IsEqualTo((ushort)1);
        await Assert.That(glyphIds.Count(x => x != 0)).IsEqualTo(1);
    }

    [Test]
    public async Task TheBuiltInEncodingNeeds256GlyphIds()
        => await Assert.That(() => SquareFont().Load().TryGetBuiltInEncoding(new ushort[255])).Throws<ArgumentException>();

    [Test]
    public async Task TheBudgetIsChargedTheBufferAndTheArrays()
    {
        Type1FontBuilder builder = SquareFont();
        builder.AddSubr("return");
        builder.AddSubr("return");
        builder.AddSubr("return");
        const int glyphs = 2;
        const int subrs = 3;

        // Eight arrays: the buffer, two for each of the two charstring tables, the name offsets and lengths, and the encoding.
        long expected = (8 * LoadBudget.ArrayOverhead)
            + builder.BuildPrivate().Length
            + (glyphs * ((2 * sizeof(int)) + sizeof(int) + sizeof(byte)))
            + (subrs * 2 * sizeof(int))
            + (256 * sizeof(ushort));

        foreach (byte[] container in (byte[][])[builder.BuildPfa(), builder.BuildPfa(hex: false), builder.BuildPfb()])
        {
            LoadBudget budget = new(0, null);
            Type1Program.Load(container, ref budget);
            await Assert.That(budget.RetainedBytes).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task TheSniffAcceptsPfbAndPostScriptAndNothingElse()
    {
        Type1FontBuilder builder = SquareFont();

        await Assert.That(Type1Program.IsType1(builder.BuildPfb())).IsTrue();
        await Assert.That(Type1Program.IsType1(builder.BuildPfa())).IsTrue();
        await Assert.That(Type1Program.IsType1([0, 1, 0, 0, 0, 12])).IsFalse();
        await Assert.That(Type1Program.IsType1("OTTO"u8)).IsFalse();
        await Assert.That(Type1Program.IsType1([])).IsFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("%!PS-AdobeFont-1.0\n/FontName /X def\n")]
    [Arguments("%!PS-AdobeFont-1.0\ncurrentfile eexec\n")]
    [Arguments("%!PS-AdobeFont-1.0\ncurrentfile eexec\nabc")]
    [Arguments("%!PS-AdobeFont-1.0\ncurrentfile eexec\n0123456789abcdef0123456789abcdef")]
    [Arguments("%!PS-AdobeFont-1.0\n/FontMatrix [0.001 0 0 oops 0 0] def currentfile eexec\n0123456789abcdef")]
    public async Task DataThatIsNotATypeOneFontIsRejected(string text)
        => await Assert.That(() => Type1TestFonts.Load(Encoding.Latin1.GetBytes(text))).Throws<InvalidFontFileException>();

    [Test]
    public async Task AFontWithoutGlyphsIsRejected()
        => await Assert.That(() => new Type1FontBuilder().Load()).Throws<InvalidFontFileException>();

    private static Type1FontBuilder SquareFont()
        => new Type1FontBuilder()
            .AddGlyph(".notdef", "0 500 hsbw endchar")
            .AddGlyph("A", "10 500 hsbw 100 vlineto 100 hlineto closepath endchar");

    private static string Names(Type1Program font)
        => string.Join(" ", Enumerable.Range(0, font.GlyphCount).Select(x => font.GetGlyphName((ushort)x)));
}
