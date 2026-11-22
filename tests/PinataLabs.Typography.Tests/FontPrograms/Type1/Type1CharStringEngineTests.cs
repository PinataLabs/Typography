// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

public class Type1CharStringEngineTests
{
    [Test]
    public async Task PathOperatorsWriteContoursInGlyphSpace()
    {
        const string program =
            "30 500 hsbw 10 20 rmoveto 5 hlineto 7 vlineto 1 2 rlineto 1 2 3 4 5 6 rrcurveto closepath "
            + "100 hmoveto 1 2 3 4 vhcurveto 1 2 3 4 hvcurveto closepath 50 vmoveto 9 hlineto endchar";

        await Assert.That(Outline(program)).IsEqualTo(
            "M 40 20 L 45 20 L 45 27 L 46 29 C 47 31 50 35 55 41 Z "
            + "M 155 41 C 155 42 157 45 161 45 C 162 45 164 48 164 52 Z "
            + "M 164 102 L 173 102 Z");
        await Assert.That(Advance(program)).IsEqualTo(500F);
    }

    [Test]
    public async Task MoveThatDrawsNothingWritesNothing()
        => await Assert.That(Outline("0 250 hsbw 10 10 rmoveto closepath 5 5 rmoveto endchar")).IsEqualTo(string.Empty);

    [Test]
    public async Task SbwSetsBothSideBearingsAndTheAdvance()
    {
        const string program = "10 20 300 0 sbw 5 5 rmoveto 10 hlineto 10 vlineto closepath endchar";

        await Assert.That(Outline(program)).IsEqualTo("M 15 25 L 25 25 L 25 35 Z");
        await Assert.That(Advance(program)).IsEqualTo(300F);
    }

    [Test]
    public async Task DivGivesFractions()
    {
        const string program = "1 2 div 1001 2 div hsbw 3 4 div vmoveto 1 hlineto closepath endchar";

        await Assert.That(Outline(program)).IsEqualTo("M 0.5 0.75 L 1.5 0.75 Z");
        await Assert.That(Advance(program)).IsEqualTo(500.5F);
    }

    [Test]
    public async Task HintOperatorsAreIgnored()
        => await Assert.That(Outline(
            "0 500 hsbw 0 10 hstem 0 10 vstem 0 1 2 3 4 5 hstem3 0 1 2 3 4 5 vstem3 dotsection 1 1 rmoveto 2 hlineto closepath endchar"))
            .IsEqualTo("M 1 1 L 3 1 Z");

    [Test]
    public async Task FlexWritesTwoCurvesAndEndsAtTheStatedPoint()
    {
        const string program =
            "0 500 hsbw 100 100 rmoveto 0 1 callothersubr "
            + "50 0 rmoveto 0 2 callothersubr -40 2 rmoveto 0 2 callothersubr 20 3 rmoveto 0 2 callothersubr "
            + "20 hmoveto 0 2 callothersubr 20 0 rmoveto 0 2 callothersubr 20 -3 rmoveto 0 2 callothersubr "
            + "10 -2 rmoveto 0 2 callothersubr 50 200 100 3 0 callothersubr pop pop setcurrentpoint "
            + "10 hlineto closepath endchar";

        await Assert.That(Outline(program))
            .IsEqualTo("M 100 100 C 110 102 130 105 150 105 C 170 105 190 102 200 100 L 210 100 Z");
    }

    [Test]
    public async Task HintReplacementCallsTheSubroutineThatPopReturns()
    {
        Type1FontBuilder builder = new();
        builder.AddSubr("3 0 callothersubr pop pop setcurrentpoint return");
        builder.AddSubr("0 1 callothersubr return");
        builder.AddSubr("0 2 callothersubr return");
        builder.AddSubr("return");
        builder.AddSubr("1 3 callothersubr pop callsubr return");
        builder.AddSubr("0 10 hstem return");
        builder.AddGlyph(".notdef", "0 500 hsbw endchar");
        builder.AddGlyph("a", "0 500 hsbw 5 4 callsubr 1 1 rmoveto 2 hlineto closepath endchar");

        await Assert.That(Outline(builder.Load(), "a")).IsEqualTo("M 1 1 L 3 1 Z");
    }

    [Test]
    public async Task UnknownOtherSubrReturnsItsArgumentsInOrder()
        => await Assert.That(Outline("0 500 hsbw 7 9 2 99 callothersubr pop pop rlineto closepath endchar"))
            .IsEqualTo("M 0 0 L 7 9 Z");

    [Test]
    public async Task SeacPlacesTheAccentRelativeToTheSideBearingOfTheComposite()
    {
        Type1FontBuilder builder = SeacFont("70 600 hsbw 20 200 500 65 194 seac");
        Type1Program font = builder.Load();

        // The base stays at its own origin. The accent origin is at 70 + 200 - 20 = 250, so the
        // accent, whose own side bearing is 20, starts at x = 270.
        await Assert.That(Outline(font, "Aacute"))
            .IsEqualTo("M 50 0 L 50 100 L 150 100 Z M 270 500 L 270 510 L 280 510 Z");
        font.TryGetGlyphId("Aacute", out ushort glyphId);
        await Assert.That(font.TryGetAdvanceWidth(glyphId, out float advance)).IsTrue();
        await Assert.That(advance).IsEqualTo(600F);
    }

