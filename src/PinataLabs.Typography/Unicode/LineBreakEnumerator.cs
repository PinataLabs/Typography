// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Supports a simple iteration over a linebreak collection.
/// Implementation of the Unicode Line Break Algorithm. UAX:14
/// <see href="https://www.unicode.org/reports/tr14/"/>
/// Methods are pattern-matched by compiler to allow using foreach pattern.
/// </summary>
/// <remarks>
/// The rules are those of Unicode 18.0.0, in the form of LineBreakTest.html, which LineBreakTest.txt checks.
/// Rule LB9 joins a character and its combining marks into one unit, and the rules from LB11 on apply to units.
/// </remarks>
internal ref struct LineBreakEnumerator
{
    private const int FirstPictographBlock = 0x1F000;
    private const int LastPictographBlock = 0x1FFFF;

    private readonly ReadOnlySpan<char> source;
    private readonly int pointsLength;
    private int charPosition;
    private int position;
    private bool done;

    // The class, after LB1, of the character before the position.
    private LineBreakClass previousCharacter;

    // The unit before the position, and the last unit before it that is not a space.
    private Unit previous;
    private Unit beforeSpaces;

    public LineBreakEnumerator(ReadOnlySpan<char> source)
        : this()
    {
        this.source = source;
        this.pointsLength = CodePoint.GetCodePointCount(source);
        this.previous = Unit.Sot;
        this.beforeSpaces = Unit.Sot;
    }

    [Flags]
    private enum Flags
    {
        None = 0,

        // Properties of the character that starts a unit.
        EastAsian = 1 << 0,
        InitialQuote = 1 << 1,
        FinalQuote = 1 << 2,
        DottedCircle = 1 << 3,
        ExtendedPictographicUnassigned = 1 << 4,

        // Context of a unit, from the units before it.
        OpeningQuote = 1 << 5,
        HyphenAfterBreak = 1 << 6,
        HyphenAfterHebrew = 1 << 7,
        QuoteAfterNonEastAsian = 1 << 8,
        ViramaAfterAksara = 1 << 9,
        OddRegionalIndicator = 1 << 10,
        Number = 1 << 11,
        ClosedNumber = 1 << 12,
    }

    private enum Decision
    {
        NoBreak,
        Break,
        Mandatory,
    }

    public LineBreak Current { get; private set; }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that iterates through the collection.</returns>
    public readonly LineBreakEnumerator GetEnumerator() => this;

    /// <summary>
    /// Advances the enumerator to the next element of the collection.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the enumerator was successfully advanced to the next element;
    /// <see langword="false"/> if the enumerator has passed the end of the collection.
    /// </returns>
    public bool MoveNext()
    {
        while (this.position < this.pointsLength)
        {
            int boundary = this.position;
            Character next = this.Read(this.charPosition, out int count);
            this.charPosition += count;
            this.position++;

            // LB2: never break at the start of text.
            bool joins = false;
            Decision decision = Decision.NoBreak;
            if (boundary > 0)
            {
                decision = this.Decide(next, out joins);
            }

            if (!joins)
            {
                this.previous = Unit.Create(next, this.previous);
                if (this.previous.Class != LineBreakClass.SP)
                {
                    this.beforeSpaces = this.previous;
                }
            }

            this.previousCharacter = next.Class;

            if (decision != Decision.NoBreak)
            {
                this.Current = new LineBreak(this.FindPriorNonWhitespace(boundary), boundary, decision == Decision.Mandatory);
                return true;
            }
        }

        // LB3: always break at the end of text.
        if (!this.done && this.pointsLength > 0)
        {
            this.done = true;
            bool required = this.previousCharacter is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL;
            this.Current = new LineBreak(this.FindPriorNonWhitespace(this.pointsLength), this.pointsLength, required);
            return true;
        }

        this.Current = default;
        return false;
    }

    private static bool IsAksara(Unit unit)
        => unit.Class is LineBreakClass.AK or LineBreakClass.AS || unit.Has(Flags.DottedCircle);

    private static bool IsAksaraOrDottedCircle(Unit unit)
        => unit.Class == LineBreakClass.AK || unit.Has(Flags.DottedCircle);

    private readonly Decision Decide(Character next, out bool joins)
    {
        joins = false;
        LineBreakClass c = next.Class;

        // LB4, LB5
        switch (this.previousCharacter)
        {
            case LineBreakClass.BK:
            case LineBreakClass.LF:
            case LineBreakClass.NL:
                return Decision.Mandatory;
            case LineBreakClass.CR:
                return c == LineBreakClass.LF ? Decision.NoBreak : Decision.Mandatory;
        }

        // LB6, LB7
        if (c is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
            or LineBreakClass.SP or LineBreakClass.ZW)
        {
            return Decision.NoBreak;
        }

        // LB8
        if (this.beforeSpaces.Class == LineBreakClass.ZW)
        {
            return Decision.Break;
        }

        // LB8a
        if (this.previousCharacter == LineBreakClass.ZWJ)
        {
            return Decision.NoBreak;
        }

        // LB9. A mark after a space or ZW is a unit of its own, of class AL (LB10).
        if (c is LineBreakClass.CM or LineBreakClass.ZWJ
            && this.previousCharacter is not (LineBreakClass.SP or LineBreakClass.ZW))
        {
            joins = true;
            return Decision.NoBreak;
        }

        Unit b = this.previous;
        Unit a = Unit.Create(next, b);
        LineBreakClass bc = b.Class;
        LineBreakClass ac = a.Class;

        // LB11, LB12, LB12a
        if (ac == LineBreakClass.WJ || bc is LineBreakClass.WJ or LineBreakClass.GL)
        {
            return Decision.NoBreak;
        }

        if (ac == LineBreakClass.GL && bc is not (LineBreakClass.SP or LineBreakClass.HY or LineBreakClass.HH))
        {
            return Decision.NoBreak;
        }

        // LB13
        if (ac is LineBreakClass.EX or LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.SY)
        {
            return Decision.NoBreak;
        }

        // LB14, LB15a
        if (this.beforeSpaces.Class == LineBreakClass.OP || this.beforeSpaces.Has(Flags.OpeningQuote))
        {
            return Decision.NoBreak;
        }

        // LB15b
        if (a.Has(Flags.FinalQuote))
        {
            Character after = this.PeekUnit(this.charPosition, c, out _);
            if (after.Class is Character.Eot or LineBreakClass.SP or LineBreakClass.GL or LineBreakClass.WJ
                or LineBreakClass.CL or LineBreakClass.QU or LineBreakClass.CP or LineBreakClass.EX
                or LineBreakClass.IS or LineBreakClass.SY or LineBreakClass.BK or LineBreakClass.CR
                or LineBreakClass.LF or LineBreakClass.NL or LineBreakClass.ZW)
            {
                return Decision.NoBreak;
            }
        }

        // LB15c, LB15d
        if (ac == LineBreakClass.IS)
        {
            if (bc == LineBreakClass.SP && this.PeekUnit(this.charPosition, c, out _).Class == LineBreakClass.NU)
            {
                return Decision.Break;
            }

            return Decision.NoBreak;
        }

        // LB16, LB17
        if ((ac == LineBreakClass.NS && this.beforeSpaces.Class is LineBreakClass.CL or LineBreakClass.CP)
            || (ac == LineBreakClass.B2 && this.beforeSpaces.Class == LineBreakClass.B2))
        {
            return Decision.NoBreak;
        }

        // LB18
        if (bc == LineBreakClass.SP)
        {
            return Decision.Break;
        }

        // LB19, LB19a
        if (ac == LineBreakClass.QU)
        {
            if (!a.Has(Flags.InitialQuote) || !b.Has(Flags.EastAsian))
            {
                return Decision.NoBreak;
            }

            Character after = this.PeekUnit(this.charPosition, c, out _);
            if (after.Class == Character.Eot || (after.Flags & Flags.EastAsian) == 0)
            {
                return Decision.NoBreak;
            }
        }

        if (bc == LineBreakClass.QU
            && (!b.Has(Flags.FinalQuote) || !a.Has(Flags.EastAsian) || b.Has(Flags.QuoteAfterNonEastAsian)))
        {
            return Decision.NoBreak;
        }

        // LB20
        if (ac == LineBreakClass.CB || bc == LineBreakClass.CB)
        {
            return Decision.Break;
        }

        // LB20a
        if (b.Has(Flags.HyphenAfterBreak) && ac is LineBreakClass.AL or LineBreakClass.HL)
        {
            return Decision.NoBreak;
        }

        // LB21, LB21a, LB21b
        if (ac is LineBreakClass.BA or LineBreakClass.HH or LineBreakClass.HY or LineBreakClass.NS
            || bc == LineBreakClass.BB
            || (b.Has(Flags.HyphenAfterHebrew) && ac != LineBreakClass.HL)
            || (bc == LineBreakClass.SY && ac == LineBreakClass.HL))
        {
            return Decision.NoBreak;
        }

        // LB22
        if (ac == LineBreakClass.IN)
        {
            return Decision.NoBreak;
        }

        // LB23, LB23a, LB24
        bool letterBefore = bc is LineBreakClass.AL or LineBreakClass.HL;
        bool letterAfter = ac is LineBreakClass.AL or LineBreakClass.HL;
        if ((letterBefore && ac is LineBreakClass.NU or LineBreakClass.PR or LineBreakClass.PO)
            || (letterAfter && bc is LineBreakClass.NU or LineBreakClass.PR or LineBreakClass.PO)
            || (bc == LineBreakClass.PR && ac is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM)
            || (ac == LineBreakClass.PO && bc is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM))
        {
            return Decision.NoBreak;
        }

        // LB25
        if (((b.Has(Flags.Number) || b.Has(Flags.ClosedNumber)) && ac is LineBreakClass.PO or LineBreakClass.PR)
            || (b.Has(Flags.Number) && ac == LineBreakClass.NU)
            || (ac == LineBreakClass.NU && bc is LineBreakClass.PO or LineBreakClass.PR or LineBreakClass.HY or LineBreakClass.IS))
        {
            return Decision.NoBreak;
        }

        if (ac == LineBreakClass.OP && bc is LineBreakClass.PO or LineBreakClass.PR)
        {
            Character after = this.PeekUnit(this.charPosition, c, out int index);
            if (after.Class == LineBreakClass.IS)
            {
                after = this.PeekUnit(index, after.Class, out _);
            }

            if (after.Class == LineBreakClass.NU)
            {
                return Decision.NoBreak;
            }
        }

        // LB26, LB27
        switch (bc)
        {
            case LineBreakClass.JL when ac is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3:
            case LineBreakClass.JV or LineBreakClass.H2 when ac is LineBreakClass.JV or LineBreakClass.JT:
            case LineBreakClass.JT or LineBreakClass.H3 when ac == LineBreakClass.JT:
            case LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3 when ac == LineBreakClass.PO:
            case LineBreakClass.PR when ac is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3:
                return Decision.NoBreak;
        }

        // LB28
        if (letterBefore && letterAfter)
        {
            return Decision.NoBreak;
        }

        // LB28a
        if ((bc == LineBreakClass.AP && IsAksara(a))
            || (IsAksara(b) && ac is LineBreakClass.VF or LineBreakClass.VI)
            || (b.Has(Flags.ViramaAfterAksara) && IsAksaraOrDottedCircle(a)))
        {
            return Decision.NoBreak;
        }

        if (IsAksara(b) && IsAksara(a) && this.PeekUnit(this.charPosition, c, out _).Class == LineBreakClass.VF)
        {
            return Decision.NoBreak;
        }

        // LB29
        if (bc == LineBreakClass.IS && letterAfter)
        {
            return Decision.NoBreak;
        }

        // LB30
        if ((ac == LineBreakClass.OP && !a.Has(Flags.EastAsian) && bc is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU)
            || (bc == LineBreakClass.CP && !b.Has(Flags.EastAsian) && ac is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU))
        {
            return Decision.NoBreak;
        }

        // LB30a
        if (ac == LineBreakClass.RI && b.Has(Flags.OddRegionalIndicator))
        {
            return Decision.NoBreak;
        }

        // LB30b
        if (ac == LineBreakClass.EM && (bc == LineBreakClass.EB || b.Has(Flags.ExtendedPictographicUnassigned)))
        {
            return Decision.NoBreak;
        }

        // LB31
        return Decision.Break;
    }

    /// <summary>
    /// Reads the character at a UTF-16 index and resolves its class by LB1.
    /// </summary>
    private readonly Character Read(int index, out int count)
    {
        var cp = CodePoint.DecodeFromUtf16At(this.source, index, out count);
        uint value = UnicodeData.GetLineBreakValue((uint)cp.Value);
        LineBreakClass cls = TrieValueLayout.GetLineBreakClass(value);
        Flags flags = TrieValueLayout.IsEastAsianWidth(value) ? Flags.EastAsian : Flags.None;

        // An unassigned code point is XX or ID. The unassigned pictographs are all in the blocks from U+1F000 to
        // U+1FFFF, so the ideographs outside them, which are most of ID, need no further lookup.
        if (cls is LineBreakClass.XX or LineBreakClass.ID
            && cp.Value is >= FirstPictographBlock and <= LastPictographBlock
            && CodePoint.GetGeneralCategory(cp) == UnicodeCategory.OtherNotAssigned
            && CodePoint.GetGraphemeClusterClass(cp) == GraphemeClusterClass.ExtendedPictographic)
        {
            flags |= Flags.ExtendedPictographicUnassigned;
        }

        switch (cls)
        {
            case LineBreakClass.AI:
            case LineBreakClass.SG:
            case LineBreakClass.XX:
                cls = LineBreakClass.AL;
                break;

            case LineBreakClass.SA:
                cls = CodePoint.GetGeneralCategory(cp) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                    ? LineBreakClass.CM
                    : LineBreakClass.AL;
                break;

            case LineBreakClass.CJ:
                cls = LineBreakClass.NS;
                break;

            case LineBreakClass.QU:
                UnicodeCategory category = CodePoint.GetGeneralCategory(cp);
                if (category == UnicodeCategory.InitialQuotePunctuation)
                {
                    flags |= Flags.InitialQuote;
                }
                else if (category == UnicodeCategory.FinalQuotePunctuation)
                {
                    flags |= Flags.FinalQuote;
                }

                break;

            case LineBreakClass.AL when cp.Value == 0x25CC:
                flags |= Flags.DottedCircle;
                break;
        }

        return new Character(cls, flags);
    }

    /// <summary>
    /// Reads the unit that starts after a character. The combining marks that LB9 joins to the character are skipped.
    /// </summary>
    /// <param name="index">The UTF-16 index after the character.</param>
    /// <param name="cls">The class of the character.</param>
    /// <param name="end">The UTF-16 index after the first character of the unit.</param>
    private readonly Character PeekUnit(int index, LineBreakClass cls, out int end)
    {
        bool joinsMarks = cls is not (LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
            or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW);

        while (index < this.source.Length)
        {
            Character character = this.Read(index, out int count);
            index += count;
            if (joinsMarks && character.Class is LineBreakClass.CM or LineBreakClass.ZWJ)
            {
                continue;
            }

            end = index;
            return character.Class is LineBreakClass.CM or LineBreakClass.ZWJ
                ? new Character(LineBreakClass.AL, character.Flags)
                : character;
        }

        end = index;
        return new Character(Character.Eot, Flags.None);
    }

    private readonly int FindPriorNonWhitespace(int from)
    {
        if (from > 0)
        {
            var cp = CodePoint.DecodeFromUtf16At(this.source, from - 1, out int count);
            LineBreakClass cls = CodePoint.GetLineBreakClass(cp);

            if (cls is LineBreakClass.BK or LineBreakClass.LF or LineBreakClass.CR)
            {
                from -= count;
            }
        }

        while (from > 0)
        {
            var cp = CodePoint.DecodeFromUtf16At(this.source, from - 1, out int count);
            LineBreakClass cls = CodePoint.GetLineBreakClass(cp);

            if (cls == LineBreakClass.SP)
            {
                from -= count;
            }
            else
            {
                break;
            }
        }

        return from;
    }

    /// <summary>
    /// A character with its class after LB1.
    /// </summary>
    private readonly struct Character
    {
        // The end of the text. LB1 resolves XX to AL, so no character has this class.
        public const LineBreakClass Eot = LineBreakClass.XX;

        public Character(LineBreakClass cls, Flags flags)
        {
            this.Class = cls;
            this.Flags = flags;
        }

        public LineBreakClass Class { get; }

        public Flags Flags { get; }
    }

    /// <summary>
    /// A character and the combining marks that LB9 joins to it, with its class after LB10.
    /// </summary>
    private readonly struct Unit
    {
        // The start of the text. LB1 resolves XX to AL, so no unit has this class.
        public static readonly Unit Sot = new(LineBreakClass.XX, Flags.None);

        private readonly Flags flags;

        private Unit(LineBreakClass cls, Flags flags)
        {
            this.Class = cls;
            this.flags = flags;
        }

        public LineBreakClass Class { get; }

        public static Unit Create(Character character, Unit previous)
        {
            LineBreakClass cls = character.Class is LineBreakClass.CM or LineBreakClass.ZWJ ? LineBreakClass.AL : character.Class;
            Flags flags = character.Flags;
            LineBreakClass before = previous.Class;
            bool sot = before == LineBreakClass.XX;

            switch (cls)
            {
                case LineBreakClass.QU:
                    if ((flags & Flags.InitialQuote) != 0
                        && (sot || before is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
                            or LineBreakClass.OP or LineBreakClass.QU or LineBreakClass.GL or LineBreakClass.SP or LineBreakClass.ZW))
                    {
                        flags |= Flags.OpeningQuote;
                    }

                    if (sot || !previous.Has(Flags.EastAsian))
                    {
                        flags |= Flags.QuoteAfterNonEastAsian;
                    }

                    break;

                case LineBreakClass.HY:
                case LineBreakClass.HH:
                    if (sot || before is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
                        or LineBreakClass.SP or LineBreakClass.ZW or LineBreakClass.CB or LineBreakClass.GL)
                    {
                        flags |= Flags.HyphenAfterBreak;
                    }
                    else if (before == LineBreakClass.HL)
                    {
                        flags |= Flags.HyphenAfterHebrew;
                    }

                    break;

                case LineBreakClass.VI when IsAksara(previous):
                    flags |= Flags.ViramaAfterAksara;
                    break;

                case LineBreakClass.RI when !previous.Has(Flags.OddRegionalIndicator):
                    flags |= Flags.OddRegionalIndicator;
                    break;

                case LineBreakClass.NU:
                    flags |= Flags.Number;
                    break;

                case LineBreakClass.SY or LineBreakClass.IS when previous.Has(Flags.Number):
                    flags |= Flags.Number;
                    break;

                case LineBreakClass.CL or LineBreakClass.CP when previous.Has(Flags.Number):
                    flags |= Flags.ClosedNumber;
                    break;
            }

            return new Unit(cls, flags);
        }

        public bool Has(Flags flag) => (this.flags & flag) != 0;
    }
}
