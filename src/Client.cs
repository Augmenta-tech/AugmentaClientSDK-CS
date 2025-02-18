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
        virtual public void Update(float time)
        {
            var objectsToRemove = new List<int>();
            foreach (var o in objects.Values)
            {
                o.Update(time);
                if (o.timeSinceGhost > 1)
                    objectsToRemove.Add(o.objectID);
            }

            foreach (var oid in objectsToRemove)
            {
                var o = objects[oid];
                RemoveObject(o);
            }
        }

        public void ProcessMessage(string message)
        {
            JSONObject o = new JSONObject(message);
            if (o.HasField("status"))
            {
                if (o["status"].str == "ok")
                {
                    if (o.HasField("setup"))
                    {
                        SetupWorld(o["setup"]);
                    }
                }
            }
            else if (o.HasField("update"))
            {
                var data = o["update"];
                var address = o["update"]["address"].str;
                var container = GetContainerForAddress(address);
                if (container != null) container.HandleUpdate(data);
            }
        }

        internal virtual void SetupWorld(JSONObject data)
        {
            if (worldContainer != null)
            {
                worldContainer.Clear();
            }

            addressContainerMap.Clear();

            if (data == null)
            {
                return;
            }

            worldContainer = CreateContainer(data[0]);

        }

        public virtual void ProcessData(float time, byte[] bytes, int offset = 0, bool decompress = false)
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
                    ProcessData(time, bytes, pos);
                    pos += pSize;
                }
            }

            var packetDataPos = offset + 5;

            switch (type)
            {
                case 0: //Object
                    {
                        ProcessObject(time, data, packetDataPos);
                    }
                    break;

                case 1: //Zone
                    {
                        ProcessZone(time, data, packetDataPos);
                    }
                    break;

                case 2:
                    {
                        ProcessScene(time, data, packetDataPos);
                    }
                    break;
            }
        }

        private void ProcessObject(float time, ReadOnlySpan<byte> data, int offset)
        {
            var objectID = Utils.ReadInt(data, offset);

            BaseObject o = null;
            if (objects.ContainsKey(objectID)) o = objects[objectID];
            if (o == null) o = AddObject(objectID);

            ProcessObjectInternal(o);

            o.UpdateData(time, data, offset);
        }
        virtual protected void ProcessObjectInternal(BaseObject o) { }

        void ProcessZone(float time, ReadOnlySpan<byte> data, int offset)
        {
            ProcessZoneInternal(time, data, offset);
        }

        virtual protected void ProcessZoneInternal(float time, ReadOnlySpan<byte> data, int offset) { }
        void ProcessScene(float time, ReadOnlySpan<byte> data, int offset)
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
                workingScene = GetContainerForAddress(sceneID);

            }
        }

        protected abstract BaseObject CreateObject();
        internal abstract BaseContainer CreateContainer(JSONObject data);

        public virtual void RegisterContainer(BaseContainer c)
        {
            if (addressContainerMap == null) addressContainerMap = new Dictionary<string, BaseContainer>();
            addressContainerMap.Add(c.address, c);
        }

        public virtual void UnregisterContainer(BaseContainer c)
        {
            addressContainerMap.Remove(c.address);
        }

        public BaseContainer GetContainerForAddress(string address)
        {
            if (!addressContainerMap.ContainsKey(address)) return null;
            return addressContainerMap[address];
        }

        protected void OnObjectRemove(BaseObject o)
        {
            RemoveObject(o);
        }

        protected virtual BaseObject AddObject(int objectID)
        {
            var o = CreateObject();
            o.objectID = objectID;
            o.onRemove += OnObjectRemove;
            objects.Add(objectID, o);
            return o;
        }

        protected virtual void RemoveObject(BaseObject o)
        {
            objects.Remove(o.objectID);
            o.Kill();
        }

        virtual public void Clear()
        {
            foreach (var o in objects.Values)
                o.Kill(true);
            objects.Clear();

            if (worldContainer != null)
            {
                worldContainer.Clear();
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
        protected override BaseObject CreateObject()
        {
            return new ObjectT();
        }

        internal override BaseContainer CreateContainer(JSONObject data)
        {
            var container = new Container<TVector3>(this, data, null);
            OnContainerCreated(ref container);
            return container;
        }

        protected virtual void OnContainerCreated(ref Container<TVector3> newContainer)
        {
        }

        protected override BaseObject AddObject(int objectID)
        {
            var o = base.AddObject(objectID);
            AddObjectInternal(o as ObjectT);
            return o;
        }
        protected virtual void AddObjectInternal(ObjectT o) { }

        protected override void RemoveObject(BaseObject o)
        {
            RemoveObjectInternal(o as ObjectT);
            base.RemoveObject(o);
        }
        protected virtual void RemoveObjectInternal(ObjectT o) { }

        protected override void ProcessZoneInternal(float time, ReadOnlySpan<byte> data, int offset)
        {
            var zoneIDSize = Utils.ReadInt(data, offset);
            var zoneID = Utils.ReadString(data, offset + 4, zoneIDSize);

            Zone<TVector3> zone = GetContainerForAddress(zoneID) as Zone<TVector3>;
            if (zone == null) return;
            zone.ProcessData(time, data, offset + 4 + zoneIDSize);
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
