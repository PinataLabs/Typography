// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;

namespace PinataLabs.Typography.Tests.Unicode;

/// <summary>
/// The universal shaper gives each code point the category that HarfBuzz gives it.
/// </summary>
public class UniversalShapingCategoryTests
{
    private const int CodePointCount = 0x110000;

    [Test]
    public async Task EveryCodePointHasTheCategoryThatHarfBuzzGives()
    {
        string[] expected = ReadHarfBuzzCategories();
        Dictionary<string, List<string>> differences = [];
        for (int i = 0; i < CodePointCount; i++)
        {
            string actual = UniversalShapingData.Categories[UnicodeData.GetUniversalShapingSymbolCount((uint)i)];
            if (actual != expected[i])
            {
                string key = $"{actual}, expected {expected[i]}";
                if (!differences.TryGetValue(key, out List<string>? codePoints))
                {
                    differences[key] = codePoints = [];
                }

                codePoints.Add(i.ToString("X4"));
            }
        }

        List<string> summary = differences
            .OrderByDescending(x => x.Value.Count)
            .Select(x => $"{x.Key}: {x.Value.Count} code points, such as {string.Join(' ', x.Value.Take(5))}")
            .ToList();

        await Assert.That(summary).IsEmpty();
    }

    // tests/tools/derive_harfbuzz_use_categories.py writes the file from the table of HarfBuzz 15.0.0.
    private static string[] ReadHarfBuzzCategories()
    {
        string[] values = Enumerable.Repeat("O", CodePointCount).ToArray();
        Regex data = new(@"^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(\w+)");
        foreach (string line in File.ReadLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "UniversalShapingCategory.harfbuzz.txt")))
        {
            Match match = data.Match(line);
            if (match.Success)
            {
                int start = Convert.ToInt32(match.Groups[1].Value, 16);
                int end = match.Groups[2].Success ? Convert.ToInt32(match.Groups[2].Value, 16) : start;
                values.AsSpan(start, end - start + 1).Fill(match.Groups[3].Value);
            }
        }

        return values;
    }
}
