// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Runs one input through the font program API and checks the contract of issue #74.
/// Load may throw <see cref="InvalidFontFileException"/> and nothing else. Once a program has loaded,
/// no call throws, every call agrees with the others, the outline protocol holds, and allocation stays
/// under the bounds that the <see cref="FontProgram"/> remarks state.
/// A broken rule throws <see cref="FuzzFindingException"/>, which no library code throws.
/// </summary>
/// <remarks>
/// The fuzz harness and the deterministic mutation test in the test project both compile this file,
/// so a fuzz finding and a CI failure are judged by the same rules.
/// </remarks>
internal static class FontProgramOracle
{
    /// <summary>
    /// The input is a font file: <see cref="FontProgram.Load(ReadOnlyMemory{byte})"/>, which also takes bare CFF.
    /// </summary>
    public const string Program = "program";

    /// <summary>
    /// The input is a Type 1 font with its private part in plain text after <c>eexec</c>.
    /// The oracle encrypts it before the load, so a mutation lands where it was made
    /// instead of garbling every byte after it.
    /// </summary>
    public const string Type1Plain = "type1-plain";

    /// <summary>
    /// The largest number of sink calls that one outline can make. The engines stop at 1,000,000.
    /// A CFF engine can write one more ClosePath after it stops.
    /// </summary>
    public const int MaxSinkCalls = 1_000_001;

    /// <summary>
    /// What a load may allocate beyond what the program retains: the transient copy of a Type 1
    /// cleartext split over PFB segments (no larger than the data), and a fixed amount for the objects of the program.
    /// </summary>
    public const long LoadOverheadBytes = 64 * 1024;

    private const long FixedRetainedBytes = 32 * 1024;

    public static readonly string[] Targets = [Program, Type1Plain];

    public static bool IsKnown(string target) => Array.IndexOf(Targets, target) >= 0;

    /// <summary>
    /// Runs the input.
    /// </summary>
    /// <param name="target">The target, <see cref="Program"/> or <see cref="Type1Plain"/>.</param>
    /// <param name="data">The input.</param>
    /// <returns>What happened, for replay: a rejection or a summary of the loaded program.</returns>
    /// <exception cref="FuzzFindingException">The input broke the contract.</exception>
    public static string Run(string target, ReadOnlySpan<byte> data)
        => target switch
        {
            Program => RunProgram(data.ToArray()),
            Type1Plain => RunProgram(EncryptType1PrivatePart(data)),
            _ => throw new ArgumentException($"Unknown fuzz target '{target}'.", nameof(target)),
        };

    /// <summary>
    /// Gets the most that <see cref="FontProgram.RetainedByteCount"/> may be for data of a given length and format.
    /// TrueType keeps only the data. CFF adds a charset of 2 bytes per glyph, and a glyph takes at least
    /// a byte of the CharStrings INDEX, plus at most 256 Font DICT records. Type 1 keeps a private part no larger
    /// than the data, 15 bytes for each glyph, which takes at least 5 bytes of the private part, and 8 bytes for
    /// each subroutine index up to the highest present, which is below the length of the private part.
    /// </summary>
    public static long MaxRetainedBytes(FontProgramFormat format, long dataLength)
        => format switch
        {
            FontProgramFormat.TrueType => dataLength,
            FontProgramFormat.Cff => (3 * dataLength) + FixedRetainedBytes,
            _ => (12 * dataLength) + FixedRetainedBytes,
        };

    /// <summary>
    /// Encrypts the part of a Type 1 font that follows <c>eexec</c> and its line break.
    /// Without <c>eexec</c> the data is returned as it is.
    /// </summary>
    public static byte[] EncryptType1PrivatePart(ReadOnlySpan<byte> data)
    {
        int eexec = data.IndexOf("eexec"u8);
        if (eexec < 0)
        {
            return data.ToArray();
        }

        int start = eexec + 5;
        if (start < data.Length && data[start] == (byte)'\n')
        {
            start++;
        }

        ReadOnlySpan<byte> plain = data.Slice(start);
        byte[] result = new byte[start + 4 + plain.Length];
        data.Slice(0, start).CopyTo(result);
        ushort r = 55665;
        for (int i = 0; i < 4 + plain.Length; i++)
        {
            byte value = i < 4 ? (byte)0 : plain[i - 4];
            byte cipher = (byte)(value ^ (r >> 8));
            result[start + i] = cipher;
            r = (ushort)(((cipher + r) * 52845) + 22719);
        }

        return result;
    }

