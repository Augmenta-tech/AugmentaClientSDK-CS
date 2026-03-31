using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    public abstract class BaseObject
    {
        public int objectID;
        public bool isCluster;
        internal bool updatedThisFrame = true;

        public enum PositionUpdateMode { None, Centroid, BoxCenter }
        public PositionUpdateMode posUpdateMode = PositionUpdateMode.Centroid;
        public enum CoordMode { Absolute, Relative }
        public CoordMode pointMode = CoordMode.Relative;
        public enum State { Enter = 0, Update = 1, Leave = 2, Ghost = 3 };
        public State state;

        public delegate void OnEnterEvent(BaseObject obj);
        public event OnEnterEvent onEnter;

        public delegate void OnUpdateEvent(BaseObject obj);
        public event OnUpdateEvent onUpdate;

        public delegate void OnLeaveEvent(BaseObject obj);
        public event OnLeaveEvent onLeave;

        virtual internal void UpdateData(float time, ReadOnlySpan<byte> data, int offset)
        {
            updatedThisFrame = true;
        }

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
    }

    public abstract class GenericObject<TVector3> : BaseObject where TVector3 : struct
    {
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

        internal override void UpdateData(float time, ReadOnlySpan<byte> data, int offset)
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

            base.UpdateData(time, data, offset);
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

        virtual protected TVector3 ReadVector(ReadOnlySpan<byte> data, int offset)
        {
            return MemoryMarshal.Cast<byte, TVector3>(data.Slice(offset))[0];
        }
    }
}