using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Augmenta
{
    public enum ProtocolVersion
    {
        v1,
        v2,
        Latest
    }

    public class AxisTransform
    {
        public enum AxisMode
        {
            ZUpRightHanded,
            ZUpLeftHanded,
            YUpRightHanded,
            YUpLeftHanded,
        };

        public enum OriginMode
        {
            BottomLeft,
            BottomRight,
        };

        public enum CoordinateSpace
        {
            Absolute,
            Relative,
            Normalized,
        };

        public AxisMode axis = AxisMode.ZUpRightHanded;
        public OriginMode origin = OriginMode.BottomLeft;
        public bool flipX = false;
        public bool flipY = false;
        public bool flipZ = false;
        public CoordinateSpace coordinateSpace = CoordinateSpace.Absolute;
        //public originOffset; // TODO
        //public customMatrix; // TODO
    };

    public class ProtocolOptions
    {
        public enum RotationMode
        {
            Radians,
            Degrees,
            Quaternions,
        };

        public ProtocolVersion version = ProtocolVersion.Latest;
        public List<string> tags;
        public int downSample = 1;
        public bool streamClouds = true;
        public bool streamClusters = true;
        public bool streamClusterPoints = true;
        public bool streamZonePoints = false;
        public RotationMode boxRotationMode = RotationMode.Quaternions;
        public AxisTransform axisTransform = new AxisTransform();
        public bool useCompression = true;
        public bool usePolling = false;
    }
}
