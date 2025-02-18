using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    public class Scene<TVector3> : Container<TVector3> where TVector3 : struct
    {
        public TVector3 size;
        public Scene(BaseClient client, JSONObject o, Container<TVector3> parent) : base(client, o, parent, ContainerType.Scene)
        {
            size = Utils.GetVector<TVector3>(o["size"]);
        }

        protected override void HandleParamUpdateInternal(string prop, JSONObject data)
        {
            base.HandleParamUpdateInternal(prop, data);
            if (prop == "size") size = Utils.GetVector<TVector3>(data);
        }
    }
}