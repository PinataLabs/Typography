// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Damages a font one byte at a time and at every length, and checks the two error rules:
/// load throws only <see cref="InvalidFontFileException"/>, and no call after load throws.
/// </summary>
public class FontProgramRobustnessTests
{
    [Test]
    [Arguments("cff-type2.otf")]
    [Arguments("cff-type2-widths.otf")]
    [Arguments("cff-seac.otf")]
    [Arguments("cff-cid-fdselect0.otf")]
    [Arguments("cff-cid-fdselect3.otf")]
    [Arguments("cff-encoding-format0.otf")]
    [Arguments("cff-encoding-format1.otf")]
    [Arguments("cff-expertsubset.otf")]
    [Arguments("tt-composite.ttf")]
    [Arguments("tt-names.ttf")]
    public async Task DamagedFontFailsOnlyInTheDocumentedWays(string fontFile)
    {
        byte[] original = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/" + fontFile));
        List<string> failures = [];

        // The same damage on every run.
        Random random = new(20261122);
        byte[] data = (byte[])original.Clone();
        for (int position = 0; position < original.Length; position++)
        {
            byte value = original[position];
            foreach (byte damaged in (byte[])[0x00, 0xFF, (byte)(value ^ 0x80), (byte)(value + 1), (byte)random.Next(256)])
            {
                data[position] = damaged;
                Exercise(data, $"byte {position} = {damaged}", failures);
            }

            data[position] = value;
        }

        for (int length = 0; length < original.Length; length++)
        {
            Exercise(original.AsMemory(0, length), $"length {length}", failures);
        }

        await Assert.That(failures.Count > 10 ? failures.GetRange(0, 10) : failures).IsEmpty();
    }

    private static void Exercise(ReadOnlyMemory<byte> data, string damage, List<string> failures)
    {
        FontProgram font;
        try
        {
            font = FontProgram.Load(data);
        }
        catch (InvalidFontFileException)
        {
            return;
        }
        catch (Exception e)
        {
            failures.Add($"{damage}: load threw {e.GetType().Name}");
            return;
        }

        try
        {
            Span<ushort> codes = stackalloc ushort[256];
            font.TryGetBuiltInEncoding(codes);
            font.TryGetGlyphId("A", out _);
            font.TryGetGlyphId("custom_b", out _);
            font.TryGetGlyphIdForCid(700, out _);

            // Damage can raise the glyph count to 65535, so take glyphs from both ends.
            for (int i = 0; i < font.GlyphCount; i = i < 64 ? i + 1 : Math.Max(i + 1, font.GlyphCount - 8))
            {
                DiscardingSink sink = default;
                font.TryWriteOutline((ushort)i, ref sink);
                font.TryGetAdvanceWidth((ushort)i, out _);
                font.GetGlyphName((ushort)i);
            }
        }
        catch (Exception e)
        {
            failures.Add($"{damage}: a call after load threw {e.GetType().Name}: {e.StackTrace?.Split('\n')[0].Trim()}");
        }
    }

    private struct DiscardingSink : IGlyphOutlineSink
    {
        public readonly void MoveTo(Vector2 point)
        {
        }

        public readonly void LineTo(Vector2 point)
        {
        }

        public readonly void QuadraticBezierTo(Vector2 control, Vector2 point)
        {
        }

        public readonly void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        {
        }

        public readonly void ClosePath()
        {
        }
    }
}
