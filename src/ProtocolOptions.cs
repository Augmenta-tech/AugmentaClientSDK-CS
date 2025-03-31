using System.Collections.Generic;

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
            TopLeft,
            TopRight,
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

        virtual public bool Equals(AxisTransform other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return axis == other.axis &&
                   origin == other.origin &&
                   flipX == other.flipX &&
                   flipY == other.flipY &&
                   flipZ == other.flipZ &&
                   coordinateSpace == other.coordinateSpace;
        }
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
        public List<string> tags = new List<string>();
        public int downSample = 1;
        public bool streamClouds = true;
        public bool streamClusters = true;
        public bool streamClusterPoints = true;
        public bool streamZonePoints = false;
        public RotationMode boxRotationMode = RotationMode.Quaternions;
        public AxisTransform axisTransform = new AxisTransform();
        public bool useCompression = true;
        public bool usePolling = false;

        virtual public bool Equals(ProtocolOptions other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (tags.Count != other.tags.Count)
            {
                return false;
            }

            for(int i = 0; i < tags.Count; i++)
            {
                if (!tags[i].Equals(other.tags[i]))
                {
                    return false;
                }
            }

            return version == other.version &&
                   downSample == other.downSample &&
                   streamClouds == other.streamClouds &&
                   streamClusters == other.streamClusters &&
                   streamClusterPoints == other.streamClusterPoints &&
                   streamZonePoints == other.streamZonePoints &&
                   boxRotationMode == other.boxRotationMode &&
                   axisTransform.Equals(other.axisTransform) &&
                   useCompression == other.useCompression &&
                   usePolling == other.usePolling;
        }
    }
}
