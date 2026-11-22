// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

public class FontProgramMemoryTests
{
    private const int ArrayHeaderBytes = 24;

    private static readonly MethodInfo SizeOfMethod = typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!;

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    public async Task RetainedByteCountEqualsTheBytesReachableFromTheProgram(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));

        FontProgram font = FontProgram.Load(data);

        await Assert.That(font.RetainedByteCount).IsEqualTo(data.Length + MeasureOwnedBytes(font, data));
    }

    [Test]
    public async Task ATrueTypeProgramRetainsOnlyItsData()
    {
        byte[] data = File.ReadAllBytes(TestFonts.OpenSansFile);

        FontProgram font = FontProgram.Load(data);

        await Assert.That(font.RetainedByteCount).IsEqualTo(data.Length);
    }

    [Test]
    public async Task ACffProgramRetainsItsCharsetAndFontDicts()
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf"));

        FontProgram font = FontProgram.Load(data);

        // One charset of 2 bytes per glyph and one Font DICT record, each with its array header.
        long charset = ArrayHeaderBytes + (2L * font.GlyphCount);
        long fontDicts = ArrayHeaderBytes + FontDictRecordSize();
        await Assert.That(font.RetainedByteCount).IsEqualTo(data.Length + charset + fontDicts);
    }

    [Test]
    public async Task ASliceCountsOnlyTheSlice()
    {
        byte[] font = File.ReadAllBytes(TestFonts.OpenSansFile);
        byte[] larger = new byte[font.Length + 1000];
        font.CopyTo(larger, 500);

        FontProgram sliced = FontProgram.Load(new ReadOnlyMemory<byte>(larger, 500, font.Length));

        await Assert.That(sliced.RetainedByteCount).IsEqualTo(font.Length);
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    public async Task RetainedByteCountDoesNotChangeAfterUse(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        FontProgram font = FontProgram.Load(data);
        long before = font.RetainedByteCount;
        long reachableBefore = MeasureOwnedBytes(font, data);

        Use(font);

        await Assert.That(font.RetainedByteCount).IsEqualTo(before);
        await Assert.That(MeasureOwnedBytes(font, data)).IsEqualTo(reachableBefore);
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    public async Task ALimitEqualToTheRetainedByteCountLoads(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        long size = FontProgram.Load(data).RetainedByteCount;

        FontProgram limited = FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = size });

        await Assert.That(limited.RetainedByteCount).IsEqualTo(size);
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    public async Task ALimitBelowTheRetainedByteCountRefuses(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        long size = FontProgram.Load(data).RetainedByteCount;

        FontProgramTooLargeException? exception = null;
        try
        {
            FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = size - 1 });
        }
        catch (FontProgramTooLargeException e)
        {
            exception = e;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.MaxRetainedBytes).IsEqualTo(size - 1);
        await Assert.That(exception.RetainedByteCount).IsEqualTo(size);
    }

    [Test]
    public async Task ALimitBelowTheDataLengthRefusesBeforeParsing()
    {
        // Zeros are not a font, so a parse would throw the base exception. Only the size check throws the derived one.
        byte[] notAFont = new byte[1000];

        await Assert.That(() => FontProgram.Load(notAFont, new FontProgramOptions { MaxRetainedBytes = 999 }))
            .ThrowsExactly<FontProgramTooLargeException>();
        await Assert.That(() => FontProgram.Load(notAFont, new FontProgramOptions { MaxRetainedBytes = 1000 }))
            .ThrowsExactly<InvalidFontFileException>();
    }

    [Test]
    public async Task ATooLargeFontReportsTheDataLengthAndTheLimit()
    {
        byte[] data = File.ReadAllBytes(TestFonts.OpenSansFile);

        FontProgramTooLargeException exception = (await Assert.That(
            () => FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = 10 }))
            .Throws<FontProgramTooLargeException>())!;

        await Assert.That(exception.RetainedByteCount).IsEqualTo(data.Length);
        await Assert.That(exception.MaxRetainedBytes).IsEqualTo(10);
        await Assert.That(exception).IsAssignableTo<InvalidFontFileException>();
    }

    [Test]
    public async Task NoLimitLoadsAnyFont()
    {
        byte[] data = File.ReadAllBytes(TestFonts.OpenSansFile);

        FontProgram font = FontProgram.Load(data, new FontProgramOptions());

        await Assert.That(font.RetainedByteCount).IsEqualTo(data.Length);
    }

    [Test]
    public async Task ANegativeLimitIsRejected()
        => await Assert.That(() => new FontProgramOptions { MaxRetainedBytes = -1 }).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task NullOptionsAreRejected()
        => await Assert.That(() => FontProgram.Load(File.ReadAllBytes(TestFonts.OpenSansFile), null!)).Throws<ArgumentNullException>();

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    public async Task LoadAllocatesLittleBeyondTheCountedArrays(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        FontProgram warm = FontProgram.Load(data);

        long start = GC.GetAllocatedBytesForCurrentThread();
        FontProgram font = FontProgram.Load(data);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;

        long owned = font.RetainedByteCount - data.Length;
        Console.WriteLine($"{fontFile}: data {data.Length}, owned {owned}, retained {font.RetainedByteCount}, allocated during Load {allocated}");
        await Assert.That(allocated - owned).IsLessThan(1024);
        GC.KeepAlive(warm);
    }

    private static long FontDictRecordSize()
    {
        Type record = typeof(FontProgram).Assembly.GetType("PinataLabs.Typography.Tables.Cff.CffFontDict")!;
        return (int)SizeOfMethod.MakeGenericMethod(record).Invoke(null, null)!;
    }

    private static void Use(FontProgram font)
    {
        NullSink sink = default;
        Span<ushort> encoding = stackalloc ushort[256];
        for (int glyph = 0; glyph < font.GlyphCount; glyph++)
        {
            font.TryWriteOutline((ushort)glyph, ref sink);
            font.TryGetAdvanceWidth((ushort)glyph, out _);
            string? name = font.GetGlyphName((ushort)glyph);
            if (name is not null)
            {
                font.TryGetGlyphId(name, out _);
            }

            font.TryGetGlyphIdForCid(glyph, out _);
        }

        font.TryGetBuiltInEncoding(encoding);
    }

    // Sums the array headers and elements reachable from the program, not counting the font data.
    // It reads the fields, so a field that a later change adds is counted here and the test fails if the property misses it.
    private static long MeasureOwnedBytes(FontProgram font, byte[] data)
    {
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance) { data };
        long total = 0;
        Visit(font, seen, ref total);
        return total;
    }

    private static void Visit(object? value, HashSet<object> seen, ref long total)
    {
        if (value is null || value is Type or Delegate)
        {
            return;
        }

        if (!value.GetType().IsValueType && !seen.Add(value))
        {
            return;
        }

        switch (value)
        {
            case string text:
                total += ArrayHeaderBytes + (2L * text.Length);
                break;
            case Array array:
                Type element = array.GetType().GetElementType()!;
                total += ArrayHeaderBytes + ((long)array.Length * (int)SizeOfMethod.MakeGenericMethod(element).Invoke(null, null)!);
                if (!element.IsPrimitive)
                {
                    foreach (object? item in array)
                    {
                        Visit(item, seen, ref total);
                    }
                }

                break;
            default:
                VisitFields(value, seen, ref total);
                break;
        }
    }

    private static void VisitFields(object value, HashSet<object> seen, ref long total)
    {
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
        {
            return;
        }

        for (Type? t = type; t is not null; t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    Visit(field.GetValue(value), seen, ref total);
                }
            }
        }
    }

    private struct NullSink : IGlyphOutlineSink
    {
        public void MoveTo(Vector2 point)
        {
        }

        public void LineTo(Vector2 point)
        {
        }

        public void QuadraticBezierTo(Vector2 control, Vector2 point)
        {
        }

        public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        {
        }

        public void ClosePath()
        {
        }
    }
}
