// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class MaximumProfileTableTests
    {
        [Test]
        public async Task ShouldThrowExceptionWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (MemoryStream stream = writer.GetStream())
            {
                InvalidFontTableException exception = await Assert.That(
                    () => MaximumProfileTable.Load(new FontReader(stream))).ThrowsExactly<InvalidFontTableException>();

                await Assert.That(exception.Table).IsEqualTo("maxp");
            }
        }
    }
}
