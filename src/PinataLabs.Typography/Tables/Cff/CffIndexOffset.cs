// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff;

internal readonly struct CffIndexOffset
{
    /// <summary>
    /// The starting offset
    /// </summary>
    public readonly int Start;

    /// <summary>
    /// The length
    /// </summary>
    public readonly int Length;

    public CffIndexOffset(int start, int len)
    {
        this.Start = start;
        this.Length = len;
    }

#if DEBUG
    public override string ToString() => "Start:" + this.Start + ",Length:" + this.Length;
#endif
}
