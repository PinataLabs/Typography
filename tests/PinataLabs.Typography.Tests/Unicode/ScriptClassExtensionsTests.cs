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

public class ScriptClassExtensionsTests
{
    [Test]
    public async Task GetIso15924Code_MatchesPropertyValueAliasesForEveryScript()
    {
        Dictionary<ScriptClass, string> expected = ReadCodes();

        string[] mismatches = Enum.GetValues<ScriptClass>()
            .Select(script => (script, expected: expected[script], actual: script.GetIso15924Code()))
            .Where(x => x.expected != x.actual)
            .Select(x => $"{x.script}: {x.actual}, expected {x.expected}")
            .ToArray();

        await Assert.That(expected.Count).IsEqualTo(Enum.GetValues<ScriptClass>().Length);
        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    public async Task GetIso15924Code_IsUniqueAndFourLetters()
    {
        string[] codes = Enum.GetValues<ScriptClass>().Select(x => x.GetIso15924Code()).ToArray();

        await Assert.That(codes).HasDistinctItems();
        await Assert.That(codes).All(x => Regex.IsMatch(x, "^[A-Z][a-z]{3}$"));
    }

    [Test]
    [Arguments(ScriptClass.Latin, "Latn")]
    [Arguments(ScriptClass.Arabic, "Arab")]
    [Arguments(ScriptClass.Hebrew, "Hebr")]
    [Arguments(ScriptClass.Common, "Zyyy")]
    [Arguments(ScriptClass.Inherited, "Zinh")]
    [Arguments(ScriptClass.Unknown, "Zzzz")]
    [Arguments(ScriptClass.Nko, "Nkoo")]
    public async Task GetIso15924Code_OfKnownScripts(ScriptClass script, string expected)
        => await Assert.That(script.GetIso15924Code()).IsEqualTo(expected);

    [Test]
    public async Task GetIso15924Code_OfAnUndefinedScript_Throws()
        => await Assert.That(() => ((ScriptClass)1000).GetIso15924Code()).Throws<ArgumentOutOfRangeException>();

    private static Dictionary<ScriptClass, string> ReadCodes()
    {
        var line = new Regex(@"^sc\s*;\s*(\w+)\s*;\s*(\w+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        string path = Path.Combine(GeneratorPaths.GetRulesDirectory(TestEnvironment.SolutionDirectoryFullPath), "PropertyValueAliases.txt");

        return File.ReadLines(path)
            .Select(text => line.Match(text))
            .Where(match => match.Success)
            .ToDictionary(
                match => Enum.Parse<ScriptClass>(match.Groups[2].Value.Replace("_", string.Empty)),
                match => match.Groups[1].Value);
    }
}
