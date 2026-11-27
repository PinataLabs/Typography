// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// A table that is built when the file is written, so the file holds it once.
/// </summary>
internal interface ISubsetTable
{
    /// <summary>
    /// Gets the length of the table in bytes, before padding.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Writes the table.
    /// </summary>
    /// <param name="destination">The destination, which is exactly <see cref="Length"/> bytes long and holds zeros.</param>
    void Write(Span<byte> destination);
}
