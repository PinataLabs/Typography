// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// The kinds of DICT that hold an offset into the CFF table.
/// </summary>
internal enum CffDictKind
{
    /// <summary>The Top DICT.</summary>
    Top,

    /// <summary>A Font DICT of an FDArray.</summary>
    FontDict,

    /// <summary>A Private DICT.</summary>
    Private,
}
