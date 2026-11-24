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

    // Where a font without GPOS places a mark on its base, in two fields of a value of the combining class trie.
    // The value of a field is the index of the word in these lists, and 0 means that the mark is not placed.
    private const int MarkAlignmentShift = 9;

    private const int MarkSideShift = 12;

    private const uint MarkPlacementMask = 7;

    private static readonly string[] MarkAlignments = ["none", "center", "left", "right", "double"];

    private static readonly string[] MarkSides = ["none", "below", "attached-below", "above", "attached-above"];

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
        Dictionary<int, uint> classPlacements = [];
        Dictionary<int, uint> characterPlacements = [];
        uint order = FirstMarkOrder;
        using (StreamReader reader = GetStreamReader("HarfBuzzMarkOrder.txt"))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0] == "#")
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "place":
                        uint placement = ((uint)IndexOf(MarkAlignments, parts[3], line) << MarkAlignmentShift)
                            | ((uint)IndexOf(MarkSides, parts[4], line) << MarkSideShift);

                        if (parts[1] == "ccc")
                        {
                            classPlacements.Add(int.Parse(parts[2]), placement);
                        }
                        else
                        {
                            characterPlacements.Add(ParseHexInt(parts[2]), placement);
                        }

                        break;
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

            value |= characterPlacements.Remove(codePoint, out uint characterPlacement)
                ? characterPlacement
                : classPlacements[combiningClass];

            builder.Set(codePoint, value);
        }

        // A mark of class 0 that HarfBuzz still places is a starter in the mark order.
        foreach ((int codePoint, uint placement) in characterPlacements)
        {
            builder.Set(codePoint, placement);
        }

        GenerateTrieClass("CombiningClass", builder.Freeze());
    }

    private static int IndexOf(string[] words, string word, string line)
    {
        int index = Array.IndexOf(words, word);
        return index >= 0 ? index : throw new InvalidDataException($"Unknown word in HarfBuzzMarkOrder.txt: {line}");
    }

    private static string ToPascalCase(string word)
        => string.Concat(word.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

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
        writer.WriteLine("/// <summary>How a font without GPOS aligns a mark with its base.</summary>");
        writer.WriteLine("internal enum MarkAlignment");
        writer.WriteLine("{");
        writer.WriteLine($"    {string.Join(", ", MarkAlignments.Select(ToPascalCase))},");
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("/// <summary>The side of its base that a font without GPOS places a mark on.</summary>");
        writer.WriteLine("internal enum MarkSide");
        writer.WriteLine("{");
        writer.WriteLine($"    {string.Join(", ", MarkSides.Select(ToPascalCase))},");
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("internal static class CanonicalMappingData");
        writer.WriteLine("{");
        writer.WriteLine("    /// <summary>The shift, in a value of the combining class trie, of the <see cref=\"MarkAlignment\"/> of a mark.</summary>");
        writer.WriteLine($"    public const int MarkAlignmentShift = {MarkAlignmentShift};");
        writer.WriteLine();
        writer.WriteLine("    /// <summary>The shift, in a value of the combining class trie, of the <see cref=\"MarkSide\"/> of a mark.</summary>");
        writer.WriteLine($"    public const int MarkSideShift = {MarkSideShift};");
        writer.WriteLine();
        writer.WriteLine("    /// <summary>The mask of the alignment or the side of a mark, after the shift.</summary>");
        writer.WriteLine($"    public const uint MarkPlacementMask = 0x{MarkPlacementMask:X};");
        writer.WriteLine();
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
