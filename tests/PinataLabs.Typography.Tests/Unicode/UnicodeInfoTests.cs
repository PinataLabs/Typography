// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using UnicodeTrieGenerator;

namespace PinataLabs.Typography.Tests.Unicode;

public class UnicodeInfoTests
{
    [Test]
    public async Task Version_IsTheVersionOfTheRuleFilesTheTriesAreBuiltFrom()
    {
        string rules = GeneratorPaths.GetRulesDirectory(TestEnvironment.SolutionDirectoryFullPath);

        await Assert.That(UnicodeInfo.Version).IsEqualTo(UnicodeVersionReader.Read(rules));
    }

    [Test]
    public async Task Version_IsTheVersionOfTheConformanceTestData()
    {
        // BidiTest, BidiCharacterTest, GraphemeBreakTest and LineBreakTest name their version in the first line.
        await Assert.That(UnicodeInfo.Version).IsEqualTo(UnicodeVersionReader.Read(TestEnvironment.UnicodeTestDataFullPath));
    }

    [Test]
    public async Task Read_FilesOfOneVersion_ReturnsIt()
    {
        using TempDirectory directory = new();
        directory.Write("LineBreak.txt", "# LineBreak-17.0.0.txt");
        directory.Write("Scripts.txt", "# Scripts-17.0.0.txt");
        directory.Write("emoji-data.txt", "# emoji-data.txt");
        directory.Write("UnicodeData.txt", "0000;<control>;Cc;0;BN;;;;;N;NULL;;;;");

        await Assert.That(UnicodeVersionReader.Read(directory.Path)).IsEqualTo("17.0.0");
    }

    [Test]
    public async Task Read_VersionLineInTheHeader_IsChecked()
    {
        using TempDirectory directory = new();
        directory.Write("LineBreak.txt", "# LineBreak-18.0.0.txt");
        directory.Write("emoji-data.txt", "# emoji-data.txt\n# Version: 17.0.0");

        await Assert.That(() => UnicodeVersionReader.Read(directory.Path)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Read_FilesOfTwoVersions_Throws()
    {
        using TempDirectory directory = new();
        directory.Write("LineBreak.txt", "# LineBreak-17.0.0.txt");
        directory.Write("Scripts.txt", "# Scripts-18.0.0.txt");

        await Assert.That(() => UnicodeVersionReader.Read(directory.Path)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Read_NoFileNamesAVersion_Throws()
    {
        using TempDirectory directory = new();
        directory.Write("emoji-data.txt", "# emoji-data.txt");

        await Assert.That(() => UnicodeVersionReader.Read(directory.Path)).ThrowsExactly<InvalidOperationException>();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() => this.Path = Directory.CreateTempSubdirectory().FullName;

        public string Path { get; }

        public void Write(string name, string firstLine)
            => File.WriteAllText(System.IO.Path.Combine(this.Path, name), firstLine + "\n# Date: 2026-01-01\n");

        public void Dispose() => Directory.Delete(this.Path, recursive: true);
    }
}
