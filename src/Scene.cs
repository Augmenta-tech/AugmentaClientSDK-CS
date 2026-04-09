using System.Collections.Generic;

namespace Augmenta
{
    public class Scene<TVector3> : Container<TVector3> where TVector3 : struct
    {
        public TVector3 size;

        public List<GenericObject<TVector3>> objects = new List<GenericObject<TVector3>>(); // Objects part of this container. 

        public delegate void OnObjectEnteredEvent(GenericObject<TVector3> obj);
        public event OnObjectEnteredEvent onObjectEntered;

        public delegate void OnObjectExitedEvent(GenericObject<TVector3> obj);
        public event OnObjectExitedEvent onObjectExited;

        public Scene(Client<TVector3> client, JSONObject o, Container<TVector3> parent) : base(client, o, parent, ContainerType.Scene)
        {
            size = Utils.GetVector<TVector3>(o["size"]);
        }

        internal override void HandleUpdate(JSONObject o)
        {
            if (o.HasField("size")) size = Utils.GetVector<TVector3>(o["size"]);
            base.HandleUpdate(o);
        }

        internal GenericObject<TVector3> GetObject(int objectID)
        {
            return this.objects.Find(o => o.objectID == objectID);
        }

        internal void AddObject(ref GenericObject<TVector3> objectToAdd)
        {
            this.objects.Add(objectToAdd);
            this.onObjectEntered?.Invoke(objectToAdd);
        }

        internal void RemoveObject(int objectIdx)
        {
            var o = this.objects[objectIdx];
            this.objects.RemoveAt(objectIdx);
            this.onObjectExited?.Invoke(o);
        }
    }
}