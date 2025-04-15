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
        public ProtocolOptions options;

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

        /// <summary>
        /// Process a string message received from the server through the text/control channel
        /// </summary>
        public void ProcessMessage(string message)
        {
            JSONObject o = new JSONObject(message);
            if (o.HasField("status"))
            {
                if (o["status"].str == "ok")
                {
                    if (o.HasField("setup"))
                    {
                        var worldJson = o["setup"];
                        SetupWorld(worldJson);
                    }
                }
            }
            else if (o.HasField("update"))
            {
                var updatedObject = o["update"][0];
                var address = updatedObject["address"].str;
                var container = GetContainerForAddress(address);
                if (container == null)
                {
                    Debug.WriteLine("Could not find container for address");
                    return;
                }
                    
                container.HandleUpdate(updatedObject);
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

        /// <summary>
        /// Process a data blob received through the data channel of a websocket connection,
        /// updating the data structure as needed.
        /// </summary>
        public void ProcessData(float time, ReadOnlySpan<byte> dataBuffer)
        {
            ReadOnlySpan<byte> packet;

            byte[] decompressedBuffer;
            if (options.useCompression)
            {
                decompressedBuffer = Utils.DecompressData(dataBuffer);
                packet = decompressedBuffer;
            }
            else
            {
                packet = dataBuffer;
            }

            ProcessPacket(time, packet, 0);
        }

        private void ProcessPacket(float time, ReadOnlySpan<byte> packet, int offset)
        {
            var packetSize = Utils.ReadInt(packet, offset);
            var type = packet[offset + 4];

            if (type == 255) //bundle
            {
                workingScene = null;

                var packetCount = Utils.ReadInt(packet, offset + 5);
                var pos = offset + 9; //start of child packets

                while (pos < packet.Length - 4)
                {
                    var pSize = Utils.ReadInt(packet, pos);
                    ProcessPacket(time, packet, pos);
                    pos += pSize;
                }
            }

            var packetDataPos = offset + 5;

            switch (type)
            {
                case 0: //Object
                    {
                        ProcessObject(time, packet, packetDataPos);
                    }
                    break;

                case 1: //Zone
                    {
                        ProcessZone(time, packet, packetDataPos);
                    }
                    break;

                case 2:
                    {
                        ProcessScene(time, packet, packetDataPos);
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

        private void ProcessZone(float time, ReadOnlySpan<byte> data, int offset)
        {
            ProcessZoneInternal(time, data, offset);
        }

        virtual protected void ProcessZoneInternal(float time, ReadOnlySpan<byte> data, int offset) { }
        
        private void ProcessScene(float time, ReadOnlySpan<byte> data, int offset)
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

        /// <summary>
        /// Generate a Register message according to current options and settings and returns it as a JSON string,
        ///  ready to be sent to the server. 
        /// </summary>
        public string GetRegisterMessage(string clientName)
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

            switch(options.boxRotationMode)
            {
                case ProtocolOptions.RotationMode.Quaternions:
                    optionsJson.AddField("boxRotationMode", "quaternions");
                    break;
                case ProtocolOptions.RotationMode.Radians:
                    optionsJson.AddField("boxRotationMode", "radians");
                    break;
                case ProtocolOptions.RotationMode.Degrees:
                    optionsJson.AddField("boxRotationMode", "degrees");
                    break;
            }

            JSONObject tagsJson = JSONObject.Create();
            foreach (var tag in options.tags)
            {
                tagsJson.Add(tag);
            }
            optionsJson.AddField("tags", tagsJson);

            JSONObject axisTransformJson = JSONObject.Create();
            switch(options.axisTransform.axis)
            {
                case AxisTransform.AxisMode.ZUpRightHanded:
                    axisTransformJson.AddField("axis", "z_up_right");
                    break;
                case AxisTransform.AxisMode.ZUpLeftHanded:
                    axisTransformJson.AddField("axis", "z_up_left");
                    break;
                case AxisTransform.AxisMode.YUpRightHanded:
                    axisTransformJson.AddField("axis", "y_up_right");
                    break;
                case AxisTransform.AxisMode.YUpLeftHanded:
                    axisTransformJson.AddField("axis", "y_up_left");
                    break;
            }

            switch(options.axisTransform.origin)
            {
                case AxisTransform.OriginMode.BottomLeft:
                    axisTransformJson.AddField("origin", "bottom_left");
                    break;
                case AxisTransform.OriginMode.BottomRight:
                    axisTransformJson.AddField("origin", "bottom_right");
                    break;
                case AxisTransform.OriginMode.TopLeft:
                    axisTransformJson.AddField("origin", "top_left");
                    break;
                case AxisTransform.OriginMode.TopRight:
                    axisTransformJson.AddField("origin", "top_right");
                    break;
            }

            axisTransformJson.AddField("flipX", options.axisTransform.flipX);
            axisTransformJson.AddField("flipY", options.axisTransform.flipY);
            axisTransformJson.AddField("flipZ", options.axisTransform.flipZ);

            switch(options.axisTransform.coordinateSpace)
            {
                case AxisTransform.CoordinateSpace.Absolute:
                    axisTransformJson.AddField("coordinateSpace", "absolute");
                    break;
                case AxisTransform.CoordinateSpace.Relative:
                    axisTransformJson.AddField("coordinateSpace", "relative");
                    break;
                case AxisTransform.CoordinateSpace.Normalized:
                    axisTransformJson.AddField("coordinateSpace", "normalized");
                    break;
            }

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
