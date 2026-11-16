// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Runtime.InteropServices;

namespace System.IO.Compression
{
    /// <summary>
    /// Provides methods and static methods to decode data compressed in the GZip data format in a streamless, non-allocating, and performant manner.
    /// </summary>
    public sealed class GZipDecoder : IDisposable
    {
        private readonly DeflateDecoder _deflateDecoder;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GZipDecoder"/> class.
        /// </summary>
        /// <exception cref="IOException">Failed to create the <see cref="GZipDecoder"/> instance.</exception>
        public GZipDecoder()
        {
            _deflateDecoder = new DeflateDecoder(ZLibNative.GZip_DefaultWindowBits);
        }

        /// <summary>
        /// Frees and disposes unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _deflateDecoder.Dispose();
        }

        private void EnsureNotDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        /// <summary>
        /// Decompresses a read-only byte span into a destination span.
        /// </summary>
        /// <param name="source">A read-only span of bytes containing the compressed source data.</param>
        /// <param name="destination">When this method returns, a byte span where the decompressed data is stored.</param>
        /// <param name="bytesConsumed">When this method returns, the total number of bytes that were read from <paramref name="source"/>.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination"/>.</param>
        /// <returns>One of the enumeration values that describes the status with which the span-based operation finished.</returns>
        public OperationStatus Decompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesConsumed, out int bytesWritten)
        {
            EnsureNotDisposed();
            return _deflateDecoder.Decompress(source, destination, out bytesConsumed, out bytesWritten);
        }

        /// <summary>
        /// Resets the decoder to its initial state so the same instance can be reused for a new, independent decompression operation.
        /// </summary>
        /// <remarks>
        /// After this method returns, any sliding-window history from a previous decompression is discarded.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The decoder has been disposed.</exception>
        public void Reset()
        {
            EnsureNotDisposed();
            _deflateDecoder.Reset();
        }

        /// <summary>
        /// Tries to decompress a source byte span into a destination span.
        /// </summary>
        /// <param name="source">A read-only span of bytes containing the compressed source data.</param>
        /// <param name="destination">When this method returns, a span of bytes where the decompressed data is stored.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination"/>.</param>
        /// <returns><see langword="true"/> if the decompression operation was successful; <see langword="false"/> otherwise.</returns>
        public static unsafe bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten)
        {
            fixed (byte* input = &MemoryMarshal.GetReference(source))
            fixed (byte* output = &MemoryMarshal.GetReference(destination))
            {
                ZLibNative.ZStream stream = new()
                {
                    nextIn = (IntPtr)input,
                    availIn = (uint)source.Length,
                    nextOut = (IntPtr)output,
                    availOut = (uint)destination.Length,
                };
                ZLibNative.ErrorCode result;
                try
                {
                    result = Interop.ZLib.InflateOnce(&stream, ZLibNative.GZip_DefaultWindowBits);
                }
                catch (Exception e)
                {
                    throw new ZLibException(SR.ZLibErrorDLLLoadError, e);
                }

                if (result is not (ZLibNative.ErrorCode.Ok or ZLibNative.ErrorCode.StreamEnd))
                {
                    ZLibNative.ThrowInitializationError(result, "inflateInit2_", stream.msg);
                }

                bool success = result == ZLibNative.ErrorCode.StreamEnd && stream.availIn == 0;
                bytesWritten = success ? destination.Length - (int)stream.availOut : 0;
                return success;
            }
        }
    }
}
