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
            size = Utils.GetVector<T>(o["size"]);
        }

        protected override void handleParamUpdateInternal(string prop, JSONObject data)
        {
            base.handleParamUpdateInternal(prop, data);
            if (prop == "size") size = Utils.GetVector<T>(data);
        }
    }
}