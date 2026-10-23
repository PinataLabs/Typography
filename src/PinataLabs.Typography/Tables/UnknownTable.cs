// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables
{
    internal sealed class UnknownTable : Table
    {
        internal UnknownTable(string name)
            => this.Name = name;

        public string Name { get; }
    }
}
