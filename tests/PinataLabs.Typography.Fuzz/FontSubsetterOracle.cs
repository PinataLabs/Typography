// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Runs one input through <see cref="FontSubsetter"/> and checks its contract. The input is a font file, and the oracle asks for glyphs
/// that it derives from the input. A subset may throw <see cref="InvalidFontFileException"/> and nothing else, and a glyph ID
/// that the font does not have is only asked for when <see cref="FontProgram"/> could not load the font. A subset that is returned
/// loads, has the glyph count of the font, draws each asked glyph as the font did, and draws every other glyph as the font did or draws
/// nothing. A limit on the retained bytes refuses a font that its data fills, and a generous limit changes nothing.
/// A broken rule throws <see cref="FuzzFindingException"/>.
/// </summary>
/// <remarks>
/// The fuzz harness and the deterministic mutation test in the test project both compile this file,
/// so a fuzz finding and a CI failure are judged by the same rules.
/// </remarks>
internal static class FontSubsetterOracle
{
    /// <summary>
    /// The input is a font file in a format that <see cref="FontSubsetter"/> reads.
    /// </summary>
    public const string Target = "font-subsetter";

    // What a subset may allocate beyond 8 times the font: the arrays that are charged to the limit are at most
    // 16 bytes for each of 65,536 glyphs, and 256 Font DICT records, and the rest is small.
    private const long AllocationOverheadBytes = 2 * 1024 * 1024;

    // A font with more glyphs than this has its glyphs sampled, as the asked glyphs and a stride of the rest.
    private const int MaxFullCheck = 600;

