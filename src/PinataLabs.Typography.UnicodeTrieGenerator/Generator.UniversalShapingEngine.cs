// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PinataLabs.Typography.Unicode;
using UnicodeTrieGenerator.StateAutomation;
using GC = System.Globalization.UnicodeCategory;
using IPC = PinataLabs.Typography.Unicode.IndicPositionalCategory;
using ISC = PinataLabs.Typography.Unicode.IndicSyllabicCategory;

namespace UnicodeTrieGenerator;

/// <content>
/// Contains code to generate a trie and state machine for storing Universal Shaping Data category data.
/// </content>
public static partial class Generator
{
    private static List<Codepoint> GenerateUniversalShapingDataTrie(
                UnicodeTrie unicodeGeneralCategory,
                UnicodeTrie indicSyllabicCategoryTrie,
                UnicodeTrie indicPositionalCategoryTrie,
                UnicodeTrie arabicJoiningTrie)
    {
        static IEnumerable<int>? ParseCodes(string code)
        {
            if (string.IsNullOrEmpty(code) || code.StartsWith('<'))
            {
                return null;
            }

            return Array.ConvertAll(code.Split(' '), ParseHexInt);
        }

        static ArabicJoiningType GetJoiningType(int codePoint, uint value, GC category)
        {
            var type = (ArabicJoiningType)(value & 0xFF);

            // All others not explicitly listed have joining type U
            if (type == ArabicJoiningType.NonJoining)
            {
                // 200C; ZERO WIDTH NON-JOINER; U; No_Joining_Group
                // 200D; ZERO WIDTH JOINER; C; No_Joining_Group
                // 202F; NARROW NO-BREAK SPACE; U; No_Joining_Group
                // 2066; LEFT-TO-RIGHT ISOLATE; U; No_Joining_Group
                // 2067; RIGHT-TO-LEFT ISOLATE; U; No_Joining_Group
                // 2068; FIRST STRONG ISOLATE; U; No_Joining_Group
                // 2069; POP DIRECTIONAL ISOLATE; U; No_Joining_Group
                if (codePoint is 0x200C
                    or 0x200D
                    or 0x202F
                    or 0x2066
                    or 0x2067
                    or 0x2068
                    or 0x2069)
                {
                    return type;
                }

                // Those that are not explicitly listed and that are of General Category Mn, Me, or Cf have joining type T.
                if (category is GC.NonSpacingMark or GC.EnclosingMark or GC.Format)
                {
                    type = ArabicJoiningType.Transparent;
                }
            }

            return type;
        }

        List<Codepoint> codePoints = new();
        using StreamReader sr = GetStreamReader("UnicodeData.txt");
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            string[] parts = line.Split(';');

            Codepoint codepoint = new()
            {
                Code = ParseHexInt(parts[0])
            };

            // See https://www.unicode.org/reports/tr44 for mapping of fields
            if (parts.Length > 4)
            {
                codepoint.Decomposition = ParseCodes(parts[5]) ?? Array.Empty<int>();
            }

            codepoint.Category = (GC)unicodeGeneralCategory.Get((uint)codepoint.Code);

            codepoint.IndicSyllabicCategory = (ISC)indicSyllabicCategoryTrie.Get((uint)codepoint.Code);
            codepoint.IndicPositionalCategory = (IPC)indicPositionalCategoryTrie.Get((uint)codepoint.Code);

            uint value = arabicJoiningTrie.Get((uint)codepoint.Code);
            codepoint.ArabicJoiningType = GetJoiningType(codepoint.Code, value, codepoint.Category);
            codepoint.ArabicJoiningGroup = (ArabicJoiningGroup)((value >> 16) & 0xFF);

            codePoints.Add(codepoint);
        }

