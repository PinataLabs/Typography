// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

/// <summary>
/// The PingFang fonts of macOS keep their outlines in Apple's 'hvgl' table and have no 'glyf', 'CFF ' or 'CFF2' table.
/// The fonts here are copies of test fonts with the outline table renamed to 'hvgl'.
/// </summary>
public class Issues_136
{
    public static IEnumerable<Func<(string Font, string OutlineTable)>> Fonts()
    {
        yield return () => (TestFonts.SimpleFontFile, "glyf");
        yield return () => (TestFonts.NotoSansKRRegular, "CFF ");
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task Add_FontWithoutSupportedOutlines_Throws(string font, string outlineTable)
    {
        string path = WriteWithOutlinesRenamed(font, outlineTable);
        try
        {
            await Assert.That(() => new FontCollection().Add(path)).Throws<InvalidFontFileException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AddCollection_FontWithoutSupportedOutlines_Throws()
    {
        string path = WriteWithOutlinesRenamed(TestFonts.SimpleTrueTypeCollection, "glyf");
        try
        {
            await Assert.That(() => new FontCollection().AddCollection(path)).Throws<InvalidFontFileException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task SystemFonts_FontWithoutSupportedOutlines_IsAbsent()
    {
        string font = WriteWithOutlinesRenamed(TestFonts.SimpleFontFile, "glyf");
        string collection = WriteWithOutlinesRenamed(TestFonts.SimpleTrueTypeCollection, "glyf");
        try
        {
            FontCollection fonts = SystemFontCollection.CreateSystemFontCollection([TestFonts.OpenSansFile, font, collection], []);

            await Assert.That(fonts.Families.Select(x => x.Name)).IsEquivalentTo(["Open Sans"]);
            FontRectangle size = TextMeasurer.MeasureSize("Hello", new TextOptions(fonts.Get("Open Sans").CreateFont(12)));
            await Assert.That(size.Width).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(font);
            File.Delete(collection);
        }
    }

    // Renames the outline table of each face of a font or font collection to 'hvgl', in a new temporary file.
    private static string WriteWithOutlinesRenamed(string source, string outlineTable)
    {
        byte[] data = File.ReadAllBytes(source);
        int[] faces = BinaryPrimitives.ReadUInt32BigEndian(data) == 0x74746366
            ? Enumerable.Range(0, (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8)))
                .Select(i => (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + (i * 4))))
                .ToArray()
            : [0];

        byte[] tag = Encoding.ASCII.GetBytes(outlineTable);
        int renamed = 0;
        foreach (int face in faces)
        {
            int tableCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(face + 4));
            for (int i = 0; i < tableCount; i++)
            {
                Span<byte> record = data.AsSpan(face + 12 + (i * 16), 4);
                if (record.SequenceEqual(tag))
                {
                    Encoding.ASCII.GetBytes("hvgl").CopyTo(record);
                    renamed++;
                }
            }
        }

        if (renamed != faces.Length)
        {
            throw new InvalidOperationException($"{source} has {renamed} '{outlineTable}' tables for {faces.Length} faces.");
        }

        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + Path.GetExtension(source));
        File.WriteAllBytes(path, data);
        return path;
    }
}
