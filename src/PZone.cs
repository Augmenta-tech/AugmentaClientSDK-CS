using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using static Augmenta.BasePObject;

namespace Augmenta
{
    internal class PZone<T> : PShape<T> where T : struct
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

        private T[] pointsA = new T[0];
        private int pointCount;
        public ArraySegment<T> points => new ArraySegment<T>(pointsA, 0, pointCount);


        public PZone(BasePleiadesClient client, JSONObject o, PContainer<T> parent) : base(client, o, parent, ContainerType.Zone)
        {
            setupSliderAxis(o["localSliderAxis"]);
        }

        public virtual void processData(float time, ReadOnlySpan<byte> data, int offset)
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
                        processCloudInternal(time, data, extraPos + 5);
                        break;

                }
                curExtra++;
                extraPos += extraSize;
            }
        }

        protected override void handleParamUpdateInternal(string prop, JSONObject data)
        {
            base.handleParamUpdateInternal(prop, data);
            if (prop == "localSliderAxis") setupSliderAxis(data);
        }


        protected virtual void processCloudInternal(float time, ReadOnlySpan<byte> data, int offset)
        {
            //to be implemented by derived classes
            pointCount = Utils.ReadInt(data, offset);

            var vectors = Utils.ReadVectors<T>(data, offset + sizeof(int), pointCount * 12);

            if (pointsA.Length < pointCount)
                pointsA = new T[(int)(pointCount * 1.5)];

            for (int i = 0; i < vectors.Length; i++)
                updateCloudPoint(ref pointsA[i], vectors[i]);

        }


        private void setupSliderAxis(JSONObject data)
        {
            string axisStr = data.str;
            if (axisStr == "x") sliderAxis = 0;
            else if (axisStr == "y") sliderAxis = 1;
            else if (axisStr == "z") sliderAxis = 2;
        }

    }
}