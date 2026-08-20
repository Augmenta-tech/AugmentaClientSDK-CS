using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Augmenta
{
    public class Client<TVector3> where TVector3 : struct
    {
        private enum Status
        {
            Uninitialized,
            WaitingForHandshake,
            Alive
        }

        public readonly string appName;
        public readonly string appVersion;
        public readonly string pluginVersion;
        private ProtocolOptions options;
        Status state = Status.Uninitialized;

        private Container<TVector3> worldContainer = null;
        private Scene<TVector3> workingScene; //the scene provided in the bundle data on receive
        private Dictionary<string, Container<TVector3>> addressContainerMap = new Dictionary<string, Container<TVector3>>();

        public delegate void OnSetupCompleted(Container<TVector3> world);
        public event OnSetupCompleted onSetupCompleted;

        public Client(string appName, string appVersion, string pluginVersion)
        {
            this.appName = appName;
            this.appVersion = appVersion;
            this.pluginVersion = pluginVersion;
        }

        /// <summary>
        /// Process a string message received from the server through the text/control channel
        /// </summary>
        public void ProcessMessage(string message)
        {
            JSONObject json = new JSONObject(message);
           
            if (json.HasField("status"))
            {
                if (json["status"].str == "ok")
                {
                    // The SDK only handles version 2 for now, so if for any reason the server answers with something else, keep waiting
                    json.GetField(out int version, "version", -1);
                    if (version != 2)
                    {
                        return;
                    }

                    var worldJson = json["setup"];
                    SetupWorld(worldJson);

                    // TODO: Force back options based on the recieved setup message

                    state = Status.Alive;
                }
            }
            else if (json.HasField("update"))
            {
                // TMP: Set up the world with received data if it was not initialized
                // This is a dirty workaround for the case where the client fails to send the register message before the server times out.
                // It should be removed later
                if (worldContainer == null)
                {
                    SetupWorld(json["update"]);
                    state = Status.Alive;
                    return;
                }

                var updatedObject = json["update"][0];
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

        /// <summary>
        /// Process a data blob received through the data channel of a websocket connection,
        /// updating the data structure as needed.
        /// </summary>
        public void ProcessData(ReadOnlySpan<byte> dataBuffer)
        {
            if (state != Status.Alive)
            {
                // Skip processing while waiting for handshake to avoid parsing errors
                // (for example, we do not know whether the payload is compressed yet)
                return;
            }

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

            ProcessPacket(packet, 0);
        }

        private void ProcessPacket(ReadOnlySpan<byte> packet, int offset)
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
                    ProcessPacket(packet, pos);
                    pos += pSize;
                }

                // Remove objects that were not updated this frame
                for (int i = workingScene.objects.Count - 1; i >= 0; i--)
                {
                    var obj = workingScene.objects[i];
                    if (!obj.updatedThisFrame)
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
                        ProcessObject(packet, packetDataPos);
                    }
                    break;

                case 1: // Zone
                    {
                        ProcessZone(packet, packetDataPos);
                    }
                    break;

                case 2: // Scene
                    {
                        ProcessScene(packet, packetDataPos);

                        // We just started updating a scene, reset object update status
                        foreach (var obj in workingScene.objects)
                        {
                            obj.updatedThisFrame = false;
                        }
                    }
                    break;
            }
        }

        private void ProcessObject(ReadOnlySpan<byte> data, int offset)
        {
            var objectID = Utils.ReadInt(data, offset);

            var o = workingScene.GetObject(objectID);
            bool objectAlreadyExists = o != null;
            if (!objectAlreadyExists)
            {
                o = new GenericObject<TVector3>();
                o.objectID = objectID;
            }

            // TODO: Should pass options as well, to deal with different rotation representations
            o.UpdateData(data, offset);

            if (!objectAlreadyExists)
            {
                workingScene.AddObject(ref o);
            }

            o.NotifyUpdate();
        }

        private void ProcessZone(ReadOnlySpan<byte> data, int offset)
        {
            var zoneIDSize = Utils.ReadInt(data, offset);
            var zoneID = Utils.ReadString(data, offset + 4, zoneIDSize);

            Zone<TVector3> zone = GetContainerForAddress(zoneID) as Zone<TVector3>;
            if (zone == null) return;
            zone.ProcessData(data, offset + 4 + zoneIDSize);
        }

        private void ProcessScene(ReadOnlySpan<byte> data, int offset)
        {
            var sceneIDSize = Utils.ReadInt(data, offset);
            var sceneID = Utils.ReadString(data, offset + 4, sceneIDSize);

            workingScene = GetContainerForAddress(sceneID) as Scene<TVector3>;
        }

        internal void RegisterContainer(Container<TVector3> c)
        {
            addressContainerMap.Add(c.address, c);
        }

        internal void UnregisterContainer(Container<TVector3> c)
        {
            addressContainerMap.Remove(c.address);
        }

        public Container<TVector3> GetContainerForAddress(string address)
        {
            if (!addressContainerMap.ContainsKey(address)) return null;
            return addressContainerMap[address];
        }

        public void Clear()
        {
            if (worldContainer != null)
            {
                worldContainer.Clear();
                worldContainer = null;
            }
            addressContainerMap.Clear();
        }

        /// <summary>
        /// Initialize the client with a set of options. Returns a Register message as a JSON string that should be sent to the server.
        /// </summary>
        public string Initialize(string clientName, ref ProtocolOptions options)
        {
            Debug.Assert(state == Status.Uninitialized);

            this.options = options;

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

            state = Status.WaitingForHandshake;

            return dataJson.ToString();
        }

        public void Shutdown()
        {
            Debug.Assert(state == Status.Alive || state == Status.WaitingForHandshake);
            state = Status.Uninitialized;
        }

        public ProtocolOptions GetOptions()
        {
            return options;
        }

        public string GetPollMessage()
        {
            JSONObject pollJson = JSONObject.Create();
            pollJson.AddField("poll", true);
            return pollJson.ToString();
        }

        private void SetupWorld(JSONObject data)
        {
            if (worldContainer != null)
            {
                worldContainer.Clear();
            }
            addressContainerMap.Clear();

            worldContainer = new Container<TVector3>(this, data[0], null);

            onSetupCompleted?.Invoke(worldContainer as Container<TVector3>);
        }

    }
}
