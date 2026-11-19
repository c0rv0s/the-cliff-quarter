using UnityEditor;
using UnityEngine;

namespace TinyWorld.EditorArt
{
    public static class QuarterDoorArt
    {
        const string Asset = "Assets/Geometry/Discoveries/Hinged doors.asset";
        public static void Place(Transform parent, Matrix4x4 basis, Vector3 sill, float yaw, string name, float width, float height, Color paint, bool bronze)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Geometry/Discoveries")) AssetDatabase.CreateFolder("Assets/Geometry", "Discoveries");
            if (!AssetDatabase.LoadMainAssetAtPath(Asset)) AssetDatabase.CreateAsset(new Mesh { name = "Hinged doors" }, Asset);
            var root = new GameObject(name).AddComponent<QuarterSwingDoor>();
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(basis.MultiplyPoint3x4(sill), basis.rotation * Quaternion.Euler(0, yaw, 0));
            root.width = width; root.height = height;
            foreach (int side in new[] { -1, 1 })
            {
                var leaf = new GameObject(side < 0 ? "Left leaf" : "Right leaf", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider)).transform;
                leaf.SetParent(root.transform, false); leaf.localPosition = Vector3.right * (side * width / 2);
                if (side < 0) root.left = leaf; else root.right = leaf;
                float span = width * .5f - .025f;
                var g = new QuarterForms(paint);
                Vector3 V(float x, float y, float z) => new Vector3(-side * x, y, z);
                int boards = bronze ? 4 : 6;
                for (int i = 0; i < boards; i++)
                {
                    g.ink = Color.Lerp(paint, Color.white, (i % 3) * .026f);
                    g.Block(V(span * (i + .5f) / boards, height / 2, 0), new Vector3(span / boards - .009f, height - .06f, .12f));
                }
                for (int face = -1; face <= 1; face += 2)
                {
                    g.ink = bronze ? Palette.Brass : Palette.Limestone;
                    foreach (float h in new[] { .17f, .8f }) g.Block(V(span / 2, height * h, face * .085f), new Vector3(span, .105f, .06f));
                    if (!bronze) g.Rod(V(.06f, height * .19f, face * .103f), V(span - .06f, height * .78f, face * .103f), .072f, .072f, 4);
                    else for(int i=0;i<3;i++) g.Block(V(span*.5f, height*(.34f+i*.13f), face*.076f),new Vector3(span*.61f,.055f,.042f));
                    g.ink = Palette.Brass; g.Rod(V(span-.23f,1.04f,face*.19f),V(span-.23f,1.3f,face*.19f),.044f,.044f);
                }
                leaf.GetComponent<MeshFilter>().sharedMesh = g.Bake(name + " " + side, Asset);
                leaf.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Quarter/Shutters.mat");
                var box = leaf.GetComponent<BoxCollider>(); box.center=V(span/2,height/2,0); box.size=new Vector3(span,height-.06f,.12f);
            }
        }
    }
}
