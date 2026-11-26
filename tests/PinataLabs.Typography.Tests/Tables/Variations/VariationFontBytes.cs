// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// A font file in memory, with a table that a test can change, for the tests of malformed variation tables.
/// </summary>
internal static class VariationFontBytes
{
    /// <summary>
    /// Reads a font file from the test fonts.
    /// </summary>
    /// <param name="relativePath">The path under the Fonts folder.</param>
    /// <returns>The bytes of the file, which the caller can change.</returns>
    public static byte[] Read(string relativePath) => File.ReadAllBytes(TestFonts.GetPath(relativePath));

    /// <summary>
    /// Gives the bytes of a table in a font file, to change them in place.
    /// </summary>
    /// <param name="font">The bytes of the font file.</param>
    /// <param name="table">The tag of the table.</param>
    /// <returns>A span over the table inside <paramref name="font"/>.</returns>
    public static Span<byte> Table(byte[] font, string table)
    {
        uint tag = BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(table.PadRight(4)));
        if (!SfntDirectory.Read(font).TryGetTable(tag, out ReadOnlyMemory<byte> memory)
            || !MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
        {
            throw new InvalidOperationException($"The font has no '{table}' table.");
        }

        return font.AsSpan(segment.Offset, segment.Count);
    }

    /// <summary>
    /// Loads a font that is in memory into a new collection.
    /// </summary>
    /// <param name="font">The bytes of the font file.</param>
    /// <returns>The family of the font.</returns>
    public static FontFamily Load(byte[] font) => new FontCollection().Add(new MemoryStream(font));
}
