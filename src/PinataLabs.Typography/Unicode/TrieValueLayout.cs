// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// The bits of the trie values that hold more than one property. The trie generator writes them and
/// UnicodeData reads them. A caller that needs both properties of a value reads the value once and takes them apart here.
/// </summary>
internal static class TrieValueLayout
{
    /// <summary>The bits of a grapheme trie value that hold the <see cref="GraphemeClusterClass"/>.</summary>
    public const uint GraphemeClusterClassMask = 0xF;

    /// <summary>The shift of the <see cref="IndicConjunctBreak"/> in a grapheme trie value.</summary>
    public const int IndicConjunctBreakShift = 4;

    /// <summary>The bits of a line break trie value that hold the <see cref="LineBreakClass"/>.</summary>
    public const uint LineBreakClassMask = 0x3F;

    /// <summary>The flag in a line break trie value for East_Asian_Width F, W or H.</summary>
    public const uint EastAsianWidthFlag = 0x40;

    public static GraphemeClusterClass GetGraphemeClusterClass(uint graphemeValue)
        => (GraphemeClusterClass)(graphemeValue & GraphemeClusterClassMask);

    public static IndicConjunctBreak GetIndicConjunctBreak(uint graphemeValue)
        => (IndicConjunctBreak)(graphemeValue >> IndicConjunctBreakShift);

    public static LineBreakClass GetLineBreakClass(uint lineBreakValue)
        => (LineBreakClass)(lineBreakValue & LineBreakClassMask);

    public static bool IsEastAsianWidth(uint lineBreakValue)
        => (lineBreakValue & EastAsianWidthFlag) != 0;
}
