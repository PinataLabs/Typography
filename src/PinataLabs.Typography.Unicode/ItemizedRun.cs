// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// A run of text that is in one script and one direction, which is the unit that a shaper takes.
/// </summary>
public readonly struct ItemizedRun : IEquatable<ItemizedRun>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemizedRun"/> struct.
    /// </summary>
    /// <param name="start">The index of the first code unit of the run, in the text.</param>
    /// <param name="length">The number of code units in the run.</param>
    /// <param name="script">The script of the run.</param>
    /// <param name="level">The embedding level of the run.</param>
    public ItemizedRun(int start, int length, ScriptClass script, byte level)
    {
        this.Start = start;
        this.Length = length;
        this.Script = script;
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
    /// Gets the script of the run: <see cref="ScriptClass.Common"/> when the run has no letters of any script in it.
    /// </summary>
    public ScriptClass Script { get; }

    /// <summary>
    /// Gets the embedding level of the run. Even levels run left to right and odd levels run right to left.
    /// </summary>
    public byte Level { get; }

    /// <summary>
    /// Gets a value indicating whether the run is written right to left, which is when its level is odd.
    /// </summary>
    public bool IsRightToLeft => (this.Level & 1) != 0;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static bool operator ==(ItemizedRun left, ItemizedRun right) => left.Equals(right);

    public static bool operator !=(ItemizedRun left, ItemizedRun right) => !left.Equals(right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <inheritdoc/>
    public bool Equals(ItemizedRun other)
        => this.Start == other.Start && this.Length == other.Length && this.Script == other.Script && this.Level == other.Level;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ItemizedRun other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Start, this.Length, this.Script, this.Level);

    /// <inheritdoc/>
    public override string ToString() => $"[{this.Start}, {this.Start + this.Length}) {this.Script} level {this.Level}";
}
