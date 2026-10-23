// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.WellKnownIds;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class NameTableTests
    {
        [Test]
        public async Task LoadFormat0()
        {
            var writer = new BigEndianBinaryWriter();

            writer.WriteNameTable(
                new Dictionary<KnownNameIds, string>
                {
                    { KnownNameIds.CopyrightNotice, "copyright" },
                    { KnownNameIds.FullFontName, "fullname" },
                    { KnownNameIds.FontFamilyName, "family" },
                    { KnownNameIds.FontSubfamilyName, "subfamily" },
                    { KnownNameIds.UniqueFontID, "id" },
                    { (KnownNameIds)90, "other1" },
                    { (KnownNameIds)91, "other2" }
                });

            var table = NameTable.Load(writer.GetReader());

            await Assert.That(table.FontName(CultureInfo.InvariantCulture)).IsEqualTo("fullname");
            await Assert.That(table.FontFamilyName(CultureInfo.InvariantCulture)).IsEqualTo("family");
            await Assert.That(table.FontSubFamilyName(CultureInfo.InvariantCulture)).IsEqualTo("subfamily");
            await Assert.That(table.Id(CultureInfo.InvariantCulture)).IsEqualTo("id");
            await Assert.That(table.GetNameById(CultureInfo.InvariantCulture, KnownNameIds.CopyrightNotice)).IsEqualTo("copyright");
            await Assert.That(table.GetNameById(CultureInfo.InvariantCulture, 90)).IsEqualTo("other1");
            await Assert.That(table.GetNameById(CultureInfo.InvariantCulture, 91)).IsEqualTo("other2");
        }

        [Test]
        public async Task LoadFormat1()
        {
            var writer = new BigEndianBinaryWriter();

            writer.WriteNameTable(
                new Dictionary<KnownNameIds, string>
                {
                    { KnownNameIds.CopyrightNotice, "copyright" },
                    { KnownNameIds.FullFontName, "fullname" },
                    { KnownNameIds.FontFamilyName, "family" },
                    { KnownNameIds.FontSubfamilyName, "subfamily" },
                    { KnownNameIds.UniqueFontID, "id" },
                    { (KnownNameIds)90, "other1" },
                    { (KnownNameIds)91, "other2" }
                },
                new List<string>
                {
                    "lang1",
                    "lang2"
                });

            var table = NameTable.Load(writer.GetReader());

            await Assert.That(table.FontName(CultureInfo.InvariantCulture)).IsEqualTo("fullname");
            await Assert.That(table.FontFamilyName(CultureInfo.InvariantCulture)).IsEqualTo("family");
            await Assert.That(table.FontSubFamilyName(CultureInfo.InvariantCulture)).IsEqualTo("subfamily");
            await Assert.That(table.Id(CultureInfo.InvariantCulture)).IsEqualTo("id");
            await Assert.That(table.GetNameById(CultureInfo.InvariantCulture, KnownNameIds.CopyrightNotice)).IsEqualTo("copyright");
            await Assert.That(table.GetNameById(CultureInfo.InvariantCulture, 90)).IsEqualTo("other1");
            await Assert.That(table.GetNameById(CultureInfo.InvariantCulture, 91)).IsEqualTo("other2");
        }

        [Test]
        public async Task ShouldThrowExceptionWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                InvalidFontTableException exception = await Assert.That(
                    () => NameTable.Load(new FontReader(stream))).ThrowsExactly<InvalidFontTableException>();

                await Assert.That(exception.Table).IsEqualTo("name");
            }
        }
    }
}
