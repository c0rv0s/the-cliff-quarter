using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    // Flat-shaded surfaces share vertices only within each face, preserving crisp folds.
    internal sealed class QuarterForms
    {
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<int> indices = new();
        public Color ink;
        public float wind;
        public QuarterForms(Color color) { ink = color; }

        public void Face(params Vector3[] points)
        {
            int n = vertices.Count;
            var color = ink.linear; color.a = wind;
            foreach (var p in points) { vertices.Add(p); colors.Add(color); }
            for (int i = 2; i < points.Length; i++) { indices.Add(n); indices.Add(n + i - 1); indices.Add(n + i); }
        }
        public void Block(Vector3 center, Vector3 size)
        {
            var h = size * .5f;
            Vector3 P(int x, int y, int z) => center + new Vector3(x*h.x, y*h.y, z*h.z);
            Face(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));
            Face(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1));
            Face(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));
            Face(P(1,-1,-1),P(1,-1,1),P(-1,-1,1),P(-1,-1,-1));
            Face(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1));
            Face(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
        }
        public void Silk(Vector3 a, Vector3 b, Vector3 c)
        {
            Face(a,b,c); Face(c,b,a);
        }
        public void Rod(Vector3 a, Vector3 b, float r, float tip, int sides = 8)
        {
            var axis = (b-a).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < .85f ? Vector3.up : Vector3.forward).normalized;
            var v = Vector3.Cross(axis, u);
            for (int i = 0; i < sides; i++)
            {
                Vector3 Rad(int j) => u * Mathf.Cos(j * 2 * Mathf.PI / sides) + v * Mathf.Sin(j * 2 * Mathf.PI / sides);
                var p = Rad(i); var q = Rad(i+1);
                Face(a+p*r, a+q*r, b+q*tip, b+p*tip);
                if (tip > 0) Face(b, b+p*tip, b+q*tip);
                if (r > 0) Face(a, a+q*r, a+p*r);
            }
        }
        public void Lathe(Vector3 p, int sides, params (float y, float radius)[] rings)
        {
            Vector3 At(int j, (float y, float radius) k)
            {
                float a = j * Mathf.PI * 2 / sides;
                return p + new Vector3(Mathf.Cos(a)*k.radius,k.y,Mathf.Sin(a)*k.radius);
            }
            for (int k = 1; k < rings.Length; k++)
                for (int j = 0; j < sides; j++)
                    Face(At(j+1,rings[k-1]),At(j,rings[k-1]),At(j,rings[k]),At(j+1,rings[k]));
        }
        public void Strand(Vector3 a, Vector3 b, float sag, float radius)
        {
            Vector3 prev = a;
            for (int j = 1; j <= 12; j++)
            {
                float t = j/12f;
                var point = Vector3.Lerp(a,b,t) + Vector3.down * (4*sag*t*(1-t));
                Rod(prev,point,radius,radius,5); prev = point;
            }
        }
        public Mesh Bake(string name, string asset)
        {
            Mesh mesh=null;
            foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(asset))
                if(obj is Mesh saved && saved.name==name){mesh=saved;break;}
            bool creating=!mesh;
            if(creating)mesh=new Mesh {name=name};
            else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(.35f);mesh.bounds=bounds;
            mesh.UploadMeshData(false);
            if(creating)AssetDatabase.AddObjectToAsset(mesh,asset);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