        Dictionary<int, string> categories = GetUniversalCategories();
        Dictionary<string, int> symbols = UniversalCategoryNames.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i);
        UnicodeTrieBuilder builder = new((uint)symbols["O"]);
        foreach (KeyValuePair<int, string> item in categories)
        {
            builder.Set(item.Key, (uint)symbols[item.Value]);
        }

        Dictionary<int, List<int>> decompositions = new();
        foreach (Codepoint codePoint in codePoints)
        {
            if (codePoint.IndicSyllabicCategory == ISC.VowelDependent && codePoint.Decomposition.Any())
            {
                decompositions[codePoint.Code] = Decompose(codePoint.Code, codePoints);
            }
        }

        UnicodeTrie trie = builder.Freeze();
        GenerateTrieClass("UniversalShaping", trie);

        StateMachine machine = GetStateMachine("use", symbols);

        GenerateDataClass("UniversalShaping", symbols, decompositions, machine);

        return codePoints;
    }

    private static List<int> Decompose(int code, List<Codepoint> codepoints)
    {
        List<int> decomposition = new();
        Codepoint codePoint = codepoints.First(x => x.Code == code);
        foreach (int c in codePoint.Decomposition)
        {
            List<int> codes = Decompose(c, codepoints);
            codes = codes.Count > 0 ? codes : new List<int> { c };
            decomposition.AddRange(codes);
        }

        return decomposition;
    }

    /// <summary>
    /// Generates the supplementary data for the shaper.
    /// </summary>
    /// <param name="name">The name of the class.</param>
    /// <param name="symbols">The symbols data.</param>
    /// <param name="decompositions">The decompositions data.</param>
    /// <param name="machine">The state machine.</param>
    /// <param name="partial">Whether to emit the class as partial.</param>
    private static void GenerateDataClass(
        string name,
        Dictionary<string, int>? symbols,
        Dictionary<int, List<int>>? decompositions,
        StateMachine machine,
        bool partial = false)
    {
        using FileStream fileStream = GetStreamWriter($"{name}Data.Generated.cs");
        using StreamWriter writer = new(fileStream);

        string partialKeyword = partial ? " partial " : " ";

        writer.WriteLine("// Copyright (c) Six Labors.");
        writer.WriteLine("// Copyright (c) PinataLabs.");
        writer.WriteLine("// Licensed under the Apache License, Version 2.0.");
        writer.WriteLine();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("using System;");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine();
        writer.WriteLine("namespace PinataLabs.Typography.Unicode.Resources;");
        writer.WriteLine();
        writer.WriteLine($"internal static{partialKeyword}class {name}Data");
        writer.WriteLine("{");

        int counter = 0;
        int max = 0;

        if (symbols != null)
        {
            // Write the categories.
            writer.WriteLine("    public static string[] Categories { get; } = new string[]");
            writer.WriteLine("    {");

            max = symbols.Count - 1;
            foreach (KeyValuePair<string, int> item in symbols)
            {
                writer.Write($"        \"{item.Key}\"");
                if (counter != max)
                {
                    writer.Write(",");
                }

                counter++;
                writer.Write(Environment.NewLine);
            }

            writer.WriteLine("    };");
            writer.Write(Environment.NewLine);
        }

        // Write the decompositions
        if (decompositions != null)
        {
            writer.WriteLine("    public static Dictionary<int, int[]> Decompositions { get; } = new()");
            writer.WriteLine("    {");

            counter = 0;
            max = decompositions.Count - 1;
            foreach (KeyValuePair<int, List<int>> item in decompositions)
            {
                writer.Write($"        {{ 0x{item.Key:X}, new int[] {{ {string.Join(',', item.Value.Select(x => "0x" + x.ToString("X")))} }} }}");
                if (counter != max)
                {
                    writer.Write(",");
                }

                counter++;
                writer.Write(Environment.NewLine);
            }

            writer.WriteLine("    };");
            writer.Write(Environment.NewLine);
        }

        // Writes the state machine state table.
        writer.WriteLine($"    public static int[][] StateTable => new int[{machine.StateTable.Length}][]");
        writer.WriteLine("    {");

        counter = 0;
        max = machine.StateTable.Length - 1;
        foreach (int[] item in machine.StateTable)
        {
            writer.Write($"        new int[] {{ {string.Join(',', item.Select(x => x))} }}");
            if (counter != max)
            {
                writer.Write(",");
            }

            counter++;
            writer.Write(Environment.NewLine);
        }

        writer.WriteLine("    };");

        // Writes the state machine accepting states.
        writer.Write(Environment.NewLine);
        writer.WriteLine("    public static bool[] AcceptingStates => new bool[]");
        writer.WriteLine("    {");

        counter = 0;
        max = machine.Accepting.Length - 1;
        foreach (bool item in machine.Accepting)
        {
            writer.Write($"        {item}".ToLowerInvariant());
            if (counter != max)
            {
                writer.Write(",");
            }

            counter++;
            writer.Write(Environment.NewLine);
        }

        writer.WriteLine("    };");

        // Writes the state machine tags.
        writer.Write(Environment.NewLine);
        writer.WriteLine($"    public static string[][] Tags => new string[{machine.Tags.Length}][]");
        writer.WriteLine("    {");

        counter = 0;
        max = machine.Tags.Length - 1;
        foreach (ICollection<string> item in machine.Tags)
        {
            if (item.Count == 0)
            {
                writer.Write($"        Array.Empty<string>()");
            }
            else
            {
                writer.Write($"        new string[] {{ {string.Join(',', item.Select(x => $"\"{x}\""))} }}");
            }

            if (counter != max)
            {
                writer.Write(",");
            }

            counter++;
            writer.Write(Environment.NewLine);
        }

        writer.WriteLine("    };");

        writer.WriteLine("}");
    }

    private static StateMachine GetStateMachine(string name, Dictionary<string, int> symbols)
    {
        using StreamReader sr = GetStreamReader($"{name}.machine");
        string machine = sr.ReadToEnd();
        StateMachine stateMachine = Compile.Build(machine, symbols);

        // A state can end matches of several rules. As in HarfBuzz, the first tag is that of the earliest rule.
        string[] order = Regex.Matches(machine, @"(\w+):").Select(m => m.Groups[1].Value).Distinct().ToArray();
        string[][] tags = stateMachine.Tags
            .Select(t => t.OrderBy(x => Array.IndexOf(order, x)).ToArray())
            .ToArray();

        return new StateMachine(stateMachine.StateTable, stateMachine.Accepting, tags);
    }

    private class Codepoint
    {
        public int Code { get; set; }

        public ISC IndicSyllabicCategory { get; set; }

        public IPC IndicPositionalCategory { get; set; }

        public ArabicJoiningType ArabicJoiningType { get; set; }

        public ArabicJoiningGroup ArabicJoiningGroup { get; set; }

        public IEnumerable<int> Decomposition { get; set; } = Array.Empty<int>();

        public GC Category { get; set; }

        public string Block { get; set; } = "No_Block";
    }
}
