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
            z_up_right,
            z_up_left,
            y_up_left,
        };

        public enum OriginMode
        {
            bottom_left,
            bottom_right
        };

        public enum CoordinateSpace
        {
            Absolute,
            Relative,
            Normalized,
        };

        public AxisMode axis = AxisMode.z_up_right;
        public OriginMode origin = OriginMode.bottom_left;
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
        public AxisTransform axisTransform = new AxisTransform(); // TODO: Default ?
        public bool useCompression = true;
        public bool usePolling = false;
    }
}
