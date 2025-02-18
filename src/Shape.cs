using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Augmenta
{
    public class ShapeContainer<TVector3> : Container<TVector3> where TVector3 : struct
    {
        public Shape<TVector3> shape;
        
        public ShapeContainer(BaseClient client, JSONObject o, Container<TVector3> parent, ContainerType type) : base(client, o, parent, type)
        {
            setupShape(o["shape"]);
        }

        protected override void handleParamUpdateInternal(string prop, JSONObject data)
        {
            base.handleParamUpdateInternal(prop, data);
            if (prop == "shape") setupShape(data);
            else if (prop == "shapeParam")
            {
                if (shape != null)
                {
                    foreach (var p in data.keys)
                    {
                        shape.handleParamUpdate(p, data[p]);
                    }
                }
            }
        }

        void setupShape(JSONObject o)
        {
            if (o == null)
                return;

            if (shape != null && shape.isType(o["type"].str))
            {
                foreach (var prop in o.keys) shape.handleParamUpdate(prop, o[prop]);
                return;
            }

            shape = null;



            switch (o["type"].str)
            {
                case "Sphere":
                    shape = new SphereShape<TVector3>(o);
                    break;

                case "Box":
                    shape = new BoxShape<TVector3>(o);
                    break;

                case "Cylinder":
                    shape = new CylinderShape<TVector3>(o);
                    break;

                case "Cone":
                    shape = new ConeShape<TVector3>(o);
                    break;

                case "Mesh":
                    shape = new MeshShape<TVector3>(o);
                    break;
            }
        }
    }

    public class Shape<TVector3> where TVector3 : struct
    {
        public enum ShapeType { Sphere, Box, Cylinder, Cone, Plane, Mesh }
        public static string[] ShapeTypeIds = { "Sphere", "Box", "Cylinder", "Cone", "Plane", "Mesh" };
        public ShapeType shapeType;

        public Shape(JSONObject o, ShapeType st)
        {
            shapeType = st;
        }

        public bool isType(string type)
        {
            return ShapeTypeIds[(int)shapeType] == type;
        }
        virtual public void handleParamUpdate(string prop, JSONObject data) { }
    }

    public class SphereShape<TVector3> : Shape<TVector3> where TVector3 : struct
    {
        public float radius;

        public SphereShape(JSONObject o) : base(o, ShapeType.Sphere)
        {
            radius = o["radius"].f;
        }

        public override void handleParamUpdate(string prop, JSONObject data)
        {
            base.handleParamUpdate(prop, data);
            if (prop == "radius") radius = data.f;
        }

    }

    public class BoxShape<TVector3> : Shape<TVector3> where TVector3 : struct
    {
        public TVector3 size;
        public BoxShape(JSONObject o) : base(o, ShapeType.Box)
        {
            size = Utils.GetVector<TVector3>(o["boxSize"]);
        }

        public override void handleParamUpdate(string prop, JSONObject data)
        {
            base.handleParamUpdate(prop, data);
            if (prop == "boxSize") size = Utils.GetVector<TVector3>(data);
        }
    }

    public class CylinderShape<TVector3> : Shape<TVector3> where TVector3 : struct
    {
        public float radius;
        public float height;
        public CylinderShape(JSONObject o) : base(o, ShapeType.Cylinder)
        {
            radius = o["radius"].f;
            height = o["height"].f;
        }

        public override void handleParamUpdate(string prop, JSONObject data)
        {
            base.handleParamUpdate(prop, data);
            if (prop == "radius") radius = data.f;
            else if (prop == "height") height = data.f;
        }
    }

    public class ConeShape<TVector3> : Shape<TVector3> where TVector3 : struct
    {
        public float radius;
        public float height;

        public ConeShape(JSONObject o) : base(o, ShapeType.Cone)
        {
            radius = o["radius"].f;
            height = o["height"].f;
        }

        public override void handleParamUpdate(string prop, JSONObject data)
        {
            base.handleParamUpdate(prop, data);
            if (prop == "radius") radius = data.f;
            else if (prop == "height") height = data.f;
        }
    }

    public class MeshShape<TVector3> : Shape<TVector3> where TVector3 : struct
    {
        public List<TVector3> points;
        public MeshShape(JSONObject o) : base(o, ShapeType.Mesh)
        {
            points = new List<TVector3>();
            var p = o["points"];
            for (int i = 0; i < p.Count; i++)
                points.Add(Utils.GetVector<TVector3>(p[i]));
        }

        public override void handleParamUpdate(string prop, JSONObject data)
        {
            base.handleParamUpdate(prop, data);
            if (prop == "points")
            {
                points.Clear();
                for (int i = 0; i < data.Count; i++)
                    points.Add(Utils.GetVector<TVector3>(data[i]));
            }
        }
    }
}