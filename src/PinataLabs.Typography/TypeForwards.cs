// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using PinataLabs.Typography.Unicode;

// These types moved to PinataLabs.Typography.Unicode. The forwards let code compiled against an earlier
// PinataLabs.Typography, which defined them, load them from the new assembly.
//
// The public API analyzer counts a forwarded type and each of its members as part of this assembly's API
// and asks for a "(forwarded, contained in ...)" line for every one of them. That would copy the Unicode API
// baseline into this project, and every change to it would then be made twice. PublicAPI.Shipped.txt and
// PublicAPI.Unshipped.txt in PinataLabs.Typography.Unicode are the baseline for these types.
#pragma warning disable RS0016
[assembly: TypeForwardedTo(typeof(ArabicJoiningClass))]
[assembly: TypeForwardedTo(typeof(ArabicJoiningGroup))]
[assembly: TypeForwardedTo(typeof(ArabicJoiningType))]
[assembly: TypeForwardedTo(typeof(BidiCharacterType))]
[assembly: TypeForwardedTo(typeof(BidiClass))]
[assembly: TypeForwardedTo(typeof(BidiPairedBracketType))]
[assembly: TypeForwardedTo(typeof(CodePoint))]
[assembly: TypeForwardedTo(typeof(GraphemeClusterClass))]
[assembly: TypeForwardedTo(typeof(IndicPositionalCategory))]
[assembly: TypeForwardedTo(typeof(IndicSyllabicCategory))]
[assembly: TypeForwardedTo(typeof(LineBreakClass))]
[assembly: TypeForwardedTo(typeof(MemoryExtensions))]
[assembly: TypeForwardedTo(typeof(ScriptClass))]
[assembly: TypeForwardedTo(typeof(SpanCodePointEnumerator))]
[assembly: TypeForwardedTo(typeof(SpanGraphemeEnumerator))]
[assembly: TypeForwardedTo(typeof(VerticalOrientationType))]
#pragma warning restore RS0016
