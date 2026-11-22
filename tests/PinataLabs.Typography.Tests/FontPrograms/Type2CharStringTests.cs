// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Evaluates hand-assembled Type 2 charstrings. fontTools does not run the arithmetic operators and
/// rejects malformed charstrings, so these cases have no fontTools reference.
/// </summary>
public class Type2CharStringTests
{
    private const string Rmoveto = "rmoveto";
    private const string Rlineto = "rlineto";
    private const string Hlineto = "hlineto";
    private const string Callsubr = "callsubr";
    private const string Endchar = "endchar";

    private static readonly Dictionary<string, byte[]> Operators = new()
    {
        ["hstem"] = [1],
        ["rlineto"] = [5],
        ["hlineto"] = [6],
        ["callsubr"] = [10],
        ["return"] = [11],
        ["endchar"] = [14],
        ["hintmask"] = [19],
        ["rmoveto"] = [21],
        ["and"] = [12, 3],
        ["or"] = [12, 4],
        ["not"] = [12, 5],
        ["abs"] = [12, 9],
        ["add"] = [12, 10],
        ["sub"] = [12, 11],
        ["div"] = [12, 12],
        ["neg"] = [12, 14],
        ["eq"] = [12, 15],
        ["drop"] = [12, 18],
        ["put"] = [12, 20],
        ["get"] = [12, 21],
        ["ifelse"] = [12, 22],
        ["random"] = [12, 23],
        ["mul"] = [12, 24],
        ["sqrt"] = [12, 26],
        ["dup"] = [12, 27],
        ["exch"] = [12, 28],
        ["index"] = [12, 29],
        ["roll"] = [12, 30],
        ["reserved"] = [12, 38],
    };

    [Test]
    public async Task WritesEachContourAsMoveToSegmentsClosePath()
    {
        string? outline = Evaluate(Assemble(10, 20, Rmoveto, 30, 0, Rlineto, 0, 40, Rlineto, 100, 100, Rmoveto, 5, Hlineto, Endchar));

        await Assert.That(outline).IsEqualTo("M 10 20 L 40 20 L 40 60 Z M 140 160 L 145 160 Z");
    }

    [Test]
    public async Task SubroutinesNestTenDeep()
    {
        byte[][] subrs = new byte[10][];
        for (int i = 0; i < subrs.Length - 1; i++)
        {
            subrs[i] = Assemble(i + 1 - 107, Callsubr, "return");
        }

        subrs[^1] = Assemble(7, Hlineto, "return");

        string? outline = Evaluate(Assemble(1, 2, Rmoveto, -107, Callsubr, Endchar), subrs);

        await Assert.That(outline).IsEqualTo("M 1 2 L 8 2 Z");
    }

    [Test]
    public async Task SubroutineThatCallsItselfFails()
    {
        string? outline = Evaluate(Assemble(1, 2, Rmoveto, -107, Callsubr, Endchar), Assemble(-107, Callsubr, "return"));

        await Assert.That(outline).IsNull();
    }

    [Test]
    [Arguments("a missing subroutine")]
    [Arguments("a truncated number")]
    [Arguments("too few arguments")]
    [Arguments("a reserved operator")]
    [Arguments("a hint mask past the end")]
    [Arguments("too many arguments")]
    public async Task MalformedCharStringFailsWithoutThrowing(string defect)
    {
        byte[] charString = defect switch
        {
            "a missing subroutine" => Assemble(5, Callsubr, Endchar),
            "a truncated number" => [28, 1],
            "too few arguments" => Assemble(1, Rmoveto, Endchar),
            "a reserved operator" => Assemble(1, 2, Rmoveto, "reserved", Endchar),
            "a hint mask past the end" => Assemble(1, 2, 3, 4, "hstem", "hintmask"),
            _ => Assemble([.. Repeat(1, 600), Endchar]),
        };

        await Assert.That(Evaluate(charString)).IsNull();
    }

    [Test]
    [Arguments(48, true)]
    [Arguments(50, false)]
    public async Task ArgumentStackHoldsTheType2LimitOf48(int argumentCount, bool succeeds)
    {
        string? outline = Evaluate(Assemble([0, 0, Rmoveto, .. Repeat(1, argumentCount), Rlineto, Endchar]));

        await Assert.That(outline is not null).IsEqualTo(succeeds);
    }

    [Test]
    public async Task OutlineOfMoreThanAMillionSegmentsFails()
    {
        // Each subroutine calls the next one four times, six levels deep. The last draws 480 lines,
        // so the glyph has about two million segments from about 200 bytes.
        byte[][] subrs = new byte[7][];
        for (int i = 0; i < 6; i++)
        {
            object[] call = [i + 1 - 107, Callsubr];
            subrs[i] = Assemble([.. call, .. call, .. call, .. call, "return"]);
        }

        List<object> lines = [];
        for (int i = 0; i < 20; i++)
        {
            lines.AddRange(Repeat(1, 48));
            lines.Add(Rlineto);
        }

        subrs[6] = Assemble([.. lines, "return"]);
        CffGlyphData glyph = new(0, default, BuildIndex(subrs), 0, Assemble(0, 0, Rmoveto, -107, Callsubr, Endchar));
        SegmentCounter counter = default;

        bool written = glyph.TryWriteOutline(ref counter);

        await Assert.That(written).IsFalse();
        await Assert.That(counter.Calls).IsLessThanOrEqualTo(1_000_000);
    }

