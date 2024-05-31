using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    public class PScene<T> : PContainer<T> where T : struct
    {
        public T size;
        public PScene(BasePleiadesClient client, JSONObject o, PContainer<T> parent) : base(client, o, parent, ContainerType.Scene)
        {
            size = (T)Activator.CreateInstance(typeof(T), new object[] { o["size"][0].f, o["size"][1].f, o["size"][2].f });
        }
    }
}