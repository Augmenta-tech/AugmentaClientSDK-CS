using System.Collections.Generic;
using System.Drawing;

namespace Augmenta
{
    public enum ContainerType { Container, Zone, Scene }

    public abstract class BaseContainer
    {
        public ContainerType containerType;

        public bool isRoot;
        public string name;
        public string address;

        public BaseClient client;
        public BaseContainer parent;
        public List<BaseContainer> children;

        public object wrapperObject;


        public BaseContainer(BaseClient client, JSONObject o, BaseContainer parent, ContainerType type = ContainerType.Container)
        {
            this.containerType = type;
            this.parent = parent;
            this.client = client;

            isRoot = this.parent == null;
            name = o["name"].str;
            address = o["address"].str;

            client.RegisterContainer(this);
            Setup(o);
        }

        protected void Setup(JSONObject o)
        {

            children = new List<BaseContainer>();

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
                                children.Add(CreateScene(c));
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


        abstract protected BaseContainer CreateContainer(JSONObject o);
        abstract protected BaseContainer CreateZone(JSONObject o);
        abstract protected BaseContainer CreateScene(JSONObject o);

        public void HandleUpdate(JSONObject o)
        {
            foreach (var prop in o.keys)
            {
                HandleParamUpdateInternal(prop, o[prop]);
            }
        }

        virtual protected void HandleParamUpdateInternal(string prop, JSONObject o) { }

        protected int GetChildIndex(string address)
        {
            for(int i = 0; i < children.Count; ++i)
            {
                var child = children[i];
                if (child.address == address)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    public class Container<TVector3> : BaseContainer where TVector3 : struct
    {
        public TVector3 position;
        public TVector3 rotation;
        public Color color;

        public Container(BaseClient client = null, JSONObject o = null, BaseContainer parent = null, ContainerType type = ContainerType.Container) :
            base(client, o, parent, type)
        {
            if (!isRoot)
            {
                position = Utils.GetVector<TVector3>(o["position"]);
                rotation = Utils.GetVector<TVector3>(o["rotation"]);
                color = Utils.GetColor(o["color"]);
            }
        }


        protected override BaseContainer CreateContainer(JSONObject o)
        {
            return new Container<TVector3>(client, o, this);
        }

        protected override BaseContainer CreateZone(JSONObject o)
        {
            return new Zone<TVector3>(client, o, this);
        }

        protected override BaseContainer CreateScene(JSONObject o)
        {
            return new Scene<TVector3>(client, o, this);
        }

        protected override void HandleParamUpdateInternal(string prop, JSONObject data)
        {
            if (prop == "position") position = Utils.GetVector<TVector3>(data);
            else if (prop == "rotation") rotation = Utils.GetVector<TVector3>(data);
            else if (prop == "color") color = Utils.GetColor(data);
            else if (prop == "children")
            {
                for (int i = 0; i < data.Count; i++)
                {
                    var childData = data[i];
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