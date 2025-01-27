using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    internal class PZone<T> : PShape<T> where T : struct
    {
        public int presence = 0;
        public float density = 0;
        public float sliderValue = 0;
        public float padX = 0;
        public float padY = 0;

        public delegate void EnterExitEvent(int count);
        public EnterExitEvent enterEvent;
        public EnterExitEvent exitEvent;


        public PZone(BasePleiadesClient client, JSONObject o, PContainer<T> parent) : base(client, o, parent, ContainerType.Zone)
        {
        }

        public virtual void processData(float time, ReadOnlySpan<byte> data, int offset)
        {
            int numEntered = Utils.ReadInt(data, offset);
            if (numEntered > 0 && enterEvent != null) enterEvent.Invoke(numEntered);

            int numExited = Utils.ReadInt(data, offset + 4);
            if (numExited > 0 && exitEvent != null) exitEvent.Invoke(numExited);

            presence = Utils.ReadInt(data, offset + 8);
            density = Utils.ReadFloat(data, offset + 12);

            int extraDataCount = Utils.ReadInt(data, offset + 16);
            int curExtra = 0;
            int extraPos = offset + 20;
            while (curExtra < extraDataCount)
            {
                int extraSize = Utils.ReadInt(data, extraPos);
                byte extraType = data[extraPos + 5];
                switch (extraType)
                {
                    case 0: //slider
                        {
                            sliderValue = Utils.ReadFloat(data, extraPos + 6);
                        }
                        break;

                    case 1:
                        {
                            padX = Utils.ReadFloat(data, extraPos + 6);
                            padY = Utils.ReadFloat(data, extraPos + 10);
                        }
                        break;

                    case 2: //cloud, to be handled internally
                        processCloudInternal(time, data, extraPos);
                        break;

                }
            }
        }


        protected virtual void processCloudInternal(float time, ReadOnlySpan<byte> data, int offset)
        {
            //to be implemented by derived classes
        }

     
    }
}