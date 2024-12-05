using System.Collections.Generic;
using System.Numerics;
using System;
using System.Reflection;
using System.Diagnostics;

namespace Augmenta
{
    public enum ContainerType { Container, Zone, Scene }
    abstract internal class BasePContainer
    {

        public ContainerType containerType;

        public bool isRoot;
        public string name;
        public string id;

        public BasePleiadesClient client;
        public BasePContainer parent;
        public List<BasePContainer> children;

        public object wrapperObject;


        public BasePContainer(BasePleiadesClient client, JSONObject o, BasePContainer parent, ContainerType type = ContainerType.Container)
        {
            this.containerType = type;
            this.parent = parent;
            this.client = client;

            isRoot = !o.HasField("id");
            name = o["name"].str;

            id = isRoot ? null : o["id"].str;
            if (!isRoot) client.registerContainer(this);
            setup(o);
        }

        protected void setup(JSONObject o)
        {
            children = new List<BasePContainer>();

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
                                children.Add(createZone(c));
                                break;

                            case "Scene":
                                children.Add(createScene(c));
                                break;

                            default:
                                children.Add(createContainer(c));
                                break;
                        }
                    }
                }
            }
        }

        virtual public void clear()
        {
            foreach (var c in children) c.clear();
            children.Clear();
            parent = null;
            client.unregisterContainer(this);
        }


        abstract protected BasePContainer createContainer(JSONObject o);
        abstract protected BasePContainer createZone(JSONObject o);
        abstract protected BasePContainer createScene(JSONObject o);

        public void handleUpdate(JSONObject o)
        {
            foreach (var prop in o.keys)
            {
                handleParamUpdateInternal(prop, o[prop]);
            }
        }

        virtual protected void handleParamUpdateInternal(string prop, JSONObject o) { }

    }

    internal class PContainer<T> : BasePContainer where T : struct
    {
        public T position;
        public T rotation;

        public PContainer(BasePleiadesClient client = null, JSONObject o = null, BasePContainer parent = null, ContainerType type = ContainerType.Container) : base(client, o, parent, type)
        {
            if (!isRoot)
            {
                position = Utils.GetVector<T>(o["position"]);
                rotation = Utils.GetVector<T>(o["rotation"]);
            }
        }


        protected override BasePContainer createContainer(JSONObject o)
        {
            return new PContainer<T>(client, o, this);
        }

        protected override BasePContainer createZone(JSONObject o)
        {
            return new PZone<T>(client, o, this);
        }

        protected override BasePContainer createScene(JSONObject o)
        {
            return new PScene<T>(client, o, this);
        }

        protected override void handleParamUpdateInternal(string prop, JSONObject data)
        {
            if (prop == "position") position = Utils.GetVector<T>(data);
            else if (prop == "rotation") rotation = Utils.GetVector<T>(data);
        }

        public override string ToString()
        {
            return "[Container (" + containerType + ") : " + name + ", " + id + "]";
        }
    }
}