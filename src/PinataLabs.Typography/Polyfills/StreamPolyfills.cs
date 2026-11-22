// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

// StyleCop reads the extension receiver as an instance member and asks for 'this.', which does not compile here.
#pragma warning disable SA1101
namespace System.IO;

/// <summary>
/// Provides <see cref="Stream"/> members that the netstandard2.1 BCL lacks.
/// </summary>
internal static class StreamPolyfills
{
    extension(Stream stream)
    {
        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from the stream into <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="offset">The index in <paramref name="buffer"/> at which to start storing bytes.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <exception cref="EndOfStreamException">The end of the stream was reached before <paramref name="count"/> bytes were read.</exception>
        public void ReadExactly(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Span<byte> span = buffer.AsSpan(offset, count);
            if (stream.ReadAtLeast(span, span.Length) < span.Length)
            {
                throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Reads at least <paramref name="minimumBytes"/> bytes from the stream into <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="minimumBytes">The minimum number of bytes to read.</param>
        /// <param name="throwOnEndOfStream">
        /// True to throw if the end of the stream is reached before <paramref name="minimumBytes"/> bytes are read;
        /// false to return the number of bytes read.
        /// </param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        /// <exception cref="EndOfStreamException">
        /// <paramref name="throwOnEndOfStream"/> is true and the end of the stream was reached early.
        /// </exception>
        public int ReadAtLeast(Span<byte> buffer, int minimumBytes, bool throwOnEndOfStream = true)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(minimumBytes);
            if (buffer.Length < minimumBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumBytes), "Argument must be less than or equal to the buffer length.");
            }

            int totalRead = 0;
            while (totalRead < minimumBytes)
            {
                int read = stream.Read(buffer.Slice(totalRead));
                if (read == 0)
                {
                    if (throwOnEndOfStream)
                    {
                        throw new EndOfStreamException();
                    }

                    return totalRead;
                }

                totalRead += read;
            }

            return totalRead;
        }
    }
}