    /// <summary>
    /// Runs the input.
    /// </summary>
    /// <param name="data">The input.</param>
    /// <returns>What happened, for replay.</returns>
    /// <exception cref="FuzzFindingException">The input broke the contract.</exception>
    public static string Run(ReadOnlySpan<byte> data)
    {
        byte[] font = data.ToArray();
        FontProgram? original = TryLoad(font);
        ushort[] glyphs = PickGlyphs(font, original?.GlyphCount);

        long before = GC.GetAllocatedBytesForCurrentThread();
        byte[] subset;
        try
        {
            subset = FontSubsetter.Subset(font, glyphs);
        }
        catch (InvalidFontFileException)
        {
            return "rejected";
        }
        catch (ArgumentOutOfRangeException) when (original is null)
        {
            return "rejected, glyph out of range";
        }
        catch (Exception e)
        {
            throw new FuzzFindingException($"Subset threw {e.GetType().Name}.", e);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long maxAllocated = (8L * font.Length) + AllocationOverheadBytes;
        if (allocated > maxAllocated)
        {
            throw new FuzzFindingException($"Subset of {font.Length} bytes allocated {allocated} bytes, above {maxAllocated}.");
        }

        CheckLimit(font, glyphs, subset);
        if (original is null)
        {
            return $"subset of a font that does not load, {font.Length} -> {subset.Length} bytes";
        }

        FontProgram after;
        try
        {
            after = FontProgram.Load(subset);
        }
        catch (Exception e)
        {
            throw new FuzzFindingException($"The subset does not load: {e.GetType().Name}.", e);
        }

        Compare(original, after, glyphs);
        return $"loaded {original.Format}, subset {glyphs.Length} of {original.GlyphCount} glyphs, {font.Length} -> {subset.Length} bytes";
    }

    private static FontProgram? TryLoad(byte[] font)
    {
        try
        {
            return FontProgram.Load(font);
        }
        catch (InvalidFontFileException)
        {
            return null;
        }
        catch (Exception e)
        {
            throw new FuzzFindingException($"Load threw {e.GetType().Name}.", e);
        }
    }

    // The first glyph, the last, and a few in between that the bytes of the font choose. Without a loaded font, some low glyph IDs.
    private static ushort[] PickGlyphs(byte[] font, int? glyphCount)
    {
        if (glyphCount is not int count)
        {
            return [0, 1, 2, 5];
        }

        uint state = (uint)font.Length ^ 0x9E3779B9;
        ushort[] glyphs = new ushort[6];
        glyphs[0] = (ushort)(count - 1);
        glyphs[1] = (ushort)Math.Min(1, count - 1);
        for (int i = 2; i < glyphs.Length; i++)
        {
            state = (state * 1664525) + 1013904223 + font[(i * 31) % font.Length];
            glyphs[i] = (ushort)((state >> 8) % (uint)count);
        }

        return glyphs;
    }

    // A generous limit changes nothing, and a limit under the font data refuses it.
    private static void CheckLimit(byte[] font, ushort[] glyphs, byte[] subset)
    {
        try
        {
            byte[] limited = FontSubsetter.Subset(
                font,
                glyphs,
                new FontSubsetOptions { ProgramOptions = new FontProgramOptions { MaxRetainedBytes = (4L * font.Length) + (1024 * 1024) } });
            if (!limited.AsSpan().SequenceEqual(subset))
            {
                throw new FuzzFindingException("A generous limit on the retained bytes changed the subset.");
            }

            FontSubsetter.Subset(font, glyphs, new FontSubsetOptions { ProgramOptions = new FontProgramOptions { MaxRetainedBytes = font.Length / 2 } });
        }
        catch (FontProgramTooLargeException e) when (e.MaxRetainedBytes == font.Length / 2)
        {
            return;
        }
        catch (Exception e) when (e is not FuzzFindingException)
        {
            throw new FuzzFindingException($"A subset under a limit threw {e.GetType().Name}.", e);
        }

        throw new FuzzFindingException($"A limit of {font.Length / 2} bytes, below the font data, did not refuse the font.");
    }

    private static void Compare(FontProgram original, FontProgram subset, ushort[] glyphs)
    {
        if (subset.Format != original.Format || subset.GlyphCount != original.GlyphCount || subset.IsCidKeyed != original.IsCidKeyed)
        {
            throw new FuzzFindingException($"The subset is {subset.Format} with {subset.GlyphCount} glyphs, and the font is {original.Format} with {original.GlyphCount}.");
        }

        HashSet<ushort> asked = [0, .. glyphs];
        int stride = Math.Max(1, original.GlyphCount / MaxFullCheck);
        for (int i = 0; i < original.GlyphCount; i += asked.Contains((ushort)i) ? 1 : stride)
        {
            ushort glyphId = (ushort)i;
            Recorder expected = default;
            Recorder actual = default;
            bool drew = original.TryWriteOutline(glyphId, ref expected);
            bool subsetDrew = subset.TryWriteOutline(glyphId, ref actual);
            bool same = drew == subsetDrew && expected.Hash == actual.Hash && expected.Calls == actual.Calls;
            if (asked.Contains(glyphId))
            {
                if (drew && !same)
                {
                    throw new FuzzFindingException($"Glyph {glyphId} was asked for and draws another outline.");
                }
            }
            else if (!same && !(subsetDrew && actual.Calls == 0))
            {
                throw new FuzzFindingException($"Glyph {glyphId} was not asked for, and draws neither as it did nor nothing.");
            }
        }
    }

    // Hashes an outline and counts its calls, so that two outlines compare.
    private struct Recorder : IGlyphOutlineSink
    {
        private ulong hash;

        public int Calls { get; private set; }

        public readonly ulong Hash => this.hash;

        public void MoveTo(Vector2 point) => this.Add(1, point, default, default);

        public void LineTo(Vector2 point) => this.Add(2, point, default, default);

        public void QuadraticBezierTo(Vector2 control, Vector2 point) => this.Add(3, control, point, default);

        public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point) => this.Add(4, control1, control2, point);

        public void ClosePath() => this.Add(5, default, default, default);

        private void Add(int kind, Vector2 a, Vector2 b, Vector2 c)
        {
            this.Calls++;
            this.hash = Mix(Mix(Mix(Mix(Mix(Mix(Mix(this.hash, (ulong)kind), BitConverter.SingleToUInt32Bits(a.X)), BitConverter.SingleToUInt32Bits(a.Y)), BitConverter.SingleToUInt32Bits(b.X)), BitConverter.SingleToUInt32Bits(b.Y)), BitConverter.SingleToUInt32Bits(c.X)), BitConverter.SingleToUInt32Bits(c.Y));
        }

        private static ulong Mix(ulong hash, ulong value) => (hash ^ value) * 1099511628211UL;
    }
}
