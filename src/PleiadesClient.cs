using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using UnityEditor.Build;

namespace Augmenta
{
    abstract internal class BasePleiadesClient
    {
        public Dictionary<int, BasePObject> objects = new Dictionary<int, BasePObject>();
        public BasePContainer worldContainer;
        protected BasePContainer workingScene; //the scene provided in the bundle data on receive

        protected List<BasePContainer> containers;

        public BasePleiadesClient()
        {
            containers = new List<BasePContainer>();
        }

        //Call once per frame
        virtual public void update(float time)
        {
            var objectsToRemove = new List<int>();
            foreach (var o in objects.Values)
            {
                o.update(time);
                if (o.timeSinceGhost > 1)
                    objectsToRemove.Add(o.objectID);
            }

            foreach (var oid in objectsToRemove)
            {
                var o = objects[oid];
                removeObject(o);
            }
        }

        public void processMessage(string message)
        {
            JSONObject o = new JSONObject(message);
            if (o.HasField("status"))
            {
                if (o["status"].str == "ok")
                {
                    if (o.HasField("setup"))
                    {
                        setupWorld(o["setup"]);
                    }
                }
            }
            else if (o.HasField("update"))
            {
                var id = o["update"]["id"].str;
                var data = o["update"]["data"];
                var container = getContainerForAddress(id);
                if (container != null) container.handleUpdate(data);
            }
        }

        public virtual void setupWorld(JSONObject data)
        {
            if (worldContainer != null)
            {
                worldContainer.clear();
            }

            worldContainer = createContainer(data[0]);

        }

        public void processData(float time, ReadOnlySpan<byte> data, int offset = 0)
        {
            var type = data[offset];


            if (type == 255) //bundle
            {
                workingScene = null;

                var pos = offset + 1; //offset + sizeof(packettype)
                while (pos < data.Length - 5) //-sizeof(packettype) - sizeof(packetsize)
                {
                    var packetSize = Utils.ReadInt(data, pos + 1);  //pos + sizeof(packettype)
                    processData(time, data.Slice(pos, packetSize), 0);
                    pos += packetSize;
                }
            }

            //Debug.Log("Packet type : " + type);

            switch (type)
            {
                case 0: //Object
                    {
                        processObject(time, data, offset);
                    }
                    break;

                case 1: //Zone
                    {
                        processZone(time, data, offset);
                    }
                    break;

                case 2:
                    {
                        processScene(time, data, offset);
                    }
                    break;
            }
        }

        private void processObject(float time, ReadOnlySpan<byte> data, int offset)
        {
            var objectID = Utils.ReadInt(data, offset + 1 + sizeof(int)); //offset + sizeof(packettype) + sizeof(packetsize)

            BasePObject o = null;
            if (objects.ContainsKey(objectID)) o = objects[objectID];
            if (o == null) o = addObject(objectID);

            processObjectInternal(o);

            o.updateData(time, data, offset);
        }
        virtual protected void processObjectInternal(BasePObject o) { }

        void processZone(float time, ReadOnlySpan<byte> data, int offset)
        {
        }

        void processScene(float time, ReadOnlySpan<byte> data, int offset)
        {
            var sizeOffset = offset + 1 + sizeof(int);
            var sceneIDSize = Utils.ReadInt(data, sizeOffset); //offset + sizeof(packettype) + sizeof(packetsize)
            var sceneID = Utils.ReadString(data, sizeOffset + sizeof(int), sceneIDSize);

            if (sceneID == "")
            {
                workingScene = worldContainer;
            }
            else
            {
                if (worldContainer == null) return;
                workingScene = getContainerForAddress(sceneID);


            }

            //if (workingScene != null) UnityEngine.Debug.Log("working scene found : " + workingScene);
            //else UnityEngine.Debug.Log("working scene null");
        }

        protected abstract BasePObject createObject();
        protected abstract BasePContainer createContainer(JSONObject data);

        public virtual void registerContainer(BasePContainer c)
        {
            if (containers == null) containers = new List<BasePContainer>();
            containers.Add(c);
            //UnityEngine.Debug.Log("Container registered " + c);
        }

        public virtual void unregisterContainer(BasePContainer c)
        {
            containers.Remove(c);
        }

        public BasePContainer getContainerForAddress(string id)
        {
            foreach (var c in containers)
            {
                //UnityEngine.Debug.Log("container id " + c.id + " <> " + id);
                if (c.id == id) return c;
            }
            return null;
        }
        protected void onObjectRemove(BasePObject o)
        {
            removeObject(o);
        }

        protected virtual BasePObject addObject(int objectID)
        {
            var o = createObject();
            o.objectID = objectID;
            o.onRemove += onObjectRemove;
            objects.Add(objectID, o);
            return o;
        }

        protected virtual void removeObject(BasePObject o)
        {
            objects.Remove(o.objectID);
            o.kill();
        }

        virtual public void clear()
        {
            foreach (var o in objects.Values)
                o.kill(true);
            objects.Clear();

            if (worldContainer != null)
            {
                worldContainer.clear();
                worldContainer = null;
            }
            containers.Clear();
        }
    }
    internal class PleiadesClient<ObjectT, T> : BasePleiadesClient where ObjectT : BasePObject, new() where T : struct
    {
        public PleiadesClient() : base()
        {
        }
        protected override BasePObject createObject()
        {
            return new ObjectT();
        }

        protected override BasePContainer createContainer(JSONObject data)
        {
            return createContainerInternal(data);
        }

        protected virtual PContainer<T> createContainerInternal(JSONObject data)
        {
            return new PContainer<T>(this, data, null);
        }

        protected override BasePObject addObject(int objectID)
        {
            var o = base.addObject(objectID);
            addObjectInternal(o as ObjectT);
            return o;
        }
        protected virtual void addObjectInternal(ObjectT o) { }

        protected override void removeObject(BasePObject o)
        {
            removeObjectInternal(o as ObjectT);
            base.removeObject(o);
        }
        protected virtual void removeObjectInternal(ObjectT o) { }

    }
}
