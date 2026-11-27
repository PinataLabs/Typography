// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using UnicodeTrieGenerator;

namespace PinataLabs.Typography.Tests.Unicode;

public class ScriptClassTests
{
    private const int CodePointCount = 0x110000;

    [Test]
    public async Task ScriptsTxtIsTheVersionOfTheLibrary()
    {
        string first = File.ReadLines(ScriptsPath()).First();

        await Assert.That(first).IsEqualTo($"# Scripts-{UnicodeInfo.Version}.txt");
    }

    [Test]
    public async Task GetScriptClass_MatchesScriptsTxtForEveryCodePoint()
    {
        ScriptClass[] expected = ReadScripts();

        var mismatches = new List<string>();
        for (int value = 0; value < CodePointCount; value++)
        {
            if (value is >= 0xD800 and <= 0xDFFF)
            {
                await Assert.That(expected[value]).IsEqualTo(ScriptClass.Unknown);
                continue;
            }

            ScriptClass actual = CodePoint.GetScriptClass(new CodePoint(value));
            if (actual != expected[value] && mismatches.Count < 10)
            {
                mismatches.Add($"U+{value:X4}: {actual}, expected {expected[value]}");
            }
        }

        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    [Arguments(0x0041, ScriptClass.Latin)]
    [Arguments(0x05D0, ScriptClass.Hebrew)]
    [Arguments(0x0627, ScriptClass.Arabic)]
    [Arguments(0x0020, ScriptClass.Common)]
    [Arguments(0x0301, ScriptClass.Inherited)]
    [Arguments(0x0378, ScriptClass.Unknown)]
    public async Task GetScriptClass_OfKnownCodePoints(int value, ScriptClass expected)
        => await Assert.That(CodePoint.GetScriptClass(new CodePoint(value))).IsEqualTo(expected);

    private static string ScriptsPath()
        => Path.Combine(GeneratorPaths.GetRulesDirectory(TestEnvironment.SolutionDirectoryFullPath), "Scripts.txt");

    private static ScriptClass[] ReadScripts()
    {
        var line = new Regex(@"^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(\w+)\s*#", RegexOptions.None, TimeSpan.FromSeconds(1));
        var scripts = new ScriptClass[CodePointCount];
        foreach (string text in File.ReadLines(ScriptsPath()))
        {
            Match match = line.Match(text);
            if (!match.Success)
            {
                continue;
            }

            int start = Convert.ToInt32(match.Groups[1].Value, 16);
            int end = match.Groups[2].Success ? Convert.ToInt32(match.Groups[2].Value, 16) : start;
            scripts.AsSpan(start, end - start + 1).Fill(Enum.Parse<ScriptClass>(match.Groups[3].Value.Replace("_", string.Empty)));
        }

        return scripts;
    }
}
