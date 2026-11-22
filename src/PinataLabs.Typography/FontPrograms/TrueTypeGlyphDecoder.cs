// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// Streams the outline of a 'glyf' glyph to a sink. It allocates nothing: it reads the points of a
/// contour from the glyph data as often as it needs them.
/// </summary>
internal static class TrueTypeGlyphDecoder
{
    private const int HeaderSize = 10;

    private const int MaxDepth = 16;

    // Components can repeat work without a limit on the glyph size, so bound the number of components
    // and the number of sink calls per outline.
    private const int MaxComponents = 4096;
    private const int MaxSinkCalls = 1_000_000;

    private const int OnCurve = 0x01;
    private const int XShort = 0x02;
    private const int YShort = 0x04;
    private const int Repeat = 0x08;
    private const int XSameOrPositive = 0x10;
    private const int YSameOrPositive = 0x20;

    private const int ArgsAreWords = 0x0001;
    private const int ArgsAreXYValues = 0x0002;
    private const int HasScale = 0x0008;
    private const int MoreComponents = 0x0020;
    private const int HasXYScale = 0x0040;
    private const int HasTwoByTwo = 0x0080;
    private const int ScaledComponentOffset = 0x0800;

    /// <summary>
    /// Writes the outline of a glyph in glyph space. Each contour starts at its first on-curve point.
    /// </summary>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="font">The font.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="sink">The sink that receives the outline.</param>
    /// <returns><see langword="false"/> if the glyph ID is out of range or the glyph is malformed.</returns>
    public static bool TryWriteOutline<TSink>(TrueTypeProgram font, ushort glyphId, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        Budget budget = new() { Components = MaxComponents, SinkCalls = MaxSinkCalls };
        return TryWriteGlyph(font, glyphId, Matrix3x2.Identity, 0, ref budget, ref sink);
    }

    private static bool TryWriteGlyph<TSink>(TrueTypeProgram font, int glyphId, Matrix3x2 matrix, int depth, ref Budget budget, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (!font.TryGetGlyphData(glyphId, out ReadOnlySpan<byte> glyph))
        {
            return false;
        }

        if (glyph.IsEmpty)
        {
            return true;
        }

        if (glyph.Length < HeaderSize)
        {
            return false;
        }

        int contourCount = BinaryPrimitives.ReadInt16BigEndian(glyph);
        if (contourCount < 0)
        {
            return TryWriteComposite(font, glyph, matrix, depth, ref budget, ref sink);
        }

        if (matrix.IsIdentity)
        {
            return TryWriteSimple(glyph, contourCount, ref budget, ref sink);
        }

        TransformedSink<TSink> transformed = new(sink, matrix);
        bool result = TryWriteSimple(glyph, contourCount, ref budget, ref transformed);
        sink = transformed.Inner;
        return result;
    }

    private static bool TryWriteComposite<TSink>(TrueTypeProgram font, ReadOnlySpan<byte> glyph, Matrix3x2 matrix, int depth, ref Budget budget, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (depth >= MaxDepth)
        {
            return false;
        }

        ComponentReader components = new(glyph);
        while (components.TryRead(out Component component))
        {
            if (--budget.Components < 0
                || !TryPlaceComponent(font, glyph, in component, depth, ref budget, out Matrix3x2 placement)
                || !TryWriteGlyph(font, component.GlyphId, placement * matrix, depth + 1, ref budget, ref sink))
            {
                return false;
            }
        }

        return !components.Failed;
    }

    /// <summary>
    /// Gets the matrix that maps the points of a component into the space of its composite glyph.
    /// </summary>
    private static bool TryPlaceComponent(TrueTypeProgram font, ReadOnlySpan<byte> glyph, in Component component, int depth, ref Budget budget, out Matrix3x2 placement)
    {
        placement = component.Transform;
        Vector2 offset;
        if ((component.Flags & ArgsAreXYValues) != 0)
        {
            // An offset is in the space of the composite, unless the flag says that the transform scales it too.
            offset = new Vector2(component.Argument1, component.Argument2);
            if ((component.Flags & ScaledComponentOffset) != 0)
            {
                offset = Vector2.TransformNormal(offset, component.Transform);
            }
        }
        else
        {
            // The arguments are point numbers: a point of the components placed so far,
            // and the point of this component that moves onto it.
            int parentIndex = component.Argument1;
            int childIndex = component.Argument2;
            if (!TryFindCompositePoint(font, glyph, component.Start, Matrix3x2.Identity, depth, ref budget, ref parentIndex, out Vector2 parent)
                || parentIndex >= 0
                || !TryFindPoint(font, component.GlyphId, component.Transform, depth + 1, ref budget, ref childIndex, out Vector2 child)
                || childIndex >= 0)
            {
                return false;
            }

            offset = parent - child;
        }

        placement.Translation = offset;
        return true;
    }

