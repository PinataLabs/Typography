// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Text;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Maps glyph names to text and code points to glyph names, by the Adobe Glyph List and the Adobe Glyph List
/// Specification (AGL).
/// </summary>
/// <remarks>
/// <para>
/// The Adobe Glyph List gives several names to some code points, such as <c>space</c>, <c>spacehackarabic</c> and
/// <c>nbspace</c>. <see cref="GetGlyphName"/> returns one preferred name for a code point: the name that aglfn.txt, the
/// Adobe Glyph List for New Fonts, gives it, or a <c>uniXXXX</c> or <c>uXXXXX</c> name when aglfn.txt has none.
/// </para>
/// <para>
/// A few names map to a compatibility character, not to the letter that a reader expects, and aglfn.txt agrees:
/// <c>Omega</c> is U+2126 (OHM SIGN), <c>Delta</c> is U+2206 (INCREMENT) and <c>mu</c> is U+00B5 (MICRO SIGN).
/// <c>Omegagreek</c>, <c>Deltagreek</c> and <c>mugreek</c> are the Greek letters U+03A9, U+0394 and U+03BC. The result
/// is the listed character so that <see cref="GetGlyphName"/> reads it back. Text extraction that wants the letters
/// normalizes the text: U+2126 becomes U+03A9 under NFC, and U+00B5 becomes U+03BC under NFKC. U+2206 has no
/// normalization to U+0394.
/// </para>
/// </remarks>
public static class AdobeGlyphList
{
    private const int MaxCodePoint = 0x10FFFF;

    /// <summary>
    /// Gets the text of a glyph name, as the Adobe Glyph List Specification maps it.
    /// </summary>
    /// <param name="glyphName">The glyph name.</param>
    /// <param name="text">When this method returns <see langword="true"/>, the text.</param>
    /// <returns>
    /// <see langword="true"/> if every component of the name has text. The specification maps a component without text
    /// to the empty string, so <c>f_oops</c> has the text <c>f</c>. This method returns <see langword="false"/> for it, so
    /// that a caller knows the name is not understood and can use another source, such as the font's <c>cmap</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The name is cut at the first period, so <c>a.alt</c> maps as <c>a</c>. The rest is split at each underscore
    /// into components, which map to text that is joined, so <c>f_f_i</c> is <c>ffi</c>. Each component maps through the
    /// first of these that applies. It is a name in the Adobe Glyph List. It is <c>uni</c> and then one or more groups of
    /// four uppercase hexadecimal digits, each a code point of the Basic Multilingual Plane. It is <c>u</c> and then
    /// four to six uppercase hexadecimal digits that are a code point. Surrogate code points and lowercase digits
    /// do not map.
    /// </para>
    /// </remarks>
    public static bool TryGetUnicode(ReadOnlySpan<char> glyphName, out string text)
        => TryGetUnicode(glyphName, zapfDingbats: false, out text);

    /// <summary>
    /// Gets the text of a glyph name in a ZapfDingbats font, such as <c>a1</c>.
    /// </summary>
    /// <param name="glyphName">The glyph name.</param>
    /// <param name="text">When this method returns <see langword="true"/>, the text.</param>
    /// <returns>
    /// <see langword="true"/> if every component has text. This method maps as <see cref="TryGetUnicode(ReadOnlySpan{char}, out string)"/> does,
    /// except that the names of the ITC Zapf Dingbats glyph list map first.
    /// </returns>
    public static bool TryGetZapfDingbatsUnicode(ReadOnlySpan<char> glyphName, out string text)
        => TryGetUnicode(glyphName, zapfDingbats: true, out text);

    /// <summary>
    /// Gets the preferred glyph name of a code point.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>
    /// The name that aglfn.txt gives the code point, such as <c>Euro</c> for U+20AC, or else <c>uniXXXX</c> for a
    /// code point of the Basic Multilingual Plane and <c>uXXXXX</c> or <c>uXXXXXX</c> for the others, with uppercase digits.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="codePoint"/> is a surrogate or is not in the range 0 to 0x10FFFF.
    /// </exception>
    public static string GetGlyphName(int codePoint)
    {
        if (!IsScalarValue(codePoint))
        {
            throw new ArgumentOutOfRangeException(nameof(codePoint), codePoint, "Not a Unicode scalar value.");
        }

        int index = Array.BinarySearch(AdobeGlyphListData.PreferredCodePoints, codePoint);
        if (index >= 0)
        {
            return AdobeGlyphListData.PreferredNames[index];
        }

        string digits = codePoint.ToString("X4", CultureInfo.InvariantCulture);
        return codePoint <= 0xFFFF ? "uni" + digits : "u" + digits;
    }

    private static bool TryGetUnicode(ReadOnlySpan<char> glyphName, bool zapfDingbats, out string text)
    {
        int period = glyphName.IndexOf('.');
        if (period >= 0)
        {
            glyphName = glyphName.Slice(0, period);
        }

        int underscore = glyphName.IndexOf('_');
        if (underscore < 0)
        {
            return TryGetComponent(glyphName, zapfDingbats, out text);
        }

        var builder = new StringBuilder();
        while (true)
        {
            if (!TryGetComponent(glyphName.Slice(0, underscore), zapfDingbats, out string component))
            {
                text = string.Empty;
                return false;
            }

            builder.Append(component);
            glyphName = glyphName.Slice(underscore + 1);
            underscore = glyphName.IndexOf('_');
            if (underscore < 0)
            {
                break;
            }
        }

        if (!TryGetComponent(glyphName, zapfDingbats, out string last))
        {
            text = string.Empty;
            return false;
        }

        text = builder.Append(last).ToString();
        return true;
    }

    private static bool TryGetComponent(ReadOnlySpan<char> component, bool zapfDingbats, out string text)
    {
        text = string.Empty;
        if (component.IsEmpty)
        {
            return false;
        }

        if (zapfDingbats && AdobeGlyphListData.ZapfDingbatsList.TryGet(component, out text))
        {
            return true;
        }

        if (AdobeGlyphListData.GlyphList.TryGet(component, out text))
        {
            return true;
        }

        if (component.StartsWith("uni".AsSpan(), StringComparison.Ordinal))
        {
            ReadOnlySpan<char> digits = component.Slice(3);
            if (digits.IsEmpty || (digits.Length & 3) != 0)
            {
                return false;
            }

            var builder = new StringBuilder(digits.Length / 4);
            for (int i = 0; i < digits.Length; i += 4)
            {
                if (!TryParseUpperHex(digits.Slice(i, 4), out int value) || !IsScalarValue(value))
                {
                    return false;
                }

                builder.Append((char)value);
            }

            text = builder.ToString();
            return true;
        }

        if (component[0] == 'u'
            && component.Length is >= 5 and <= 7
            && TryParseUpperHex(component.Slice(1), out int codePoint)
            && IsScalarValue(codePoint))
        {
            text = char.ConvertFromUtf32(codePoint);
            return true;
        }

        return false;
    }

    private static bool TryParseUpperHex(ReadOnlySpan<char> digits, out int value)
    {
        value = 0;
        foreach (char c in digits)
        {
            int digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
            if (digit < 0)
            {
                return false;
            }

            value = (value << 4) | digit;
        }

        return true;
    }

    private static bool IsScalarValue(int value) => (uint)value <= MaxCodePoint && (value < 0xD800 || value > 0xDFFF);
}
