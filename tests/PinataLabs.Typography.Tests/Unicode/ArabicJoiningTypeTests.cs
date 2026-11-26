// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using UnicodeTrieGenerator;

namespace PinataLabs.Typography.Tests.Unicode;

public class ArabicJoiningTypeTests
{
    private static readonly Dictionary<string, ArabicJoiningType> JoiningTypes = new()
    {
        ["R"] = ArabicJoiningType.RightJoining,
        ["L"] = ArabicJoiningType.LeftJoining,
        ["D"] = ArabicJoiningType.DualJoining,
        ["C"] = ArabicJoiningType.JoinCausing,
        ["U"] = ArabicJoiningType.NonJoining,
        ["T"] = ArabicJoiningType.Transparent,
    };

    // A character that ArabicShaping.txt lists has the joining type that it lists, even when its General Category
    // gives an unlisted character joining type T. HarfBuzz reads the same file.
    [Test]
    public async Task ListedCharactersHaveTheirListedJoiningType()
    {
        string path = Path.Combine(GeneratorPaths.GetRulesDirectory(TestEnvironment.SolutionDirectoryFullPath), "ArabicShaping.txt");
        List<string> different = [];
        foreach (string line in File.ReadLines(path))
        {
            string[] fields = line.Split('#')[0].Split(';');
            if (fields.Length < 4)
            {
                continue;
            }

            int value = int.Parse(fields[0].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ArabicJoiningType expected = JoiningTypes[fields[2].Trim()];
            ArabicJoiningType actual = CodePoint.GetArabicJoiningClass(new CodePoint(value)).JoiningType;
            if (actual != expected)
            {
                different.Add($"{value:X4} {fields[1].Trim()} expected {expected} actual {actual}");
            }
        }

        await Assert.That(different).IsEmpty();
    }
}
