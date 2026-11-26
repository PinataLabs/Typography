// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnicodeTrieGenerator;

/// <content>
/// Gives each code point its universal shaper (USE) category, with the rules of gen-use-table.py in HarfBuzz 15.0.0.
/// </content>
public static partial class Generator
{
    /// <summary>
    /// The categories of the universal shaper, in the order of their symbols. O is first, so a code point
    /// that the trie does not set has the category O, as in HarfBuzz.
    /// </summary>
    private static readonly string[] UniversalCategoryNames =
    [
        "O", "B", "N", "GB", "CGJ", "SUB", "H", "HN", "ZWNJ", "WJ", "R", "CS", "IS", "Sk", "G", "J", "SB", "SE",
        "HVM", "HM", "HR", "RK", "FAbv", "FBlw", "FPst", "MAbv", "MBlw", "MPst", "MPre", "CMAbv", "CMBlw", "VAbv",
        "VBlw", "VPst", "VPre", "VMAbv", "VMBlw", "VMPst", "VMPre", "SMAbv", "SMBlw", "FMAbv", "FMBlw", "FMPst"
    ];

    // HarfBuzz shapes these scripts with shapers of their own, so they take no category.
    private static readonly HashSet<string> UniversalDisabledScripts = ["Arabic", "Lao", "Samaritan", "Syriac", "Thai"];

    private static readonly (string Name, Func<UseProperties, bool> Test)[] UniversalCategoryRules =
    [
        ("B", IsUseBase),
        ("N", p => p.Isc == "Brahmi_Joining_Number"),
        ("GB", IsUseBaseOther),
        ("CGJ", IsUseCgj),
        ("F", p => (p.Isc == "Consonant_Final" && p.Gc != "Lo") || p.Isc == "Consonant_Succeeding_Repha"),
        ("FM", p => p.Isc == "Syllable_Modifier"),
        ("M", p => (p.Isc == "Consonant_Medial" && p.Gc != "Lo") || p.Isc == "Consonant_Initial_Postfixed"),
        ("CM", p => p.Isc is "Nukta" or "Gemination_Mark" or "Consonant_Killer"),
        ("SUB", p => p.Isc == "Consonant_Subjoined" && p.Gc != "Lo"),
        ("CS", p => p.Isc == "Consonant_With_Stacker"),
        ("H", p => p.Isc == "Virama" && p.CodePoint != 0x0DCA),
        ("HVM", p => p.CodePoint == 0x0DCA),
        ("HN", p => p.Isc == "Number_Joiner"),
        ("IS", p => p.Isc == "Invisible_Stacker" && p.CodePoint != 0x1A60),
        ("G", p => p.Isc == "Hieroglyph"),
        ("HM", p => p.Isc == "Hieroglyph_Modifier"),
        ("HR", p => p.Isc == "Hieroglyph_Mirror"),
        ("J", p => p.Isc == "Hieroglyph_Joiner"),
        ("SB", p => p.Isc is "Hieroglyph_Mark_Begin" or "Hieroglyph_Segment_Begin"),
        ("SE", p => p.Isc is "Hieroglyph_Mark_End" or "Hieroglyph_Segment_End"),
        ("ZWNJ", p => p.Isc == "Non_Joiner"),
        ("O", IsUseOther),
        ("RK", p => p.Isc == "Reordering_Killer"),
        ("R", p => p.Isc is "Consonant_Preceding_Repha" or "Consonant_Prefixed"),
        ("Sk", p => p.CodePoint == 0x1A60),
        ("SM", IsUseSymbolModifier),
        ("V", p => p.Isc == "Pure_Killer" || (p.Gc != "Lo" && p.Isc is "Vowel" or "Vowel_Dependent")),
        ("VM", p => p.Isc is "Tone_Mark" or "Cantillation_Mark" or "Register_Shifter" or "Visarga"
            || (p.Gc != "Lo" && p.Isc == "Bindu")),
        ("WJ", IsUseWordJoiner),
    ];

