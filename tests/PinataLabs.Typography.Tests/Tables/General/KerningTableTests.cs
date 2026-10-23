// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.Kern;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class KerningTableTests
    {
        [Test]
        public async Task ShouldReturnDefaultValueWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                var table = KerningTable.Load(new FontReader(stream));
                await Assert.That(table).IsNotNull();
            }
        }
    }
}
