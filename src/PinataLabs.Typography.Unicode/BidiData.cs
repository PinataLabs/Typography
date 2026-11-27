// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Represents a unicode string and all associated attributes
/// for each character required for the Bidi algorithm
/// </summary>
internal class BidiData
{
    private ArrayBuilder<BidiCharacterType> types;
    private ArrayBuilder<BidiPairedBracketType> pairedBracketTypes;
    private ArrayBuilder<int> pairedBracketValues;
    private ArrayBuilder<sbyte> tempLevelBuffer;
    private readonly List<int> paragraphEnds = [];

    public sbyte ParagraphEmbeddingLevel { get; private set; }

    /// <summary>
    /// Gets the end of each paragraph of the text, in order: the index of the code point after its paragraph separator,
    /// or the length of the text for a last paragraph with no separator. Empty text is one empty paragraph.
    /// </summary>
    /// <remarks>
    /// Rule P1 of UAX #9 splits the text after each paragraph separator. A CR followed by an LF is one separator, as the
    /// newline guidelines of the Unicode Standard (section 5.8) have it and as rule LB5 of UAX #14 keeps them on one line.
    /// </remarks>
    public IReadOnlyList<int> ParagraphEnds => this.paragraphEnds;

    public bool HasBrackets { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the data holds a character that rule X9 removes: an embedding or
    /// override control, or a boundary neutral.
    /// </summary>
    public bool HasEmbeddings { get; private set; }

    public bool HasIsolates { get; private set; }

    /// <summary>
    /// Gets the length of the data held by the BidiData
    /// </summary>
    public int Length => this.types.Length;

    /// <summary>
    /// Gets the bidi character type of each code point
    /// </summary>
    public ArraySlice<BidiCharacterType> Types { get; private set; }

    /// <summary>
    /// Gets the paired bracket type for each code point
    /// </summary>
    public ArraySlice<BidiPairedBracketType> PairedBracketTypes { get; private set; }

    /// <summary>
    /// Gets the paired bracket value for code point
    /// </summary>
    /// <remarks>
    /// The paired bracket values are the code points
    /// of each character where the opening code point
    /// is replaced with the closing code point for easier
    /// matching.  Also, bracket code points are mapped
    /// to their canonical equivalents
    /// </remarks>
    public ArraySlice<int> PairedBracketValues { get; private set; }

    /// <summary>
    /// Initialize with a text value.
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <param name="paragraphEmbeddingLevel">The paragraph embedding level</param>
    public void Init(ReadOnlySpan<char> text, sbyte paragraphEmbeddingLevel)
    {
        // Set working buffer sizes
        // TODO: This allocates more than it should for some arrays.
        int length = CodePoint.GetCodePointCount(text);
        this.types.Length = length;
        this.pairedBracketTypes.Length = length;
        this.pairedBracketValues.Length = length;

        this.paragraphEnds.Clear();
        this.ParagraphEmbeddingLevel = paragraphEmbeddingLevel;

        // Resolve the BidiCharacterType, paired bracket type and paired
        // bracket values for all code points
        this.HasBrackets = false;
        this.HasEmbeddings = false;
        this.HasIsolates = false;

        int i = 0;
        bool afterCarriageReturn = false;
        var codePointEnumerator = new SpanCodePointEnumerator(text);
        while (codePointEnumerator.MoveNext())
        {
            CodePoint codePoint = codePointEnumerator.Current;
            BidiClass bidi = CodePoint.GetBidiClass(codePoint);

            // Look up BidiCharacterType
            BidiCharacterType dir = bidi.CharacterType;
            this.types[i] = dir;

            switch (dir)
            {
                case BidiCharacterType.ParagraphSeparator:
                    if (afterCarriageReturn && codePoint.Value == '\n')
                    {
                        // The LF ends the paragraph that the CR before it ended.
                        this.paragraphEnds[^1] = i + 1;
                    }
                    else
                    {
                        this.paragraphEnds.Add(i + 1);
                    }

                    break;

                case BidiCharacterType.LeftToRightEmbedding:
                case BidiCharacterType.LeftToRightOverride:
                case BidiCharacterType.RightToLeftEmbedding:
                case BidiCharacterType.RightToLeftOverride:
                case BidiCharacterType.PopDirectionalFormat:
                case BidiCharacterType.BoundaryNeutral:
                    this.HasEmbeddings = true;
                    break;

                case BidiCharacterType.LeftToRightIsolate:
                case BidiCharacterType.RightToLeftIsolate:
                case BidiCharacterType.FirstStrongIsolate:
                case BidiCharacterType.PopDirectionalIsolate:
                    this.HasIsolates = true;
                    break;
            }

            // Lookup paired bracket types
            BidiPairedBracketType pbt = bidi.PairedBracketType;
            this.pairedBracketTypes[i] = pbt;

            if (pbt != BidiPairedBracketType.None)
            {
                this.pairedBracketValues[i] = bidi.GetBracketPairKey(codePoint);
                this.HasBrackets = true;
            }

            afterCarriageReturn = codePoint.Value == '\r';
            i++;
        }

        // The text after the last separator, or empty text, is a paragraph too.
        if (this.paragraphEnds.Count == 0 || this.paragraphEnds[^1] != length)
        {
            this.paragraphEnds.Add(length);
        }

        // Create slices on work buffers
        this.Types = this.types.AsSlice();
        this.PairedBracketTypes = this.pairedBracketTypes.AsSlice();
        this.PairedBracketValues = this.pairedBracketValues.AsSlice();
    }

    /// <summary>
    /// Gets a temporary level buffer. Used by TextLayout when
    /// resolving style runs with different BidiCharacterType.
    /// </summary>
    /// <param name="length">Length of the required ExpandableBuffer</param>
    /// <returns>An uninitialized level ExpandableBuffer</returns>
    public ArraySlice<sbyte> GetTempLevelBuffer(int length)
    {
        this.tempLevelBuffer.Clear();
        return this.tempLevelBuffer.Add(length, false);
    }
}
