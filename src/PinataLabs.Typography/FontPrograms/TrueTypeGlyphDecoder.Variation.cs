// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.FontPrograms;

/// <content>
/// Reads the points of a glyph at a variation instance.
/// </content>
internal static partial class TrueTypeGlyphDecoder
{
    // The four phantom points follow the points of a glyph.
    private const int PhantomPointCount = 4;

    /// <summary>
    /// Reads the points of a glyph at a variation instance, in the form that <see cref="TryReadPoints"/> gives for the
    /// default instance: the points of every contour in order, and the instructions of the glyph. The 'gvar' deltas of the
    /// glyph move its points, or the offsets of its components, and a composite glyph takes the varied points of its components.
    /// </summary>
    /// <param name="glyphs">The 'loca' and 'glyf' tables of the font.</param>
    /// <param name="gvar">The 'gvar' table of the font.</param>
    /// <param name="coordinates">The F2Dot14 coordinates of the instance, in <c>fvar</c> order.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="vector">The points of the glyph. An empty glyph has none.</param>
    /// <param name="phantoms">The deltas that the table gives the phantom points of the glyph itself.</param>
    /// <returns><see langword="false"/> if the glyph ID is out of range, or the glyph or its variation data is malformed.</returns>
    public static bool TryReadVariedPoints(TrueTypeGlyphData glyphs, GvarTable gvar, ReadOnlySpan<short> coordinates, ushort glyphId, out GlyphVector vector, out PhantomDeltas phantoms)
    {
        vector = default;
        List<ControlPoint> points = [];
        List<ushort> endPoints = [];
        Budget budget = new() { Components = MaxComponents, SinkCalls = MaxSinkCalls };
        if (!TryAppendVariedPoints(glyphs, gvar, coordinates, glyphId, 0, ref budget, points, endPoints, out phantoms)
            || points.Count > ushort.MaxValue
            || !glyphs.TryGetGlyphData(glyphId, out ReadOnlyMemory<byte> data))
        {
            return false;
        }

        bool isComposite = !data.IsEmpty && BinaryPrimitives.ReadInt16BigEndian(data.Span) < 0;
        vector = new GlyphVector(points.ToArray(), endPoints.ToArray(), default, ReadInstructions(data, isComposite), isComposite);
        return true;
    }

    // Appends the varied points of a glyph in its own space, and gives the deltas of its phantom points.
    private static bool TryAppendVariedPoints(
        TrueTypeGlyphData glyphs,
        GvarTable gvar,
        ReadOnlySpan<short> coordinates,
        int glyphId,
        int depth,
        ref Budget budget,
        List<ControlPoint> points,
        List<ushort> endPoints,
        out PhantomDeltas phantoms)
    {
        phantoms = default;
        if (!glyphs.TryGetGlyphData(glyphId, out ReadOnlyMemory<byte> data))
        {
            return false;
        }

        ReadOnlySpan<byte> glyph = data.Span;
        if (glyph.IsEmpty)
        {
            Vector2[] deltas = new Vector2[PhantomPointCount];
            if (!gvar.TryGetDeltas(glyphId, coordinates, new Vector2[PhantomPointCount], [], deltas))
            {
                return false;
            }

            phantoms = GetPhantomDeltas(deltas, 0);
            return true;
        }

        if (glyph.Length < HeaderSize)
        {
            return false;
        }

        int contourCount = BinaryPrimitives.ReadInt16BigEndian(glyph);
        return contourCount < 0
            ? TryAppendVariedComposite(glyphs, gvar, coordinates, glyphId, glyph, depth, ref budget, points, endPoints, out phantoms)
            : TryAppendVariedSimple(gvar, coordinates, glyphId, glyph, contourCount, ref budget, points, endPoints, out phantoms);
    }

