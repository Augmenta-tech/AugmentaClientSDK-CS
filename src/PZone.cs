using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    internal class PZone<T> : PShape<T> where T : struct
    {
        public PZone(BasePleiadesClient client, JSONObject o, PContainer<T> parent) : base(client, o, parent, ContainerType.Zone)
        {
        }

       
    }

   
}