// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Learns what a charstring uses while the evaluation engine runs it.
/// </summary>
internal interface ICffCallObserver
{
    /// <summary>
    /// Called when a charstring or a subroutine calls a subroutine that exists, before the subroutine runs.
    /// </summary>
    /// <param name="isGlobal"><see langword="true"/> for a global subroutine, <see langword="false"/> for a local one.</param>
    /// <param name="index">The position of the subroutine in its INDEX, after the bias is added.</param>
    void SubrCalled(bool isGlobal, int index);

    /// <summary>
    /// Called when a seac endchar draws a glyph, once for the base and once for the accent, before either runs.
    /// </summary>
    /// <param name="glyphId">The glyph ID of the component.</param>
    void SeacComponentUsed(int glyphId);
}
