// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.Post;
using PinataLabs.Typography.Tests.FontPrograms;

namespace PinataLabs.Typography.Tests.Tables.General;

public class PostTableTests
{
    // The font uses the strings of its format 2 table out of order, leaves one unused,
    // and names two glyphs with the same string. The names come from fontTools.
    [Test]
    public async Task Format2NamesAreSelectedByIndex()
    {
        FontProgramFixture fixture = FontProgramFixture.Load("tt-names");
        using FileStream stream = File.OpenRead(fixture.FontPath);

        PostTable post = new FontReader(stream).GetTable<PostTable>();

        await Assert.That(string.Join(' ', post.PostRecords.Select(x => x.Name)))
            .IsEqualTo(string.Join(' ', fixture.Glyphs.Select(x => x.Name)));
    }
}
