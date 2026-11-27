// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// The embedding permissions that the <c>fsType</c> field of the <c>OS/2</c> table of a font gives.
/// </summary>
public readonly struct FontEmbeddingPermissions
{
    private const ushort RestrictedBit = 1 << 1;
    private const ushort PreviewAndPrintBit = 1 << 2;
    private const ushort EditableBit = 1 << 3;
    private const ushort NoSubsettingBit = 1 << 8;
    private const ushort BitmapEmbeddingOnlyBit = 1 << 9;

    internal FontEmbeddingPermissions(ushort fsType, bool isStated)
    {
        this.FsType = fsType;
        this.IsStated = isStated;
    }

    /// <summary>
    /// Gets a value indicating whether the font states its permissions. A font with no <c>OS/2</c> table states none.
    /// Then <see cref="Usage"/> is <see cref="FontEmbeddingUsage.Installable"/> and the other values are
    /// <see langword="false"/>.
    /// </summary>
    public bool IsStated { get; }

    /// <summary>
    /// Gets the <c>fsType</c> field as the font stores it, or 0 when the font has no <c>OS/2</c> table.
    /// </summary>
    public ushort FsType { get; }

    /// <summary>
    /// Gets the usage permission. When bits 1 to 3 give more than one, the least restrictive one applies, as the
    /// OpenType specification says: editable, then preview and print, then restricted.
    /// </summary>
    public FontEmbeddingUsage Usage
        => (this.FsType & EditableBit) != 0 ? FontEmbeddingUsage.Editable
        : (this.FsType & PreviewAndPrintBit) != 0 ? FontEmbeddingUsage.PreviewAndPrint
        : (this.FsType & RestrictedBit) != 0 ? FontEmbeddingUsage.Restricted
        : FontEmbeddingUsage.Installable;

    /// <summary>
    /// Gets a value indicating whether the font must not be subset before it is embedded. Bit 8.
    /// </summary>
    public bool NoSubsetting => (this.FsType & NoSubsettingBit) != 0;

    /// <summary>
    /// Gets a value indicating whether only the bitmaps of the font can be embedded. Bit 9.
    /// </summary>
    public bool BitmapEmbeddingOnly => (this.FsType & BitmapEmbeddingOnlyBit) != 0;
}
