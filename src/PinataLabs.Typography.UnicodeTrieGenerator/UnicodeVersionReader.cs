// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnicodeTrieGenerator;

/// <summary>
/// Reads the Unicode version of the rule files the generator builds the tries from.
/// </summary>
internal static partial class UnicodeVersionReader
{
    /// <summary>
    /// Gets the Unicode version that every versioned rule file in a directory names in its first line,
    /// such as <c># LineBreak-18.0.0.txt</c>. A file without a version in that line, such as
    /// <c>emoji-data.txt</c>, is not read.
    /// </summary>
    /// <param name="rulesDirectory">The directory that holds the Unicode rule files.</param>
    /// <returns>The version, such as <c>18.0.0</c>.</returns>
    /// <exception cref="InvalidOperationException">No file names a version, or two files name different versions.</exception>
    public static string Read(string rulesDirectory)
    {
        SortedDictionary<string, List<string>> filesByVersion = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(rulesDirectory, "*.txt"))
        {
            using StreamReader reader = File.OpenText(file);
            if (HeaderVersion().Match(reader.ReadLine() ?? string.Empty) is { Success: true } match)
            {
                string version = match.Groups[1].Value;
                if (!filesByVersion.TryGetValue(version, out List<string>? files))
                {
                    filesByVersion[version] = files = [];
                }

                files.Add(Path.GetFileName(file));
            }
        }

        if (filesByVersion.Count != 1)
        {
            throw new InvalidOperationException(filesByVersion.Count == 0
                ? $"No rule file in {rulesDirectory} names a Unicode version in its first line."
                : $"The rule files in {rulesDirectory} are of more than one Unicode version: "
                    + string.Join("; ", filesByVersion.Select(pair => $"{pair.Key} ({string.Join(", ", pair.Value)})")));
        }

        return filesByVersion.Keys.Single();
    }

    // A header such as "# BidiTest-18.0.0.txt". The first line of a conformance test file has the same form.
    [GeneratedRegex(@"^#\s+\S+?-(\d+\.\d+\.\d+)\.txt\s*$")]
    private static partial Regex HeaderVersion();
}
