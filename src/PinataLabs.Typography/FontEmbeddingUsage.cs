// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// The usage permission that bits 1 to 3 of the <c>fsType</c> field of the <c>OS/2</c> table give.
/// </summary>
public enum FontEmbeddingUsage
{
    /// <summary>
    /// The font can be embedded and installed on the remote system. None of the bits 1 to 3 is set.
    /// </summary>
    Installable = 0,

    /// <summary>
    /// The font must not be embedded without the permission of its owner. Bit 1.
    /// </summary>
    Restricted = 1,

    /// <summary>
    /// The font can be embedded to view and print a document, and the document must be read-only. Bit 2.
    /// </summary>
    PreviewAndPrint = 2,

    /// <summary>
    /// The font can be embedded to view, print and edit a document. Bit 3.
    /// </summary>
    Editable = 3,
}