    // A composite numbers the points of its components in order.
    private static bool TryFindPoint(TrueTypeProgram font, int glyphId, Matrix3x2 matrix, int depth, ref Budget budget, ref int index, out Vector2 point)
    {
        point = default;
        if (!font.TryGetGlyphData(glyphId, out ReadOnlySpan<byte> glyph))
        {
            return false;
        }

        if (glyph.IsEmpty)
        {
            return true;
        }

        if (glyph.Length < HeaderSize)
        {
            return false;
        }

        int contourCount = BinaryPrimitives.ReadInt16BigEndian(glyph);
        if (contourCount < 0)
        {
            return TryFindCompositePoint(font, glyph, glyph.Length, matrix, depth, ref budget, ref index, out point);
        }

        if (!PointReader.TryCreate(glyph, contourCount, out PointReader points))
        {
            return false;
        }

        if (index >= points.Count)
        {
            index -= points.Count;
            return true;
        }

        for (int i = 0; i <= index; i++)
        {
            points.Read(out point, out _);
        }

        point = Vector2.Transform(point, matrix);
        index = -1;
        return true;
    }

    private static bool TryFindCompositePoint(TrueTypeProgram font, ReadOnlySpan<byte> glyph, int end, Matrix3x2 matrix, int depth, ref Budget budget, ref int index, out Vector2 point)
    {
        point = default;
        if (depth >= MaxDepth)
        {
            return false;
        }

        ComponentReader components = new(glyph);
        while (index >= 0 && components.TryRead(out Component component) && component.Start < end)
        {
            if (--budget.Components < 0
                || !TryPlaceComponent(font, glyph, in component, depth, ref budget, out Matrix3x2 placement)
                || !TryFindPoint(font, component.GlyphId, placement * matrix, depth + 1, ref budget, ref index, out point))
            {
                return false;
            }
        }

        return !components.Failed;
    }

    private static bool TryWriteSimple<TSink>(ReadOnlySpan<byte> glyph, int contourCount, ref Budget budget, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (!PointReader.TryCreate(glyph, contourCount, out PointReader points))
        {
            return false;
        }

        // A contour writes a MoveTo, at most one segment for each point and one to close it, and a ClosePath.
        budget.SinkCalls -= points.Count + (3 * contourCount);
        if (budget.SinkCalls < 0)
        {
            return false;
        }

        int start = 0;
        for (int i = 0; i < contourCount; i++)
        {
            int end = BinaryPrimitives.ReadUInt16BigEndian(glyph.Slice(HeaderSize + (i * 2)));
            if (end < start || end >= points.Count)
            {
                return false;
            }

            WriteContour(ref points, end - start + 1, ref sink);
            start = end + 1;
        }

        return true;
    }

