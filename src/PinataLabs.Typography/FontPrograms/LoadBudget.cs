// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// Sums the bytes that a font program keeps reachable, and refuses an allocation that would pass the cap.
/// Every array that the load path allocates comes from <see cref="Allocate{T}"/>, so the count and the cap use one figure.
/// </summary>
internal struct LoadBudget
{
    /// <summary>
    /// The bytes that the header of an array adds to its elements on 64-bit CoreCLR:
    /// the object header, the method table pointer and the length.
    /// The size of an element is <c>Unsafe.SizeOf</c> on the running runtime. Another runtime may use a few bytes more or less for the header.
    /// </summary>
    public const int ArrayOverhead = 24;

    private readonly long? maxRetainedBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadBudget"/> struct and charges the font data.
    /// </summary>
    /// <param name="dataLength">The length of the font data when the program keeps it alive, else 0.</param>
    /// <param name="maxRetainedBytes">The cap, or <see langword="null"/> for none.</param>
    /// <exception cref="FontProgramTooLargeException">The data alone is larger than the cap.</exception>
    public LoadBudget(int dataLength, long? maxRetainedBytes)
    {
        this.maxRetainedBytes = maxRetainedBytes;
        this.RetainedBytes = 0;
        this.Charge(dataLength);
    }

    /// <summary>
    /// Gets the bytes charged so far.
    /// </summary>
    public long RetainedBytes { get; private set; }

    /// <summary>
    /// Allocates an array that the program keeps, after it checks the cap.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements.</param>
    /// <returns>The new array.</returns>
    /// <exception cref="FontProgramTooLargeException">The array does not fit under the cap.</exception>
    public T[] Allocate<T>(int length)
    {
        this.Charge(ArrayOverhead + ((long)length * Unsafe.SizeOf<T>()));
        return new T[length];
    }

    /// <summary>
    /// Allocates two arrays that the program keeps, after it checks the cap once for both.
    /// A refusal then carries the size of both, so a retry with that size as the cap succeeds.
    /// </summary>
    /// <typeparam name="TFirst">The element type of the first array.</typeparam>
    /// <typeparam name="TSecond">The element type of the second array.</typeparam>
    /// <param name="firstLength">The number of elements of the first array.</param>
    /// <param name="secondLength">The number of elements of the second array.</param>
    /// <returns>The new arrays.</returns>
    /// <exception cref="FontProgramTooLargeException">The arrays do not fit under the cap.</exception>
    public (TFirst[] First, TSecond[] Second) Allocate<TFirst, TSecond>(int firstLength, int secondLength)
    {
        this.Charge(ArrayOverhead + ((long)firstLength * Unsafe.SizeOf<TFirst>()) + ArrayOverhead + ((long)secondLength * Unsafe.SizeOf<TSecond>()));
        return (new TFirst[firstLength], new TSecond[secondLength]);
    }

    private void Charge(long bytes)
    {
        long total = this.RetainedBytes + bytes;
        if (total > this.maxRetainedBytes)
        {
            throw new FontProgramTooLargeException(total, this.maxRetainedBytes.GetValueOrDefault());
        }

        this.RetainedBytes = total;
    }
}
