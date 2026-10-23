// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables;

using Xunit;

namespace PinataLabs.Typography.Tests.Tables
{
    public class TableHeaderTests
    {
        public static System.Collections.Generic.IEnumerable<(string Tag, uint Checksum, uint Offset, uint Length)> ReadAllValuesData { get; } =
            [
                ("TAG1", 98, 18, 1218),
                ("TAG2", 198, 0, 121),
            ];

        [Test]
        [MethodDataSource(nameof(ReadAllValuesData))]
        public void ReadAllValues(string tag, uint checksum, uint offset, uint length)
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTableHeader(tag, checksum, offset, length);

            var header = TableHeader.Read(writer.GetReader());

            Assert.Equal(checksum, header.CheckSum);
            Assert.Equal(length, header.Length);
            Assert.Equal(offset, header.Offset);
            Assert.Equal(tag, header.Tag);
        }
    }
}
