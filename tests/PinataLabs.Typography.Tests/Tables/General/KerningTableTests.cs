// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.General.Kern;
using Xunit;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class KerningTableTests
    {
        [Fact]
        public void ShouldReturnDefaultValueWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                var table = KerningTable.Load(new FontReader(stream));
                Assert.NotNull(table);
            }
        }
    }
}
