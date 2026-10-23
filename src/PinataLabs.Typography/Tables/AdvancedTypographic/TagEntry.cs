// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace PinataLabs.Typography.Tables.AdvancedTypographic
{
    [DebuggerDisplay("Tag: {Tag}, Enabled: {Enabled}")]
    internal struct TagEntry
    {
        public TagEntry(Tag tag, bool enabled)
        {
            this.Tag = tag;
            this.Enabled = enabled;
        }

        public bool Enabled { get; set; }

        public Tag Tag { get; }
    }
}