    private static bool TryAppendVariedSimple(
        GvarTable gvar,
        ReadOnlySpan<short> coordinates,
        int glyphId,
        ReadOnlySpan<byte> glyph,
        int contourCount,
        ref Budget budget,
        List<ControlPoint> points,
        List<ushort> endPoints,
        out PhantomDeltas phantoms)
    {
        phantoms = default;
        if (!PointReader.TryCreate(glyph, contourCount, out PointReader reader))
        {
            return false;
        }

        // The interpreter numbers points with 16 bits.
        budget.SinkCalls -= reader.Count;
        int first = points.Count;
        if (budget.SinkCalls < 0 || first + reader.Count > ushort.MaxValue)
        {
            return false;
        }

        ushort[] ends = new ushort[contourCount];
        int start = 0;
        for (int i = 0; i < contourCount; i++)
        {
            int end = BinaryPrimitives.ReadUInt16BigEndian(glyph.Slice(HeaderSize + (i * 2)));
            if (end < start || end >= reader.Count)
            {
                return false;
            }

            ends[i] = (ushort)end;
            start = end + 1;
        }

        Vector2[] original = new Vector2[reader.Count + PhantomPointCount];
        bool[] onCurve = new bool[reader.Count];
        for (int i = 0; i < reader.Count; i++)
        {
            reader.Read(out original[i], out onCurve[i]);
        }

        Vector2[] deltas = new Vector2[original.Length];
        if (!gvar.TryGetDeltas(glyphId, coordinates, original, ends, deltas))
        {
            return false;
        }

        foreach (ushort end in ends)
        {
            endPoints.Add((ushort)(first + end));
        }

        for (int i = 0; i < reader.Count; i++)
        {
            points.Add(new ControlPoint(original[i] + deltas[i], onCurve[i]));
        }

        phantoms = GetPhantomDeltas(deltas, reader.Count);
        return true;
    }

    // The varied points of a composite are those of its components. Each component moves by its offset, which has a delta
    // of its own, or places a point of its own on a point of the components before it.
    private static bool TryAppendVariedComposite(
        TrueTypeGlyphData glyphs,
        GvarTable gvar,
        ReadOnlySpan<short> coordinates,
        int glyphId,
        ReadOnlySpan<byte> glyph,
        int depth,
        ref Budget budget,
        List<ControlPoint> points,
        List<ushort> endPoints,
        out PhantomDeltas phantoms)
    {
        phantoms = default;
        if (depth >= MaxDepth)
        {
            return false;
        }

        List<Component> components = [];
        ComponentReader reader = new(glyph);
        while (reader.TryRead(out Component component))
        {
            if (--budget.Components < 0)
            {
                return false;
            }

            components.Add(component);
        }

        if (reader.Failed)
        {
            return false;
        }

        // A component that places a point on a point has no offset to vary.
        Vector2[] offsets = new Vector2[components.Count + PhantomPointCount];
        for (int i = 0; i < components.Count; i++)
        {
            if ((components[i].Flags & ArgsAreXYValues) != 0)
            {
                offsets[i] = new Vector2(components[i].Argument1, components[i].Argument2);
            }
        }

        Vector2[] deltas = new Vector2[offsets.Length];
        if (!gvar.TryGetDeltas(glyphId, coordinates, offsets, [], deltas))
        {
            return false;
        }

        for (int i = 0; i < components.Count; i++)
        {
            Component component = components[i];
            List<ControlPoint> componentPoints = [];
            List<ushort> componentEnds = [];
            if (!TryAppendVariedPoints(glyphs, gvar, coordinates, component.GlyphId, depth + 1, ref budget, componentPoints, componentEnds, out _))
            {
                return false;
            }

            for (int j = 0; j < componentPoints.Count; j++)
            {
                componentPoints[j] = new ControlPoint(Vector2.Transform(componentPoints[j].Point, component.Transform), componentPoints[j].OnCurve);
            }

            Vector2 offset;
            if ((component.Flags & ArgsAreXYValues) != 0)
            {
                // An offset is in the space of the composite, unless the flag says that the transform scales it too.
                offset = offsets[i] + deltas[i];
                if ((component.Flags & ScaledComponentOffset) != 0)
                {
                    offset = Vector2.TransformNormal(offset, component.Transform);
                }
            }
            else if ((uint)component.Argument1 < (uint)points.Count && (uint)component.Argument2 < (uint)componentPoints.Count)
            {
                offset = points[component.Argument1].Point - componentPoints[component.Argument2].Point;
            }
            else
            {
                return false;
            }

            int first = points.Count;
            foreach (ControlPoint point in componentPoints)
            {
                points.Add(new ControlPoint(point.Point + offset, point.OnCurve));
            }

            foreach (ushort end in componentEnds)
            {
                endPoints.Add((ushort)(first + end));
            }

            if (points.Count > ushort.MaxValue)
            {
                return false;
            }
        }

        phantoms = GetPhantomDeltas(deltas, components.Count);
        return true;
    }

    private static PhantomDeltas GetPhantomDeltas(Vector2[] deltas, int pointCount)
        => new(deltas[pointCount].X, deltas[pointCount + 1].X, deltas[pointCount + 2].Y, deltas[pointCount + 3].Y);
}
