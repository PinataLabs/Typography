// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// Options for <see cref="FontProgram.Load(ReadOnlyMemory{byte}, FontProgramOptions)"/>.
/// </summary>
public sealed class FontProgramOptions
{
    private readonly long? maxRetainedBytes;

    /// <summary>
    /// Gets the most bytes that the font program may retain, as <see cref="FontProgram.RetainedByteCount"/> counts them,
    /// or <see langword="null"/> for no limit. <see cref="FontProgram.Load(ReadOnlyMemory{byte}, FontProgramOptions)"/>
    /// throws <see cref="FontProgramTooLargeException"/> instead of retaining more.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long? MaxRetainedBytes
    {
        get => this.maxRetainedBytes;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.GetValueOrDefault(), nameof(value));
            this.maxRetainedBytes = value;
        }
    }
}
