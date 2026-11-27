// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// A run of the code units of a line that share one embedding level, and so one direction.
/// </summary>
public readonly struct BidiLevelRun : IEquatable<BidiLevelRun>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BidiLevelRun"/> struct.
    /// </summary>
    /// <param name="start">The index of the first code unit of the run, in the text.</param>
    /// <param name="length">The number of code units in the run.</param>
    /// <param name="level">The embedding level of the run.</param>
    public BidiLevelRun(int start, int length, byte level)
    {
        this.Start = start;
        this.Length = length;
        this.Level = level;
    }

    /// <summary>
    /// Gets the index of the first code unit of the run, in the text and in logical order.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of code units in the run.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the embedding level of the run. Even levels run left to right and odd levels run right to left.
    /// </summary>
    public byte Level { get; }

    /// <summary>
    /// Gets a value indicating whether the run is written right to left, which is when its level is odd.
    /// </summary>
    public bool IsRightToLeft => (this.Level & 1) != 0;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static bool operator ==(BidiLevelRun left, BidiLevelRun right) => left.Equals(right);

    public static bool operator !=(BidiLevelRun left, BidiLevelRun right) => !left.Equals(right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <inheritdoc/>
    public bool Equals(BidiLevelRun other)
        => this.Start == other.Start && this.Length == other.Length && this.Level == other.Level;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BidiLevelRun other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Start, this.Length, this.Level);

    /// <inheritdoc/>
    public override string ToString() => $"[{this.Start}, {this.Start + this.Length}) level {this.Level}";
}
