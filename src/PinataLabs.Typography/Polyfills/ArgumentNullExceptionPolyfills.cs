// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace System;

/// <summary>
/// Provides <see cref="ArgumentNullException"/> members that the netstandard2.1 BCL lacks.
/// </summary>
internal static class ArgumentNullExceptionPolyfills
{
    extension(ArgumentNullException)
    {
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
