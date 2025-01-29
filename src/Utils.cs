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

        internal static byte[] DecompressData(byte[] data)
        {
            return new Decompressor().Unwrap(data);
        }
    }
}
