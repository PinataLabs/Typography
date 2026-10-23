// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables;

namespace PinataLabs.Typography.Tests.Tables;

public class TableHeaderTests
{
    public static System.Collections.Generic.IEnumerable<(string Tag, uint Checksum, uint Offset, uint Length)> ReadAllValuesData { get; } =
        [
            ("TAG1", 98, 18, 1218),
            ("TAG2", 198, 0, 121),
        ];

    [Test]
    [MethodDataSource(nameof(ReadAllValuesData))]
    public async Task ReadAllValues(string tag, uint checksum, uint offset, uint length)
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteTableHeader(tag, checksum, offset, length);

        var header = TableHeader.Read(writer.GetReader());

        await Assert.That(header.CheckSum).IsEqualTo(checksum);
        await Assert.That(header.Length).IsEqualTo(length);
        await Assert.That(header.Offset).IsEqualTo(offset);
        await Assert.That(header.Tag).IsEqualTo(tag);
    }
}
