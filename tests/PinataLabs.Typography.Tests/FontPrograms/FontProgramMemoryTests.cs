// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tests.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms;

public class FontProgramMemoryTests
{
    private const int ArrayHeaderBytes = 24;

    private static readonly MethodInfo SizeOfMethod = typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!;

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    [Arguments("Type1/bchr8a.pfb")]
    [Arguments("Type1/bchr8a.pfa")]
    [Arguments("Type1/bchr8a.fontfile")]
    [Arguments("Type1/cmr10.pfb")]
    [Arguments("Type1/cmmi10.pfb")]
    [Arguments("Type1/qtmr.pfb")]
    public async Task RetainedByteCountEqualsTheBytesReachableFromTheProgram(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));

        FontProgram font = FontProgram.Load(data);

        await Assert.That(font.RetainedByteCount).IsEqualTo(Walk(font, data, out _));
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
    public async Task AType1ProgramRetainsItsDecryptedPartAndItsArraysButNotItsData()
    {
        Type1FontBuilder builder = Type1Font();
        const int glyphs = 2;
        const int subrs = 3;

        // Nine arrays: the decrypted private part, two for each of the two charstring tables,
        // the name offsets, lengths and sorted order, and the encoding.
        long expected = (9 * ArrayHeaderBytes)
            + builder.BuildPrivate().Length
            + (glyphs * ((2 * sizeof(int)) + sizeof(int) + sizeof(byte) + sizeof(ushort)))
            + (subrs * 2 * sizeof(int))
            + (256 * sizeof(ushort));

        foreach (byte[] data in (byte[][])[builder.BuildPfa(hex: true), builder.BuildPfa(hex: false), builder.BuildPfb()])
        {
            await Assert.That(FontProgram.Load(data).RetainedByteCount).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ALimitBelowTheDecryptedPartRefusesAType1FontBeforeItDecrypts()
    {
        Type1FontBuilder builder = Type1Font();
        long decrypted = ArrayHeaderBytes + builder.BuildPrivate().Length;

        FontProgramTooLargeException exception = (await Assert.That(
            () => FontProgram.Load(builder.BuildPfb(), new FontProgramOptions { MaxRetainedBytes = decrypted - 1 }))
            .Throws<FontProgramTooLargeException>())!;

        await Assert.That(exception.RetainedByteCount).IsEqualTo(decrypted);
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
    [Arguments("Type1/bchr8a.pfb")]
    [Arguments("Type1/bchr8a.pfa")]
    [Arguments("Type1/bchr8a.fontfile")]
    [Arguments("Type1/cmr10.pfb")]
    [Arguments("Type1/cmmi10.pfb")]
    [Arguments("Type1/qtmr.pfb")]
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
    [Arguments("Type1/bchr8a.pfb")]
    [Arguments("Type1/bchr8a.pfa")]
    [Arguments("Type1/bchr8a.fontfile")]
    [Arguments("Type1/cmr10.pfb")]
    [Arguments("Type1/cmmi10.pfb")]
    [Arguments("Type1/qtmr.pfb")]
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
    [Arguments("Type1/bchr8a.pfb")]
    [Arguments("Type1/bchr8a.pfa")]
    [Arguments("Type1/bchr8a.fontfile")]
    [Arguments("Type1/cmr10.pfb")]
    [Arguments("Type1/cmmi10.pfb")]
    [Arguments("Type1/qtmr.pfb")]
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
    [Arguments("Type1/bchr8a.pfb")]
    [Arguments("Type1/bchr8a.pfa")]
    [Arguments("Type1/bchr8a.fontfile")]
    [Arguments("Type1/cmr10.pfb")]
    [Arguments("Type1/cmmi10.pfb")]
    [Arguments("Type1/qtmr.pfb")]
    public async Task ARefusalCarriesTheSizeThatLoads(string fontFile)
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        long size = FontProgram.Load(data).RetainedByteCount;

        // The first charge is the data of a CFF font and the decrypted private part of a Type 1 font.
        // A refusal there carries only that, because the counts of the arrays are read after it.
        long first = (await Assert.That(() => FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = 0 }))
            .Throws<FontProgramTooLargeException>())!.RetainedByteCount;
        long[] limits = [first, first + 1, first + ((size - first) / 2), size - ArrayHeaderBytes - 1, size - 1];

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
    [Arguments("Type1/bchr8a.pfb")]
    [Arguments("Type1/bchr8a.pfa")]
    [Arguments("Type1/bchr8a.fontfile")]
    [Arguments("Type1/cmr10.pfb")]
    [Arguments("Type1/cmmi10.pfb")]
    [Arguments("Type1/qtmr.pfb")]
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

        long owned = font.RetainedByteCount - (font.Format == FontProgramFormat.Type1 ? 0 : data.Length);
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
            t.Namespace is "PinataLabs.Typography.FontPrograms" or "PinataLabs.Typography.FontPrograms.Type1" or "PinataLabs.Typography.Tables.Cff" || t == typeof(FontProgram)));
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

    private static Type1FontBuilder Type1Font()
    {
        Type1FontBuilder builder = new Type1FontBuilder()
            .AddGlyph(".notdef", "0 500 hsbw endchar")
            .AddGlyph("A", "10 500 hsbw 0 callsubr 100 hlineto closepath endchar");
        builder.AddSubr("100 vlineto return");
        builder.AddSubr("return");
        builder.AddSubr("return");
        return builder;
    }

    private static long FontDictRecordSize()
    {
        Type record = typeof(FontProgram).Assembly.GetType("PinataLabs.Typography.Tables.Cff.CffFontDict")!;
        return (int)SizeOfMethod.MakeGenericMethod(record).Invoke(null, null)!;
    }

    private static void Use(FontProgram font)
    {
        NullGlyphOutlineSink sink = default;
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

    // Sums the array headers and elements reachable from the program, plus the length of the font data when the
    // program reaches it, and records each object it reaches by reference. It reads the fields, so a field that a
    // later change adds is counted here and the test fails if the property misses it. It sees a reference that a
    // field holds, so a cache in an instance field fails the test when use changes the objects reachable.
    // It cannot see a static field, so StaticFieldsHoldOnlyReadOnlyTables covers those.
    private static long Walk(FontProgram font, byte[] data, out HashSet<object> reachable)
    {
        reachable = new(ReferenceEqualityComparer.Instance);
        long total = 0;
        Visit(font, data, reachable, ref total);
        return total;
    }

    private static void Visit(object? value, byte[] data, HashSet<object> seen, ref long total)
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
                Visit(action.Target, data, seen, ref total);
                break;
            case byte[] bytes when ReferenceEquals(bytes, data):
                total += data.Length;
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
                        VisitElement(item, element, data, seen, ref total);
                    }
                }

                break;
            default:
                VisitFields(value, data, seen, ref total);
                break;
        }
    }

    // A value read from a value-typed slot is a fresh box on every read, so only the references inside it count.
    private static void VisitElement(object? value, Type declared, byte[] data, HashSet<object> seen, ref long total)
    {
        if (!declared.IsValueType)
        {
            Visit(value, data, seen, ref total);
        }
        else if (value is not null)
        {
            VisitFields(value, data, seen, ref total);
        }
    }

    private static void VisitFields(object value, byte[] data, HashSet<object> seen, ref long total)
    {
        for (Type? t = value.GetType(); t is not null; t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && !field.FieldType.IsPointer)
                {
                    VisitElement(field.GetValue(value), field.FieldType, data, seen, ref total);
                }
            }
        }
    }
}