    [Test]
    [Arguments("70 600 hsbw 20 200 500 65 193 seac")]
    [Arguments("70 600 hsbw 20 200 500 65 300 seac")]
    [Arguments("70 600 hsbw 20 200 500 65 202 seac")]
    public async Task SeacFailsForAMissingOrCompositeComponent(string program)
        => await Assert.That(Outline(SeacFont(program).Load(), "Aacute")).IsNull();

    [Test]
    [Arguments("")]
    [Arguments("0 500 hsbw 1 1 rmoveto 2 hlineto closepath")]
    [Arguments("0 500 hsbw 1 rmoveto endchar")]
    [Arguments("0 500 hsbw 1 0 div hmoveto endchar")]
    [Arguments("0 500 hsbw pop endchar")]
    [Arguments("0 500 hsbw 3 callsubr endchar")]
    [Arguments("0 500 hsbw 1 1 rmoveto 0 2 callothersubr endchar")]
    [Arguments("0 500 hsbw 1 1 rmoveto 0 1 callothersubr 0 0 0 3 0 callothersubr endchar")]
    [Arguments("0 500 hsbw 5 9 callothersubr endchar")]
    [Arguments("0 500 hsbw return")]
    public async Task MalformedCharStringsWriteNoCompleteOutline(string program)
        => await Assert.That(Outline(program)).IsNull();

    [Test]
    public async Task UnknownOperatorsAndTruncatedNumbersFail()
    {
        byte[] hsbw = Type1FontBuilder.CharString("0 500 hsbw");
        foreach (byte[] tail in new byte[][] { [0], [2], [15], [12, 99], [12], [247], [255, 0, 0] })
        {
            Type1FontBuilder builder = new();
            builder.Glyphs.Add((".notdef", [.. hsbw, .. tail, 14]));
            await Assert.That(Outline(builder.Load(), ".notdef")).IsNull();
        }
    }

    [Test]
    public async Task TheOperandStackHolds24Numbers()
    {
        string numbers = string.Join(" ", Enumerable.Repeat("1", 24));

        await Assert.That(Outline($"0 500 hsbw {numbers} hstem endchar")).IsEqualTo(string.Empty);
        await Assert.That(Outline($"0 500 hsbw {numbers} 1 hstem endchar")).IsNull();
    }

    [Test]
    [Arguments(10, true)]
    [Arguments(11, false)]
    public async Task SubroutinesNestTenDeep(int depth, bool succeeds)
    {
        Type1FontBuilder builder = new();
        for (int i = 1; i < depth; i++)
        {
            builder.AddSubr($"{i} callsubr return");
        }

        builder.AddSubr("return");
        builder.AddGlyph(".notdef", "0 500 hsbw 0 callsubr endchar");

        await Assert.That(Outline(builder.Load(), ".notdef") is not null).IsEqualTo(succeeds);
    }

    [Test]
    public async Task ASubroutineThatCallsItselfFails()
    {
        Type1FontBuilder builder = new();
        builder.AddSubr("0 callsubr return");
        builder.AddGlyph(".notdef", "0 500 hsbw 0 callsubr endchar");

        await Assert.That(Outline(builder.Load(), ".notdef")).IsNull();
    }

    [Test]
    [Arguments(3, true)]
    [Arguments(4, false)]
    public async Task AGlyphRunsAtMost100000Operators(int levels, bool succeeds)
    {
        // Each level calls the next 40 times: 3 levels run about 66,000 operators, 4 levels 2.6 million.
        Type1FontBuilder builder = new();
        for (int i = 1; i < levels; i++)
        {
            builder.AddSubr(string.Join(" ", Enumerable.Repeat($"{i} callsubr", 40)) + " return");
        }

        builder.AddSubr(string.Join(" ", Enumerable.Repeat("dotsection", 40)) + " return");
        builder.AddGlyph(".notdef", "0 500 hsbw 0 callsubr endchar");

        await Assert.That(Outline(builder.Load(), ".notdef") is not null).IsEqualTo(succeeds);
    }

    [Test]
    public async Task AGlyphWithoutASideBearingOperatorHasNoAdvance()
    {
        Type1Program font = new Type1FontBuilder().AddGlyph(".notdef", "1 1 rmoveto 2 hlineto closepath endchar").Load();

        await Assert.That(font.TryGetAdvanceWidth(0, out _)).IsFalse();
        await Assert.That(Outline(font, ".notdef")).IsEqualTo("M 1 1 L 3 1 Z");
    }

    internal static string? Outline(Type1Program font, string glyphName)
    {
        GlyphOutline outline = new();
        return font.TryGetGlyphId(glyphName, out ushort glyphId) && font.TryWriteOutline(glyphId, ref outline)
            ? outline.ToString()
            : null;
    }

    private static string? Outline(string program)
        => Outline(new Type1FontBuilder().AddGlyph(".notdef", program).Load(), ".notdef");

    private static float? Advance(string program)
        => new Type1FontBuilder().AddGlyph(".notdef", program).Load().TryGetAdvanceWidth(0, out float advance) ? advance : null;

    private static Type1FontBuilder SeacFont(string composite)
        => new Type1FontBuilder()
            .AddGlyph(".notdef", "0 500 hsbw endchar")
            .AddGlyph("A", "50 600 hsbw 100 vlineto 100 hlineto closepath endchar")
            .AddGlyph("acute", "20 300 hsbw 10 vlineto 10 hlineto closepath endchar")
            .AddGlyph("ring", "70 600 hsbw 20 200 500 65 194 seac")
            .AddGlyph("Aacute", composite);
}
