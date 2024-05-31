using System;
using System.Runtime.InteropServices;
using System.Text;

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

        internal static T GetVector<T>(JSONObject v)
        {
            return (T)Activator.CreateInstance(typeof(T), new object[] { v[0].f, v[1].f, v[2].f });
        }
    }
}
