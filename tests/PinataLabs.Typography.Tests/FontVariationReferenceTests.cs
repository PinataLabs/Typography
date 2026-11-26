// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The axes, the named instances and the normalised coordinates of the variable test fonts match fontTools: the
/// reference holds the F2Dot14 coordinates that <c>normalizeLocation</c> and the <c>avar</c> segment maps give, at the
/// default, the minimum and maximum of each axis, each named instance and some user locations between them.
/// tests/tools/generate_font_variations_reference.py writes the reference.
/// </summary>
public class FontVariationReferenceTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FontTools = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.FontVariationsFontToolsReference),
        Options)!;

    [Test]
    [Arguments("Teko-Variable.ttf")]
    [Arguments("SourceSans3VF-Upright.otf")]
    [Arguments("VariationAxes.ttf")]
    [Arguments("VariationSlant.ttf")]
    public async Task NormalizedCoordinatesMatchFontTools(string fontName)
    {
        FontReference reference = FontTools.Fonts[fontName];
        FvarTable fvar = LoadFace(reference).Description.Fvar!;
        List<string> differences = [];

        foreach (Location location in reference.Locations)
        {
            double[] user = fvar.Axes.Select(axis => location.User[axis.Tag.ToString()]).ToArray();
            NormalizedCoordinates actual = fvar.Normalize(user);
            if (!actual.Values.SequenceEqual(location.Coordinates.AsSpan()))
            {
                differences.Add($"{location.Name}: [{string.Join(", ", actual.Values.ToArray())}], expected [{string.Join(", ", location.Coordinates)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [Arguments("Teko-Variable.ttf")]
    [Arguments("SourceSans3VF-Upright.otf")]
    [Arguments("VariationAxes.ttf")]
    [Arguments("VariationSlant.ttf")]
    public async Task NormalizedCoordinatesFromFontVariationsMatchFontTools(string fontName)
    {
        // A caller passes the values as floats. The values of the reference are exact in a float.
        FontReference reference = FontTools.Fonts[fontName];
        FvarTable fvar = LoadFace(reference).Description.Fvar!;
        List<string> differences = [];

        foreach (Location location in reference.Locations)
        {
            FontVariation[] variations = location.User.Select(x => new FontVariation(Tag.Parse(x.Key), (float)x.Value)).ToArray();
            double[] user = fvar.CreateUserCoordinates(FontStyle.Regular, 12F, variations);
            NormalizedCoordinates actual = fvar.Normalize(user);
            if (!actual.Values.SequenceEqual(location.Coordinates.AsSpan()))
            {
                differences.Add($"{location.Name}: [{string.Join(", ", actual.Values.ToArray())}], expected [{string.Join(", ", location.Coordinates)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [Arguments("Teko-Variable.ttf")]
    [Arguments("SourceSans3VF-Upright.otf")]
    [Arguments("VariationAxes.ttf")]
    [Arguments("VariationSlant.ttf")]
    public async Task AxesMatchFontTools(string fontName)
    {
        FontReference reference = FontTools.Fonts[fontName];
        IReadOnlyList<FontVariationAxis> axes = LoadFace(reference).VariationAxes;

        await Assert.That(axes.Count).IsEqualTo(reference.Axes.Length);
        for (int i = 0; i < axes.Count; i++)
        {
            AxisReference expected = reference.Axes[i];
            await Assert.That(axes[i].Tag).IsEqualTo(Tag.Parse(expected.Tag));
            await Assert.That(axes[i].MinValue).IsEqualTo(expected.Min);
            await Assert.That(axes[i].DefaultValue).IsEqualTo(expected.Default);
            await Assert.That(axes[i].MaxValue).IsEqualTo(expected.Max);
            await Assert.That(axes[i].IsHidden).IsEqualTo(expected.Hidden);
            await Assert.That(axes[i].Name(CultureInfo.InvariantCulture)).IsEqualTo(expected.Name);
        }
    }

    [Test]
    [Arguments("Teko-Variable.ttf")]
    [Arguments("SourceSans3VF-Upright.otf")]
    [Arguments("VariationAxes.ttf")]
    [Arguments("VariationSlant.ttf")]
    public async Task NamedInstancesMatchFontTools(string fontName)
    {
        FontReference reference = FontTools.Fonts[fontName];
        FontMetrics face = LoadFace(reference);

        await Assert.That(face.NamedInstances.Count).IsEqualTo(reference.Instances.Length);
        for (int i = 0; i < reference.Instances.Length; i++)
        {
            FontNamedInstance instance = face.NamedInstances[i];
            InstanceReference expected = reference.Instances[i];

            await Assert.That(instance.Name(CultureInfo.InvariantCulture)).IsEqualTo(expected.Name);
            await Assert.That(instance.Variations.Select(x => x.Axis.ToString())).IsEquivalentTo(face.VariationAxes.Select(x => x.Tag.ToString()), TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(instance.Variations.Select(x => (double)x.Value)).IsEquivalentTo(face.VariationAxes.Select(x => expected.Coordinates[x.Tag.ToString()]), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Arguments("Teko-Variable.ttf")]
    [Arguments("SourceSans3VF-Upright.otf")]
    public async Task TheReferenceHasTheMinimumEachNamedInstanceAndTheMaximum(string fontName)
    {
        // Both variable fonts of the test project have one axis.
        FontReference reference = FontTools.Fonts[fontName];
        string[] names = reference.Locations.Select(x => x.Name).ToArray();
        AxisReference axis = reference.Axes.Single();

        short minimum = axis.Min == axis.Default ? (short)0 : (short)-16384;

        await Assert.That(reference.Locations.Any(x => x.Name == "Minimum" && x.User[axis.Tag] == axis.Min && x.Coordinates[0] == minimum)).IsTrue();
        await Assert.That(reference.Locations.Any(x => x.Name == "Maximum" && x.User[axis.Tag] == axis.Max && x.Coordinates[0] == 16384)).IsTrue();
        foreach (InstanceReference instance in reference.Instances)
        {
            await Assert.That(names).Contains(instance.Name);
        }
    }

    private static FontMetrics LoadFace(FontReference reference)
        => new FontCollection().Add(TestFonts.GetPath(reference.File)).Faces[0];

    private sealed class Reference
    {
        public string FontTools { get; init; } = string.Empty;

        public Dictionary<string, FontReference> Fonts { get; init; } = [];
    }

    private sealed class FontReference
    {
        public string File { get; init; } = string.Empty;

        public AxisReference[] Axes { get; init; } = [];

        public InstanceReference[] Instances { get; init; } = [];

        public Location[] Locations { get; init; } = [];
    }

    private sealed class AxisReference
    {
        public string Tag { get; init; } = string.Empty;

        public float Min { get; init; }

        public float Default { get; init; }

        public float Max { get; init; }

        public bool Hidden { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class InstanceReference
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, double> Coordinates { get; init; } = [];
    }

    private sealed class Location
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, double> User { get; init; } = [];

        public short[] Coordinates { get; init; } = [];
    }
}
