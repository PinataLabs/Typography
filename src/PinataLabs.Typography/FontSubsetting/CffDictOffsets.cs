// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// The new values of the operators that hold an offset into the CFF table.
/// </summary>
internal readonly struct CffDictOffsets
{
    /// <summary>Gets the operand of Charset: an offset, or 0, 1 or 2 for a predefined charset.</summary>
    public int Charset { get; init; }

    /// <summary>Gets the operand of Encoding: an offset, or 0 or 1 for a predefined encoding.</summary>
    public int Encoding { get; init; }

    /// <summary>Gets the offset of the CharStrings INDEX.</summary>
    public int CharStrings { get; init; }

    /// <summary>Gets the size of the Private DICT.</summary>
    public int PrivateSize { get; init; }

    /// <summary>Gets the offset of the Private DICT.</summary>
    public int PrivateOffset { get; init; }

    /// <summary>Gets the offset of the FDArray INDEX, or 0 for none.</summary>
    public int FdArray { get; init; }

    /// <summary>Gets the offset of the FDSelect, or 0 for none.</summary>
    public int FdSelect { get; init; }

    /// <summary>Gets the offset of the local Subrs INDEX from the start of the Private DICT.</summary>
    public int Subrs { get; init; }
}
