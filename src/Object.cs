using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    public abstract class BaseObject
    {
        public int objectID;
        public float lastUpdateTime;

        public float killDelayTime = 0;
        public float timeSinceGhost;
        public bool drawDebug;

        public float weight;
        public bool isCluster;

        public enum PositionUpdateMode { None, Centroid, BoxCenter }
        public PositionUpdateMode posUpdateMode = PositionUpdateMode.Centroid;
        public enum CoordMode { Absolute, Relative }
        public CoordMode pointMode = CoordMode.Relative;
        public enum State { Enter = 0, Update = 1, Leave = 2, Ghost = 3 };
        public State state;

        public delegate void OnRemoveEvent(BaseObject obj);
        public event OnRemoveEvent onRemove;

        public void update(float time)
        {
            if (time - lastUpdateTime > .5f)
                timeSinceGhost = time;
            else
                timeSinceGhost = -1;
        }

        virtual public void updateData(float time, ReadOnlySpan<byte> data, int offset)
        {
            lastUpdateTime = time;
        }

        virtual protected void updateClusterData(ReadOnlySpan<byte> data, int offset)
        {
            {
                state = (State)Utils.ReadInt(data, offset);
                if (state == State.Leave) //Will leave
                {
                    onRemove?.Invoke(this);
                    return;
                }
            }
        }

        virtual public void kill(bool immediate = false) { }
        virtual public void clear()
        {
            state = State.Ghost;
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

        // Update is called once per frame
        public override void updateData(float time, ReadOnlySpan<byte> data, int offset)
        {

            var propertiesCount = Utils.ReadInt(data, offset + 4); //first data is ID (4 bytes)
            var pos = offset + 8;
            while (pos < data.Length)
            {
                var propertySize = Utils.ReadInt(data, pos);
                var propertyID = Utils.ReadInt(data, pos + 4);

                if (propertySize < 0)
                {
                    break;
                }

                var propertyDataPos = pos + 8;

                switch (propertyID)
                {
                    case 0: updatePointsData(data, propertyDataPos); break;
                    case 1:
                        isCluster = true;
                        updateClusterData(data, propertyDataPos); 
                        break;
                }

                pos += propertySize;
            }

            base.updateData(time, data, offset);
        }

        void updatePointsData(ReadOnlySpan<byte> data, int offset)
        {
            pointCount = Utils.ReadInt(data, offset);
            var vectors = Utils.ReadVectors<TVector3>(data, offset + sizeof(int), pointCount * 12);

            if (pointsA.Length < pointCount)
                pointsA = new TVector3[(int)(pointCount * 1.5)];

            vectors.CopyTo(pointsA.AsSpan());
            
            //We're deciding to not use custom transformation here, final client will take care of this
            //if (pointMode == CoordMode.Absolute)
            //else
            //{
            //    for (int i = 0; i < vectors.Length; i++)
            //        updateCloudPoint(ref pointsA[i], vectors[i]);

            //}
        }

        override protected void updateClusterData(ReadOnlySpan<byte> data, int offset)
        {
            base.updateClusterData(data, offset);

            const int numProperties = 4;
            var clusterData = new TVector3[numProperties];
            for (int i = 0; i < 4; i++)
            {
                var si = offset + sizeof(int) + i * 12;

                clusterData[i] = ReadVector(data, si);

                //We're deciding to not use custom transformation here, final client will take care of this
                //if (i == 1) clusterData[i] = p; //don't transform the velocity, it's already in world space
                //else updateClusterPoint(ref clusterData[i], p);

            }

            centroid = clusterData[0];
            velocity = clusterData[1];
            boxCenter = clusterData[2];
            boxSize = clusterData[3];

            int weightDataIndex = offset + 4 + numProperties * 12;
            weight = Utils.ReadFloat(data, weightDataIndex);

            rotation = ReadVector(data, weightDataIndex + 4);

            updateTransform();
        }

        public override void clear()
        {
            base.clear();
            pointCount = 0;
            pointsA = new TVector3[0];

            centroid = default;
            velocity = default;
        }

        abstract protected void updateTransform();

        virtual protected TVector3 ReadVector(ReadOnlySpan<byte> data, int offset)
        {
            return MemoryMarshal.Cast<byte, TVector3>(data.Slice(offset))[0];
        }

    }
}