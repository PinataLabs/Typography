// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PinataLabs.Typography.Unicode;

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

    /// <summary>Reads the code to glyph name table of an encoding from its source file.</summary>
    public static Dictionary<int, string> ReadEncoding(SimpleFontEncoding encoding)
    {
        Dictionary<int, string> table = [];
        switch (encoding)
        {
            case SimpleFontEncoding.Standard:
            case SimpleFontEncoding.Symbol:
            case SimpleFontEncoding.ZapfDingbats:
                string afm = encoding == SimpleFontEncoding.Standard ? "Times-Roman.afm" : encoding + ".afm";
                foreach (string line in File.ReadLines(Path.Combine(Directory, afm)))
                {
                    // C 32 ; WX 250 ; N space ; B 0 0 0 0 ;
                    string[] fields = line.Split(';');
                    string[] code = fields[0].Split(' ');
                    if (code[0] == "C" && int.Parse(code[1], CultureInfo.InvariantCulture) >= 0)
                    {
                        table.Add(int.Parse(code[1], CultureInfo.InvariantCulture), fields[2].Trim().Substring(2));
                    }
                }

                break;
            default:
                bool inSection = false;
                foreach (string line in File.ReadLines(Path.Combine(Directory, "pdf-latin-encodings.txt")))
                {
                    string content = line.Split('#')[0].Trim();
                    if (content.StartsWith('['))
                    {
                        inSection = content == $"[{encoding}]";
                    }
                    else if (inSection && content.Length > 0)
                    {
                        string[] fields = content.Split(' ');
                        table.Add(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1]);
                    }
                }

                break;
        }

        return table;
    }

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
