using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using static Augmenta.BaseObject;

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

        public delegate void EnterExitEvent(int count);
        public EnterExitEvent enterEvent;
        public EnterExitEvent exitEvent;

        private TVector3[] pointsA = new TVector3[0];
        private int pointCount;
        public ArraySegment<TVector3> points => new ArraySegment<TVector3>(pointsA, 0, pointCount);


        public Zone(BaseClient client, JSONObject o, Container<TVector3> parent) : base(client, o, parent, ContainerType.Zone)
        {
            SetupSliderAxis(o["localSliderAxis"]);
        }

        public virtual void ProcessData(float time, ReadOnlySpan<byte> data, int offset)
        {
            byte numEntered = data[offset];
            if (numEntered > 0 && enterEvent != null) enterEvent.Invoke((int)numEntered);

            byte numExited = data[offset + 1];
            if (numExited > 0 && exitEvent != null) exitEvent.Invoke((int)numExited);

            presence = Utils.ReadInt(data, offset + 2);
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
                        {
                            sliderValue = Utils.ReadFloat(data, extraPos + 5);
                        }
                        break;

                    case 1:
                        {
                            padX = Utils.ReadFloat(data, extraPos + 5);
                            padY = Utils.ReadFloat(data, extraPos + 9);
                        }
                        break;

                    case 2: //cloud, to be handled internally
                        ProcessCloudInternal(time, data, extraPos + 5);
                        break;

                }
                curExtra++;
                extraPos += extraSize;
            }
        }

        protected override void HandleParamUpdateInternal(string prop, JSONObject data)
        {
            base.HandleParamUpdateInternal(prop, data);
            if (prop == "localSliderAxis") SetupSliderAxis(data);
        }


        protected virtual void ProcessCloudInternal(float time, ReadOnlySpan<byte> data, int offset)
        {
            //to be implemented by derived classes
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