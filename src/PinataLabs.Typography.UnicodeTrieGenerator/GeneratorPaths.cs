// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace UnicodeTrieGenerator;

/// <summary>
/// Builds the generator's input and output directories from the solution directory.
/// </summary>
internal static class GeneratorPaths
{
    /// <summary>
    /// Gets the directory that holds the Unicode rule files the generator reads.
    /// </summary>
    /// <param name="solutionDirectory">The directory that contains the solution file.</param>
    /// <returns>The full path of the rules directory.</returns>
    public static string GetRulesDirectory(string solutionDirectory)
        => Path.Combine(solutionDirectory, "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules");

    /// <summary>
    /// Gets the directory that the generator writes the Unicode resources to.
    /// </summary>
    /// <param name="solutionDirectory">The directory that contains the solution file.</param>
    /// <returns>The full path of the resources directory.</returns>
    public static string GetResourcesDirectory(string solutionDirectory)
        => Path.Combine(solutionDirectory, "src", "PinataLabs.Typography", "Unicode", "Resources");
}
