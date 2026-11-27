// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The descriptor metrics and the embedding permissions of every face of the test fonts match fontTools.
/// tests/tools/generate_font_descriptor_reference.py writes the reference.
/// </summary>
public class FontDescriptorMetricsReferenceTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FontTools = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.FontDescriptorFontToolsReference),
        Options)!;

    [Test]
    public async Task DescriptorMetricsMatchFontTools()
    {
        List<string> differences = [];
        foreach ((string key, FaceReference expected) in FontTools.Faces)
        {
            FontMetrics face;
            try
            {
                face = LoadFace(key);
            }
            catch (Exception ex)
            {
                differences.Add($"{key}: does not load: {ex.Message}");
                continue;
            }

            FontDescriptorMetrics actual = face.DescriptorMetrics;
            Compare(differences, key, "unitsPerEm", face.UnitsPerEm, expected.UnitsPerEm);
            Compare(differences, key, "ascent", actual.Ascent, expected.Ascent);
            Compare(differences, key, "ascentSource", actual.AscentSource.ToString(), expected.AscentSource);
            Compare(differences, key, "descent", actual.Descent, expected.Descent);
            Compare(differences, key, "descentSource", actual.DescentSource.ToString(), expected.DescentSource);
            Compare(differences, key, "capHeight", actual.CapHeight, expected.CapHeight);
            Compare(differences, key, "capHeightSource", actual.CapHeightSource.ToString(), expected.CapHeightSource);
            Compare(differences, key, "xHeight", actual.XHeight, expected.XHeight);
            Compare(differences, key, "xHeightSource", actual.XHeightSource.ToString(), expected.XHeightSource);
            Compare(differences, key, "italicAngle", actual.ItalicAngle, (float)expected.ItalicAngle);
            Compare(differences, key, "italicAngleSource", actual.ItalicAngleSource.ToString(), expected.ItalicAngleSource);
            Compare(
                differences,
                key,
                "boundingBox",
                $"{actual.BoundingBox.XMin},{actual.BoundingBox.YMin},{actual.BoundingBox.XMax},{actual.BoundingBox.YMax}",
                string.Join(",", expected.BoundingBox));
            Compare(differences, key, "boundingBoxSource", actual.BoundingBoxSource.ToString(), expected.BoundingBoxSource);
            Compare(differences, key, "flags", (int)actual.Flags, expected.Flags);
        }

        await Assert.That(FontTools.Faces.Count).IsGreaterThan(100);
        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task DescriptorMetricsOfAFontOfACollectionMatchTheMetricsOfItsFile()
    {
        // FileFontMetrics forwards to the metrics that it loads from the file.
        List<string> differences = [];
        foreach (string key in new[] { "FontDescriptor/Win.ttf#0", "FontDescriptor/SymbolFormat6.ttf#0", "FontDescriptor/PanoseSerif.ttf#0" })
        {
            string path = Path.Combine(Path.GetDirectoryName(TestFonts.FontDescriptorFontToolsReference)!, key[..key.LastIndexOf('#')]);
            FontMetrics viaFont = new FontCollection().Add(path).CreateFont(12).FontMetrics;
            FontMetrics direct = LoadFace(key);

            FontDescriptorMetrics a = viaFont.DescriptorMetrics;
            FontDescriptorMetrics b = direct.DescriptorMetrics;
            Compare(differences, key, "ascent", a.Ascent, b.Ascent);
            Compare(differences, key, "descent", a.Descent, b.Descent);
            Compare(differences, key, "capHeight", a.CapHeight, b.CapHeight);
            Compare(differences, key, "xHeight", a.XHeight, b.XHeight);
            Compare(differences, key, "embedding", viaFont.EmbeddingPermissions.FsType, direct.EmbeddingPermissions.FsType);
            Compare(differences, key, "flags", (int)viaFont.DescriptorMetrics.Flags, FontTools.Faces[key].Flags);
            Compare(differences, key, "fsType", viaFont.EmbeddingPermissions.FsType, FontTools.Faces[key].Embedding.FsType);
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task DescriptorMetricsOfVariationInstancesMatchFontTools()
    {
        // MVAR varies cpht and xhgt of Teko-Variable and xhgt of SourceSans3VF-Upright, which the instancer of fontTools adds.
        List<string> differences = [];
        bool varied = false;
        foreach ((string key, List<InstanceReference> instances) in FontTools.Instances)
        {
            FontFamily family = new FontCollection().Add(Path.Combine(Path.GetDirectoryName(TestFonts.FontDescriptorFontToolsReference)!, key));
            foreach (InstanceReference expected in instances)
            {
                FontVariation[] variations = [.. expected.Location.Select(axis => new FontVariation(Tag.Parse(axis.Key), (float)axis.Value))];
                FontDescriptorMetrics actual = family.CreateFont(family.Faces[0].UnitsPerEm, default, variations).FontMetrics.DescriptorMetrics;
                string name = $"{key} {string.Join(",", expected.Location.Select(axis => $"{axis.Key}={axis.Value}"))}";

                Compare(differences, name, "ascent", actual.Ascent, expected.Ascent);
                Compare(differences, name, "descent", actual.Descent, expected.Descent);
                Compare(differences, name, "capHeight", actual.CapHeight, expected.CapHeight);
                Compare(differences, name, "capHeightSource", actual.CapHeightSource, MetricSource.Os2CapHeight);
                Compare(differences, name, "xHeight", actual.XHeight, expected.XHeight);
                Compare(differences, name, "xHeightSource", actual.XHeightSource, MetricSource.Os2XHeight);
                varied |= expected.CapHeight != instances[0].CapHeight || expected.XHeight != instances[0].XHeight;
            }
        }

        // A reference whose instances all give the default values would test nothing.
        await Assert.That(varied).IsTrue();
        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task EmbeddingPermissionsMatchFontTools()
    {
        List<string> differences = [];
        foreach ((string key, FaceReference expected) in FontTools.Faces)
        {
            FontEmbeddingPermissions actual = LoadFace(key).EmbeddingPermissions;
            Compare(differences, key, "isStated", actual.IsStated, expected.Embedding.IsStated);
            Compare(differences, key, "fsType", actual.FsType, expected.Embedding.FsType);
            Compare(differences, key, "usage", actual.Usage.ToString(), expected.Embedding.Usage);
            Compare(differences, key, "noSubsetting", actual.NoSubsetting, expected.Embedding.NoSubsetting);
            Compare(differences, key, "bitmapEmbeddingOnly", actual.BitmapEmbeddingOnly, expected.Embedding.BitmapEmbeddingOnly);
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [Arguments(0x0000, FontEmbeddingUsage.Installable)]
    [Arguments(0x0002, FontEmbeddingUsage.Restricted)]
    [Arguments(0x0004, FontEmbeddingUsage.PreviewAndPrint)]
    [Arguments(0x0008, FontEmbeddingUsage.Editable)]
    [Arguments(0x0006, FontEmbeddingUsage.PreviewAndPrint)]
    [Arguments(0x000A, FontEmbeddingUsage.Editable)]
    [Arguments(0x000E, FontEmbeddingUsage.Editable)]
    [Arguments(0x0001, FontEmbeddingUsage.Installable)]
    public async Task UsageIsTheLeastRestrictiveBitSet(int fsType, FontEmbeddingUsage usage)
    {
        FontEmbeddingPermissions permissions = new((ushort)fsType, true);

        await Assert.That(permissions.Usage).IsEqualTo(usage);
    }

    [Test]
    public async Task NoSubsettingAndBitmapOnlyAreBits8And9()
    {
        FontEmbeddingPermissions both = new(0x0300, true);
        FontEmbeddingPermissions neither = new(0x00FE, true);

        await Assert.That(both.NoSubsetting).IsTrue();
        await Assert.That(both.BitmapEmbeddingOnly).IsTrue();
        await Assert.That(neither.NoSubsetting).IsFalse();
        await Assert.That(neither.BitmapEmbeddingOnly).IsFalse();
    }

    [Test]
    [Arguments(FontDescriptorFlags.FixedPitch, 1)]
    [Arguments(FontDescriptorFlags.Serif, 2)]
    [Arguments(FontDescriptorFlags.Symbolic, 4)]
    [Arguments(FontDescriptorFlags.Script, 8)]
    [Arguments(FontDescriptorFlags.Nonsymbolic, 32)]
    [Arguments(FontDescriptorFlags.Italic, 64)]
    [Arguments(FontDescriptorFlags.AllCap, 1 << 16)]
    [Arguments(FontDescriptorFlags.SmallCap, 1 << 17)]
    [Arguments(FontDescriptorFlags.ForceBold, 1 << 18)]
    public async Task FlagsUseThePdfBitPositions(FontDescriptorFlags flag, int bit)
    {
        await Assert.That((int)flag).IsEqualTo(bit);
    }

    private static void Compare<T>(List<string> differences, string key, string name, T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            differences.Add($"{key} {name}: {actual}, expected {expected}");
        }
    }

    private static FontMetrics LoadFace(string key)
    {
        int separator = key.LastIndexOf('#');
        string path = Path.Combine(Path.GetDirectoryName(TestFonts.FontDescriptorFontToolsReference)!, key[..separator]);
        int index = int.Parse(key[(separator + 1)..], System.Globalization.CultureInfo.InvariantCulture);

        return path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
            ? StreamFontMetrics.LoadFontCollection(path)[index]
            : StreamFontMetrics.LoadFont(path);
    }

    private sealed class Reference
    {
        public Dictionary<string, FaceReference> Faces { get; set; } = [];

        public Dictionary<string, List<InstanceReference>> Instances { get; set; } = [];
    }

    private sealed class InstanceReference
    {
        public Dictionary<string, double> Location { get; set; } = [];

        public short Ascent { get; set; }

        public short Descent { get; set; }

        public short CapHeight { get; set; }

        public short XHeight { get; set; }
    }

    private sealed class FaceReference
    {
        public ushort UnitsPerEm { get; set; }

        public short Ascent { get; set; }

        public string AscentSource { get; set; } = string.Empty;

        public short Descent { get; set; }

        public string DescentSource { get; set; } = string.Empty;

        public short CapHeight { get; set; }

        public string CapHeightSource { get; set; } = string.Empty;

        public short XHeight { get; set; }

        public string XHeightSource { get; set; } = string.Empty;

        public double ItalicAngle { get; set; }

        public string ItalicAngleSource { get; set; } = string.Empty;

        public short[] BoundingBox { get; set; } = [];

        public string BoundingBoxSource { get; set; } = string.Empty;

        public int Flags { get; set; }

        public EmbeddingReference Embedding { get; set; } = new();
    }

    private sealed class EmbeddingReference
    {
        public bool IsStated { get; set; }

        public ushort FsType { get; set; }

        public string Usage { get; set; } = string.Empty;

        public bool NoSubsetting { get; set; }

        public bool BitmapEmbeddingOnly { get; set; }
    }
}
