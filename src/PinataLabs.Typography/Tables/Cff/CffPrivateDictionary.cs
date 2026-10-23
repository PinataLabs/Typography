// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff
{
    internal class CffPrivateDictionary
    {
        public CffPrivateDictionary(byte[][] localSubrRawBuffers, int defaultWidthX, int nominalWidthX)
        {
            this.LocalSubrRawBuffers = localSubrRawBuffers;
            this.DefaultWidthX = defaultWidthX;
            this.NominalWidthX = nominalWidthX;
        }

        public byte[][] LocalSubrRawBuffers { get; set; }

        public int DefaultWidthX { get; set; }

        public int NominalWidthX { get; set; }
    }
}
