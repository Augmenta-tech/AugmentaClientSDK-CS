using System.Collections.Generic;
using System.Numerics;
using System;
using System.Reflection;
using System.Diagnostics;

namespace Augmenta
{
    public enum ContainerType { Container, Zone, Scene }
    abstract public class BasePContainer
    {

        public ContainerType containerType;

        bool isRoot;
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

            name = o["name"].str;
            isRoot = !o.HasField("id");

            id = isRoot ? null : o["id"].str;
            if (!isRoot)
            {
                client.registerContainer(this);
                updateTransform(o);
            }
            setup(o);
        }

        protected void setup(JSONObject o)
        {
            children = new List<BasePContainer>();

            if (o.HasField("children"))
            {
                foreach (var c in o["children"].list)
                {

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


        abstract protected void updateTransform(JSONObject o);
        abstract protected BasePContainer createContainer(JSONObject o);
        abstract protected BasePContainer createZone(JSONObject o);
        abstract protected BasePContainer createScene(JSONObject o);
    }

    public class PContainer<T> : BasePContainer where T : struct
    {
        public T position;
        public T rotation;

        public PContainer(BasePleiadesClient client = null, JSONObject o = null, BasePContainer parent = null, ContainerType type = ContainerType.Container) : base(client, o, parent, type)
        {
            position = new T();
            rotation = new T();
        }

        protected override void updateTransform(JSONObject o)
        {

            position = (T)Activator.CreateInstance(typeof(T), new object[] { o["position"][0].f, o["position"][1].f, o["position"][2].f });
            rotation = (T)Activator.CreateInstance(typeof(T), new object[] { o["rotation"][0].f, o["rotation"][1].f, o["rotation"][2].f });
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

        public override string ToString()
        {
            return "[Container (" + containerType + ") : " + name + ", " + id + "]";
        }
    }
}