    // The categories that take a suffix from the positional category, with the positional categories of each suffix.
    private static readonly Dictionary<string, (string Suffix, string[] Positions)[]> UniversalPositions = new()
    {
        { "F", [("Abv", ["Top"]), ("Blw", ["Bottom"]), ("Pst", ["Right"])] },
        {
            "M",
            [
                ("Abv", ["Top"]),
                ("Blw", ["Bottom", "Bottom_And_Left", "Bottom_And_Right"]),
                ("Pst", ["Right"]),
                ("Pre", ["Left", "Top_And_Bottom_And_Left"])
            ]
        },
        { "CM", [("Abv", ["Top"]), ("Blw", ["Bottom", "Overstruck"])] },
        {
            "V",
            [
                ("Abv", ["Top", "Top_And_Bottom", "Top_And_Bottom_And_Right", "Top_And_Right"]),
                ("Blw", ["Bottom", "Overstruck", "Bottom_And_Right"]),
                ("Pst", ["Right"]),
                ("Pre", ["Left", "Top_And_Left", "Top_And_Left_And_Right", "Left_And_Right"])
            ]
        },
        { "VM", [("Abv", ["Top"]), ("Blw", ["Bottom", "Overstruck"]), ("Pst", ["Right"]), ("Pre", ["Left"])] },
        { "SM", [("Abv", ["Top"]), ("Blw", ["Bottom"])] },
        { "FM", [("Abv", ["Top"]), ("Blw", ["Bottom"]), ("Pst", ["Not_Applicable"])] },
    };

    private static bool IsUseBase(UseProperties p)
        => p.Isc is "Number" or "Consonant" or "Consonant_Head_Letter" or "Tone_Letter" or "Vowel_Independent"
        || (p.Ajt is "C" or "D" or "L" or "R" && p.Isc != "Joiner")
        || (p.Gc == "Lo" && p.Isc is "Avagraha" or "Bindu" or "Consonant_Final" or "Consonant_Medial"
            or "Consonant_Subjoined" or "Vowel" or "Vowel_Dependent");

    private static bool IsUseBaseOther(UseProperties p)
        => p.Isc == "Consonant_Placeholder" || p.CodePoint is 0x2015 or 0x2022 or 0x25FB or 0x25FC or 0x25FD or 0x25FE;

    // Also variation selectors and ZWJ.
    private static bool IsUseCgj(UseProperties p)
        => p.Isc == "Joiner" || (p.Di && p.Gc is "Mc" or "Me" or "Mn");

    private static bool IsUseSymbolModifier(UseProperties p)
        => p.Isc == "Symbol_Modifier";

    private static bool IsUseOther(UseProperties p)
        => (p.Gc == "Po" || p.Isc is "Consonant_Dead" or "Joiner" or "Modifying_Letter" or "Other")
        && !IsUseBase(p)
        && !IsUseBaseOther(p)
        && !IsUseCgj(p)
        && !IsUseSymbolModifier(p)
        && !IsUseWordJoiner(p);

    // Also unassigned code points.
    private static bool IsUseWordJoiner(UseProperties p)
        => (p.Di
            && p.CodePoint is not (0x115F or 0x1160 or 0x3164 or 0xFFA0 or 0x1BCA0 or 0x1BCA1 or 0x1BCA2 or 0x1BCA3)
            && p.Isc == "Other"
            && !IsUseCgj(p))
        || p.Gc == "Cn";

