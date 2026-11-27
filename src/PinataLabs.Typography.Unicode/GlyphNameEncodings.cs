// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Maps the character codes of a PDF simple font to glyph names, through the encodings of ISO 32000-1 Annex D.
/// </summary>
/// <remarks>
/// <para>
/// Names the glyph that a character code selects. <see cref="AdobeGlyphList"/> turns that name into text.
/// </para>
/// <para>
/// The tables follow Annex D, with these additions. In <see cref="SimpleFontEncoding.WinAnsi"/> every unused code above
/// 0x20 is <c>bullet</c>, and 0xA0 and 0xAD are <c>space</c> and <c>hyphen</c>, as the notes of Annex D say.
/// <see cref="SimpleFontEncoding.MacRoman"/> has 0xCA as <c>space</c>, and also the 15 symbols of Mac OS Roman that
/// Annex D leaves out (such as <c>notequal</c> and <c>infinity</c>), because PDF readers resolve them.
/// <see cref="SimpleFontEncoding.ZapfDingbats"/> has the codes 0x80 to 0x8D of the font's built-in encoding.
/// </para>
/// </remarks>
public static class GlyphNameEncodings
{
    /// <summary>
    /// Gets the name of the glyph that a code selects in an encoding.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="code">The character code.</param>
    /// <returns>The glyph name, or <see langword="null"/> if the encoding gives the code no glyph.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="encoding"/> is not a defined value.</exception>
    public static string? GetGlyphName(SimpleFontEncoding encoding, byte code) => GetTable(encoding)[code];

    /// <summary>
    /// Gets the code that selects a glyph in an encoding.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="glyphName">The glyph name, compared as it is, with no suffix or <c>uniXXXX</c> form resolved.</param>
    /// <param name="code">When this method returns <see langword="true"/>, the code. When more than one code selects
    /// the glyph, as 0x20 and 0xA0 select <c>space</c> in <see cref="SimpleFontEncoding.WinAnsi"/>, the lowest.</param>
    /// <returns><see langword="true"/> if the encoding has the glyph name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="encoding"/> is not a defined value.</exception>
    public static bool TryGetCode(SimpleFontEncoding encoding, ReadOnlySpan<char> glyphName, out byte code)
    {
        string?[] table = GetTable(encoding);
        if (!glyphName.IsEmpty)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (glyphName.SequenceEqual(table[i].AsSpan()))
                {
                    code = (byte)i;
                    return true;
                }
            }
        }

        code = 0;
        return false;
    }

    internal static string?[] GetTable(SimpleFontEncoding encoding) => encoding switch
    {
        SimpleFontEncoding.Standard => PdfEncodingData.Standard,
        SimpleFontEncoding.WinAnsi => PdfEncodingData.WinAnsi,
        SimpleFontEncoding.MacRoman => PdfEncodingData.MacRoman,
        SimpleFontEncoding.MacExpert => PdfEncodingData.MacExpert,
        SimpleFontEncoding.Symbol => PdfEncodingData.Symbol,
        SimpleFontEncoding.ZapfDingbats => PdfEncodingData.ZapfDingbats,
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Not a SimpleFontEncoding."),
    };
}
