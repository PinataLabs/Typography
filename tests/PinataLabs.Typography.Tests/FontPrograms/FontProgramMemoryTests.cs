// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        long bytesBefore = Walk(font, data, out HashSet<object> objectsBefore);

        Use(font);

        long bytesAfter = Walk(font, data, out HashSet<object> objectsAfter);
        await Assert.That(font.RetainedByteCount).IsEqualTo(before);
        await Assert.That(bytesAfter).IsEqualTo(bytesBefore);
        await Assert.That(objectsAfter.SetEquals(objectsBefore)).IsTrue();
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
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    public async Task ARefusalCarriesTheSizeThatLoads(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        long size = FontProgram.Load(data).RetainedByteCount;
        long[] limits = [data.Length, data.Length + 1, data.Length + ((size - data.Length) / 2), size - ArrayHeaderBytes - 1, size - 1];

        foreach (long limit in limits)
        {
            FontProgramTooLargeException refusal = (await Assert.That(
                () => FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = limit }))
                .Throws<FontProgramTooLargeException>())!;

            await Assert.That(refusal.RetainedByteCount).IsEqualTo(size);
            await Assert.That(FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = refusal.RetainedByteCount }).RetainedByteCount).IsEqualTo(size);
        }
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
        FontProgram font = FontProgram.Load(data);

        // The runtime can charge its own work, such as compiling a hot loop, to the thread during one load.
        // The load itself allocates the same each time, so the least of a few loads measures it.
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            font = FontProgram.Load(data);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - start);
        }

        long owned = font.RetainedByteCount - data.Length;
        Console.WriteLine($"{fontFile}: data {data.Length}, owned {owned}, retained {font.RetainedByteCount}, allocated during Load {allocated}");
        await Assert.That(allocated - owned).IsLessThan(1024);
    }

    [Test]
    public async Task StaticFieldsHoldOnlyReadOnlyTables()
    {
        List<string> offenders = [];
        foreach (Type type in FontProgramTypes())
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string name = $"{(type.DeclaringType is null ? type.Name : type.DeclaringType.Name + "+" + type.Name)}.{field.Name}";
                if (field.IsLiteral || type.Name == "<>c" || HoldsNoReferences(field.FieldType) || (ReadOnlyTables.Contains(name) && field.IsInitOnly && IsImmutableTable(field.FieldType)))
                {
                    continue;
                }

                offenders.Add(name);
            }
        }

        await Assert.That(offenders).IsEmpty();
    }

    // A static of a type with no references is a fixed number of bytes, so it cannot grow.
    // The compiler caches each static lambda in one delegate for the process, whatever the number of fonts.
    // A static that use writes is memory that RetainedByteCount does not count and the walk cannot see.
    // These tables are filled once by their initializer and never written, and their elements are values or strings.
    private static readonly HashSet<string> ReadOnlyTables =
    [
        "MacGlyphNames.Names",
        "CffPredefinedTables.ExpertCharset",
        "CffPredefinedTables.ExpertSubsetCharset",
        "CffPredefinedTables.StandardEncoding",
        "CffPredefinedTables.ExpertEncoding",
        "CffStandardStrings.StringIdentifierToString",
        "CffStandardStrings.<Count>k__BackingField",
    ];

    private static bool HoldsNoReferences(Type type)
        => type.IsPrimitive
        || type.IsEnum
        || (type.IsValueType && type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(f => HoldsNoReferences(f.FieldType)));

    private static bool IsImmutableTable(Type type)
    {
        Type item = type.IsArray ? type.GetElementType()! : type;
        return item.IsPrimitive || item == typeof(string);
    }

    private static HashSet<Type> FontProgramTypes()
    {
        Assembly assembly = typeof(FontProgram).Assembly;
        HashSet<Type> types = [];
        Queue<Type> pending = new(assembly.GetTypes().Where(t =>
            t.Namespace is "PinataLabs.Typography.FontPrograms" or "PinataLabs.Typography.Tables.Cff" || t == typeof(FontProgram)));
        while (pending.Count > 0)
        {
            Type type = pending.Dequeue();
            if (type.Assembly != assembly || !types.Add(type))
            {
                continue;
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Type fieldType = field.FieldType;
                while (fieldType.HasElementType)
                {
                    fieldType = fieldType.GetElementType()!;
                }

                pending.Enqueue(fieldType.IsGenericType ? fieldType.GetGenericTypeDefinition() : fieldType);
                foreach (Type argument in fieldType.IsGenericType ? fieldType.GetGenericArguments() : [])
                {
                    pending.Enqueue(argument);
                }
            }

            if (type.BaseType is not null)
            {
                pending.Enqueue(type.BaseType);
            }
        }

        return types;
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

    // Walks the instance fields from the program and sums the array headers and elements it reaches, not counting the font data.
    // It sees a reference that a field holds, so a cache in an instance field fails the test when use changes the objects reachable.
    // It cannot see a static field, so StaticFieldsHoldOnlyReadOnlyTables covers those.
    private static long MeasureOwnedBytes(FontProgram font, byte[] data)
        => Walk(font, data, out _);

    private static long Walk(FontProgram font, byte[] data, out HashSet<object> reachable)
    {
        reachable = new(ReferenceEqualityComparer.Instance) { data };
        long total = 0;
        Visit(font, reachable, ref total);
        return total;
    }

    private static void Visit(object? value, HashSet<object> seen, ref long total)
    {
        if (value is null || !seen.Add(value))
        {
            return;
        }

        switch (value)
        {
            case Type:
                break;
            case Delegate action:
                Visit(action.Target, seen, ref total);
                break;
            case string text:
                total += ArrayHeaderBytes + (2L * text.Length);
                break;
            case Array array:
                Type element = array.GetType().GetElementType()!;
                total += ArrayHeaderBytes + ((long)array.Length * (int)SizeOfMethod.MakeGenericMethod(element).Invoke(null, null)!);
                if (!element.IsPrimitive && !element.IsEnum)
                {
                    foreach (object? item in array)
                    {
                        VisitElement(item, element, seen, ref total);
                    }
                }

                break;
            default:
                VisitFields(value, seen, ref total);
                break;
        }
    }

    // A value read from a value-typed slot is a fresh box on every read, so only the references inside it count.
    private static void VisitElement(object? value, Type declared, HashSet<object> seen, ref long total)
    {
        if (!declared.IsValueType)
        {
            Visit(value, seen, ref total);
        }
        else if (value is not null)
        {
            VisitFields(value, seen, ref total);
        }
    }

    private static void VisitFields(object value, HashSet<object> seen, ref long total)
    {
        for (Type? t = value.GetType(); t is not null; t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && !field.FieldType.IsPointer)
                {
                    VisitElement(field.GetValue(value), field.FieldType, seen, ref total);
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
