using System;
using System.Runtime.InteropServices;

namespace Augmenta
{
    public class GenericObject<TVector3> where TVector3 : struct
    {
        public int objectID;
        public bool isCluster;
        internal bool updatedThisFrame = true;

        public enum State { Enter = 0, Update = 1, Leave = 2, Ghost = 3 };
        public State state;

        public delegate void OnEnterEvent(GenericObject<TVector3> obj);
        public event OnEnterEvent onEnter;

        public delegate void OnUpdateEvent(GenericObject<TVector3> obj);
        public event OnUpdateEvent onUpdate;

        public delegate void OnLeaveEvent(GenericObject<TVector3> obj);
        public event OnLeaveEvent onLeave;

        private TVector3[] pointsA = new TVector3[0];
        private int pointCount;
        public ArraySegment<TVector3> points => new ArraySegment<TVector3>(pointsA, 0, pointCount);

        //cluster
        public TVector3 centroid;
        public TVector3 velocity;
        public TVector3 boxCenter;
        public TVector3 boxSize;
        public TVector3 rotation;
        public float weight;

        internal void NotifyEnter()
        {
            this.onEnter?.Invoke(this);
        }

        internal void NotifyLeave()
        {
            this.onLeave?.Invoke(this);
        }

        internal void NotifyUpdate()
        {
            this.onUpdate?.Invoke(this);
        }

        internal void UpdateData(ReadOnlySpan<byte> data, int offset)
        {
            var propertiesCount = Utils.ReadInt(data, offset + 4); //first data is ID (4 bytes)
            var propertyByteOffset = offset + 8;
            for (int i = 0; i < propertiesCount; i++)
            {
                var propertySize = Utils.ReadInt(data, propertyByteOffset);
                var propertyID = Utils.ReadInt(data, propertyByteOffset + 4);

                switch (propertyID)
                {
                    case 0:
                        UpdatePointsData(data, propertyByteOffset + 8);
                        break;
                    case 1:
                        isCluster = true;
                        UpdateClusterData(data, propertyByteOffset + 8);
                        break;
                }

                propertyByteOffset += propertySize;
            }

            updatedThisFrame = true;
        }

        private void UpdatePointsData(ReadOnlySpan<byte> data, int offset)
        {
            pointCount = Utils.ReadInt(data, offset);
            var vectors = Utils.ReadVectors<TVector3>(data, offset + sizeof(int), pointCount * 12);

            // TODO: ???
            if (pointsA.Length < pointCount)
                pointsA = new TVector3[(int)(pointCount * 1.5)];

            vectors.CopyTo(pointsA.AsSpan());
        }

        private void UpdateClusterData(ReadOnlySpan<byte> data, int offset)
        {
            state = (State) Utils.ReadInt(data, offset);
            offset += 4;

            centroid = ReadVector(data, offset);
            offset += 12;

            velocity = ReadVector(data, offset);
            offset += 12;

            boxCenter = ReadVector(data, offset);
            offset += 12;

            boxSize = ReadVector(data, offset);
            offset += 12;

            weight = Utils.ReadFloat(data, offset);
            offset += 4;

            // TODO: Handle Quaternions if the option was set
            rotation = ReadVector(data, offset);
            offset += 12;

            //lookAt = ReadVector(data, offset);
            //offset += 12;
        }

        // TODO: Move to Utils
        private TVector3 ReadVector(ReadOnlySpan<byte> data, int offset)
        {
            return MemoryMarshal.Cast<byte, TVector3>(data.Slice(offset))[0];
        }
    }
}