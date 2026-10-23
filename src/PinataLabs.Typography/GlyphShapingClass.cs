// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography
{
    internal readonly struct GlyphShapingClass
    {
        public GlyphShapingClass(bool isMark, bool isBase, bool isLigature, ushort markAttachmentType)
        {
            this.IsMark = isMark;
            this.IsBase = isBase;
            this.IsLigature = isLigature;
            this.MarkAttachmentType = markAttachmentType;
        }

        public bool IsMark { get; }

        public bool IsBase { get; }

        public bool IsLigature { get; }

        public ushort MarkAttachmentType { get; }
    }
}
