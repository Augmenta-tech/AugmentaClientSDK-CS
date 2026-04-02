using System.Collections.Generic;
using System.Drawing;

namespace Augmenta
{
    public enum ContainerType { Container, Zone, Scene }

    public class Container<TVector3> where TVector3 : struct
    {
        public string name;
        public string address;
        public ContainerType containerType;
        public bool isRoot;

        public Client<TVector3> client;
        public Container<TVector3> parent;
        public List<Container<TVector3>> children;

        // TODO: Private with getters
        public TVector3 position;
        public TVector3 rotation;
        public Color color;

        public delegate void OnUpdateEvent(Container<TVector3> obj);
        public event OnUpdateEvent onUpdate;

        public Container(Client<TVector3> client = null, JSONObject o = null, Container<TVector3> parent = null, ContainerType type = ContainerType.Container)
        {
            this.containerType = type;
            this.parent = parent;
            this.client = client;

            isRoot = (this.parent == null);
            name = o["name"].str;
            address = o["address"].str;

            client.RegisterContainer(this);
            Setup(o);

            if (!isRoot)
            {
                position = Utils.GetVector<TVector3>(o["position"]);
                rotation = Utils.GetVector<TVector3>(o["rotation"]);
                color = Utils.GetColor(o["color"]);
            }
        }

        protected void Setup(JSONObject o)
        {
            children = new List<Container<TVector3>>();

            if (o.HasField("children"))
            {
                for (int i = 0; i < o["children"].Count; i++)
                {
                    JSONObject c = o["children"][i];
                    if (c.HasField("type"))
                    {
                        switch (c["type"].str)
                        {
                            case "Zone":
                                children.Add(CreateZone(c));
                                break;

                            case "Scene":
                                var scene = CreateScene(c);
                                children.Add(scene);
                                break;

                            default:
                                children.Add(CreateContainer(c));
                                break;
                        }
                    }
                }
            }
        }

        virtual public void Clear()
        {
            foreach (var c in children) c.Clear();
            children.Clear();
            parent = null;
            client.UnregisterContainer(this);
        }

        internal virtual void HandleUpdate(JSONObject o)
        {
            if (o.HasField("position")) position = Utils.GetVector<TVector3>(o["position"]);
            if (o.HasField("rotation")) rotation = Utils.GetVector<TVector3>(o["rotation"]);
            if (o.HasField("color")) color = Utils.GetColor(o["color"]);
            if (o.HasField("children"))
            {
                var childrenJson = o["children"];
                for (int i = 0; i < childrenJson.Count; i++)
                {
                    var childData = childrenJson[i];
                    var existingChildIdx = GetChildIndex(childData["address"].str);
                    if (existingChildIdx >= 0)
                    {
                        var child = children[existingChildIdx];
                        child.HandleUpdate(childData);
                    }
                    else
                    {
                        // TODO: Create a new child (for now hierarchy change do not trigger update messages)
                    }
                }
                // TODO: Remove children that no longer exit
            }

            this.onUpdate?.Invoke(this);
        }

        protected int GetChildIndex(string address)
        {
            for (int i = 0; i < children.Count; ++i)
            {
                var child = children[i];
                if (child.address == address)
                {
                    return i;
                }
            }
            return -1;
        }


        private Container<TVector3> CreateContainer(JSONObject o)
        {
            return new Container<TVector3>(client, o, this);
        }

        private Container<TVector3> CreateZone(JSONObject o)
        {
            return new Zone<TVector3>(client, o, this);
        }

        private Container<TVector3> CreateScene(JSONObject o)
        {
            return new Scene<TVector3>(client, o, this);
        }

        virtual protected void UpdateCloudPoint(ref TVector3 pointInArray, TVector3 point)
        {
            pointInArray = point; //no transformation by default
        }

        public override string ToString()
        {
            return "[Container (" + containerType + ") : " + name + ", " + address + "]";
        }
    }
}