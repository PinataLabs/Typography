// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// Interprets one Type 1 charstring and writes its unhinted outline, in glyph space with y up, to a sink.
/// It returns <see langword="false"/> for malformed data and never throws for it.
/// </summary>
/// <remarks>
/// Limits: 24 operands, 10 nested subroutine calls, 100,000 operators per glyph (the components of a
/// <c>seac</c> glyph included), and no <c>seac</c> inside a <c>seac</c> component.
/// </remarks>
internal ref struct Type1CharStringEngine
{
    /// <summary>
    /// The length of the scratch span that the caller allocates on its stack.
    /// </summary>
    public const int ScratchLength = MaxStack + MaxStack + (FlexPointCount * 2);

    private const int MaxStack = 24;
    private const int MaxSubrDepth = 10;
    private const int MaxOperators = 100_000;

    // A flex is the reference point and then the six points of two curves.
    private const int FlexPointCount = 7;

    private const int HStem = 1;
    private const int VStem = 3;
    private const int VMoveTo = 4;
    private const int RLineTo = 5;
    private const int HLineTo = 6;
    private const int VLineTo = 7;
    private const int RRCurveTo = 8;
    private const int ClosePath = 9;
    private const int CallSubr = 10;
    private const int Return = 11;
    private const int Escape = 12;
    private const int Hsbw = 13;
    private const int EndChar = 14;
    private const int RMoveTo = 21;
    private const int HMoveTo = 22;
    private const int VHCurveTo = 30;
    private const int HVCurveTo = 31;

    private const int DotSection = 0;
    private const int VStem3 = 1;
    private const int HStem3 = 2;
    private const int Seac = 6;
    private const int Sbw = 7;
    private const int Div = 12;
    private const int CallOtherSubr = 16;
    private const int Pop = 17;
    private const int SetCurrentPoint = 33;

    private const int OtherSubrFlexEnd = 0;
    private const int OtherSubrFlexStart = 1;
    private const int OtherSubrFlexPoint = 2;

    private readonly Type1Program program;
    private readonly Span<double> stack;

    // What an OtherSubrs procedure left on the PostScript stack, top last.
    private readonly Span<double> results;
    private readonly Span<double> flexPoints;
    private int stackCount;
    private int resultCount;
    private int flexCount;
    private bool flexing;
    private double x;
    private double y;
    private double originX;
    private double originY;
    private double sideBearingX;
    private float advanceWidth;
    private bool hasWidth;
    private bool widthOnly;
    private bool inComponent;
    private bool contourOpen;
    private int operatorCount;

    public Type1CharStringEngine(Type1Program program, Span<double> scratch)
    {
        this = default;
        this.program = program;
        this.stack = scratch.Slice(0, MaxStack);
        this.results = scratch.Slice(MaxStack, MaxStack);
        this.flexPoints = scratch.Slice(MaxStack + MaxStack, FlexPointCount * 2);
    }

    private enum Status
    {
        /// <summary>The charstring or subroutine ran to its end without ending the glyph.</summary>
        Continue,

        /// <summary>The glyph ended, or the advance width is known and nothing else was asked for.</summary>
        Done,

        /// <summary>The data is malformed or exceeds a limit.</summary>
        Failed,
    }

    private readonly Vector2 Point => new((float)this.x, (float)this.y);

    public bool TryWriteOutline<TSink>(ReadOnlySpan<byte> charString, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.Execute(charString, 0, ref sink) != Status.Done)
        {
            return false;
        }

        this.CloseContour(ref sink);
        return true;
    }

    public bool TryGetAdvanceWidth(ReadOnlySpan<byte> charString, out float advanceWidth)
    {
        this.widthOnly = true;
        NullGlyphOutlineSink sink = default;
        Status status = this.Execute(charString, 0, ref sink);
        advanceWidth = this.advanceWidth;
        return status == Status.Done && this.hasWidth;
    }

    private static bool TryReadNumber(ReadOnlySpan<byte> code, int b0, ref int position, out int value)
    {
        if (b0 <= 246)
        {
            value = b0 - 139;
            return true;
        }

        if (b0 <= 254)
        {
            if (position >= code.Length)
            {
                value = 0;
                return false;
            }

            int b1 = code[position++];
            value = b0 <= 250
                ? ((b0 - 247) * 256) + b1 + 108
                : -((b0 - 251) * 256) - b1 - 108;
            return true;
        }

        if (position > code.Length - 4)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32BigEndian(code.Slice(position, 4));
        position += 4;
        return true;
    }

    private static bool TryGetInt(double value, out int result)
    {
        if (value is >= int.MinValue and <= int.MaxValue)
        {
            result = (int)value;
            return result == value;
        }

        result = 0;
        return false;
    }

    private Status Execute<TSink>(ReadOnlySpan<byte> code, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        int position = 0;
        while (position < code.Length)
        {
            int b0 = code[position++];
            if (b0 >= 32)
            {
                if (!TryReadNumber(code, b0, ref position, out int number) || this.stackCount == MaxStack)
                {
                    return Status.Failed;
                }

                this.stack[this.stackCount++] = number;
                continue;
            }

            if (++this.operatorCount > MaxOperators)
            {
                return Status.Failed;
            }

            if (b0 == Return)
            {
                return Status.Continue;
            }

            Status status;
            if (b0 != Escape)
            {
                status = this.ExecuteOperator(b0, depth, ref sink);
            }
            else if (position < code.Length)
            {
                status = this.ExecuteEscapedOperator(code[position++], ref sink);
            }
            else
            {
                status = Status.Failed;
            }

            if (status != Status.Continue)
            {
                return status;
            }
        }

        return Status.Continue;
    }

    private Status ExecuteOperator<TSink>(int op, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        ReadOnlySpan<double> a;
        switch (op)
        {
            case HStem:
            case VStem:
                this.stackCount = 0;
                return Status.Continue;

            case Hsbw:
                if (!this.TryTakeArguments(2, out a))
                {
                    return Status.Failed;
                }

                return this.SetSideBearing(a[0], 0, a[1]);

            case RMoveTo:
                if (!this.TryTakeArguments(2, out a))
                {
                    return Status.Failed;
                }

                this.MoveBy(a[0], a[1], ref sink);
                return Status.Continue;

            case HMoveTo:
                if (!this.TryTakeArguments(1, out a))
                {
                    return Status.Failed;
                }

                this.MoveBy(a[0], 0, ref sink);
                return Status.Continue;

            case VMoveTo:
                if (!this.TryTakeArguments(1, out a))
                {
                    return Status.Failed;
                }

                this.MoveBy(0, a[0], ref sink);
                return Status.Continue;

            case RLineTo:
                if (!this.TryTakeArguments(2, out a))
                {
                    return Status.Failed;
                }

                this.LineBy(a[0], a[1], ref sink);
                return Status.Continue;

            case HLineTo:
                if (!this.TryTakeArguments(1, out a))
                {
                    return Status.Failed;
                }

                this.LineBy(a[0], 0, ref sink);
                return Status.Continue;

            case VLineTo:
                if (!this.TryTakeArguments(1, out a))
                {
                    return Status.Failed;
                }

                this.LineBy(0, a[0], ref sink);
                return Status.Continue;

            case RRCurveTo:
                if (!this.TryTakeArguments(6, out a))
                {
                    return Status.Failed;
                }

                this.CurveBy(a[0], a[1], a[2], a[3], a[4], a[5], ref sink);
                return Status.Continue;

            case VHCurveTo:
                if (!this.TryTakeArguments(4, out a))
                {
                    return Status.Failed;
                }

                this.CurveBy(0, a[0], a[1], a[2], a[3], 0, ref sink);
                return Status.Continue;

            case HVCurveTo:
                if (!this.TryTakeArguments(4, out a))
                {
                    return Status.Failed;
                }

                this.CurveBy(a[0], 0, a[1], a[2], 0, a[3], ref sink);
                return Status.Continue;

            case ClosePath:
                this.stackCount = 0;
                this.CloseContour(ref sink);
                return Status.Continue;

            case CallSubr:
                if (this.stackCount == 0
                    || depth == MaxSubrDepth
                    || !TryGetInt(this.stack[--this.stackCount], out int index)
                    || !this.program.TryGetSubr(index, out ReadOnlySpan<byte> subr))
                {
                    return Status.Failed;
                }

                return this.Execute(subr, depth + 1, ref sink);

            case EndChar:
                return Status.Done;

            default:
                return Status.Failed;
        }
    }

    private Status ExecuteEscapedOperator<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        ReadOnlySpan<double> a;
        switch (op)
        {
            case DotSection:
            case VStem3:
            case HStem3:
                this.stackCount = 0;
                return Status.Continue;

            case Sbw:
                if (!this.TryTakeArguments(4, out a))
                {
                    return Status.Failed;
                }

                return this.SetSideBearing(a[0], a[1], a[2]);

            case Seac:
                return this.ExecuteSeac(ref sink);

            case Div:
                if (this.stackCount < 2 || this.stack[this.stackCount - 1] == 0)
                {
                    return Status.Failed;
                }

                this.stackCount--;
                this.stack[this.stackCount - 1] /= this.stack[this.stackCount];
                return Status.Continue;

            case CallOtherSubr:
                return this.ExecuteOtherSubr(ref sink);

            case Pop:
                if (this.resultCount == 0 || this.stackCount == MaxStack)
                {
                    return Status.Failed;
                }

                this.stack[this.stackCount++] = this.results[--this.resultCount];
                return Status.Continue;

            case SetCurrentPoint:
                if (!this.TryTakeArguments(2, out a))
                {
                    return Status.Failed;
                }

                this.x = this.originX + a[0];
                this.y = this.originY + a[1];
                return Status.Continue;

            default:
                return Status.Failed;
        }
    }

    private Status ExecuteOtherSubr<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.stackCount < 2
            || !TryGetInt(this.stack[this.stackCount - 1], out int number)
            || !TryGetInt(this.stack[this.stackCount - 2], out int count)
            || count < 0
            || count > this.stackCount - 2)
        {
            return Status.Failed;
        }

        this.stackCount -= count + 2;
        ReadOnlySpan<double> arguments = this.stack.Slice(this.stackCount, count);
        this.resultCount = 0;
        switch (number)
        {
            case OtherSubrFlexStart:
                this.OpenContour(ref sink);
                this.flexing = true;
                this.flexCount = 0;
                return Status.Continue;

            case OtherSubrFlexPoint:
                if (!this.flexing || this.flexCount == FlexPointCount)
                {
                    return Status.Failed;
                }

                this.flexPoints[this.flexCount * 2] = this.x;
                this.flexPoints[(this.flexCount * 2) + 1] = this.y;
                this.flexCount++;
                return Status.Continue;

            case OtherSubrFlexEnd:
                if (!this.flexing || !this.contourOpen || this.flexCount != FlexPointCount || count != 3)
                {
                    return Status.Failed;
                }

                this.flexing = false;
                ReadOnlySpan<double> p = this.flexPoints;
                sink.CubicBezierTo(
                    new Vector2((float)p[2], (float)p[3]),
                    new Vector2((float)p[4], (float)p[5]),
                    new Vector2((float)p[6], (float)p[7]));
                sink.CubicBezierTo(
                    new Vector2((float)p[8], (float)p[9]),
                    new Vector2((float)p[10], (float)p[11]),
                    new Vector2((float)p[12], (float)p[13]));

                // The procedure leaves the end point for the font's own "pop pop setcurrentpoint", x on top.
                this.results[0] = arguments[2];
                this.results[1] = arguments[1];
                this.resultCount = 2;
                return Status.Continue;

            default:
                // The Type 1 format pushes the arguments on the PostScript stack last first, so that
                // each pop gives them back in their original order. Hint replacement (3) needs only this.
                for (int i = 0; i < count; i++)
                {
                    this.results[i] = arguments[count - 1 - i];
                }

                this.resultCount = count;
                return Status.Continue;
        }
    }

    private Status ExecuteSeac<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.widthOnly)
        {
            return Status.Done;
        }

        if (this.inComponent
            || !this.TryTakeArguments(5, out ReadOnlySpan<double> a)
            || !TryGetInt(a[3], out int baseCode)
            || !TryGetInt(a[4], out int accentCode)
            || !this.program.TryGetStandardEncodingCharString(baseCode, out ReadOnlySpan<byte> baseGlyph)
            || !this.program.TryGetStandardEncodingCharString(accentCode, out ReadOnlySpan<byte> accentGlyph))
        {
            return Status.Failed;
        }

        // The accent origin is at adx - asb from the left sidebearing point of the composite, not from
        // its origin. FreeType and fontTools both place it there.
        double accentX = this.sideBearingX + a[1] - a[0];
        double accentY = a[2];

        this.inComponent = true;
        if (this.ExecuteComponent(baseGlyph, 0, 0, ref sink) != Status.Done)
        {
            return Status.Failed;
        }

        return this.ExecuteComponent(accentGlyph, accentX, accentY, ref sink) == Status.Done
            ? Status.Done
            : Status.Failed;
    }

    private Status ExecuteComponent<TSink>(ReadOnlySpan<byte> charString, double offsetX, double offsetY, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        this.CloseContour(ref sink);
        this.stackCount = 0;
        this.resultCount = 0;
        this.flexing = false;
        this.originX = offsetX;
        this.originY = offsetY;
        this.x = offsetX;
        this.y = offsetY;
        return this.Execute(charString, 0, ref sink);
    }

    private Status SetSideBearing(double sideBearingX, double sideBearingY, double width)
    {
        // A seac component keeps the composite's advance width.
        if (!this.inComponent)
        {
            this.sideBearingX = sideBearingX;
            this.advanceWidth = (float)width;
            this.hasWidth = true;
            if (this.widthOnly)
            {
                return Status.Done;
            }
        }

        this.x = this.originX + sideBearingX;
        this.y = this.originY + sideBearingY;
        return Status.Continue;
    }

    /// <summary>
    /// Takes the top <paramref name="count"/> operands and clears the stack, as every path and hint operator does.
    /// </summary>
    private bool TryTakeArguments(int count, out ReadOnlySpan<double> arguments)
    {
        if (this.stackCount < count)
        {
            arguments = default;
            return false;
        }

        arguments = this.stack.Slice(this.stackCount - count, count);
        this.stackCount = 0;
        return true;
    }

    private void MoveBy<TSink>(double dx, double dy, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        // Inside a flex the moves only place the flex points.
        if (!this.flexing)
        {
            this.CloseContour(ref sink);
        }

        this.x += dx;
        this.y += dy;
    }

    private void LineBy<TSink>(double dx, double dy, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        this.OpenContour(ref sink);
        this.x += dx;
        this.y += dy;
        sink.LineTo(this.Point);
    }

    private void CurveBy<TSink>(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        this.OpenContour(ref sink);
        double x1 = this.x + dx1;
        double y1 = this.y + dy1;
        double x2 = x1 + dx2;
        double y2 = y1 + dy2;
        this.x = x2 + dx3;
        this.y = y2 + dy3;
        sink.CubicBezierTo(new Vector2((float)x1, (float)y1), new Vector2((float)x2, (float)y2), this.Point);
    }

    // A contour starts at its first segment, so a move that draws nothing writes nothing.
    private void OpenContour<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (!this.contourOpen)
        {
            sink.MoveTo(this.Point);
            this.contourOpen = true;
        }
    }

    private void CloseContour<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.contourOpen)
        {
            sink.ClosePath();
            this.contourOpen = false;
        }
    }
}
