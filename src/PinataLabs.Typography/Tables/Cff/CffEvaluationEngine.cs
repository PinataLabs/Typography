// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Decodes the commands and numbers making up a Type 2 CharString. A Type 2 CharString extends on the Type 1 CharString format.
/// Compared to the Type 1 format, the Type 2 encoding offers smaller size and an opportunity for better rendering quality and
/// performance. The Type 2 charstring operators are (with one exception) a superset of the Type 1 operators.
/// </summary>
/// <remarks>
/// A Type 2 charstring program is a sequence of unsigned 8-bit bytes that encode numbers and operators.
/// The byte value specifies a operator, a number, or subsequent bytes that are to be interpreted in a specific manner.
/// The engine allocates nothing and shares no state. A malformed charstring makes it stop and report failure; it does not throw.
/// </remarks>
internal ref struct CffEvaluationEngine
{
    /// <summary>
    /// The size of the argument stack that the caller provides: the 513 arguments that CFF2 allows.
    /// The engine uses 48 of them for a CFF 1 charstring, the Type 2 limit.
    /// </summary>
    public const int StackSize = 513;

    /// <summary>
    /// The size of the transient array that the caller provides for the put and get operators.
    /// </summary>
    public const int TransientSize = 32;

    // The Type 2 limit on nested subroutine calls.
    private const int MaxSubrDepth = 10;

    private const int Type2StackSize = 48;

    // Subroutine calls can repeat work without a limit on the charstring size, so bound the work itself.
    private const int MaxOperators = 1 << 20;

    // Each operator can write many segments, so bound the outline too.
    private const int MaxSinkCalls = 1_000_000;

    private readonly ReadOnlySpan<byte> charString;
    private readonly CffFont? font;
    private readonly CffIndex globalSubrs;
    private readonly CffIndex localSubrs;
    private readonly float nominalWidthX;
    private readonly float defaultWidthX;
    private readonly float[]?[]? blendScalars;
    private readonly int initialVsIndex;
    private readonly int globalBias;
    private readonly int localBias;
    private readonly Span<float> stack;
    private readonly Span<float> transient;

    private int bottom;
    private int top;
    private float x;
    private float y;
    private float width;
    private bool hasWidth;
    private bool widthResolved;
    private bool widthOnly;
    private int stemCount;
    private int vsIndex;
    private bool isOpen;
    private bool ended;
    private bool failed;
    private bool inSeac;
    private int operatorCount;
    private int sinkCalls;
    private uint randomState;

    public CffEvaluationEngine(
        ReadOnlySpan<byte> charString,
        CffIndex globalSubrs,
        CffIndex localSubrs,
        float nominalWidthX,
        float defaultWidthX,
        Span<float> stack,
        Span<float> transient,
        CffFont? font = null,
        float[]?[]? blendScalars = null,
        int vsIndex = 0)
    {
        this = default;
        this.charString = charString;
        this.font = font;
        this.globalSubrs = globalSubrs;
        this.localSubrs = localSubrs;
        this.nominalWidthX = nominalWidthX;
        this.defaultWidthX = defaultWidthX;
        this.stack = blendScalars is null ? stack.Slice(0, Math.Min(stack.Length, Type2StackSize)) : stack;
        this.transient = transient;
        this.blendScalars = blendScalars;
        this.initialVsIndex = vsIndex;
        this.globalBias = CalculateBias(globalSubrs.Count);
        this.localBias = CalculateBias(localSubrs.Count);
    }

    /// <summary>
    /// Gets a value indicating whether the outline failed only because the font has no glyph for the base
    /// or the accent of a seac. The engine then writes neither component.
    /// </summary>
    public bool MissingSeacComponent { readonly get; private set; }

    private readonly int Count => this.top - this.bottom;

    /// <summary>
    /// Writes the outline in glyph space. Each contour is a MoveTo, then segments, then a ClosePath.
    /// </summary>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="sink">The sink that receives the outline.</param>
    /// <returns><see langword="false"/> if the charstring is malformed. The sink may then hold a partial outline.</returns>
    public bool TryWriteOutline<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        this.Reset();
        this.Run(this.charString, ref sink, 0);

        // Some CFF end without closing the latest contour.
        this.ClosePath(ref sink);
        return !this.failed;
    }

    /// <summary>
    /// Gets the advance width that the charstring gives.
    /// </summary>
    /// <param name="advanceWidth">The advance width in glyph space.</param>
    /// <returns><see langword="false"/> if the charstring is malformed.</returns>
    public bool TryGetWidth(out float advanceWidth)
    {
        this.Reset();
        this.widthOnly = true;
        NullGlyphOutlineSink sink = default;
        this.Run(this.charString, ref sink, 0);
        advanceWidth = this.hasWidth ? this.width : this.defaultWidthX;
        return !this.failed;
    }

    private static int CalculateBias(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        return (count < 1240) ? 107 : (count < 33900) ? 1131 : 32768;
    }

    private void Reset()
    {
        this.transient.Clear();
        this.bottom = 0;
        this.top = 0;
        this.x = 0;
        this.y = 0;
        this.width = 0;
        this.hasWidth = false;
        this.widthResolved = false;
        this.widthOnly = false;
        this.stemCount = 0;
        this.vsIndex = this.initialVsIndex;
        this.isOpen = false;
        this.ended = false;
        this.failed = false;
        this.inSeac = false;
        this.MissingSeacComponent = false;
        this.operatorCount = 0;
        this.sinkCalls = 0;
        this.randomState = 0x9E3779B9;
    }

    private void Run<TSink>(ReadOnlySpan<byte> code, ref TSink sink, int depth)
        where TSink : IGlyphOutlineSink
    {
        int position = 0;
        while (position < code.Length && !this.ended && !this.failed)
        {
            byte b0 = code[position++];
            if (b0 >= 32 || b0 == (byte)Type2Operator1.Shortint)
            {
                this.Push(ReadNumber(code, b0, ref position, ref this.failed));
                continue;
            }

            if (++this.operatorCount > MaxOperators)
            {
                this.failed = true;
                return;
            }

            bool phase;
            float c1x;
            float c1y;
            float c2x;
            float c2y;

            var oneByteOperator = (Type2Operator1)b0;
            switch (oneByteOperator)
            {
                case Type2Operator1.Hstem:
                case Type2Operator1.Vstem:
                case Type2Operator1.Hstemhm:
                case Type2Operator1.Vstemhm:

                    this.ParseStems();
                    break;

                case Type2Operator1.Hintmask:
                case Type2Operator1.Cntrmask:

                    this.ParseStems();
                    position += (this.stemCount + 7) >> 3;
                    if (position > code.Length)
                    {
                        this.failed = true;
                    }

                    break;

                case Type2Operator1.Rmoveto:

                    this.TakeWidth(this.Count > 2);
                    this.x += this.Shift();
                    this.y += this.Shift();
                    this.MoveTo(ref sink);
                    break;

                case Type2Operator1.Hmoveto:

                    this.TakeWidth(this.Count > 1);
                    this.x += this.Shift();
                    this.MoveTo(ref sink);
                    break;

                case Type2Operator1.Vmoveto:

                    this.TakeWidth(this.Count > 1);
                    this.y += this.Shift();
                    this.MoveTo(ref sink);
                    break;

                case Type2Operator1.Rlineto:

                    while (this.Count >= 2)
                    {
                        this.EnsureOpen(ref sink);
                        this.x += this.Shift();
                        this.y += this.Shift();
                        this.LineTo(ref sink, new Vector2(this.x, this.y));
                    }

                    this.Clear();
                    break;

                case Type2Operator1.Hlineto:
                case Type2Operator1.Vlineto:

                    phase = oneByteOperator == Type2Operator1.Hlineto;
                    while (this.Count >= 1)
                    {
                        this.EnsureOpen(ref sink);
                        if (phase)
                        {
                            this.x += this.Shift();
                        }
                        else
                        {
                            this.y += this.Shift();
                        }

                        this.LineTo(ref sink, new Vector2(this.x, this.y));
                        phase = !phase;
                    }

                    this.Clear();
                    break;

                case Type2Operator1.Rrcurveto:

                    while (this.Count >= 6)
                    {
                        this.RelativeCurveTo(ref sink);
                    }

                    this.Clear();
                    break;

                case Type2Operator1.Rcurveline:

                    while (this.Count >= 8)
                    {
                        this.RelativeCurveTo(ref sink);
                    }

                    this.EnsureOpen(ref sink);
                    this.x += this.Shift();
                    this.y += this.Shift();
                    this.LineTo(ref sink, new Vector2(this.x, this.y));

                    this.Clear();
                    break;

                case Type2Operator1.Rlinecurve:

                    while (this.Count >= 8)
                    {
                        this.EnsureOpen(ref sink);
                        this.x += this.Shift();
                        this.y += this.Shift();
                        this.LineTo(ref sink, new Vector2(this.x, this.y));
                    }

                    this.RelativeCurveTo(ref sink);

                    this.Clear();
                    break;

                case Type2Operator1.Vvcurveto:

                    this.EnsureOpen(ref sink);
                    if (this.Count % 2 != 0)
                    {
                        this.x += this.Shift();
                    }

                    while (this.Count >= 4)
                    {
                        c1x = this.x;
                        c1y = this.y + this.Shift();
                        c2x = c1x + this.Shift();
                        c2y = c1y + this.Shift();
                        this.x = c2x;
                        this.y = c2y + this.Shift();

                        this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(this.x, this.y));
                    }

                    this.Clear();
                    break;

                case Type2Operator1.Hhcurveto:

                    this.EnsureOpen(ref sink);
                    if (this.Count % 2 != 0)
                    {
                        this.y += this.Shift();
                    }

                    while (this.Count >= 4)
                    {
                        c1x = this.x + this.Shift();
                        c1y = this.y;
                        c2x = c1x + this.Shift();
                        c2y = c1y + this.Shift();
                        this.x = c2x + this.Shift();
                        this.y = c2y;

                        this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(this.x, this.y));
                    }

                    this.Clear();
                    break;

                case Type2Operator1.Vhcurveto:
                case Type2Operator1.Hvcurveto:

                    this.EnsureOpen(ref sink);
                    phase = oneByteOperator == Type2Operator1.Hvcurveto;
                    while (this.Count >= 4)
                    {
                        if (phase)
                        {
                            c1x = this.x + this.Shift();
                            c1y = this.y;
                            c2x = c1x + this.Shift();
                            c2y = c1y + this.Shift();
                            this.y = c2y + this.Shift();
                            this.x = c2x + (this.Count == 1 ? this.Shift() : 0);
                        }
                        else
                        {
                            c1x = this.x;
                            c1y = this.y + this.Shift();
                            c2x = c1x + this.Shift();
                            c2y = c1y + this.Shift();
                            this.x = c2x + this.Shift();
                            this.y = c2y + (this.Count == 1 ? this.Shift() : 0);
                        }

                        this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(this.x, this.y));
                        phase = !phase;
                    }

                    this.Clear();
                    break;

                case Type2Operator1.Callsubr:

                    this.CallSubr(this.localSubrs, this.localBias, ref sink, depth);
                    break;

                case Type2Operator1.Callgsubr:

                    this.CallSubr(this.globalSubrs, this.globalBias, ref sink, depth);
                    break;

                case Type2Operator1.Return:

                    return;

                case Type2Operator1.Endchar:

                    // After an optional width, endchar has no arguments, or the four of a seac.
                    this.TakeWidth((this.Count & 1) != 0);
                    this.ClosePath(ref sink);
                    if (this.Count >= 4 && !this.inSeac && !this.widthOnly)
                    {
                        this.Seac(ref sink);
                    }

                    this.ended = true;
                    break;

                case Type2Operator1.Reserved15_:

                    // CFF2 vsindex: selects the ItemVariationData used by subsequent blend operators.
                    if (this.blendScalars is not null)
                    {
                        this.vsIndex = (int)this.Pop();
                    }

                    break;

                case Type2Operator1.Reserved16_:

                    // CFF2 blend: n default values followed by n * k deltas.
                    if (this.blendScalars is not null)
                    {
                        this.Blend();
                    }

                    break;

                case Type2Operator1.Escape:

                    if (position >= code.Length)
                    {
                        this.failed = true;
                        return;
                    }

                    this.RunEscape(code[position++], ref sink);
                    break;
            }

            if (this.widthOnly && this.widthResolved)
            {
                this.ended = true;
            }
        }
    }

    private void RunEscape<TSink>(byte twoByteOperator, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        float c1x;
        float c1y;
        float c2x;
        float c2y;
        float c3x;
        float c3y;
        float c4x;
        float c4y;
        float c5x;
        float c5y;
        float c6x;
        float c6y;
        bool a;
        bool b;
        float right;
        int slot;

        switch ((Type2Operator2)twoByteOperator)
        {
            case Type2Operator2.And:

                a = this.Pop() != 0;
                b = this.Pop() != 0;
                this.Push((a && b) ? 1 : 0);
                break;

            case Type2Operator2.Or:

                a = this.Pop() != 0;
                b = this.Pop() != 0;
                this.Push((a || b) ? 1 : 0);
                break;

            case Type2Operator2.Not:

                this.Push(this.Pop() == 0 ? 1 : 0);
                break;

            case Type2Operator2.Abs:

                this.Push(Math.Abs(this.Pop()));
                break;

            case Type2Operator2.Add:

                this.Push(this.Pop() + this.Pop());
                break;

            case Type2Operator2.Sub:

                // num1 num2 sub: the second operand is on top.
                right = this.Pop();
                this.Push(this.Pop() - right);
                break;

            case Type2Operator2.Div:

                right = this.Pop();
                this.Push(this.Pop() / right);
                break;

            case Type2Operator2.Neg:

                this.Push(-this.Pop());
                break;

            case Type2Operator2.Eq:

                float second = this.Pop();
                float first = this.Pop();
                this.Push(first == second ? 1 : 0); // NOSONAR: the Type 2 eq operator compares exactly.
                break;

            case Type2Operator2.Drop:

                this.Pop();
                break;

            case Type2Operator2.Put:

                // val i put: the slot is on top.
                slot = (int)this.Pop();
                this.SetTransient(slot, this.Pop());
                break;

            case Type2Operator2.Get:

                this.Push(this.GetTransient((int)this.Pop()));
                break;

            case Type2Operator2.Ifelse:

                // s1 s2 v1 v2 ifelse: s1 if v1 <= v2, else s2.
                float v2 = this.Pop();
                float v1 = this.Pop();
                float s2 = this.Pop();
                float s1 = this.Pop();

                this.Push(v1 <= v2 ? s1 : s2);
                break;

            case Type2Operator2.Random:

                this.Push(this.NextRandom());
                break;

            case Type2Operator2.Mul:

                this.Push(this.Pop() * this.Pop());
                break;

            case Type2Operator2.Sqrt:

                this.Push(MathF.Sqrt(this.Pop()));
                break;

            case Type2Operator2.Dup:

                float m = this.Pop();
                this.Push(m);
                this.Push(m);
                break;

            case Type2Operator2.Exch:

                float ex = this.Pop();
                float ch = this.Pop();
                this.Push(ex);
                this.Push(ch);
                break;

            case Type2Operator2.Index:

                this.Index();
                break;

            case Type2Operator2.Roll:

                this.Roll();
                break;

            case Type2Operator2.Hflex:

                this.EnsureOpen(ref sink);
                c1x = this.x + this.Shift();
                c1y = this.y;
                c2x = c1x + this.Shift();
                c2y = c1y + this.Shift();
                c3x = c2x + this.Shift();
                c3y = c2y;
                c4x = c3x + this.Shift();
                c4y = c3y;

                c5x = c4x + this.Shift();
                c5y = this.y;
                c6x = c5x + this.Shift();
                c6y = this.y;
                this.x = c6x;
                this.y = c6y;

                this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(c3x, c3y));
                this.CurveTo(ref sink, new Vector2(c4x, c4y), new Vector2(c5x, c5y), new Vector2(c6x, c6y));

                this.Clear();
                break;

            case Type2Operator2.Flex:

                // Two curves of relative arguments. The last argument, the flex depth, is for hinting only.
                this.RelativeCurveTo(ref sink);
                this.RelativeCurveTo(ref sink);

                this.Clear();
                break;

            case Type2Operator2.Hflex1:

                this.EnsureOpen(ref sink);
                c1x = this.x + this.Shift();
                c1y = this.y + this.Shift();
                c2x = c1x + this.Shift();
                c2y = c1y + this.Shift();
                c3x = c2x + this.Shift();
                c3y = c2y;
                c4x = c3x + this.Shift();
                c4y = c3y;
                c5x = c4x + this.Shift();
                c5y = c4y + this.Shift();
                c6x = c5x + this.Shift();
                c6y = this.y;
                this.x = c6x;
                this.y = c6y;

                this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(c3x, c3y));
                this.CurveTo(ref sink, new Vector2(c4x, c4y), new Vector2(c5x, c5y), new Vector2(c6x, c6y));

                this.Clear();
                break;

            case Type2Operator2.Flex1:

                this.EnsureOpen(ref sink);
                float startX = this.x;
                float startY = this.y;

                c1x = this.x + this.Shift();
                c1y = this.y + this.Shift();
                c2x = c1x + this.Shift();
                c2y = c1y + this.Shift();
                c3x = c2x + this.Shift();
                c3y = c2y + this.Shift();
                c4x = c3x + this.Shift();
                c4y = c3y + this.Shift();
                c5x = c4x + this.Shift();
                c5y = c4y + this.Shift();

                // The last argument moves along the axis that the first five points moved along the most.
                // The end point returns to the start on the other axis.
                if (MathF.Abs(c5x - startX) > MathF.Abs(c5y - startY))
                {
                    c6x = c5x + this.Shift();
                    c6y = startY;
                }
                else
                {
                    c6x = startX;
                    c6y = c5y + this.Shift();
                }

                this.x = c6x;
                this.y = c6y;

                this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(c3x, c3y));
                this.CurveTo(ref sink, new Vector2(c4x, c4y), new Vector2(c5x, c5y), new Vector2(c6x, c6y));

                this.Clear();
                break;

            default:

                if (twoByteOperator >= 38)
                {
                    this.failed = true;
                }

                break;
        }
    }

    private static float ReadNumber(ReadOnlySpan<byte> code, byte b0, ref int position, ref bool failed)
    {
        int size = b0 switch
        {
            (byte)Type2Operator1.Shortint => 2,
            < 247 => 0,
            < 255 => 1,
            _ => 4,
        };

        if (position + size > code.Length)
        {
            failed = true;
            return 0;
        }

        ReadOnlySpan<byte> bytes = code.Slice(position, size);
        position += size;
        switch (size)
        {
            case 0:
                return b0 - 139;
            case 1:
                return b0 < 251
                    ? ((b0 - 247) * 256) + bytes[0] + 108
                    : (-(b0 - 251) * 256) - bytes[0] - 108;
            case 2:
                return (short)((bytes[0] << 8) | bytes[1]);
            default:
                return ((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]) / 65536F;
        }
    }

    private void CallSubr<TSink>(CffIndex subrs, int bias, ref TSink sink, int depth)
        where TSink : IGlyphOutlineSink
    {
        int index = (int)this.Pop() + bias;
        if (depth >= MaxSubrDepth || !subrs.TryGet(index, out ReadOnlyMemory<byte> subr))
        {
            this.failed = true;
            return;
        }

        this.Run(subr.Span, ref sink, depth + 1);
    }

    // adx ady bchar achar endchar: the deprecated way to build an accented glyph. It draws the glyph of
    // Standard Encoding code bchar, and then the glyph of code achar moved by (adx, ady).
    private void Seac<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        float adx = this.Shift();
        float ady = this.Shift();
        int baseCode = (int)this.Shift();
        int accentCode = (int)this.Shift();
        float ownWidth = this.width;
        bool hasOwnWidth = this.hasWidth;
        if (this.font is null
            || !this.font.TryGetSeacComponent(baseCode, out ReadOnlyMemory<byte> baseGlyph)
            || !this.font.TryGetSeacComponent(accentCode, out ReadOnlyMemory<byte> accentGlyph))
        {
            this.failed = true;
            this.MissingSeacComponent = true;
            return;
        }

        this.inSeac = true;
        this.RunSeacComponent(baseGlyph, 0, 0, ref sink);
        this.RunSeacComponent(accentGlyph, adx, ady, ref sink);

        this.width = ownWidth;
        this.hasWidth = hasOwnWidth;
    }

    private void RunSeacComponent<TSink>(ReadOnlyMemory<byte> component, float originX, float originY, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.failed)
        {
            return;
        }

        // Every operator moves relative to the current point, so starting there moves the whole component.
        this.Clear();
        this.x = originX;
        this.y = originY;
        this.stemCount = 0;
        this.widthResolved = false;
        this.ended = false;
        this.Run(component.Span, ref sink, 0);
        this.ClosePath(ref sink);
    }

    private void RelativeCurveTo<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        this.EnsureOpen(ref sink);
        float c1x = this.x + this.Shift();
        float c1y = this.y + this.Shift();
        float c2x = c1x + this.Shift();
        float c2y = c1y + this.Shift();
        this.x = c2x + this.Shift();
        this.y = c2y + this.Shift();
        this.CurveTo(ref sink, new Vector2(c1x, c1y), new Vector2(c2x, c2y), new Vector2(this.x, this.y));
    }

    private void MoveTo<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        this.ClosePath(ref sink);
        if (this.TrySpendSinkCall())
        {
            sink.MoveTo(new Vector2(this.x, this.y));
        }

        this.isOpen = true;
        this.Clear();
    }

    // A charstring that draws before its first moveto starts the contour at the current point.
    private void EnsureOpen<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (!this.isOpen)
        {
            if (this.TrySpendSinkCall())
            {
                sink.MoveTo(new Vector2(this.x, this.y));
            }

            this.isOpen = true;
        }
    }

    private void LineTo<TSink>(ref TSink sink, Vector2 point)
        where TSink : IGlyphOutlineSink
    {
        if (this.TrySpendSinkCall())
        {
            sink.LineTo(point);
        }
    }

    private void CurveTo<TSink>(ref TSink sink, Vector2 control1, Vector2 control2, Vector2 point)
        where TSink : IGlyphOutlineSink
    {
        if (this.TrySpendSinkCall())
        {
            sink.CubicBezierTo(control1, control2, point);
        }
    }

    private void ClosePath<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.isOpen)
        {
            if (this.TrySpendSinkCall())
            {
                sink.ClosePath();
            }

            this.isOpen = false;
        }
    }

    private bool TrySpendSinkCall()
    {
        if (this.sinkCalls >= MaxSinkCalls)
        {
            this.failed = true;
            return false;
        }

        this.sinkCalls++;
        return true;
    }

    private void ParseStems()
    {
        this.TakeWidth(this.Count % 2 != 0);
        this.stemCount += this.Count >> 1;
        this.Clear();
    }

    // The first operator that clears the stack may have one extra argument at the bottom: the width.
    private void TakeWidth(bool present)
    {
        if (!this.widthResolved)
        {
            this.widthResolved = true;
            if (present)
            {
                this.width = this.Shift() + this.nominalWidthX;
                this.hasWidth = true;
            }
        }
    }

    // n default values, then the k deltas of each value, one for each region of the ItemVariationData that the vsindex
    // names, then n. Each value leaves with the sum of its deltas, each scaled by the scalar of its region.
    // A subtable that the store lacks has no scalars, and a glyph that blends with it fails.
    private void Blend()
    {
        int n = (int)this.Pop();
        float[]?[] subtables = this.blendScalars!;
        if ((uint)this.vsIndex >= (uint)subtables.Length || subtables[this.vsIndex] is not float[] scalars)
        {
            this.failed = true;
            return;
        }

        long deltas = (long)n * scalars.Length;
        if (n < 0 || deltas + n > this.Count)
        {
            this.failed = true;
            return;
        }

        int firstDelta = this.top - (int)deltas;
        int firstValue = firstDelta - n;
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int j = 0; j < scalars.Length; j++)
            {
                sum += (double)scalars[j] * this.stack[firstDelta + (i * scalars.Length) + j];
            }

            this.stack[firstValue + i] += (float)sum;
        }

        this.top = firstDelta;
    }

    // numX ... num0 i index: pushes a copy of numi. A negative i copies the top element.
    private void Index()
    {
        int index = (int)this.Pop();
        if (index < 0)
        {
            index = 0;
        }

        if (index >= this.Count)
        {
            this.failed = true;
            return;
        }

        this.Push(this.stack[this.top - 1 - index]);
    }

    // num(N-1) ... num0 N J roll: rotates the top N elements J places towards the top.
    private void Roll()
    {
        int shift = (int)this.Pop();
        int count = (int)this.Pop();
        if (count < 0 || count > this.Count)
        {
            this.failed = true;
            return;
        }

        if (count == 0)
        {
            return;
        }

        Span<float> items = this.stack.Slice(this.top - count, count);
        shift = ((shift % count) + count) % count;
        items.Reverse();
        items.Slice(0, shift).Reverse();
        items.Slice(shift).Reverse();
    }

    private float GetTransient(int slot)
    {
        if ((uint)slot >= (uint)this.transient.Length)
        {
            this.failed = true;
            return 0;
        }

        return this.transient[slot];
    }

    private void SetTransient(int slot, float value)
    {
        if ((uint)slot >= (uint)this.transient.Length)
        {
            this.failed = true;
            return;
        }

        this.transient[slot] = value;
    }

    // The random operator needs a number greater than 0 and at most 1. It does not need to be unpredictable,
    // so a xorshift sequence that restarts for each charstring keeps the outline of a glyph repeatable.
    private float NextRandom()
    {
        this.randomState ^= this.randomState << 13;
        this.randomState ^= this.randomState >> 17;
        this.randomState ^= this.randomState << 5;
        return ((this.randomState >> 8) + 1) / (float)(1 << 24);
    }

    private void Push(float value)
    {
        if (this.top >= this.stack.Length)
        {
            this.failed = true;
            return;
        }

        this.stack[this.top++] = value;
    }

    private float Pop()
    {
        if (this.top <= this.bottom)
        {
            this.failed = true;
            return 0;
        }

        return this.stack[--this.top];
    }

    private float Shift()
    {
        if (this.bottom >= this.top)
        {
            this.failed = true;
            return 0;
        }

        return this.stack[this.bottom++];
    }

    private void Clear()
    {
        this.bottom = 0;
        this.top = 0;
    }
}