    /// <summary>
    /// Gives the universal shaper category of every code point that has one other than O.
    /// </summary>
    /// <returns>The category of each code point.</returns>
    private static Dictionary<int, string> GetUniversalCategories()
    {
        Dictionary<int, UseProperties> properties = [];
        UseProperties Get(int codePoint)
        {
            if (!properties.TryGetValue(codePoint, out UseProperties? value))
            {
                properties[codePoint] = value = new UseProperties(codePoint);
            }

            return value;
        }

        // As in gen-use-table.py, only the code points in these files take a category, and the ms-use files
        // override the Unicode values.
        ForEachUseLine("IndicSyllabicCategory.txt", 1, (u, v) => Get(u).Isc = v);
        ForEachUseLine("IndicPositionalCategory.txt", 1, (u, v) => Get(u).Ipc = v);
        ForEachUseLine("ArabicShaping.txt", 2, (u, v) => Get(u).Ajt = v);
        ForEachUseLine("DerivedCoreProperties.txt", 1, (u, v) =>
        {
            if (v == "Default_Ignorable_Code_Point")
            {
                Get(u).Di = true;
            }
        });

        // HarfBuzz does not yet have Consonant_Final_Modifier, so it takes it as Syllable_Modifier.
        ForEachUseLine("IndicSyllabicCategory-Additional.txt", 1, (u, v)
            => Get(u).Isc = v == "Consonant_Final_Modifier" ? "Syllable_Modifier" : v);
        ForEachUseLine("IndicPositionalCategory-Additional.txt", 1, (u, v)
            => Get(u).Ipc = v == "NA" ? "Not_Applicable" : v);

        // UnicodeData.txt gives the first and last code point of a range only, and gen-use-table.py reads it so.
        ForEachUseLine("UnicodeData.txt", 2, (u, v) =>
        {
            if (properties.TryGetValue(u, out UseProperties? p))
            {
                p.Gc = v;
            }
        });
        ForEachUseLine("Scripts.txt", 1, (u, v) =>
        {
            if (properties.TryGetValue(u, out UseProperties? p))
            {
                p.Script = v;
            }
        });

        Dictionary<int, string> categories = [];
        foreach (UseProperties p in properties.Values)
        {
            if (UniversalDisabledScripts.Contains(p.Script))
            {
                continue;
            }

            string category = GetUniversalCategory(p);
            if (category != "O")
            {
                categories[p.CodePoint] = category;
            }
        }

        return categories;
    }

    private static string GetUniversalCategory(UseProperties p)
    {
        // gen-use-table.py gives these code points the categories that the Unicode data does not yet give.
        if (p.CodePoint is >= 0x1CE2 and <= 0x1CE8)
        {
            p.Isc = "Cantillation_Mark";
        }

        if (p.CodePoint is (>= 0x0F18 and <= 0x0F19) or (>= 0x0F3E and <= 0x0F3F))
        {
            p.Isc = "Vowel_Dependent";
        }

        if (p.CodePoint == 0x1CED)
        {
            p.Isc = "Tone_Mark";
        }

        string[] matches = UniversalCategoryRules.Where(r => r.Test(p)).Select(r => r.Name).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"U+{p.CodePoint:X4} matches the categories [{string.Join(", ", matches)}].");
        }

        string category = matches[0];
        if (p.CodePoint is 0x11302 or 0x11303 or 0x114C1)
        {
            p.Ipc = "Top";
        }

        if (!UniversalPositions.TryGetValue(category, out (string Suffix, string[] Positions)[]? positions))
        {
            return category;
        }

        string[] suffixes = positions.Where(x => x.Positions.Contains(p.Ipc)).Select(x => x.Suffix).ToArray();
        if (suffixes.Length != 1)
        {
            throw new InvalidOperationException($"U+{p.CodePoint:X4} of category {category} has the position {p.Ipc}.");
        }

        return category + suffixes[0];
    }

    /// <summary>
    /// Calls <paramref name="action"/> for each code point of each data line of a rule file, as gen-use-table.py
    /// reads it: the comment is removed and the fields are split at semicolons.
    /// </summary>
    private static void ForEachUseLine(string fileName, int field, Action<int, string> action)
    {
        using StreamReader sr = GetStreamReader(fileName);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            int comment = line.IndexOf('#');
            string[] fields = (comment >= 0 ? line[..comment] : line).Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length == 1)
            {
                continue;
            }

            string[] range = fields[0].Split("..");
            int start = ParseHexInt(range[0]);
            int end = range.Length == 1 ? start : ParseHexInt(range[1]);
            for (int u = start; u <= end; u++)
            {
                action(u, fields[field]);
            }
        }
    }

    /// <summary>
    /// The properties that gen-use-table.py reads for one code point, with its defaults.
    /// </summary>
    private sealed class UseProperties(int codePoint)
    {
        public int CodePoint { get; } = codePoint;

        public string Isc { get; set; } = "Other";

        public string Ipc { get; set; } = "Not_Applicable";

        public string Ajt { get; set; } = "X";

        public bool Di { get; set; }

        public string Gc { get; set; } = "Cn";

        public string Script { get; set; } = "Unknown";
    }
}
