// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

public class SfntRewriterTests
{
    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    public async Task ARewriteWithNoChangeKeepsEveryTableByteForByte(string fontFile)
    {
        byte[] original = File.ReadAllBytes(TestFonts.GetPath(fontFile));

        byte[] rewritten = SfntRewriter.Rewrite(original, new System.Collections.Generic.Dictionary<string, byte[]?>());

        await Assert.That(BinaryPrimitives.ReadUInt16BigEndian(rewritten.AsSpan(4)))
            .IsEqualTo(BinaryPrimitives.ReadUInt16BigEndian(original.AsSpan(4)));
        foreach (string tag in (string[])["head", "maxp", "cmap", "post", "name", "OS/2"])
        {
            await Assert.That(SfntRewriter.GetTable(rewritten, tag)!.SequenceEqual(SfntRewriter.GetTable(original, tag)!)).IsTrue();
        }

        await Assert.That(FontProgram.Load(rewritten).GlyphCount).IsEqualTo(FontProgram.Load(original).GlyphCount);
    }

    [Test]
    public async Task RemovingTablesDropsThemFromTheDirectoryAndKeepsTheOthers()
    {
        byte[] original = File.ReadAllBytes(TestFonts.OpenSansFile);

        byte[] stripped = SfntRewriter.Remove(original, "name", "OS/2", "post");

        await Assert.That(SfntRewriter.GetTable(stripped, "name")).IsNull();
        await Assert.That(SfntRewriter.GetTable(stripped, "OS/2")).IsNull();
        await Assert.That(SfntRewriter.GetTable(stripped, "post")).IsNull();
        await Assert.That(SfntRewriter.GetTable(stripped, "glyf")!.SequenceEqual(SfntRewriter.GetTable(original, "glyf")!)).IsTrue();
        await Assert.That(stripped.Length).IsLessThan(original.Length);
    }
}
