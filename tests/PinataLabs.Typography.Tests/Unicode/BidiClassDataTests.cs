// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using UnicodeTrieGenerator;

namespace PinataLabs.Typography.Tests.Unicode;

public class BidiClassDataTests
{
    private const int CodePointCount = 0x110000;

    // Unassigned code points take the default class of their range (UAX #9, UAX #44).
    [Test]
    [Arguments(0x05F5, BidiCharacterType.RightToLeft)] // Hebrew
    [Arguments(0x07BF, BidiCharacterType.ArabicLetter)] // Thaana
    [Arguments(0x10D3F, BidiCharacterType.ArabicLetter)] // Hanifi Rohingya
    [Arguments(0x1EF00, BidiCharacterType.RightToLeft)]
    [Arguments(0xFE75, BidiCharacterType.ArabicLetter)] // Arabic Presentation Forms-B
    [Arguments(0x20CF, BidiCharacterType.EuropeanTerminator)] // Currency Symbols
    [Arguments(0xFDD0, BidiCharacterType.BoundaryNeutral)] // Noncharacter
    [Arguments(0x0378, BidiCharacterType.LeftToRight)] // Greek
    public async Task UnassignedCodePointHasTheDefaultClassOfItsRange(int codePoint, BidiCharacterType expected)
        => await Assert.That(CodePoint.GetBidiClass(new CodePoint(codePoint)).CharacterType).IsEqualTo(expected);

    [Test]
    public async Task EveryCodePointHasTheClassOfDerivedBidiClass()
    {
        BidiCharacterType[] expected = ReadDerivedBidiClass();
        List<string> differences = [];
        for (int i = 0; i < CodePointCount; i++)
        {
            if (i is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            BidiCharacterType actual = CodePoint.GetBidiClass(new CodePoint(i)).CharacterType;
            if (actual != expected[i] && differences.Count < 20)
            {
                differences.Add($"U+{i:X4}: {actual}, expected {expected[i]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    // The @missing lines give the default of each range, a later line overriding an earlier one,
    // and the data lines give the class of every other code point.
    private static BidiCharacterType[] ReadDerivedBidiClass()
    {
        Dictionary<string, BidiCharacterType> longNames = new(StringComparer.Ordinal)
        {
            { "Left_To_Right", BidiCharacterType.LeftToRight },
            { "Right_To_Left", BidiCharacterType.RightToLeft },
            { "Arabic_Letter", BidiCharacterType.ArabicLetter },
            { "European_Terminator", BidiCharacterType.EuropeanTerminator },
            { "Boundary_Neutral", BidiCharacterType.BoundaryNeutral },
        };

        Regex missing = new(@"^# @missing: ([0-9A-F]+)\.\.([0-9A-F]+); (\w+)");
        Regex data = new(@"^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(\w+)");
        BidiCharacterType[] values = new BidiCharacterType[CodePointCount];

        string path = Path.Combine(GeneratorPaths.GetRulesDirectory(TestEnvironment.SolutionDirectoryFullPath), "DerivedBidiClass.txt");
        foreach (string line in File.ReadLines(path))
        {
            Match match = missing.Match(line);
            BidiCharacterType value;
            if (match.Success)
            {
                value = longNames[match.Groups[3].Value];
            }
            else
            {
                match = data.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                value = UnicodeTypeMaps.BidiCharacterTypeMap[match.Groups[3].Value];
            }

            int start = Convert.ToInt32(match.Groups[1].Value, 16);
            int end = match.Groups[2].Success ? Convert.ToInt32(match.Groups[2].Value, 16) : start;
            values.AsSpan(start, end - start + 1).Fill(value);
        }

        return values;
    }
}
