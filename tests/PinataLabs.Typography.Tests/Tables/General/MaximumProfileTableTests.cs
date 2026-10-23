// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using PinataLabs.Typography.Tables.General;
using Xunit;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class MaximumProfileTableTests
    {
        [Test]
        public void ShouldThrowExceptionWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (MemoryStream stream = writer.GetStream())
            {
                InvalidFontTableException exception = Assert.Throws<InvalidFontTableException>(
                    () => MaximumProfileTable.Load(new FontReader(stream)));

                Assert.Equal("maxp", exception.Table);
            }
        }
    }
}