    [Test]
    [Arguments("10 3 sub", 7)]
    [Arguments("20 4 div", 5)]
    [Arguments("0 not", 1)]
    [Arguments("5 not", 0)]
    [Arguments("1 0 and", 0)]
    [Arguments("1 2 and", 1)]
    [Arguments("1 0 or", 1)]
    [Arguments("-5 abs", 5)]
    [Arguments("5 neg", -5)]
    [Arguments("3 4 add", 7)]
    [Arguments("3 4 mul", 12)]
    [Arguments("9 sqrt", 3)]
    [Arguments("3 3 eq", 1)]
    [Arguments("3 4 eq", 0)]
    [Arguments("7 8 drop", 7)]
    [Arguments("4 dup add", 8)]
    [Arguments("1 2 exch sub", 1)]
    [Arguments("11 22 1 2 ifelse", 11)]
    [Arguments("11 22 3 2 ifelse", 22)]
    [Arguments("42 3 put 3 get 3 get add", 84)]
    [Arguments("5 6 1 index add add", 16)]
    [Arguments("5 6 0 index add add", 17)]
    [Arguments("5 6 -1 index add add", 17)]
    [Arguments("1 2 3 3 1 roll sub sub", 4)]
    [Arguments("1 2 3 3 -1 roll sub sub", 0)]
    [Arguments("1 2 3 3 4 roll sub sub", 4)]
    public async Task ArithmeticOperatorFollowsTheType2Definition(string expression, int expected)
    {
        List<object> tokens = [];
        foreach (string token in expression.Split(' '))
        {
            tokens.Add(int.TryParse(token, out int number) ? number : token);
        }

        string? outline = Evaluate(Assemble([.. tokens, 0, Rmoveto, 1, Hlineto, Endchar]));

        await Assert.That(outline).IsEqualTo($"M {expected} 0 L {expected + 1} 0 Z");
    }

    [Test]
    public async Task RandomGivesTheSameOutlineEachTime()
    {
        byte[] charString = Assemble("random", 100, "mul", "random", 100, "mul", Rmoveto, 5, Hlineto, Endchar);

        string? first = Evaluate(charString);

        await Assert.That(first).IsNotNull();
        await Assert.That(Evaluate(charString)).IsEqualTo(first);
    }

    private static object[] Repeat(object value, int count)
    {
        object[] tokens = new object[count];
        Array.Fill(tokens, value);
        return tokens;
    }

    /// <summary>
    /// Evaluates a charstring and returns its outline, or null when the evaluation fails.
    /// </summary>
    private static string? Evaluate(byte[] charString, params byte[][] localSubrs)
    {
        CffGlyphData glyph = new(0, default, BuildIndex(localSubrs), 0, charString);
        GlyphOutline outline = new();
        return glyph.TryWriteOutline(ref outline) ? outline.ToString() : null;
    }

    private static CffIndex BuildIndex(byte[][] items)
    {
        if (items.Length == 0)
        {
            return default;
        }

        // Card16 count, Card8 offSize, Offset16 offset[count + 1], data.
        List<byte> index = [(byte)(items.Length >> 8), (byte)items.Length, 2];
        int offset = 1;
        foreach (byte[] item in items)
        {
            index.Add((byte)(offset >> 8));
            index.Add((byte)offset);
            offset += item.Length;
        }

        index.Add((byte)(offset >> 8));
        index.Add((byte)offset);
        foreach (byte[] item in items)
        {
            index.AddRange(item);
        }

        return CffIndex.Read(index.ToArray(), 0, 2, out _);
    }

    /// <summary>
    /// Assembles a charstring from integers and operator names.
    /// </summary>
    private static byte[] Assemble(params object[] tokens)
    {
        List<byte> bytes = [];
        foreach (object token in tokens)
        {
            if (token is string name)
            {
                bytes.AddRange(Operators[name]);
            }
            else if (token is int value && value is >= -107 and <= 107)
            {
                bytes.Add((byte)(value + 139));
            }
            else
            {
                short number = checked((short)(int)token);
                bytes.Add(28);
                bytes.Add((byte)(number >> 8));
                bytes.Add((byte)number);
            }
        }

        return bytes.ToArray();
    }

    private struct SegmentCounter : IGlyphOutlineSink
    {
        public int Calls { get; private set; }

        public void MoveTo(Vector2 point) => this.Calls++;

        public void LineTo(Vector2 point) => this.Calls++;

        public void QuadraticBezierTo(Vector2 control, Vector2 point) => this.Calls++;

        public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point) => this.Calls++;

        public void ClosePath() => this.Calls++;
    }
}
