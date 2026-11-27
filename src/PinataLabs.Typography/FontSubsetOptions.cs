// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PinataLabs.Typography;

/// <summary>
/// Options for <see cref="FontSubsetter.Subset"/>.
/// </summary>
public sealed class FontSubsetOptions
{
    private static readonly ReadOnlyCollection<string> DefaultTableTags = new(
    [
        "CFF ",
        "OS/2",
        "cmap",
        "cvt ",
        "fpgm",
        "glyf",
        "head",
        "hhea",
        "hmtx",
        "loca",
        "maxp",
        "name",
        "post",
        "prep",
    ]);

    /// <summary>
    /// Gets the tables that a subset keeps when <see cref="Tables"/> is not set: the outline tables
    /// (<c>CFF </c>, or <c>glyf</c> and <c>loca</c>), the tables that a PDF reader and PDF/A validators read
    /// (<c>cmap</c>, <c>OS/2</c>, <c>head</c>, <c>hhea</c>, <c>hmtx</c>, <c>maxp</c>, <c>name</c> and <c>post</c>), and the
    /// tables that TrueType hinting runs (<c>cvt </c>, <c>fpgm</c> and <c>prep</c>).
    /// Every other table is dropped. That includes the layout tables <c>GSUB</c>, <c>GPOS</c> and <c>GDEF</c>, which a PDF does not use,
    /// and <c>DSIG</c>, whose signature a subset would break.
    /// </summary>
    public static IReadOnlyCollection<string> DefaultTables => DefaultTableTags;

    /// <summary>
    /// Gets the tags of the tables that the subset keeps, each of 4 characters, such as <c>"cmap"</c> and <c>"OS/2"</c>.
    /// A tag that the font does not have is ignored. <see langword="null"/> keeps <see cref="DefaultTables"/>.
    /// </summary>
    /// <remarks>
    /// The subset always keeps the tables that make it a font program: the outline tables, 'head' and 'maxp'.
    /// The table 'CFF ' has a space as its last character. A bare CFF font has no tables, so this option does not apply to it.
    /// </remarks>
    public IReadOnlyCollection<string>? Tables { get; init; }

    /// <summary>
    /// Gets the options of a font program load, whose limit on the memory that the font program retains also applies to a subset,
    /// or <see langword="null"/> for no limit. See <see cref="FontProgramOptions.MaxRetainedBytes"/>.
    /// </summary>
    /// <remarks>
    /// The limit counts what <see cref="FontProgram.RetainedByteCount"/> counts for a load: the length of the font data, and 24 bytes
    /// for the header of each array that the subsetter allocates plus the size of its elements, which includes the array
    /// that <see cref="FontSubsetter.Subset"/> returns. <see cref="FontSubsetter.Subset"/> throws <see cref="FontProgramTooLargeException"/>
    /// before an allocation that would pass the limit.
    /// </remarks>
    public FontProgramOptions? ProgramOptions { get; init; }
}