    private static string RunProgram(byte[] data)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        FontProgram font;
        try
        {
            font = FontProgram.Load(data);
        }
        catch (InvalidFontFileException)
        {
            return "rejected";
        }
        catch (Exception e)
        {
            throw new FuzzFindingException($"Load threw {e.GetType().Name}.", e);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long maxRetained = MaxRetainedBytes(font.Format, data.Length);
        if (font.RetainedByteCount < 0 || font.RetainedByteCount > maxRetained)
        {
            throw new FuzzFindingException($"{font.Format} retains {font.RetainedByteCount} bytes of {data.Length}, above {maxRetained}.");
        }

        long maxAllocated = font.RetainedByteCount + data.Length + LoadOverheadBytes;
        if (allocated > maxAllocated)
        {
            throw new FuzzFindingException($"{font.Format} load allocated {allocated} bytes, above {maxAllocated}.");
        }

        CheckRetainedByteLimit(data, font.RetainedByteCount);
        ProgramFont adapter = new(font);
        Exercise(ref adapter);
        return $"loaded {font.Format}, {font.GlyphCount} glyphs, {font.RetainedByteCount} bytes retained";
    }

    // A limit equal to the retained size loads the same program, and a limit one byte below it refuses the font.
    private static void CheckRetainedByteLimit(byte[] data, long retained)
    {
        try
        {
            FontProgram atLimit = FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = retained });
            if (atLimit.RetainedByteCount != retained)
            {
                throw new FuzzFindingException($"A load under a limit retains {atLimit.RetainedByteCount} bytes, not {retained}.");
            }

            FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = retained - 1 });
        }
        catch (FontProgramTooLargeException e) when (e.MaxRetainedBytes == retained - 1)
        {
            return;
        }
        catch (Exception e) when (e is not FuzzFindingException)
        {
            throw new FuzzFindingException($"A load under a limit threw {e.GetType().Name}.", e);
        }

        throw new FuzzFindingException($"A limit of {retained - 1} bytes loaded a program that retains {retained}.");
    }

    private static void Exercise<TFont>(ref TFont font)
        where TFont : struct, IFuzzFont
    {
        try
        {
            if (font.GlyphCount is < 1 or > ushort.MaxValue + 1)
            {
                throw new FuzzFindingException($"The glyph count {font.GlyphCount} is out of range.");
            }

            // A static table that the input reaches for the first time allocates once. A second pass allocates nothing.
            long allocated = ExerciseOnce(ref font, out long allowed);
            if (allocated > allowed)
            {
                allocated = ExerciseOnce(ref font, out allowed);
                if (allocated > allowed)
                {
                    throw new FuzzFindingException($"Glyph calls allocated {allocated} bytes, above {allowed} for the names they returned.");
                }
            }
        }
        catch (FuzzFindingException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new FuzzFindingException($"A call after load threw {e.GetType().Name}.", e);
        }
    }

    // Returns the bytes that the calls allocated, and the bytes that the returned names account for.
    private static long ExerciseOnce<TFont>(ref TFont font, out long allowed)
        where TFont : struct, IFuzzFont
    {
        int glyphCount = font.GlyphCount;
        allowed = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();

        Span<ushort> codes = stackalloc ushort[256];
        codes.Fill(ushort.MaxValue);
        if (font.TryGetBuiltInEncoding(codes))
        {
            foreach (ushort glyphId in codes)
            {
                if (glyphId >= glyphCount)
                {
                    throw new FuzzFindingException($"The built-in encoding maps a code to glyph {glyphId} of {glyphCount}.");
                }
            }
        }
        else if (codes.IndexOfAnyExcept(ushort.MaxValue) >= 0)
        {
            throw new FuzzFindingException("TryGetBuiltInEncoding wrote glyph IDs and returned false.");
        }

        foreach (int cid in (ReadOnlySpan<int>)[0, 1, 700, glyphCount - 1, glyphCount, ushort.MaxValue, -1, int.MaxValue])
        {
            if (font.TryGetGlyphIdForCid(cid, out ushort glyphId) && glyphId >= glyphCount)
            {
                throw new FuzzFindingException($"CID {cid} maps to glyph {glyphId} of {glyphCount}.");
            }
        }

        font.TryGetGlyphId("A", out _);
        font.TryGetGlyphId(".notdef", out _);
        font.TryGetGlyphId(string.Empty, out _);

        if (glyphCount <= ushort.MaxValue)
        {
            CheckingSink outside = default;
            if (font.TryWriteOutline((ushort)glyphCount, ref outside) || font.TryGetAdvanceWidth((ushort)glyphCount, out _))
            {
                throw new FuzzFindingException($"Glyph {glyphCount} is out of range and was found.");
            }
        }

        for (int i = 0; i < glyphCount; i = NextSample(i, glyphCount))
        {
            ushort glyphId = (ushort)i;
            CheckingSink sink = default;
            bool written = font.TryWriteOutline(glyphId, ref sink);
            sink.Check(glyphId, written);
            if (i < 8)
            {
                // A program has no state that changes, so a second outline matches the first.
                CheckingSink again = default;
                if (font.TryWriteOutline(glyphId, ref again) != written || again.Hash != sink.Hash)
                {
                    throw new FuzzFindingException($"Glyph {glyphId} wrote a different outline the second time.");
                }
            }

            font.TryGetAdvanceWidth(glyphId, out _);

            string? name = font.GetGlyphName(glyphId);
            if (name is not null)
            {
                allowed += 32 + (2L * name.Length);
                if (!font.TryGetGlyphId(name, out ushort found) || found > glyphId || font.GetGlyphName(found) != name)
                {
                    throw new FuzzFindingException($"Glyph {glyphId} is named '{name}', and the name finds glyph {found}.");
                }

                allowed += 32 + (2L * name.Length);
            }
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Every glyph of a small font. Of a large one, the first 256, the last 16 and 64 between.
    private static int NextSample(int i, int glyphCount)
    {
        if (glyphCount <= 512 || i < 255 || i >= glyphCount - 16)
        {
            return i + 1;
        }

        int step = Math.Max(1, (glyphCount - 272) / 64);
        return Math.Min(i + step, glyphCount - 16);
    }

    /// <summary>
    /// The calls that a PDF renderer makes on a font program, over <see cref="FontProgram"/>.
    /// </summary>
    private interface IFuzzFont
    {
        int GlyphCount { get; }

        bool TryWriteOutline(ushort glyphId, ref CheckingSink sink);

        bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth);

        string? GetGlyphName(ushort glyphId);

        bool TryGetGlyphId(ReadOnlySpan<char> name, out ushort glyphId);

        bool TryGetGlyphIdForCid(int cid, out ushort glyphId);

        bool TryGetBuiltInEncoding(Span<ushort> glyphIds);
    }

    private readonly struct ProgramFont : IFuzzFont
    {
        private readonly FontProgram font;

        public ProgramFont(FontProgram font) => this.font = font;

        public int GlyphCount => this.font.GlyphCount;

        public bool TryWriteOutline(ushort glyphId, ref CheckingSink sink) => this.font.TryWriteOutline(glyphId, ref sink);

        public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth) => this.font.TryGetAdvanceWidth(glyphId, out advanceWidth);

        public string? GetGlyphName(ushort glyphId) => this.font.GetGlyphName(glyphId);

        public bool TryGetGlyphId(ReadOnlySpan<char> name, out ushort glyphId) => this.font.TryGetGlyphId(name, out glyphId);

        public bool TryGetGlyphIdForCid(int cid, out ushort glyphId) => this.font.TryGetGlyphIdForCid(cid, out glyphId);

        public bool TryGetBuiltInEncoding(Span<ushort> glyphIds) => this.font.TryGetBuiltInEncoding(glyphIds);
    }

    /// <summary>
    /// Counts the calls of an outline and checks their order: each contour is a MoveTo, segments, then a ClosePath.
    /// A failed outline may stop part way, so only a written one must end with its contour closed.
    /// </summary>
    private struct CheckingSink : IGlyphOutlineSink
    {
        private bool open;
        private string? error;

        public int Calls { get; private set; }

        public int Hash { get; private set; }

        public readonly void Check(ushort glyphId, bool written)
        {
            string? error = this.error ?? (written && this.open ? "ends with an open contour" : null);
            if (error is not null)
            {
                throw new FuzzFindingException($"The outline of glyph {glyphId} {error}.");
            }

            if (this.Calls > MaxSinkCalls)
            {
                throw new FuzzFindingException($"The outline of glyph {glyphId} made {this.Calls} sink calls.");
            }
        }

        public void MoveTo(Vector2 point)
        {
            this.Record(1, point);
            this.Expect(!this.open, "moves while a contour is open");
            this.open = true;
        }

        public void LineTo(Vector2 point)
        {
            this.Record(2, point);
            this.Expect(this.open, "draws a line outside a contour");
        }

        public void QuadraticBezierTo(Vector2 control, Vector2 point)
        {
            this.Record(3, control + point);
            this.Expect(this.open, "draws a curve outside a contour");
        }

        public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        {
            this.Record(4, control1 + control2 + point);
            this.Expect(this.open, "draws a curve outside a contour");
        }

        public void ClosePath()
        {
            this.Record(5, default);
            this.Expect(this.open, "closes a contour that is not open");
            this.open = false;
        }

        private void Record(int kind, Vector2 point)
        {
            this.Calls++;
            this.Hash = HashCode.Combine(this.Hash, kind, point.X, point.Y);
        }

        private void Expect(bool condition, string error)
        {
            if (!condition)
            {
                this.error ??= error;
            }
        }
    }
}

/// <summary>
/// The input broke the contract of the font program API. No library code throws this type.
/// </summary>
internal sealed class FuzzFindingException : Exception
{
    public FuzzFindingException(string message)
        : base(message)
    {
    }

    public FuzzFindingException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