    /// <summary>
    /// Writes one contour. The contour starts at its first on-curve point, so the off-curve points
    /// before that point come last. A contour without an on-curve point starts halfway between its
    /// last and first points.
    /// </summary>
    private static void WriteContour<TSink>(ref PointReader points, int count, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        PointReader contourStart = points;
        int firstOnCurve = -1;
        Vector2 start = default;
        Vector2 first = default;
        Vector2 last = default;
        for (int i = 0; i < count; i++)
        {
            points.Read(out last, out bool onCurve);
            if (i == 0)
            {
                first = last;
            }

            if (onCurve && firstOnCurve < 0)
            {
                firstOnCurve = i;
                start = last;
            }
        }

        PointReader contourEnd = points;
        if (firstOnCurve < 0)
        {
            start = (last + first) * 0.5F;
        }

        sink.MoveTo(start);

        Vector2 control = default;
        bool hasControl = false;

        for (int pass = 0; pass < 2; pass++)
        {
            points = contourStart;
            for (int i = 0; i < count; i++)
            {
                points.Read(out Vector2 point, out bool onCurve);
                if ((i > firstOnCurve) != (pass == 0))
                {
                    continue;
                }

                if (!onCurve)
                {
                    // Two off-curve points in a row have an on-curve point halfway between them.
                    if (hasControl)
                    {
                        sink.QuadraticBezierTo(control, (control + point) * 0.5F);
                    }

                    control = point;
                    hasControl = true;
                }
                else if (hasControl)
                {
                    sink.QuadraticBezierTo(control, point);
                    hasControl = false;
                }
                else if (i != firstOnCurve)
                {
                    sink.LineTo(point);
                }
            }
        }

        if (hasControl)
        {
            sink.QuadraticBezierTo(control, start);
        }

        sink.ClosePath();
        points = contourEnd;
    }

    private struct Budget
    {
        public int Components;

        public int SinkCalls;
    }

    private readonly struct Component
    {
        public Component(int start, int flags, ushort glyphId, int argument1, int argument2, Matrix3x2 transform)
        {
            this.Start = start;
            this.Flags = flags;
            this.GlyphId = glyphId;
            this.Argument1 = argument1;
            this.Argument2 = argument2;
            this.Transform = transform;
        }

        /// <summary>Gets the position of the component record in the glyph data.</summary>
        public int Start { get; }

        public int Flags { get; }

        public ushort GlyphId { get; }

        /// <summary>Gets the x offset, or the number of a point of the composite.</summary>
        public int Argument1 { get; }

        /// <summary>Gets the y offset, or the number of a point of the component.</summary>
        public int Argument2 { get; }

        /// <summary>Gets the scale or the two-by-two matrix of the component, without an offset.</summary>
        public Matrix3x2 Transform { get; }
    }

    /// <summary>
    /// Reads the component records of a composite glyph.
    /// </summary>
    private ref struct ComponentReader
    {
        private FontDataReader reader;
        private bool more;

        public ComponentReader(ReadOnlySpan<byte> glyph)
        {
            this.reader = new FontDataReader(glyph, HeaderSize);
            this.more = true;
            this.Failed = false;
        }

        /// <summary>
        /// Gets a value indicating whether a record ran past the end of the glyph data.
        /// </summary>
        public bool Failed { get; private set; }

        public bool TryRead(out Component component)
        {
            component = default;
            if (!this.more || this.Failed)
            {
                return false;
            }

            int start = this.reader.Position;
            this.Failed = !this.reader.TryReadUInt16(out ushort flags) | !this.reader.TryReadUInt16(out ushort glyphId);

            int argument1;
            int argument2;
            bool signed = (flags & ArgsAreXYValues) != 0;
            if ((flags & ArgsAreWords) != 0)
            {
                this.Failed |= !this.reader.TryReadUInt16(out ushort word1) | !this.reader.TryReadUInt16(out ushort word2);
                argument1 = signed ? (short)word1 : word1;
                argument2 = signed ? (short)word2 : word2;
            }
            else
            {
                this.Failed |= !this.reader.TryReadByte(out byte byte1) | !this.reader.TryReadByte(out byte byte2);
                argument1 = signed ? (sbyte)byte1 : byte1;
                argument2 = signed ? (sbyte)byte2 : byte2;
            }

            Matrix3x2 transform = Matrix3x2.Identity;
            if ((flags & HasScale) != 0)
            {
                transform.M11 = transform.M22 = this.ReadF2Dot14();
            }
            else if ((flags & HasXYScale) != 0)
            {
                transform.M11 = this.ReadF2Dot14();
                transform.M22 = this.ReadF2Dot14();
            }
            else if ((flags & HasTwoByTwo) != 0)
            {
                transform.M11 = this.ReadF2Dot14();
                transform.M12 = this.ReadF2Dot14();
                transform.M21 = this.ReadF2Dot14();
                transform.M22 = this.ReadF2Dot14();
            }

            this.more = (flags & MoreComponents) != 0;
            component = new Component(start, flags, glyphId, argument1, argument2, transform);
            return !this.Failed;
        }

        private float ReadF2Dot14()
        {
            this.Failed |= !this.reader.TryReadInt16(out short value);
            return value / 16384F;
        }
    }

