// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using UnicodeTrieGenerator;

namespace PinataLabs.Typography.Tests.Unicode;

public class GeneratorPathsTests
{
    [Test]
    public async Task RulesDirectory_IsBuiltFromPlatformSeparatedSegments()
    {
        string path = GeneratorPaths.GetRulesDirectory("root");

        string[] segments = path.Split(Path.DirectorySeparatorChar);

        await Assert.That(segments).IsEquivalentTo(
            ["root", "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ResourcesDirectory_IsBuiltFromPlatformSeparatedSegments()
    {
        string path = GeneratorPaths.GetResourcesDirectory("root");

        string[] segments = path.Split(Path.DirectorySeparatorChar);

        await Assert.That(segments).IsEquivalentTo(
            ["root", "src", "PinataLabs.Typography", "Unicode", "Resources"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task RulesDirectory_FromSolutionRoot_ContainsTheUnicodeRuleFiles()
    {
        string rules = GeneratorPaths.GetRulesDirectory(TestEnvironment.SolutionDirectoryFullPath);

        await Assert.That(File.Exists(Path.Combine(rules, "LineBreak.txt"))).IsTrue();
    }

    [Test]
    public async Task ResourcesDirectory_FromSolutionRoot_ContainsTheGeneratedTries()
    {
        string resources = GeneratorPaths.GetResourcesDirectory(TestEnvironment.SolutionDirectoryFullPath);

        await Assert.That(File.Exists(Path.Combine(resources, "LineBreakTrie.Generated.cs"))).IsTrue();
    }
}
