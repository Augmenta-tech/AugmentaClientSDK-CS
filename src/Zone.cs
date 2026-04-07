using System;

namespace Augmenta
{

    public class Zone<TVector3> : ShapeContainer<TVector3> where TVector3 : struct
    {
        public int sliderAxis = 0; // 0 = x, 1 = y, 2 = z
        public int presence = 0;
        public float density = 0;
        public float sliderValue = 0;
        public float padX = 0;
        public float padY = 0;

        private TVector3[] pointsA = new TVector3[0];
        private int pointCount;
        public ArraySegment<TVector3> points => new ArraySegment<TVector3>(pointsA, 0, pointCount);

        public delegate void OnObjectsEnteredEvent(Zone<TVector3> zone, int count);
        public event OnObjectsEnteredEvent onObjectsEntered;

        public delegate void OnObjectsExitedEvent(Zone<TVector3> zone, int count);
        public event OnObjectsExitedEvent onObjectsExited;

        public delegate void OnSliderUpdatedEvent(Zone<TVector3> zone, float sliderValue);
        public event OnSliderUpdatedEvent onSliderUpdated;

        public delegate void OnXYPadUpdatedEvent(Zone<TVector3> zone, float xValue, float yValue);
        public event OnXYPadUpdatedEvent onXYPadUpdated;

        public delegate void OnPresenceUpdatedEvent(Zone<TVector3> zone, int presence);
        public event OnPresenceUpdatedEvent onPresenceUpdated;

        public delegate void OnPointCloudUpdated(Zone<TVector3> zone);
        public event OnPointCloudUpdated onPointCloudUpdated;

        public Zone(Client<TVector3> client, JSONObject o, Container<TVector3> parent) : base(client, o, parent, ContainerType.Zone)
        {
            SetupSliderAxis(o["localSliderAxis"]);
        }

        public virtual void ProcessData(ReadOnlySpan<byte> data, int offset)
        {
            byte numEntered = data[offset];
            if (numEntered > 0)
            {
                onObjectsEntered?.Invoke(this, numEntered);
            }

            byte numExited = data[offset + 1];
            if (numExited > 0)
            {
                onObjectsExited?.Invoke(this, numExited);
            }

            presence = Utils.ReadInt(data, offset + 2);
            onPresenceUpdated?.Invoke(this, presence);

            density = Utils.ReadFloat(data, offset + 6);

            int extraDataCount = Utils.ReadInt(data, offset + 10);
            int curExtra = 0;
            int extraPos = offset + 14;
            while (curExtra < extraDataCount)
            {
                int extraSize = Utils.ReadInt(data, extraPos);
                byte extraType = data[extraPos + 4];
                switch (extraType)
                {
                    case 0: //slider
                        sliderValue = Utils.ReadFloat(data, extraPos + 5);
                        onSliderUpdated?.Invoke(this, sliderValue);
                        break;

                    case 1:
                        padX = Utils.ReadFloat(data, extraPos + 5);
                        padY = Utils.ReadFloat(data, extraPos + 9);
                        onXYPadUpdated?.Invoke(this, padX, padY);
                        break;

                    case 2:
                        ProcessPointCloud(data, extraPos + 5);
                        onPointCloudUpdated?.Invoke(this);
                        break;

                }
                curExtra++;
                extraPos += extraSize;
            }
        }

        internal override void HandleUpdate(JSONObject o)
        {
            if (o.HasField("localSliderAxis")) SetupSliderAxis(o["localSliderAxis"]);
            base.HandleUpdate(o);
        }

        private void ProcessPointCloud(ReadOnlySpan<byte> data, int offset)
        {
            pointCount = Utils.ReadInt(data, offset);

            var vectors = Utils.ReadVectors<TVector3>(data, offset + sizeof(int), pointCount * 12);

            if (pointsA.Length < pointCount)
                pointsA = new TVector3[(int)(pointCount * 1.5)];

            for (int i = 0; i < vectors.Length; i++)
                UpdateCloudPoint(ref pointsA[i], vectors[i]);
        }
        private void SetupSliderAxis(JSONObject data)
        {
            string axisStr = data.str;
            if (axisStr == "x") sliderAxis = 0;
            else if (axisStr == "y") sliderAxis = 1;
            else if (axisStr == "z") sliderAxis = 2;
        }

    }
}