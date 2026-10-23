// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.WellKnownIds;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests
{
    public class FontDescriptionTests
    {
        [Test]
        public async Task LoadFontDescription()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(1, 0, 0, 0);
            writer.WriteTableHeader("name", 0, 28, 999);
            writer.WriteNameTable(
                new Dictionary<KnownNameIds, string>
                {
                    { KnownNameIds.FullFontName, "name" },
                    { KnownNameIds.FontSubfamilyName, "sub" },
                    { KnownNameIds.FontFamilyName, "fam" }
                });

            var description = FontDescription.LoadDescription(writer.GetStream());
            await Assert.That(description.FontNameInvariantCulture).IsEqualTo("name");
            await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("sub");
            await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("fam");
        }

        [Test]
        public async Task LoadFontDescription_CultureNamePriority_FirstWindows()
        {
            var usCulture = new CultureInfo(0x0409);
            var c1 = new CultureInfo(1034); // spanish - international
            var c2 = new CultureInfo(3082); // spanish - traditional

            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(1, 0, 0, 0);
            writer.WriteTableHeader("name", 0, 28, 999);
            writer.WriteNameTable(
                (KnownNameIds.FullFontName, "name_c1", c1),
                (KnownNameIds.FontSubfamilyName, "sub_c1", c1),
                (KnownNameIds.FontFamilyName, "fam_c1", c1),
                (KnownNameIds.FullFontName, "name_c2", c2),
                (KnownNameIds.FontSubfamilyName, "sub_c2", c2),
                (KnownNameIds.FontFamilyName, "fam_c2", c2));

            var description = FontDescription.LoadDescription(writer.GetStream());

            // unknown culture should prioritise US, but missing so will return first
            await Assert.That(description.FontNameInvariantCulture).IsEqualTo("name_c1");
            await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("sub_c1");
            await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("fam_c1");
        }

        [Test]
        public async Task LoadFontDescription_CultureNamePriority_US()
        {
            var usCulture = new CultureInfo(0x0409);
            var c1 = new CultureInfo(1034); // spanish - international
            var c2 = new CultureInfo(3082); // spanish - traditional

            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(1, 0, 0, 0);
            writer.WriteTableHeader("name", 0, 28, 999);
            writer.WriteNameTable(
                (KnownNameIds.FullFontName, "name_c1", c1),
                (KnownNameIds.FontSubfamilyName, "sub_c1", c1),
                (KnownNameIds.FontFamilyName, "fam_c1", c1),
                (KnownNameIds.FullFontName, "name_c2", c2),
                (KnownNameIds.FontSubfamilyName, "sub_c2", c2),
                (KnownNameIds.FontFamilyName, "fam_c2", c2),
                (KnownNameIds.FullFontName, "name_us", usCulture),
                (KnownNameIds.FontSubfamilyName, "sub_us", usCulture),
                (KnownNameIds.FontFamilyName, "fam_us", usCulture));

            var description = FontDescription.LoadDescription(writer.GetStream());

            // unknown culture should prioritise US, but missing so will return first
            await Assert.That(description.FontNameInvariantCulture).IsEqualTo("name_us");
            await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("sub_us");
            await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("fam_us");
        }

        [Test]
        public async Task LoadFontDescription_CultureNamePriority_ExactMatch()
        {
            var usCulture = new CultureInfo(0x0409);
            var c1 = new CultureInfo(1034); // spanish - international
            var c2 = new CultureInfo(3082); // spanish - traditional

            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(1, 0, 0, 0);
            writer.WriteTableHeader("name", 0, 28, 999);
            writer.WriteNameTable(
                (KnownNameIds.FullFontName, "name_c1", c1),
                (KnownNameIds.FontSubfamilyName, "sub_c1", c1),
                (KnownNameIds.FontFamilyName, "fam_c1", c1),
                (KnownNameIds.FullFontName, "name_c2", c2),
                (KnownNameIds.FontSubfamilyName, "sub_c2", c2),
                (KnownNameIds.FontFamilyName, "fam_c2", c2),
                (KnownNameIds.FullFontName, "name_us", usCulture),
                (KnownNameIds.FontSubfamilyName, "sub_us", usCulture),
                (KnownNameIds.FontFamilyName, "fam_us", usCulture));

            var description = FontDescription.LoadDescription(writer.GetStream());

            // unknown culture should prioritise US, but missing so will return first
            await Assert.That(description.FontName(c2)).IsEqualTo("name_c2");
            await Assert.That(description.FontSubFamilyName(c2)).IsEqualTo("sub_c2");
            await Assert.That(description.FontFamily(c2)).IsEqualTo("fam_c2");
        }

        [Test]
        public async Task CanLoadFontCollectionDescriptionsFromPath()
        {
            FontDescription[] descriptions = FontDescription.LoadFontCollectionDescriptions(TestFonts.SimpleTrueTypeCollection);
            await Assert.That(descriptions).IsNotNull();
            await Assert.That(descriptions.Length).IsEqualTo(2);

            FontDescription description = descriptions[0];
            await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("SixLaborsSampleAB");
            await Assert.That(description.FontNameInvariantCulture).IsEqualTo("SixLaborsSampleAB regular");
            await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
            await Assert.That(description.Style).IsEqualTo(FontStyle.Regular);

            description = descriptions[1];
            await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("Open Sans");
            await Assert.That(description.FontNameInvariantCulture).IsEqualTo("Open Sans");
            await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
            await Assert.That(description.Style).IsEqualTo(FontStyle.Regular);
        }

        [Test]
        public async Task LoadFontDescription_GetNameById()
        {
            var c1 = new CultureInfo(1034); // spanish - international
            var c2 = new CultureInfo(3082); // spanish - traditional

            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(1, 0, 0, 0);
            writer.WriteTableHeader("name", 0, 28, 999);
            writer.WriteNameTable(
                (KnownNameIds.FullFontName, "name_c1", c1),
                (KnownNameIds.FontSubfamilyName, "sub_c1", c1),
                (KnownNameIds.FontFamilyName, "fam_c1", c1),
                (KnownNameIds.FullFontName, "name_c2", c2),
                (KnownNameIds.FontSubfamilyName, "sub_c2", c2),
                (KnownNameIds.FontFamilyName, "fam_c2", c2));

            var description = FontDescription.LoadDescription(writer.GetStream());

            await Assert.That(description.GetNameById(c1, KnownNameIds.FullFontName)).IsEqualTo("name_c1");
            await Assert.That(description.GetNameById(c1, KnownNameIds.FontSubfamilyName)).IsEqualTo("sub_c1");
            await Assert.That(description.GetNameById(c1, KnownNameIds.FontFamilyName)).IsEqualTo("fam_c1");

            await Assert.That(description.GetNameById(c2, KnownNameIds.FullFontName)).IsEqualTo("name_c2");
            await Assert.That(description.GetNameById(c2, KnownNameIds.FontSubfamilyName)).IsEqualTo("sub_c2");
            await Assert.That(description.GetNameById(c2, KnownNameIds.FontFamilyName)).IsEqualTo("fam_c2");
        }

        [Test]
        public async Task ThrowsCorrectExceptionWhenDecodingCollectionFromNonTTCFiles()
        {
            await Assert.That(() => FontDescription.LoadFontCollectionDescriptions(TestFonts.TwemojiMozillaFile)).ThrowsExactly<InvalidFontTableException>();
        }
    }
}
