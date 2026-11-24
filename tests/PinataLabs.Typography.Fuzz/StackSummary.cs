// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Groups findings: one defect reached by many inputs reads as one group.
/// </summary>
internal static partial class StackSummary
{
    /// <summary>
    /// Gets the grouping key of a finding: the escaped exception and the first library frame that threw it,
    /// or the broken rule with its input-derived numbers removed.
    /// </summary>
    public static string Signature(FuzzFindingException finding)
        => finding.InnerException is Exception inner
            ? $"{inner.GetType().Name} {FirstLibraryFrame(inner)}"
            : Numbers().Replace(finding.Message, "N");

    private static string FirstLibraryFrame(Exception e)
    {
        string[] frames = [.. (e.StackTrace ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];
        string? frame = Array.Find(frames, f => f.Contains("PinataLabs.Typography.", StringComparison.Ordinal)
            && !f.Contains("PinataLabs.Typography.Fuzz.", StringComparison.Ordinal));
        frame ??= frames.Length > 0 ? frames[0] : "(no stack)";
        int file = frame.IndexOf(" in ", StringComparison.Ordinal);
        return file < 0 ? frame : frame.Substring(0, file);
    }

    [GeneratedRegex("-?[0-9]+")]
    private static partial Regex Numbers();
}
