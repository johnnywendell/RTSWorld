using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RTSWorld.Presentation.Environment;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;

namespace RTSWorld.Presentation.Editor
{
    /// <summary>Explicit, isolated streaming replay. Does not move/save the user's camera or observer.</summary>
    public static class HydrologyDiagnosticSession
    {
        const string Output = "docs/validation/WORLD-004-FIX-003";
        public static string Capture(int version, int cx = -5, int cz = 11, string label = "before", int seed = 7,
            float sea = 38, float horizontal = 2, float vertical = 1.2f, float frameX = 0)
        {
            Directory.CreateDirectory(Output + "/captures");
            var root = new GameObject("Hydrology diagnostic replay") { hideFlags = HideFlags.HideAndDontSave };
            var observer = new GameObject("Diagnostic observer") { hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("Diagnostic camera") { hideFlags = HideFlags.HideAndDontSave };
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/EnvironmentPalette.mat");
            if (material == null) material = UnityEngine.Object.FindObjectsByType<EnvironmentPresentation>()
                .Select(p => p.Material).FirstOrDefault(m => m != null);
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                if (material == null) throw new InvalidOperationException("Environment material not found");
                root.transform.position = new Vector3(frameX,0,0);
                observer.transform.position = root.transform.TransformPoint(new Vector3((cx + .5f)*32*horizontal, 0, (cz + .5f)*32*horizontal));
                var presentation = root.AddComponent<EnvironmentPresentation>(); presentation.Configure(material, sea, version);
                var stream = root.AddComponent<ChunkManager>();
                stream.Configure(observer.transform, material, seed, 32, horizontal, vertical, 2, 8, 50);
                stream.SetEnvironment(presentation);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 24 * horizontal;
                camera.aspect = 1280f/720; camera.nearClipPlane = .3f; camera.farClipPlane = 1000;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.18f,.2f,.24f);
                if (((cx == -5 && cz == 11) || frameX != 0) && horizontal == 2 && vertical == 1.2f)
                    camera.transform.SetPositionAndRotation(new Vector3(-301.94f,153.09f,673.45f),Quaternion.Euler(55,45,0));
                else
                {
                    var focus = observer.transform.position + Vector3.up*50*vertical;
                    var rotation = Quaternion.Euler(55,45,0);
                    float pitch = 55*Mathf.Deg2Rad;
                    float distance = Mathf.Max(150,(100*vertical+camera.orthographicSize*Mathf.Cos(pitch)+3-focus.y)/Mathf.Sin(pitch));
                    camera.transform.SetPositionAndRotation(focus + rotation*Vector3.back*distance,rotation);
                }
                var evidence = new System.Text.StringBuilder();
                evidence.AppendLine("scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+" play="+Application.isPlaying);
                evidence.AppendLine("frame="+root.transform.position+" observerWorld="+observer.transform.position+" observerLocal="+root.transform.InverseTransformPoint(observer.transform.position));
                evidence.AppendLine("seed="+seed+" version="+version+" sea="+sea+" H="+horizontal+" V="+vertical);
                evidence.AppendLine("Replay camera="+camera.transform.position+" rotation="+camera.transform.eulerAngles+" ortho="+camera.orthographicSize+
                    "; zoom24 is explicit replay framing, historical runtime zoom unknown");
                target.Create(); camera.targetTexture = target;
                foreach (EnvironmentGeometryView view in Enum.GetValues(typeof(EnvironmentGeometryView)))
                {
                    presentation.SetGeometryView(view);
                    for (int i=0;i<100;i++) { stream.Step(); if(stream.PendingCount==0 && stream.ActiveCount==25) break; }
                    if (stream.PendingCount!=0 || stream.ActiveCount!=25 || !stream.TryGetMesh(new ChunkCoordinate(cx,cz),out var central))
                        throw new InvalidOperationException("Replay chunks not resident");
                    foreach(var f in root.GetComponentsInChildren<MeshFilter>()) f.gameObject.layer=31;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                    RenderTexture.active=target;
                    var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    try
                    {
                        texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                        File.WriteAllBytes(Output+"/captures/"+label+"-v"+version+"-"+cx+"-"+cz+"-"+view+".png",texture.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                    evidence.AppendLine(view+" center="+stream.Center+" active="+stream.ActiveCount+" pending="+stream.PendingCount+
                        " centralVertices="+central.vertexCount+" coordinates="+string.Join(";",stream.ActiveCoordinates));
                }
                var world=new WorldDefinition(seed);
                var data=new EnvironmentGenerator(world,new EnvironmentDefinition(sea,version)).Generate(new TerrainGenerator().Generate(world,new ChunkCoordinate(cx,cz)));
                foreach(var view in new[]{EnvironmentGeometryView.OriginalTerrain,EnvironmentGeometryView.EnvironmentalTerrain,EnvironmentGeometryView.WaterOnly})
                    File.WriteAllText(Output+"/"+label+"-v"+version+"-"+cx+"-"+cz+"-"+view+".json",JsonUtility.ToJson(EnvironmentGeometryDiagnostics.Capture(data,horizontal,vertical,view)));
                string metrics=Output+"/"+label+"-v"+version+"-"+cx+"-"+cz+"-capture.txt";
                File.WriteAllText(metrics,evidence.ToString());return evidence.ToString();
            }
            finally
            {
                RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(observer);UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public static string MeasureLegacy()
        {
            Directory.CreateDirectory(Output);
            var output=new System.Text.StringBuilder("version,x,z,original,environmental,water,kind,station,crossDistance,axisX,axisZ,longX,longZ,transverseX,transverseZ,waterGradientX,waterGradientZ,longDerivative,crossDerivative,profileLevel,counterfactualTaperedWater\n");
            var world=new WorldDefinition(7);var terrain=new TerrainGenerator();
            var culture=CultureInfo.InvariantCulture;
            foreach(int version in new[]{2,3,4})
            {
                var gen=new EnvironmentGenerator(world,new EnvironmentDefinition(38,version));
                foreach(var p in new[]{new Vector2Int(-154,357),new Vector2Int(-153,357),new Vector2Int(-150,360),new Vector2Int(-148,350),new Vector2Int(64,59),new Vector2Int(64,60)})
                {
                    gen.TryGetRiver(p.x,p.y,out var r);
                    double length=r.Length,ux=(r.BX-r.AX)/length,uz=(r.BZ-r.AZ)/length,nx=-uz,nz=ux;
                    double t=((p.x-r.AX)*ux+(p.y-r.AZ)*uz)/length;
                    double distance=(p.x-r.AX)*nx+(p.y-r.AZ)*nz-Math.Sin(Math.PI*t)*8;
                    double curveSlope=8*Math.PI*Math.Cos(Math.PI*t)/length;
                    double norm=Math.Sqrt(1+curveSlope*curveSlope),lx=(ux+curveSlope*nx)/norm,lz=(uz+curveSlope*nz)/norm;
                    double gx=(gen.Sample(p.x+1,p.y).WaterLevel-gen.Sample(p.x-1,p.y).WaterLevel)/2;
                    double gz=(gen.Sample(p.x,p.y+1).WaterLevel-gen.Sample(p.x,p.y-1).WaterLevel)/2;
                    var s=gen.Sample(p.x,p.y);
                    object[] fields={version,p.x,p.y,terrain.SampleElevation(world,p.x,p.y),s.Elevation,s.WaterLevel,s.Water,t,distance,ux,uz,lx,lz,nx,nz,gx,gz,gx*lx+gz*lz,gx*nx+gz*nz,r.LevelAt(t),Math.Max(38,r.LevelAt(t)-Math.Max(0,Math.Abs(distance)-6))};
                    output.AppendLine(string.Join(",",fields.Select(v=>Convert.ToString(v,culture))));
                }
            }
            File.WriteAllText(Output+"/directional-samples.csv",output.ToString());return output.ToString();
        }
    }
}