    /// <summary>
    /// Reads the points of a simple glyph in order. The flags, the x coordinates and the y coordinates
    /// are three runs of the glyph data, so the reader keeps a position in each.
    /// </summary>
    private ref struct PointReader
    {
        private readonly ReadOnlySpan<byte> glyph;
        private int flagPosition;
        private int xPosition;
        private int yPosition;
        private int repeat;
        private int flags;
        private int x;
        private int y;

        private PointReader(ReadOnlySpan<byte> glyph, int count, int flagPosition, int xPosition, int yPosition)
        {
            this.glyph = glyph;
            this.Count = count;
            this.flagPosition = flagPosition;
            this.xPosition = xPosition;
            this.yPosition = yPosition;
            this.repeat = 0;
            this.flags = 0;
            this.x = 0;
            this.y = 0;
        }

        /// <summary>
        /// Gets the number of points of the glyph.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Finds the three runs and checks that each lies within the glyph data, so that <see cref="Read"/> cannot fail.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<byte> glyph, int contourCount, out PointReader points)
        {
            points = default;
            if (contourCount == 0)
            {
                return true;
            }

            FontDataReader reader = new(glyph, HeaderSize);
            if (!reader.TrySkip((contourCount - 1) * 2)
                || !reader.TryReadUInt16(out ushort lastPoint)
                || !reader.TryReadUInt16(out ushort instructionLength)
                || !reader.TrySkip(instructionLength))
            {
                return false;
            }

            int count = lastPoint + 1;

            int flagPosition = reader.Position;
            int xLength = 0;
            int yLength = 0;
            for (int i = 0; i < count;)
            {
                if (!reader.TryReadByte(out byte flags))
                {
                    return false;
                }

                int run = 1;
                if ((flags & Repeat) != 0)
                {
                    if (!reader.TryReadByte(out byte repeat))
                    {
                        return false;
                    }

                    run += repeat;
                }

                // FreeType rejects a run past the last point. Reading on would take the coordinates of the wrong points.
                if (i + run > count)
                {
                    return false;
                }

                // A coordinate is one byte, or two bytes, or absent when it repeats the last one.
                xLength += run * ((flags & XShort) != 0 ? 1 : (flags & XSameOrPositive) != 0 ? 0 : 2);
                yLength += run * ((flags & YShort) != 0 ? 1 : (flags & YSameOrPositive) != 0 ? 0 : 2);
                i += run;
            }

            int xPosition = reader.Position;
            if (!reader.TrySkip(xLength) || !reader.TrySkip(yLength))
            {
                return false;
            }

            points = new PointReader(glyph, count, flagPosition, xPosition, xPosition + xLength);
            return true;
        }

        public void Read(out Vector2 point, out bool onCurve)
        {
            if (this.repeat > 0)
            {
                this.repeat--;
            }
            else
            {
                this.flags = this.glyph[this.flagPosition++];
                if ((this.flags & Repeat) != 0)
                {
                    this.repeat = this.glyph[this.flagPosition++];
                }
            }

            this.x += this.ReadDelta(ref this.xPosition, XShort, XSameOrPositive);
            this.y += this.ReadDelta(ref this.yPosition, YShort, YSameOrPositive);
            point = new Vector2(this.x, this.y);
            onCurve = (this.flags & OnCurve) != 0;
        }

        private readonly int ReadDelta(ref int position, int shortFlag, int sameOrPositiveFlag)
        {
            if ((this.flags & shortFlag) != 0)
            {
                int value = this.glyph[position++];
                return (this.flags & sameOrPositiveFlag) != 0 ? value : -value;
            }

            if ((this.flags & sameOrPositiveFlag) != 0)
            {
                return 0;
            }

            position += 2;
            return BinaryPrimitives.ReadInt16BigEndian(this.glyph.Slice(position - 2));
        }
    }
}
