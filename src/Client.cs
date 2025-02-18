using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Augmenta
{
    public abstract class BaseClient
    {
        public Dictionary<int, BaseObject> objects = new Dictionary<int, BaseObject>();
        public BaseContainer worldContainer;
        protected BaseContainer workingScene; //the scene provided in the bundle data on receive

        protected Dictionary<string, BaseContainer> addressContainerMap;

        public BaseClient()
        {
            addressContainerMap = new Dictionary<string, BaseContainer>();
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

        internal virtual void setupWorld(JSONObject data)
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

        public virtual void processData(float time, byte[] bytes, int offset = 0, bool decompress = false)
        {
            if (decompress) bytes = Utils.DecompressData(bytes);
            ReadOnlySpan<byte> data = bytes;

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
                    processData(time, bytes, pos);
                    pos += pSize;
                }
            }

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

            BaseObject o = null;
            if (objects.ContainsKey(objectID)) o = objects[objectID];
            if (o == null) o = addObject(objectID);

            processObjectInternal(o);

            o.updateData(time, data, offset);
        }
        virtual protected void processObjectInternal(BaseObject o) { }

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
        }

        protected abstract BaseObject createObject();
        internal abstract BaseContainer createContainer(JSONObject data);

        public virtual void registerContainer(BaseContainer c)
        {
            if (addressContainerMap == null) addressContainerMap = new Dictionary<string, BaseContainer>();
            addressContainerMap.Add(c.address, c);
        }

        public virtual void unregisterContainer(BaseContainer c)
        {
            addressContainerMap.Remove(c.address);
        }

        public BaseContainer getContainerForAddress(string address)
        {
            if (!addressContainerMap.ContainsKey(address)) return null;
            return addressContainerMap[address];
        }

        protected void onObjectRemove(BaseObject o)
        {
            removeObject(o);
        }

        protected virtual BaseObject addObject(int objectID)
        {
            var o = createObject();
            o.objectID = objectID;
            o.onRemove += onObjectRemove;
            objects.Add(objectID, o);
            return o;
        }

        protected virtual void removeObject(BaseObject o)
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
    public class Client<ObjectT, TVector3> : BaseClient where ObjectT : BaseObject, new() where TVector3 : struct
    {
        public Client() : base()
        {
        }
        protected override BaseObject createObject()
        {
            return new ObjectT();
        }

        internal override BaseContainer createContainer(JSONObject data)
        {
            var container = new Container<TVector3>(this, data, null);
            OnContainerCreated(ref container);
            return container;
        }

        protected virtual void OnContainerCreated(ref Container<TVector3> newContainer)
        {
        }

        protected override BaseObject addObject(int objectID)
        {
            var o = base.addObject(objectID);
            addObjectInternal(o as ObjectT);
            return o;
        }
        protected virtual void addObjectInternal(ObjectT o) { }

        protected override void removeObject(BaseObject o)
        {
            removeObjectInternal(o as ObjectT);
            base.removeObject(o);
        }
        protected virtual void removeObjectInternal(ObjectT o) { }

        protected override void processZoneInternal(float time, ReadOnlySpan<byte> data, int offset)
        {
            var zoneIDSize = Utils.ReadInt(data, offset);
            var zoneID = Utils.ReadString(data, offset + 4, zoneIDSize);

            Zone<TVector3> zone = getContainerForAddress(zoneID) as Zone<TVector3>;
            if (zone == null) return;
            zone.processData(time, data, offset + 4 + zoneIDSize);
        }

        public string GetRegisterMessage(string clientName, ProtocolOptions options)
        {
            JSONObject optionsJson = JSONObject.Create();
            if (options.version == ProtocolVersion.Latest)
            {
                optionsJson.AddField("version", (int)options.version);
            }
            else if (options.version != ProtocolVersion.v1)
            {
                optionsJson.AddField("version", (int)options.version + 1);
            }
            optionsJson.AddField("downSample", options.downSample);
            optionsJson.AddField("streamClouds", options.streamClouds);
            optionsJson.AddField("streamClusters", options.streamClusters);
            optionsJson.AddField("streamClusterPoints", options.streamClusterPoints);
            optionsJson.AddField("streamZonePoints", options.streamZonePoints);
            optionsJson.AddField("useCompression", options.useCompression);
            optionsJson.AddField("usePolling", options.usePolling);
            optionsJson.AddField("boxRotationMode", nameof(options.boxRotationMode));

            JSONObject tagsJson = JSONObject.Create();
            foreach (var tag in options.tags)
            {
                tagsJson.Add(tag);
            }
            optionsJson.AddField("tags", tagsJson);

            JSONObject axisTransformJson = JSONObject.Create();
            axisTransformJson.AddField("axis", nameof(options.axisTransform.axis));
            axisTransformJson.AddField("origin", nameof(options.axisTransform.origin));
            axisTransformJson.AddField("flipX", options.axisTransform.flipX);
            axisTransformJson.AddField("flipY", options.axisTransform.flipY);
            axisTransformJson.AddField("flipZ", options.axisTransform.flipZ);
            axisTransformJson.AddField("coordinateSpace", nameof(options.axisTransform.coordinateSpace));
            // TODO: OriginOffset
            // TODO: customMatrix

            optionsJson.AddField("axisTransform", axisTransformJson);

            JSONObject registerJson = JSONObject.Create();
            registerJson.AddField("name", clientName);
            registerJson.AddField("options", optionsJson);

            JSONObject dataJson = JSONObject.Create();
            dataJson.AddField("register", registerJson);

            return dataJson.ToString();
        }

        public string GetPollMessage()
        {
            JSONObject pollJson = JSONObject.Create();
            pollJson.AddField("poll", true);
            return pollJson.ToString();
        }
    }
}
