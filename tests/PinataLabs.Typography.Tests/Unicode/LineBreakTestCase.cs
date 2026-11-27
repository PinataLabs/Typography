// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;

namespace PinataLabs.Typography.Tests.Unicode;

/// <summary>
/// One line of LineBreakTest.txt: a text as code points, and the code point indexes where it may break.
/// </summary>
internal readonly record struct LineBreakTestCase(int LineNumber, int[] CodePoints, int[] BreakPoints)
{
    public string Text
    {
        get
        {
            System.Text.StringBuilder text = new();
            foreach (int codePoint in this.CodePoints)
            {
                text.Append(char.ConvertFromUtf32(codePoint));
            }

            return text.ToString();
        }
    }

    // https://www.unicode.org/Public/UCD/latest/ucd/auxiliary/LineBreakTest.html
    public static List<LineBreakTestCase> Read()
    {
        string[] lines = File.ReadAllLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "LineBreakTest.txt"));
        List<LineBreakTestCase> cases = [];
        for (int lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            // Remove the comment, and ignore a line that is then blank.
            string line = lines[lineNumber - 1].Split('#')[0].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<int> codePoints = [];
            List<int> breakPoints = [];
            foreach (string token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                switch (token)
                {
                    case "×":
                        break;
                    case "÷":
                        breakPoints.Add(codePoints.Count);
                        break;
                    default:
                        codePoints.Add(Convert.ToInt32(token, 16));
                        break;
                }
            }

            cases.Add(new LineBreakTestCase(lineNumber, [.. codePoints], [.. breakPoints]));
        }

        return cases;
    }
}
