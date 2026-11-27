// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// Builds a small bare CFF font that is CID-keyed, so a test can shape the parts that a subsetter lays out again: the Name INDEX,
/// the Font DICTs, and the Private DICTs with their local subroutines.
/// </summary>
public static class CffFontBuilder
{
    /// <summary>
    /// What the Font DICTs of a built font share.
    /// </summary>
    public enum Sharing
    {
        /// <summary>Each Font DICT has its own Private DICT and its own local Subrs INDEX.</summary>
        None,

        /// <summary>The Font DICTs share one Private DICT, and so its INDEX.</summary>
        PrivateDict,

        /// <summary>Each Font DICT has its own Private DICT, and the Private DICTs name one local Subrs INDEX.</summary>
        SubrsOnly,

        /// <summary>
        /// Font DICT <c>i</c> names a Private DICT window of <c>12 + (2 * i)</c> bytes at one offset, so the windows overlap. The
        /// bytes after the DICT are harmless operators, and one local Subrs INDEX follows them.
        /// </summary>
        OverlappingPrivateWindows,
    }

    // The bias of a font with fewer than 1,240 subroutines.
    private const int Bias = 107;

    /// <summary>
    /// Gets the charstring of a glyph that moves to a point and calls a local subroutine, which is
    /// <c>x y rmoveto</c> then <c>return</c> (see <see cref="Subr"/>), and ends.
    /// </summary>
    public static byte[] CallingGlyph(int subr)
    {
        // The operand is the index less the bias: a number of 1 byte from -107 to 107, else of 2 bytes up to 1,131.
        int operand = subr - Bias;
        return operand is >= -107 and <= 107 ? [(byte)(operand + 139), 10, 14] : [(byte)(247 + ((operand - 108) >> 8)), (byte)(operand - 108), 10, 14];
    }

    /// <summary>
    /// Gets a local subroutine that moves to a point that depends on its index.
    /// </summary>
    public static byte[] Subr(int index) => [(byte)(139 + (index % 100)), (byte)(139 + (index % 50)), 21, 11];

    /// <summary>
    /// Builds a CID-keyed bare CFF font.
    /// </summary>
    /// <param name="name">The font name.</param>
    /// <param name="charStrings">The charstring of each glyph.</param>
    /// <param name="fontDictCount">The number of Font DICTs. Glyph <c>i</c> uses Font DICT <c>i % fontDictCount</c>.</param>
    /// <param name="localSubrs">The local subroutines of each Private DICT.</param>
    /// <param name="sharing">What the Font DICTs share.</param>
    public static byte[] Build(byte[] name, byte[][] charStrings, int fontDictCount, byte[][] localSubrs, Sharing sharing)
    {
        byte[] header = [1, 0, 4, 4];
        byte[] nameIndex = Index([name]);
        byte[] strings = Index(["Adobe"u8.ToArray(), "Identity"u8.ToArray()]);
        byte[] globalSubrs = Index([]);

        byte[] charset = new byte[1 + (2 * (charStrings.Length - 1))];
        for (int i = 1; i < charStrings.Length; i++)
        {
            charset[(2 * i) - 1] = (byte)(i >> 8);
            charset[2 * i] = (byte)i;
        }

        byte[] fdSelect = [0, .. Enumerable.Range(0, charStrings.Length).Select(i => (byte)(i % fontDictCount))];
        byte[] charStringsIndex = Index(charStrings);

        // A Private DICT is a Subrs operator that names an INDEX, and a nominal width that tells the DICTs apart.
        // A DICT with its own INDEX is followed by it. DICTs that share an INDEX come first, and the INDEX follows them all.
        byte[] subrs = Index(localSubrs);
        int privateSize = 12;
        byte[] PrivateDict(int k, int subrsDistance) => [.. Number(k), 21, .. Number(subrsDistance), 19];

        int topIndexSize = Index([Top(0, 0, 0, 0)]).Length;
        int start = header.Length + nameIndex.Length + topIndexSize;
        int charsetOffset = start + strings.Length + globalSubrs.Length;
        int fdSelectOffset = charsetOffset + charset.Length;
        int charStringsOffset = fdSelectOffset + fdSelect.Length;
        int fdArrayOffset = charStringsOffset + charStringsIndex.Length;
        int fdArraySize = Index(Enumerable.Repeat(FontDict(privateSize, 0), fontDictCount).ToArray()).Length;
        int privatesOffset = fdArrayOffset + fdArraySize;

        int privateCount = sharing switch { Sharing.PrivateDict or Sharing.OverlappingPrivateWindows => 1, _ => fontDictCount };
        int privateStride = privateSize + (sharing == Sharing.None ? subrs.Length : 0);
        bool overlapping = sharing == Sharing.OverlappingPrivateWindows;
        byte[][] fontDicts = [.. Enumerable.Range(0, fontDictCount).Select(i => FontDict(overlapping ? privateSize + (2 * i) : privateSize, privatesOffset + (sharing is Sharing.PrivateDict or Sharing.OverlappingPrivateWindows ? 0 : i * privateStride)))];
        byte[] top = Top(charsetOffset, fdSelectOffset, charStringsOffset, fdArrayOffset);

        MemoryStream font = new();
        font.Write(header);
        font.Write(nameIndex);
        font.Write(Index([top]));
        font.Write(strings);
        font.Write(globalSubrs);
        font.Write(charset);
        font.Write(fdSelect);
        font.Write(charStringsIndex);
        font.Write(Index(fontDicts));
        int padding = overlapping ? 2 * (fontDictCount - 1) : 0;
        int subrsOffset = privatesOffset + (privateCount * privateStride) + padding;
        for (int i = 0; i < privateCount; i++)
        {
            int distance = sharing == Sharing.None ? privateSize : subrsOffset - (privatesOffset + (i * privateStride));
            font.Write(PrivateDict(i, distance));
            for (int k = 0; k < padding / 2; k++)
            {
                font.Write([139, 0]);
            }

            if (sharing == Sharing.None)
            {
                font.Write(subrs);
            }
        }

        if (sharing != Sharing.None)
        {
            font.Write(subrs);
        }

        return font.ToArray();
    }

    // An INDEX with 4-byte offsets, so its size does not depend on its data.
    private static byte[] Index(byte[][] objects)
    {
        if (objects.Length == 0)
        {
            return [0, 0];
        }

        List<byte> index = [(byte)(objects.Length >> 8), (byte)objects.Length, 4];
        int offset = 1;
        index.AddRange(Offset(offset));
        foreach (byte[] item in objects)
        {
            offset += item.Length;
            index.AddRange(Offset(offset));
        }

        foreach (byte[] item in objects)
        {
            index.AddRange(item);
        }

        return [.. index];
    }

    private static byte[] Offset(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    // The operand of 5 bytes, whatever the value.
    private static byte[] Number(int value) => [29, .. Offset(value)];

    private static byte[] FontDict(int privateSize, int privateOffset) => [.. Number(privateSize), .. Number(privateOffset), 18];

    private static byte[] Top(int charset, int fdSelect, int charStrings, int fdArray)
        => [
            .. Number(391), .. Number(392), .. Number(0), 12, 30,
            .. Number(charset), 15,
            .. Number(fdSelect), 12, 37,
            .. Number(charStrings), 17,
            .. Number(fdArray), 12, 36,
        ];
}
