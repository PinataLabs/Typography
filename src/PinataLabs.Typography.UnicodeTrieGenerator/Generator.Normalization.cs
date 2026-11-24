// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PinataLabs.Typography.Unicode;

namespace UnicodeTrieGenerator;

/// <content>
/// Contains code to generate the data that maps characters to their canonical parts and back,
/// and the order in which a shaper sorts combining marks.
/// </content>
public static partial class Generator
{
    // A starter has mark order 0. Orders 1 and 2 are left for the Arabic modifier marks that the shaper moves
    // to the start of a run, so every order in the trie sorts after them.
    private const int FirstMarkOrder = 3;

    private const uint ArabicModifierMark = 0x100;

    private const uint ExcludedFromComposition = 0x80000000;

    /// <summary>
    /// Generates the trie of mark orders and the tables of canonical decompositions and compositions.
    /// </summary>
    private static void GenerateNormalizationData()
    {
        Dictionary<int, int> combiningClasses = [];
        SortedDictionary<int, int[]> decompositions = [];
        using (StreamReader reader = GetStreamReader("UnicodeData.txt"))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(';');
                int codePoint = ParseHexInt(parts[0]);
                int combiningClass = int.Parse(parts[3]);
                if (combiningClass != 0)
                {
                    combiningClasses[codePoint] = combiningClass;
                }

                // A decomposition with a tag such as <compat> is not canonical.
                if (parts[5].Length > 0 && parts[5][0] != '<')
                {
                    decompositions[codePoint] = Array.ConvertAll(parts[5].Split(' '), ParseHexInt);
                }
            }
        }

        GenerateCombiningClassTrie(combiningClasses);
        GenerateCanonicalMappingData(combiningClasses, decompositions);
    }

    private static void GenerateCombiningClassTrie(Dictionary<int, int> combiningClasses)
    {
        Dictionary<int, uint> classOrders = [];
        Dictionary<int, uint> characterOrders = [];
        HashSet<int> modifiers = [];
        uint order = FirstMarkOrder;
        using (StreamReader reader = GetStreamReader("HarfBuzzMarkOrder.txt"))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || parts[0] == "#")
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "ccc":
                        classOrders.Add(int.Parse(parts[1]), order++);
                        break;
                    case "char":
                        characterOrders.Add(ParseHexInt(parts[1]), order++);
                        break;
                    case "modifier":
                        modifiers.Add(ParseHexInt(parts[1]));
                        break;
                    default:
                        throw new InvalidDataException($"Unknown line in HarfBuzzMarkOrder.txt: {line}");
                }
            }
        }

        UnicodeTrieBuilder builder = new();
        foreach ((int codePoint, int combiningClass) in combiningClasses)
        {
            uint value = characterOrders.TryGetValue(codePoint, out uint characterOrder)
                ? characterOrder
                : classOrders[combiningClass];

            if (modifiers.Contains(codePoint))
            {
                value |= ArabicModifierMark;
            }

            builder.Set(codePoint, value);
        }

        GenerateTrieClass("CombiningClass", builder.Freeze());
    }

    private static void GenerateCanonicalMappingData(Dictionary<int, int> combiningClasses, SortedDictionary<int, int[]> decompositions)
    {
        HashSet<int> listedExclusions = [];
        using (StreamReader reader = GetStreamReader("CompositionExclusions.txt"))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                int end = 0;
                while (end < line.Length && Uri.IsHexDigit(line[end]))
                {
                    end++;
                }

                if (end > 0)
                {
                    listedExclusions.Add(ParseHexInt(line.Substring(0, end)));
                }
            }
        }

        // A pair composes when its decomposition has two parts and starts with a starter.
        // The pairs that CompositionExclusions.txt lists stay in the table with a flag, because some shapers compose them.
        SortedDictionary<ulong, uint> compositions = [];
        foreach ((int codePoint, int[] parts) in decompositions)
        {
            if (parts.Length > 2)
            {
                throw new InvalidDataException($"U+{codePoint:X4} has a canonical decomposition of more than two characters.");
            }

            if (parts.Length != 2 || combiningClasses.ContainsKey(codePoint) || combiningClasses.ContainsKey(parts[0]))
            {
                continue;
            }

            uint flag = listedExclusions.Contains(codePoint) ? ExcludedFromComposition : 0;
            compositions.Add(((ulong)parts[0] << 32) | (uint)parts[1], (uint)codePoint | flag);
        }

        using FileStream fileStream = GetStreamWriter("CanonicalMappingData.Generated.cs");
        using StreamWriter writer = new(fileStream);

        writer.WriteLine("// Copyright (c) PinataLabs.");
        writer.WriteLine("// Licensed under the Apache License, Version 2.0.");
        writer.WriteLine();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("using System;");
        writer.WriteLine();
        writer.WriteLine("namespace PinataLabs.Typography.Unicode.Resources;");
        writer.WriteLine();
        writer.WriteLine("internal static class CanonicalMappingData");
        writer.WriteLine("{");
        writer.WriteLine("    /// <summary>The flag in a value of the combining class trie for a mark that the Arabic shaper moves to the start of a run.</summary>");
        writer.WriteLine($"    public const uint ArabicModifierMark = 0x{ArabicModifierMark:X};");
        writer.WriteLine();
        writer.WriteLine("    /// <summary>The flag in a value of <see cref=\"Composed\"/> for a character that CompositionExclusions.txt lists.</summary>");
        writer.WriteLine($"    public const uint ExcludedFromComposition = 0x{ExcludedFromComposition:X};");
        writer.WriteLine();
        WriteTable(writer, "uint", "Decomposed", "The characters that have a canonical decomposition, in ascending order.", decompositions.Keys.Select(x => $"0x{x:X}"));
        writer.WriteLine();
        WriteTable(writer, "uint", "DecomposedParts", "The two parts of each character of <see cref=\"Decomposed\"/>. The second part is 0 when the decomposition has one character.", decompositions.Values.SelectMany(x => new[] { $"0x{x[0]:X}", x.Length > 1 ? $"0x{x[1]:X}" : "0" }));
        writer.WriteLine();
        WriteTable(writer, "ulong", "ComposablePairs", "The pairs that compose, as the first character shifted left by 32 bits or the second, in ascending order.", compositions.Keys.Select(x => $"0x{x:X}"));
        writer.WriteLine();
        WriteTable(writer, "uint", "Composed", "The character that each pair of <see cref=\"ComposablePairs\"/> composes to, with its flag.", compositions.Values.Select(x => $"0x{x:X}"));
        writer.WriteLine("}");
    }

    private static void WriteTable(StreamWriter writer, string type, string name, string summary, IEnumerable<string> values)
    {
        writer.WriteLine($"    /// <summary>{summary}</summary>");
        writer.WriteLine($"    public static ReadOnlySpan<{type}> {name} => new {type}[]");
        writer.WriteLine("    {");
        foreach (string[] row in values.Chunk(16))
        {
            writer.WriteLine($"        {string.Join(", ", row)},");
        }

        writer.WriteLine("    };");
    }
}
