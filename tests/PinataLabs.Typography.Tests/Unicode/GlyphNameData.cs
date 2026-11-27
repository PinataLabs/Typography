// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PinataLabs.Typography.Tests.Unicode;

/// <summary>
/// Reads the glyph name lists that the generated tables come from, as the tests' own copy of the expected values.
/// </summary>
internal static class GlyphNameData
{
    private static readonly string Directory = Path.Combine(TestEnvironment.SolutionDirectoryFullPath, "tests", "tools", "glyph_names");

    /// <summary>Gets the entries of glyphlist.txt, name to text.</summary>
    public static Dictionary<string, string> GlyphList { get; } = ReadList("glyphlist.txt");

    /// <summary>Gets the entries of zapfdingbats.txt, name to text.</summary>
    public static Dictionary<string, string> ZapfDingbats { get; } = ReadList("zapfdingbats.txt");

    /// <summary>Gets the entries of aglfn.txt, code point to name.</summary>
    public static Dictionary<int, string> Aglfn { get; } = ReadAglfn();

    private static Dictionary<string, string> ReadList(string fileName)
    {
        Dictionary<string, string> list = [];
        foreach (string line in File.ReadLines(Path.Combine(Directory, fileName)))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] fields = line.Split(';');
            StringBuilder text = new();
            foreach (string value in fields[1].Split(' '))
            {
                text.Append(char.ConvertFromUtf32(int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            }

            list.Add(fields[0], text.ToString());
        }

        return list;
    }

    private static Dictionary<int, string> ReadAglfn()
    {
        Dictionary<int, string> names = [];
        foreach (string line in File.ReadLines(Path.Combine(Directory, "aglfn.txt")))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] fields = line.Split(';');
            names.Add(int.Parse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture), fields[1]);
        }

        return names;
    }
}
