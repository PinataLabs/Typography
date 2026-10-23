// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.General.Post;

internal class PostNameRecord
{
    internal PostNameRecord(ushort nameIndex, string name)
    {
        this.Name = name;
        this.NameIndex = nameIndex;
    }

    public ushort NameIndex { get; }

    public string Name { get; }
}
