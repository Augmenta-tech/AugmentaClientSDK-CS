using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Augmenta
{
    /// <summary>
    /// 
    /// </summary>
    /// <todo> This "Base" class is useless, it should be merged with the derived templated one </todo>
    public abstract class BaseClient
    {
        public readonly string appName;
        public readonly string appVersion;
        public readonly string pluginVersion;

        public BaseContainer worldContainer;
        protected BaseContainer workingScene; //the scene provided in the bundle data on receive

        protected Dictionary<string, BaseContainer> addressContainerMap;
        public ProtocolOptions options;

        public BaseClient(string appName, string appVersion, string pluginVersion)
        {
            this.appName = appName;
            this.appVersion = appVersion;
            this.pluginVersion = pluginVersion;
        }

        //Call once per frame
        [Obsolete("You no longer need to call update on the client each frame !", false)]
        virtual public void Update(float time)
        {
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

                // Remove objects that were not updated this frame
                for (int i = workingScene.objects.Count - 1; i >= 0; i--)
                {
                    if (!workingScene.objects[i].updatedThisFrame)
                    {
                        workingScene.RemoveObject(i);
                    }
                }
            }

            var packetDataPos = offset + 5;

            switch (type)
            {
                case 0: // Object
                    {
                        ProcessObject(time, packet, packetDataPos);
                    }
                    break;

                case 1: // Zone
                    {
                        ProcessZone(time, packet, packetDataPos);
                    }
                    break;

                case 2: // Scene
                    {
                        ProcessScene(time, packet, packetDataPos);

                        // We just started updating a scene, reset object update status
                        foreach (var obj in workingScene.objects)
                        {
                            obj.updatedThisFrame = false;
                        }
                    }
                    break;
            }
        }

        private void ProcessObject(float time, ReadOnlySpan<byte> data, int offset)
        {
            var objectID = Utils.ReadInt(data, offset);

            BaseObject o = workingScene.GetObject(objectID);
            bool objectAlreadyExists = o != null;
            if (!objectAlreadyExists)
            {
                o = CreateObject();
                o.objectID = objectID;
            }

            ProcessObjectInternal(o);

            // TODO: Should pass options as well, to deal with different rotation representations
            o.UpdateData(time, data, offset);
            
            if (!objectAlreadyExists)
            {
                workingScene.AddObject(ref o);
            }

            if (o.isCluster)
            {
                switch (o.state)
                {
                    case BaseObject.State.Enter:
                        o.NotifyEnter();
                        break;

                    case BaseObject.State.Update:
                        if (!objectAlreadyExists)
                        {
                            o.NotifyEnter();
                        }
                        else
                        {
                            o.NotifyUpdate();
                        }
                        break;

                    case BaseObject.State.Leave:
                        o.NotifyLeave(); // TODO: Rename leave for consistency
                        break;
                }
            }
            else // Point Cloud
            {
                o.NotifyUpdate();
            }
        }

        // TODO: Not used, deprecate ?
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

        protected abstract BaseObject CreateObject();

        virtual public void Clear()
        {
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
        public delegate void OnSetupCompleted(Container<TVector3> world);
        public event OnSetupCompleted onSetupCompleted;

        public Client(string appName, string appVersion, string pluginVersion) : base(appName, appVersion, pluginVersion)
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

            switch (options.boxRotationMode)
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
            switch (options.axisTransform.axis)
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

            switch (options.axisTransform.origin)
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

            switch (options.axisTransform.coordinateSpace)
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
            registerJson.AddField("application-name", appName);
            registerJson.AddField("application-version", appVersion);
            registerJson.AddField("plugin-version", pluginVersion);

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

        internal override void SetupWorld(JSONObject data)
        {
            base.SetupWorld(data);
            onSetupCompleted?.Invoke(worldContainer as Container<TVector3>);
        }

    }
}
