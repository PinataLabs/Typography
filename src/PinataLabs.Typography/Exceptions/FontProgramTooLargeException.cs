// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// The exception that <see cref="FontProgram.Load(System.ReadOnlyMemory{byte}, FontProgramOptions)"/> throws when
/// the font program would retain more than <see cref="FontProgramOptions.MaxRetainedBytes"/>.
/// The font is well formed as far as the load went. Nothing was parsed or allocated past the point of refusal.
/// </summary>
public sealed class FontProgramTooLargeException : InvalidFontFileException
{
    internal FontProgramTooLargeException(long retainedByteCount, long maxRetainedBytes)
        : base($"The font program would retain at least {retainedByteCount} bytes, and the limit is {maxRetainedBytes} bytes.")
    {
        this.RetainedByteCount = retainedByteCount;
        this.MaxRetainedBytes = maxRetainedBytes;
    }

    /// <summary>
    /// Gets the bytes that the font program would have retained at the allocation that was refused.
    /// This is a lower bound of its full size: the load stops there.
    /// A font that is larger than the limit by its data alone reports the length of its data.
    /// </summary>
    public long RetainedByteCount { get; }

    /// <summary>
    /// Gets the limit that was set.
    /// </summary>
    public long MaxRetainedBytes { get; }
}
