using RTSWorld.Presentation.Environment;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    internal sealed partial class VisualPrototypeLibrary
    {
        private const int EnvironmentForms = (int)EnvironmentForm.ShoreStone + 1;
        private const int ArtEnvironmentKinds = EnvironmentForms + (int)EnvironmentForm.Pebble;

        internal static int ArtKind(EnvironmentArtPlacement placement) => LegacyEnvironmentKinds + (int)placement.Form +
            (placement.Alternate && placement.Form < EnvironmentForm.Pebble ? EnvironmentForms : 0);

        internal static bool IsGroundCover(int kind) =>
            kind >= LegacyEnvironmentKinds + (int)EnvironmentForm.Shrub && kind < LegacyEnvironmentKinds + EnvironmentForms;

        private Mesh EnvironmentArtMesh(int kind, int lod)
        {
            int index = kind - LegacyEnvironmentKinds;
            var form = (EnvironmentForm)(index % EnvironmentForms);
            bool alternate = index >= EnvironmentForms;
            var geometry = new Geometry(palette.width);
            if (form < EnvironmentForm.Pebble) BuildTree(geometry, form, lod, alternate);
            else if (form < EnvironmentForm.Shrub) BuildRock(geometry, form, lod);
            else BuildGroundCover(geometry, form, lod);
            return geometry.Build($"{form}{(alternate ? " B" : "")} LOD{lod}");
        }

        private static void BuildTree(Geometry g, EnvironmentForm form, int lod, bool alternate)
        {
            bool evergreen = form is EnvironmentForm.Pine or EnvironmentForm.Spire;
            bool dead = form is EnvironmentForm.Snag or EnvironmentForm.Dead;
            float height = form switch
            {
                EnvironmentForm.Oak => 4.55f, EnvironmentForm.Rounded => 3.85f,
                EnvironmentForm.Crooked => 4.95f, EnvironmentForm.Pine => 5.3f,
                EnvironmentForm.Spire => 6.15f, EnvironmentForm.Dead => 3.55f,
                EnvironmentForm.Sparse => 3.3f, _ => 1.6f
            };
            float spread = form switch
            {
                EnvironmentForm.Oak => 1.5f, EnvironmentForm.Rounded => 1.22f,
                EnvironmentForm.Crooked => 1.4f, EnvironmentForm.Pine => 1.21f,
                EnvironmentForm.Spire => .78f, _ => .60f
            };
            Vector3 bend = new(form == EnvironmentForm.Crooked ? -.46f : .13f, height * .47f, .10f);
            Vector3 crown = bend * .65f + Vector3.up * (height * .62f);
            int sides = 6 - lod;
            var bark = alternate ? Paint.BarkLight : Paint.BarkDark;
            g.Beam(Vector3.zero, bend, .23f, .105f, bark, sides);
            g.Beam(bend, crown, .11f, .023f, Paint.Oak, sides);
            if (lod == 0)
            {
                g.Beam(new Vector3(-.18f,0,.08f), Vector3.up * .47f, .065f,.034f, bark,4);
                g.Beam(new Vector3(.13f,0,-.12f), Vector3.up * .40f, .06f,.03f, Paint.Oak,4);
            }
            if (form == EnvironmentForm.Snag)
            {
                g.Beam(bend, bend + new Vector3(.52f,.38f,-.26f),.085f,.040f,Paint.BarkLight,4);
                return;
            }
            if (evergreen)
            {
                int tiers = lod == 2 ? 3 : 4;
                for (int i = 0; i < tiers; i++)
                {
                    float phase = i / (float)(tiers-1);
                    float width = spread * (1 - phase*.70f);
                    var shoulder = new Vector3(.07f*i, height * (.29f + phase*.49f), -.05f*i);
                    g.FacetedProfile(shoulder, new Vector3(width, height*.22f, width*.85f),
                        7-lod*2, i + (int)form, alternate ? Paint.PineLight : Paint.PineDark,
                        i % 2 == 0 ? Paint.Fir : Paint.PineLight, true);
                }
                return;
            }
            int lobes = dead ? (lod == 2 ? 2 : 4) :
                form == EnvironmentForm.Sparse ? 3 : lod == 2 ? 4 : lod == 1 ? 5 : 7;
            for (int i = 0; i < lobes; i++)
            {
                float angle = i * 2.39996f + (int)form * .6f;
                float reach = spread * (i == 0 ? .21f : .58f + (i%3)*.14f);
                float lift = height * (i == 0 ? .20f : -.07f + (i%3)*.066f);
                var tip = crown + new Vector3(Mathf.Cos(angle)*reach, lift, Mathf.Sin(angle)*reach*.85f);
                if (lod < 2 || dead)
                    g.Beam(Vector3.Lerp(bend, crown, i == 0 ? .62f : .18f), tip,
                        i == 0 ? .064f : .075f, .016f, i % 2 == 0 ? bark : Paint.BarkLight, 4);
                if (dead)
                {
                    if (lod == 0)
                        g.Beam(Vector3.Lerp(bend, tip,.85f), tip + new Vector3(.16f,.37f,-.2f),
                            .036f, .008f, Paint.BarkLight, 3);
                    continue;
                }
                float width = spread * (i == 0 ? .69f : .51f + .095f*(i%3));
                var foliage = alternate ? Paint.Olive : form == EnvironmentForm.Crooked ? Paint.Willow : Paint.Sage;
                var dark = form == EnvironmentForm.Sparse ? Paint.Moss : Paint.Fern;
                g.FacetedProfile(tip, new Vector3(width,width*(form == EnvironmentForm.Rounded ? 1.18f : .83f),width*.88f),
                    6-lod, i+3*(int)form, dark, foliage, false);
            }
        }

        private static void BuildRock(Geometry g, EnvironmentForm form, int lod)
        {
            Vector3 size = form switch
            {
                EnvironmentForm.GoldNode => new(.72f,.48f,.81f),
                EnvironmentForm.Pebble => new(.64f,.38f,.57f), EnvironmentForm.Boulder => new(.84f,.75f,.76f),
                EnvironmentForm.Crag => new(.76f,1.1f,.66f), EnvironmentForm.Slab => new(.98f,.35f,.68f),
                _ => new(.83f,.78f,.77f)
            };
            g.FacetedProfile(new Vector3(-.06f,.16f,0), size, 7-lod*2, (int)form*17,
                Paint.Flint, form == EnvironmentForm.Slab ? Paint.Lichen : Paint.Granite, false);
            if (form == EnvironmentForm.GoldNode)
            {
                g.FacetedProfile(new Vector3(.16f,.36f,-.10f), new Vector3(.12f,.20f,.45f),
                    4,39,Paint.Soil,Paint.Amber,false);
                g.FacetedProfile(new Vector3(-.49f,.20f,.26f), new Vector3(.13f,.12f,.23f),
                    4,53,Paint.Flint,Paint.Amber,false);
            }
            if (form is EnvironmentForm.Pebble or EnvironmentForm.Slab or EnvironmentForm.Outcrop)
                g.FacetedProfile(new Vector3(.50f,.06f,-.28f), size*.54f, 5-lod, (int)form+8,
                    Paint.Flint, Paint.Lichen, false);
            if (form == EnvironmentForm.Outcrop && lod < 2)
                g.FacetedProfile(new Vector3(-.42f,.04f,.42f), size*.58f, 5, 54,
                    Paint.Granite, Paint.Moss, false);
        }

        private static void BuildGroundCover(Geometry g, EnvironmentForm form, int lod)
        {
            if (form == EnvironmentForm.ShoreStone)
            {
                g.FacetedProfile(new Vector3(0,.05f,0), new Vector3(.23f,.15f,.21f),
                    5, 28, Paint.Flint, Paint.Lichen, false);
                g.FacetedProfile(new Vector3(.24f,.01f,.10f), new Vector3(.15f,.11f,.14f),
                    4, 31, Paint.Moss, Paint.Granite, false);
                return;
            }
            if (form == EnvironmentForm.Shrub)
            {
                g.FacetedProfile(new Vector3(0,.22f,0),new Vector3(.33f,.24f,.23f),5,17,Paint.Fern,Paint.Sage,false);
                g.FacetedProfile(new Vector3(.23f,.16f,.12f),new Vector3(.24f,.17f,.20f),4,10,Paint.Willow,Paint.Olive,false);
                return;
            }

            int blades = lod == 0 ? 6 : 3;
            for (int i = 0; i < blades; i++)
            {
                float angle = i * 2.39996f;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var side = new Vector3(-direction.z,0,direction.x);
                float height = (form == EnvironmentForm.Reed ? .58f : .30f) * (.68f + (i%3)*.16f);
                float reach = form == EnvironmentForm.Fern ? .44f : .25f;
                var foot = direction * .07f;
                var knee = direction * (.55f * reach) + Vector3.up * (height*.76f);
                var tip = direction * reach + Vector3.up * height;
                var paint = form == EnvironmentForm.DryGrass ? Paint.Straw : i % 2 == 0 ? Paint.Sage : Paint.Grass;
                g.Leaf(foot, knee, tip, side * (form == EnvironmentForm.Fern ? .055f : .024f), paint);
                if (form is EnvironmentForm.Flower or EnvironmentForm.Reed && i < 3)
                    g.Oval(tip, new Vector3(.046f,.032f,.042f), form == EnvironmentForm.Reed ? Paint.Straw :
                        i%2 == 0 ? Paint.Blossom : Paint.Lavender, 4);
            }
        }
    }
}
