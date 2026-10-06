using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using ZstdNet;

namespace Augmenta
{
    internal static class Utils
    {
        internal static int ReadInt(ReadOnlySpan<byte> data, int offset)
        {
            return MemoryMarshal.Cast<byte, int>(data.Slice(offset))[0];
        }

        internal static float ReadFloat(ReadOnlySpan<byte> data, int offset)
        {
            return MemoryMarshal.Cast<byte, float>(data.Slice(offset))[0];
        }

        internal static string ReadString(ReadOnlySpan<byte> data, int offset, int length)
        {
            return Encoding.UTF8.GetString(data.Slice(offset, length));
        }

        internal static ReadOnlySpan<T> ReadVectors<T>(ReadOnlySpan<byte> data, int offset, int length) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(data.Slice(offset, length));
        }

        internal static T GetVector<T>(JSONObject v) where T : struct
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { v[0].f, v[1].f, v[2].f });
        }

        internal static Color GetColor(JSONObject v)
        {
            return Color.FromArgb((int)(v[3].f * 255), (int)(v[0].f * 255), (int)(v[1].f * 255), (int)(v[2].f * 255));
        }

    }

    /// <summary>
    /// Reuses both the native Zstd decompression context and the managed
    /// destination buffer across tracking frames.
    /// </summary>
    internal sealed class ReusableDecompressor : IDisposable
    {
        private const int MinimumGrowth = 4096;

        private readonly Decompressor decompressor = new Decompressor();
        private byte[] buffer = Array.Empty<byte>();
        private bool disposed;

        internal ReadOnlySpan<byte> Unwrap(ReadOnlySpan<byte> data)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ReusableDecompressor));
            }

            ulong expectedSize = Decompressor.GetDecompressedSize(data);
            if (expectedSize > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Decompressed frame size {expectedSize} exceeds the maximum supported buffer size.");
            }

            int requiredLength = (int)expectedSize;
            EnsureCapacity(requiredLength);

            int written = decompressor.Unwrap(
                data,
                buffer.AsSpan(0, requiredLength),
                bufferSizePrecheck: false);

            return buffer.AsSpan(0, written);
        }

        private void EnsureCapacity(int requiredLength)
        {
            if (buffer.Length >= requiredLength)
            {
                return;
            }

            int nextLength = buffer.Length;
            if (nextLength == 0)
            {
                nextLength = Math.Max(requiredLength, MinimumGrowth);
            }

            while (nextLength < requiredLength)
            {
                int growth = Math.Max(nextLength / 2, MinimumGrowth);
                if (nextLength > int.MaxValue - growth)
                {
                    nextLength = requiredLength;
                    break;
                }

                nextLength += growth;
            }

            buffer = new byte[nextLength];
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            decompressor.Dispose();
            disposed = true;
        }
    }
}
