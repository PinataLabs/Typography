// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// Picks the face that best matches a requested style among the faces of a family, as CSS Fonts Level 4
/// section 5.2 "Matching font styles" does: it narrows the faces by width, then by slope, then by weight.
/// </summary>
/// <remarks>
/// A face covers a range of each part, so the search treats each part as an interval, and a face
/// "has" a value when its interval contains it. Each step looks for the value that the CSS search
/// order reaches first, and keeps only the faces that have that value.
/// </remarks>
internal static class FontStyleMatcher
{
    private const int StackLimit = 64;

    // The matching of an oblique request switches direction at this angle, as CSS says.
    private const float ObliqueThreshold = 11F;
    private const float MaxObliqueAngle = 90F;

    // The start of the positive angles, where a search that says "until 0" stops.
    private const float AboveZero = float.Epsilon;

    private enum Dimension
    {
        Width,
        Weight,
        Oblique,
    }

    /// <summary>
    /// Gets the index of the face that best matches a style.
    /// </summary>
    /// <param name="faces">The style ranges of the faces of a family.</param>
    /// <param name="style">The requested style.</param>
    /// <returns>
    /// The index in <paramref name="faces"/> of the best match, which is the first of the faces that tie,
    /// or -1 when there are no faces.
    /// </returns>
    public static int Match(ReadOnlySpan<FontStyleRange> faces, FontStyle style)
    {
        if (faces.IsEmpty)
        {
            return -1;
        }

        Span<int> indexes = faces.Length <= StackLimit ? stackalloc int[faces.Length] : new int[faces.Length];
        Candidates candidates = new(faces, indexes);

        candidates.NarrowByWidth(style.Width.Percent);
        candidates.NarrowBySlope(style.Slope);
        candidates.NarrowByWeight(style.Weight.Value);
        return candidates.First;
    }

    /// <summary>
    /// The faces that are still in the running, as indexes in the order of the family.
    /// </summary>
    private ref struct Candidates
    {
        private readonly ReadOnlySpan<FontStyleRange> faces;
        private readonly Span<int> indexes;
        private int count;
        private Dimension dimension;
        private bool mirrored;

        public Candidates(ReadOnlySpan<FontStyleRange> faces, Span<int> indexes)
        {
            this.faces = faces;
            this.indexes = indexes;
            this.count = faces.Length;
            this.dimension = Dimension.Width;
            this.mirrored = false;
            for (int i = 0; i < faces.Length; i++)
            {
                indexes[i] = i;
            }
        }

        public readonly int First => this.indexes[0];

        public void NarrowByWidth(float percent)
        {
            this.Use(Dimension.Width, mirrored: false);

            // A width of 100% or less tries the narrower faces first, a wider request the wider faces first.
            _ = percent <= 100F
                ? this.Down(percent, float.NegativeInfinity) || this.Up(percent, float.PositiveInfinity)
                : this.Up(percent, float.PositiveInfinity) || this.Down(percent, float.NegativeInfinity);
        }

        public void NarrowBySlope(FontSlope slope)
        {
            this.Use(Dimension.Oblique, mirrored: false);
            switch (slope.Kind)
            {
                case FontSlopeKind.Italic:
                    // Italic faces, then oblique faces from the threshold up, then those below it down to 0,
                    // then upright faces and the faces that lean backwards.
                    _ = this.NarrowToItalic()
                        || this.Up(ObliqueThreshold, MaxObliqueAngle)
                        || this.Down(ObliqueThreshold, AboveZero)
                        || this.Down(0F, -MaxObliqueAngle);
                    break;

                case FontSlopeKind.Normal:
                    // Upright faces, then the least oblique faces, then italic faces, then the faces that lean backwards.
                    _ = this.Up(0F, MaxObliqueAngle)
                        || this.NarrowToItalic()
                        || this.Down(-AboveZero, -MaxObliqueAngle);
                    break;

                default:
                    // A request that leans backwards follows the steps for the angle with the sign taken off,
                    // with the angles of the faces negated and the directions reversed.
                    this.mirrored = slope.ObliqueAngle < 0F;
                    float angle = Math.Abs(slope.ObliqueAngle);
                    bool found = angle >= ObliqueThreshold
                        ? this.Up(angle, MaxObliqueAngle) || this.Down(angle, AboveZero)
                        : this.Up(angle, angle) || this.Down(angle, AboveZero) || this.Up(angle, MaxObliqueAngle);

                    // Italic faces, then upright faces and the faces that lean the other way.
                    _ = found || this.NarrowToItalic() || this.Down(0F, -MaxObliqueAngle);
                    break;
            }
        }

        public void NarrowByWeight(float weight)
        {
            this.Use(Dimension.Weight, mirrored: false);
            if (weight < 400F)
            {
                _ = this.Down(weight, float.NegativeInfinity) || this.Up(weight, float.PositiveInfinity);
            }
            else if (weight <= 500F)
            {
                // From the weight up to 500, then the lighter weights, then the weights above 500.
                _ = this.Up(weight, 500F)
                    || this.Down(weight, float.NegativeInfinity)
                    || this.Up(500F, float.PositiveInfinity);
            }
            else
            {
                _ = this.Up(weight, float.PositiveInfinity) || this.Down(weight, float.NegativeInfinity);
            }
        }

        private void Use(Dimension dimension, bool mirrored)
        {
            this.dimension = dimension;
            this.mirrored = mirrored;
        }

        // Looks for the smallest value from `from` up to `to` that a face has, and keeps the faces that have it.
        private bool Up(float from, float to)
        {
            float best = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < this.count; i++)
            {
                (float start, float end) = this.Interval(this.indexes[i]);
                float low = Math.Max(start, from);
                if (low <= Math.Min(end, to) && low < best)
                {
                    best = low;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            this.NarrowTo(best);
            return true;
        }

        // Looks for the largest value from `from` down to `to` that a face has, and keeps the faces that have it.
        private bool Down(float from, float to)
        {
            float best = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < this.count; i++)
            {
                (float start, float end) = this.Interval(this.indexes[i]);
                float high = Math.Min(end, from);
                if (high >= Math.Max(start, to) && high > best)
                {
                    best = high;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            this.NarrowTo(best);
            return true;
        }

        private bool NarrowToItalic()
        {
            int kept = 0;
            for (int i = 0; i < this.count; i++)
            {
                if (this.faces[this.indexes[i]].HasItalic)
                {
                    this.indexes[kept++] = this.indexes[i];
                }
            }

            if (kept == 0)
            {
                return false;
            }

            this.count = kept;
            return true;
        }

        private void NarrowTo(float value)
        {
            int kept = 0;
            for (int i = 0; i < this.count; i++)
            {
                (float start, float end) = this.Interval(this.indexes[i]);
                if (start <= value && value <= end)
                {
                    this.indexes[kept++] = this.indexes[i];
                }
            }

            this.count = kept;
        }

        private readonly (float Start, float End) Interval(int index)
        {
            FontStyleRange face = this.faces[index];
            switch (this.dimension)
            {
                case Dimension.Width:
                    return (face.MinWidth.Percent, face.MaxWidth.Percent);
                case Dimension.Weight:
                    return (face.MinWeight.Value, face.MaxWeight.Value);
                default:
                    (float start, float end) = face.ObliqueInterval;
                    return this.mirrored ? (-end, -start) : (start, end);
            }
        }
    }
}
