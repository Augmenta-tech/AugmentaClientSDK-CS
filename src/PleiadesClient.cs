using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Augmenta
{
    abstract internal class BasePleiadesClient
    {
        public Dictionary<int, BasePObject> objects = new Dictionary<int, BasePObject>();
        public BasePContainer worldContainer;
        protected BasePContainer workingScene; //the scene provided in the bundle data on receive

        protected Dictionary<string, BasePContainer> addressContainerMap;

        public BasePleiadesClient()
        {
            addressContainerMap = new Dictionary<string, BasePContainer>();
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
                var data = o["update"];
                var address = o["update"]["address"].str;
                var container = getContainerForAddress(address);
                if (container != null) container.handleUpdate(data);
            }
        }

        public virtual void setupWorld(JSONObject data)
        {
            if (worldContainer != null)
            {
                worldContainer.clear();
            }

            addressContainerMap.Clear();

            if (data == null)
            {
                return;
            }

            worldContainer = createContainer(data[0]);

        }

        public void processData(float time, ReadOnlySpan<byte> data, int offset = 0)
        {
            var packetSize = Utils.ReadInt(data, offset);
            var type = data[offset + 4];

            if (type == 255) //bundle
            {
                workingScene = null;

                var packetCount = Utils.ReadInt(data, offset + 5);
                var pos = offset + 9; //start of child packets

                while (pos < data.Length - 4)
                {
                    var pSize = Utils.ReadInt(data, pos);
                    processData(time, data, pos);
                    pos += pSize;
                }
            }

            //Debug.Log("Packet type : " + type);

            var packetDataPos = offset + 5;

            switch (type)
            {
                case 0: //Object
                    {
                        processObject(time, data, packetDataPos);
                    }
                    break;

                case 1: //Zone
                    {
                        processZone(time, data, packetDataPos);
                    }
                    break;

                case 2:
                    {
                        processScene(time, data, packetDataPos);
                    }
                    break;
            }
        }

        private void processObject(float time, ReadOnlySpan<byte> data, int offset)
        {
            var objectID = Utils.ReadInt(data, offset);

            BasePObject o = null;
            if (objects.ContainsKey(objectID)) o = objects[objectID];
            if (o == null) o = addObject(objectID);

            processObjectInternal(o);

            o.updateData(time, data, offset);
        }
        virtual protected void processObjectInternal(BasePObject o) { }

        void processZone(float time, ReadOnlySpan<byte> data, int offset)
        {
            processZoneInternal(time, data, offset);
        }

        virtual protected void processZoneInternal(float time, ReadOnlySpan<byte> data, int offset) { }
        void processScene(float time, ReadOnlySpan<byte> data, int offset)
        {
            var sceneIDSize = Utils.ReadInt(data, offset);
            var sceneID = Utils.ReadString(data, offset + 4, sceneIDSize);

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
            if (addressContainerMap == null) addressContainerMap = new Dictionary<string, BasePContainer>();
            addressContainerMap.Add(c.address, c);
            //UnityEngine.Debug.Log("Container registered " + c);
        }

        public virtual void unregisterContainer(BasePContainer c)
        {
            addressContainerMap.Remove(c.address);
        }

        public BasePContainer getContainerForAddress(string address)
        {
            if (!addressContainerMap.ContainsKey(address)) return null;
            return addressContainerMap[address];
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
            addressContainerMap.Clear();
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

        protected override void processZoneInternal(float time, ReadOnlySpan<byte> data, int offset)
        {
            var zoneIDSize = Utils.ReadInt(data, offset);
            var zoneID = Utils.ReadString(data, offset + 4, zoneIDSize);

            PZone<T> zone = getContainerForAddress(zoneID) as PZone<T>;
            if (zone == null) return;
            zone.processData(time, data, offset + 4 + zoneIDSize);
        }

    }
}
