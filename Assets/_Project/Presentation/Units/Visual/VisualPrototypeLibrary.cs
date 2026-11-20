using System;
using System.Collections.Generic;
using RTSWorld.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Units.Presentation
{
    public enum VisualArchetype { Villager, Soldier }
    public enum VisualBackend { Legacy, Instanced, MeshRenderer, Hidden }

    internal sealed partial class VisualPrototypeLibrary : IDisposable
    {
        internal const int Poses = 6;
        internal const int UnitMeshes = 2 * 2 * (Poses + 1);
        internal const int TreeStart = UnitMeshes;
        internal const int LegacyEnvironmentKinds = 4;
        internal const int EnvironmentKinds = LegacyEnvironmentKinds + ArtEnvironmentKinds;
        internal const int EnvironmentLods = 3;
        internal const int Depot = TreeStart + EnvironmentKinds * EnvironmentLods;
        internal const int Hall = Depot + 1;
        internal const int Foundation = Hall + 1;
        internal readonly Mesh[] Meshes = new Mesh[Foundation + 1];
        internal readonly Material Material;
        private readonly Texture2D palette;

        private enum Paint
        {
            Skin, Linen, Fur, Leather, Iron, Steel, Ash, Oak, Wall, Slate, Tile,
            Leaf, Fern, Fir, Rock, Ally, Enemy, Soil, Sand, Snow, Water, Grass,
            BarkLight, BarkDark, Sage, Olive, Willow, PineDark, PineLight, Moss, Flint, Granite, Lichen, Straw, Blossom, Lavender, Amber
        }

        internal VisualPrototypeLibrary(Material materialTemplate)
        {
            string[] colors = { "BC8764", "D5C6A2", "695A43", "49352B", "505963", "969B96", "997347",
                "654731", "CEBD93", "53646D", "9E5645", "627C39", "7F8D46", "405E41", "797C74",
                "356C81", "AA4E45", "79614B", "C7B184", "E0E3E1", "538A98", "71895B",
                "876648", "504031", "718356", "89934A", "657640", "375646", "5D7760",
                "6E7350", "646969", "8D8981", "A5A28B", "B5A06E", "E2D5A4", "AB839D", "CFA754" };
            palette = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false)
                { name = "Paleta medieval", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < colors.Length; i++)
            {
                ColorUtility.TryParseHtmlString("#" + colors[i], out Color color);
                palette.SetPixel(i, 0, color);
            }
            palette.Apply(false, true);
            Material = materialTemplate != null ? new Material(materialTemplate) :
                new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Material.name = "Paleta medieval URP";
            Material.enableInstancing = true;
            Material.SetTexture("_BaseMap", palette);
            Material.SetFloat("_Smoothness", .18f);
            for (int kind = 0; kind < 2; kind++)
            for (int team = 0; team < 2; team++)
            for (int pose = 0; pose <= Poses; pose++)
                Meshes[UnitMesh((VisualArchetype)kind, team, pose)] = Character(kind == 1, team == 0, pose);
            for (int lod = 0; lod < EnvironmentLods; lod++)
            {
                for (int kind = 0; kind < 3; kind++)
                    Meshes[EnvironmentMesh(kind, lod)] = Tree(kind, lod);
                Meshes[EnvironmentMesh(3, lod)] = Boulder(lod);
                for (int kind = LegacyEnvironmentKinds; kind < EnvironmentKinds; kind++)
                    Meshes[EnvironmentMesh(kind, lod)] = EnvironmentArtMesh(kind, lod);
            }
            Meshes[Depot] = Building(false);
            Meshes[Hall] = Building(true);
            var foundation = new Geometry(palette.width);
            foundation.Box(Vector3.up * .5f, Vector3.one, Paint.Rock);
            Meshes[Foundation] = foundation.Build("Fundação");
        }

        internal static int EnvironmentMesh(int kind, int lod) => TreeStart + lod * EnvironmentKinds + kind;

        internal static int UnitMesh(VisualArchetype kind, int owner, int pose) =>
            ((int)kind * 2 + (owner == 0 ? 0 : 1)) * (Poses + 1) + pose;

        private Mesh Character(bool soldier, bool ally, int pose)
        {
            var b = new Geometry(palette.width);
            bool far = pose == Poses;
            float phase = pose < 2 || far ? 0 : Mathf.Sin((pose - 2) * Mathf.PI * .5f);
            float rise = pose == 1 ? .02f : 0;
            Paint team = ally ? Paint.Ally : Paint.Enemy;
            Paint top = soldier ? Paint.Iron : Paint.Linen;
            b.Oval(new Vector3(0, 1.07f + rise, 0), new Vector3(.25f, .31f, .135f), top, 6);
            b.Beam(new Vector3(0,.68f,0), new Vector3(0,1.03f,0), .23f,.20f,
                soldier ? Paint.Iron : team, 6, .68f);
            b.Beam(new Vector3(0,.80f,0),new Vector3(0,.88f,0), .23f,.23f,Paint.Leather,6,.71f);
            b.Oval(new Vector3(0,1.48f + rise,.018f),new Vector3(.117f,.153f,.115f),Paint.Skin,6);
            b.Oval(new Vector3(0,1.58f + rise,-.004f),new Vector3(.131f,.095f,.129f),
                soldier ? Paint.Steel : Paint.Fur,6);
            if (!far)
            {
                b.Beam(new Vector3(0,1.53f+rise,0),new Vector3(0,1.57f+rise,0),.13f,.14f,
                    soldier ? Paint.Iron : Paint.Linen,6);
                b.Box(new Vector3(0,1.44f+rise,.121f),new Vector3(.058f,.069f,.052f),Paint.Skin);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float gait = phase * side;
                var hip = new Vector3(side * .107f,.77f,0);
                var knee = new Vector3(side * .115f,.39f,.14f * gait);
                var ankle = new Vector3(side * .12f,.09f,.22f * gait);
                b.Beam(hip,knee,.10f,.079f,Paint.Fur,5);
                b.Beam(knee,ankle,.078f,.063f,Paint.Leather,5);
                b.Box(ankle + new Vector3(0,0,.037f),new Vector3(.145f,.14f,.228f),Paint.Leather);
                var shoulder = new Vector3(side * .22f,1.24f+rise,0);
                var elbow = new Vector3(side * .29f, .99f, -.10f * gait);
                var hand = new Vector3(side * .28f, .80f, -.16f * gait + .045f);
                b.Beam(shoulder,elbow,.10f,.076f,soldier ? Paint.Steel : top,5);
                b.Beam(elbow,hand,.078f,.059f,Paint.Skin,5);
                if (side > 0 && !far)
                {
                    b.Beam(hand-new Vector3(0,.24f,.02f),hand+new Vector3(0,.60f,.05f),.023f,.021f,Paint.Ash,5);
                    if (soldier)
                        b.Oval(hand+new Vector3(0,.68f,.05f),new Vector3(.052f,.17f,.025f),Paint.Steel,4);
                    else
                        b.Box(hand+new Vector3(.02f,.58f,.05f),new Vector3(.20f,.072f,.087f),Paint.Iron);
                }
                if (side < 0 && soldier)
                {
                    var shield = hand + new Vector3(-.025f,.13f,.10f);
                    b.Oval(shield,new Vector3(.24f,.29f,.068f),Paint.Steel,6);
                    b.Oval(shield + Vector3.forward * .034f,new Vector3(.208f,.252f,.051f),team,6);
                    if (!far) b.Oval(shield+Vector3.forward*.08f,new Vector3(.066f,.07f,.032f),Paint.Steel,5);
                }
            }
            if (!soldier)
                b.Box(new Vector3(0,.84f,.128f),new Vector3(.235f,.51f,.047f),team);
            return b.Build(soldier ? "Soldado" : "Aldeão");
        }

        private Mesh Tree(int kind, int lod)
        {
            var b = new Geometry(palette.width);
            float height = kind == 2 ? 4.1f : 2.8f;
            b.Beam(Vector3.zero,new Vector3(.08f,height*.79f,0),.15f,.061f,Paint.Oak,6-lod);
            if (kind == 1)
            {
                for (int i = 0; i < 3; i++)
                    b.Beam(Vector3.up * (height * (.30f + .19f*i)),Vector3.up*(height*(.77f+.14f*i)),
                        .70f-.14f*i,.02f,i==0?Paint.Fir:Paint.Leaf,7-lod);
            }
            else
            {
                var centers = new[] { new Vector3(-.53f,.68f,.09f),new Vector3(.44f,.78f,.02f),
                    new Vector3(.06f,.88f,-.38f),new Vector3(.04f,.97f,.17f) };
                for (int i = 0; i < centers.Length; i++)
                {
                    var center = centers[i];
                    center.y *= height;
                    if (kind == 2) center.x *= 1.35f;
                    if (lod == 0) b.Beam(Vector3.up * (height * .45f),center,.073f,.030f,Paint.Oak,5);
                    float width = (kind == 2 ? .96f : .73f) - i * .045f;
                    b.Oval(center,new Vector3(width,width*.87f,width*.85f),i % 2 == 0 ? Paint.Leaf : Paint.Fern,7-lod*2);
                }
            }
            return b.Build(new[] { "Carvalho", "Conífera", "Carvalho alto" }[kind]);
        }

        private Mesh Boulder(int lod)
        {
            var b = new Geometry(palette.width);
            b.Oval(new Vector3(0,.34f,0),new Vector3(.67f,.50f,.48f),Paint.Rock,7-lod*2);
            b.Oval(new Vector3(.35f,.16f,-.11f),new Vector3(.31f,.23f,.29f),Paint.Slate,5-lod);
            return b.Build("Rocha facetada");
        }

        private Mesh Building(bool hall)
        {
            var b = new Geometry(palette.width);
            float width = hall ? 4.9f : 3.3f;
            float depth = hall ? 4.6f : 3.1f;
            float eaves = hall ? 2.7f : 1.9f;
            b.Box(new Vector3(0,.16f,0),new Vector3(width+.16f,.42f,depth+.12f),Paint.Rock);
            b.Box(new Vector3(0,eaves*.5f,0),new Vector3(width,eaves,depth),hall?Paint.Wall:Paint.Ash);
            b.Roof(width+.34f,depth+.38f,eaves,eaves+(hall?1.5f:1.15f),hall?Paint.Tile:Paint.Slate);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(side*(width*.5f-.09f),eaves*.5f,depth*.5f+.025f),
                    new Vector3(.18f,eaves,.16f),Paint.Oak);
                b.Box(new Vector3(side*(width*.32f),eaves*.66f,depth*.5f+.031f),
                    new Vector3(.36f,.52f,.14f),Paint.Oak);
                b.Box(new Vector3(side*(width*.32f),eaves*.66f,depth*.5f+.109f),
                    new Vector3(.066f,.49f,.054f),Paint.Linen);
            }
            b.Box(new Vector3(0,eaves*.91f,depth*.5f+.04f),new Vector3(width,.17f,.15f),Paint.Oak);
            b.Box(new Vector3(0,.73f,depth*.5f+.08f),new Vector3(hall?.93f:.86f,1.46f,.19f),Paint.Oak);
            b.Box(new Vector3(0,.68f,depth*.5f+.18f),new Vector3(.065f,1.33f,.040f),Paint.Ash);
            if (hall)
            {
                b.Box(new Vector3(-width*.29f,eaves+1.26f,-depth*.26f),new Vector3(.47f,1.24f,.47f),Paint.Rock);
                b.Box(new Vector3(-width*.29f,eaves+1.91f,-depth*.26f),new Vector3(.57f,.17f,.57f),Paint.Slate);
                b.Box(new Vector3(.66f,1.69f,depth*.5f+.13f),new Vector3(.28f,.71f,.045f),Paint.Ally);
            }
            else
            {
                b.Box(new Vector3(-.86f,.24f,depth*.37f),new Vector3(.36f,.48f,.36f),Paint.Leather);
            }
            return b.Build(hall ? "Centro principal" : "Depósito");
        }

        public void Dispose()
        {
            foreach (var mesh in Meshes) UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(Material);
            UnityEngine.Object.Destroy(palette);
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new(), normals = new();
            private readonly List<Vector2> uv = new();
            private readonly List<int> indices = new();
            private readonly int paletteWidth;
            internal Geometry(int paletteWidth) => this.paletteWidth = paletteWidth;

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Paint paint)
            {
                Vector3 normal = Vector3.Cross(b-a,c-a);
                if (normal.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(normal,inside-a) > 0) { (b,c) = (c,b); normal = -normal; }
                int start = vertices.Count;
                var sample = new Vector2(((int)paint+.5f)/paletteWidth,.5f);
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                for (int i = 0; i < 3; i++) { normals.Add(normal.normalized); uv.Add(sample); indices.Add(start+i); }
            }

            private void Join(Vector3[] a, Vector3[] b, Vector3 inside, Paint paint)
            {
                for (int i = 0; i < a.Length; i++)
                {
                    int next = (i+1)%a.Length;
                    Triangle(a[i],a[next],b[i],inside,paint);
                    Triangle(b[i],a[next],b[next],inside,paint);
                }
            }

            private static Vector3[] Ring(Vector3 center, Vector3 x, Vector3 z, int sides)
            {
                var points = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = 2 * Mathf.PI * i / sides;
                    points[i] = center + x*Mathf.Cos(angle) + z*Mathf.Sin(angle);
                }
                return points;
            }

            internal void Beam(Vector3 a, Vector3 b, float bottom, float top, Paint paint, int sides, float depth = 1)
            {
                Vector3 axis = (b-a).normalized;
                Vector3 x = Vector3.Cross(axis,Mathf.Abs(axis.z)>.9f?Vector3.right:Vector3.forward).normalized;
                Vector3 z = Vector3.Cross(axis,x)*depth;
                var first = Ring(a,x*bottom,z*bottom,sides);
                var last = Ring(b,x*top,z*top,sides);
                var center = (a+b)*.5f;
                Join(first,last,center,paint);
                for (int i = 1; i < sides-1; i++)
                {
                    Triangle(first[0],first[i],first[i+1],center,paint);
                    Triangle(last[0],last[i],last[i+1],center,paint);
                }
            }

            internal void Oval(Vector3 center, Vector3 radius, Paint paint, int sides)
            {
                var bottom = Ring(center - Vector3.up * (.44f*radius.y),Vector3.right*radius.x*.78f,Vector3.forward*radius.z*.78f,sides);
                var top = Ring(center + Vector3.up * (.34f*radius.y),Vector3.right*radius.x,Vector3.forward*radius.z,sides);
                Join(bottom,top,center,paint);
                for (int i = 0; i < sides; i++)
                {
                    int next = (i+1)%sides;
                    Triangle(bottom[i],bottom[next],center-Vector3.up*radius.y,center,paint);
                    Triangle(top[i],top[next],center+new Vector3(.19f*radius.x,radius.y,-.12f*radius.z),center,paint);
                }
            }

            internal void Leaf(Vector3 foot, Vector3 knee, Vector3 tip, Vector3 width, Paint paint)
            {
                // Folded upper surfaces have thickness in projection without a separate material or alpha test.
                var inside = knee - Vector3.up;
                var left = knee - width;
                var right = knee + width;
                knee += Vector3.up * .015f;
                Triangle(foot, left, knee, inside, paint);
                Triangle(foot, knee, right, inside, paint);
                Triangle(knee, left, tip, inside, paint);
                Triangle(knee, tip, right, inside, paint);
            }

            internal void FacetedProfile(Vector3 center, Vector3 radius, int sides, int seed,
                Paint dark, Paint light, bool skirt)
            {
                var low = new Vector3[sides];
                var shoulders = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = 2 * Mathf.PI * i / sides + .33f * seed;
                    float noise = Mathf.Sin((i + 3) * 12.9898f + seed * 4.1414f);
                    var radial = new Vector3(Mathf.Cos(angle)*radius.x, 0, Mathf.Sin(angle)*radius.z);
                    low[i] = center + radial * ((skirt ? 1 : .64f) + noise*.16f)
                        + Vector3.up * radius.y * (-.40f + noise*.17f);
                    shoulders[i] = center + radial * ((skirt ? .57f : .95f) - noise*.14f)
                        + Vector3.up * radius.y * (.24f + noise*.24f);
                }
                var bottom = center + Vector3.down * (radius.y * .72f);
                var top = center + new Vector3(radius.x*.17f,radius.y,-radius.z*.21f);
                for (int i = 0; i < sides; i++)
                {
                    int j = (i+1)%sides;
                    Triangle(low[i],low[j],bottom,center,dark);
                    Triangle(low[i],low[j],shoulders[i],center,i%3 == 0 ? light : dark);
                    Triangle(shoulders[i],low[j],shoulders[j],center,i%3 == 1 ? dark : light);
                    Triangle(shoulders[i],shoulders[j],top,center,light);
                }
            }

            internal void Box(Vector3 center, Vector3 size, Paint paint)
            {
                Vector3 h = size*.5f;
                var a = new[] {center+new Vector3(-h.x,-h.y,-h.z),center+new Vector3(h.x,-h.y,-h.z),
                    center+new Vector3(h.x,-h.y,h.z),center+new Vector3(-h.x,-h.y,h.z)};
                var b = new[] {a[0]+Vector3.up*size.y,a[1]+Vector3.up*size.y,a[2]+Vector3.up*size.y,a[3]+Vector3.up*size.y};
                Join(a,b,center,paint);
                Triangle(a[0],a[1],a[2],center,paint); Triangle(a[0],a[2],a[3],center,paint);
                Triangle(b[0],b[1],b[2],center,paint); Triangle(b[0],b[2],b[3],center,paint);
            }

            internal void Roof(float width, float length, float eaves, float ridge, Paint paint)
            {
                Vector3 a = new(-width/2,eaves,-length/2), b = new(width/2,eaves,-length/2), c = new(0,ridge,-length/2);
                Vector3 far = Vector3.forward * length;
                var inside = new Vector3(0,(eaves+ridge)*.5f,0);
                Join(new[] {a,b,c},new[] {a+far,b+far,c+far},inside,paint);
                Triangle(a,b,c,inside,Paint.Ash);
                Triangle(a+far,b+far,c+far,inside,Paint.Ash);
            }

            internal Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
