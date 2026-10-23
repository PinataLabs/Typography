// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Woff
{
    internal sealed class Woff2TableHeader : TableHeader
    {
        public Woff2TableHeader(string tag, uint checkSum, uint offset, uint len)
            : base(tag, checkSum, offset, len)
        {
        }
    }
}
