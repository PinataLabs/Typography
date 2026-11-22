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
    /// Gets the bytes that the font program would have retained.
    /// A load with <see cref="MaxRetainedBytes"/> set to this value succeeds, except in two cases.
    /// A TrueType or CFF font whose data alone is larger than the limit is refused before it is read, so this is the
    /// length of its data. For a CFF font, which also retains arrays, that is a lower bound of the full size.
    /// A Type 1 font whose decrypted private part alone is larger than the limit is refused before it is decrypted,
    /// so this is the size of that part, a lower bound of the full size.
    /// </summary>
    public long RetainedByteCount { get; }

    /// <summary>
    /// Gets the limit that was set.
    /// </summary>
    public long MaxRetainedBytes { get; }
}
