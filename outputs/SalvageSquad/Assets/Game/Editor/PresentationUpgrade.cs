using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SalvageSquad.Editor
{
    // Explicit art migrations only. Never run automatically or regenerate gameplay data.
    public static class PresentationUpgrade
    {
        const string Folder = "Assets/Game/Art/Polished";
        static Material teal, cream, ink, gold, rust, track, water, slate, glow;
        static Material palette;
        static Material[] swatches;
        static Mesh round, ball, ring;
        static Font font;

        static void Prepare()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            teal = Material("Enamel Teal", "368F91", .32f);
            cream = Material("Porcelain", "EAD7AD", .28f);
            ink = Material("Rubber Navy", "263746", .18f);
            gold = Material("Brass", "D6A450", .42f);
            rust = Material("Enemy Coral", "CB614A", .25f);
            track = Material("Deck", "79858D", .12f);
            water = Material("Coolant", "334D60", .3f);
            slate = Material("Dock Steel", "4D606C", .24f);
            glow = Material("Hot Core", "FFD890", .25f);
            glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.28f,.14f,.03f));
            swatches=new[]{teal,cream,ink,gold,rust,track,water,slate,glow};
            var atlas=new Texture2D(swatches.Length*8,8,TextureFormat.RGBA32,false);
            for(int y=0;y<8;y++)for(int x=0;x<atlas.width;x++)atlas.SetPixel(x,y,swatches[x/8].color * Mathf.Lerp(.8f,1.05f,y/7f));
            atlas.Apply();string atlasPath=Folder+"/Palette.png";File.WriteAllBytes(atlasPath,atlas.EncodeToPNG());Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(atlasPath);var atlasImport=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
            atlasImport.textureType=TextureImporterType.Default;atlasImport.mipmapEnabled=false;atlasImport.filterMode=FilterMode.Point;
            atlasImport.textureCompression=TextureImporterCompression.Uncompressed;atlasImport.SaveAndReimport();
            palette=Material("Shared Enamel", "FFFFFF", .3f);palette.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));
            palette.EnableKeyword("_EMISSION");palette.SetColor("_EmissionColor",Color.black);
            round = SaveMesh(RoundedBox(), "RoundBox");
            ball = SaveMesh(Ball(), "Ball");
            ring = SaveMesh(Ring(), "Ring");
            font = AssetDatabase.LoadAssetAtPath<Font>(Folder+"/LilitaOne-Regular.ttf");
            if(!font)throw new InvalidOperationException("Licensed display font missing; see OFL.txt.");
        }

        [MenuItem("Salvage Squad/Art/Upgrade Characters")]
        public static void Characters()
        {
            Prepare();
            var scene = EditorSceneManager.OpenScene(SliceSetup.ScenePath);
            var run = Object.FindFirstObjectByType<SliceRun>();
            if (!run) throw new InvalidOperationException("Scrapline run missing; refusing migration.");
            var robot = Robot();
            foreach (var body in run.squad.bodies) Object.DestroyImmediate(body.parent.gameObject);
            for (int i=0; i<run.squad.bodies.Length; i++)
            {
                var root = (Transform)PrefabUtility.InstantiatePrefab(robot, run.squad.transform);
                root.localPosition = SquadController.FormationSlot(i,5); root.localScale=Vector3.one*.8f;
                run.squad.bodies[i]=root.Find("Body");
                run.squad.muzzles[i]=run.squad.bodies[i].Find("Muzzle");
                run.squad.flashes[i]=run.squad.muzzles[i].Find("Muzzle flash");
            }
            run.combat.targetPrefab = Enemies();
            // The premium boss mesh contains its own arms. Keep the run's existing marker/reference.
            while(run.bossCrown.childCount>0) Object.DestroyImmediate(run.bossCrown.GetChild(0).gameObject);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Character art upgraded; gameplay, encounter assets and TrainingRange preserved.");
        }

        public static void Apply()
        {
            Characters(); WorldAndInterface();
        }

        // Batch-only asset preview: full-size silhouettes can be judged independently of crowd scale.
        public static void ExportKitPreview()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Preview export is batch-only; preserve unsaved Editor scenes.");
            // A preview must use saved art, not reimport its atlas in the rendering call.
            round=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/RoundBox.asset");
            track=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Deck.mat");
            ink=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Rubber Navy.mat");
            font=AssetDatabase.LoadAssetAtPath<Font>(Folder+"/LilitaOne-Regular.ttf");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=new GameObject("Preview floor").transform;
            Part("Deck",floor,round,new Vector3(0,-.23f,0),new Vector3(24,.3f,16),track);
            var light=new GameObject("Preview sun").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(48,-35,0);light.intensity=1.15f;light.color=new Color(1,.94f,.82f);light.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.79f,.88f);
            var ranger=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Ranger.prefab");
            for(int i=0;i<2;i++)
            {
                var t=Object.Instantiate(ranger).transform;t.position=new Vector3(-4.8f+i*1.85f,0,0);t.localScale=Vector3.one*1.3f;
                t.rotation=Quaternion.Euler(0,i==0?180:0,0);
            }
            var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/RogueMachine.prefab");
            float[] positions={-1.05f,.9f,2.7f,5.0f};
            string[] names={"RANGER","WIND-UP KEY","SCOUT","HEAVY","CHARGER","CRUSHER"};
            for(int i=0;i<4;i++)
            {
                var t=Object.Instantiate(enemy).transform;t.position=new Vector3(positions[i],i==3?1:.7f,0);
                t.localScale=Vector3.one*(i==3?1.6f:1.25f);t.GetComponent<EnemyVisual>().Configure((EnemyKind)i);
            }
            float[] labelX={-4.8f,-2.95f,-1.05f,.9f,2.7f,5.0f};
            for(int i=0;i<names.Length;i++)WorldText(names[i],null,names[i],new Vector3(labelX[i],.04f,-1.35f),.032f,ink.color);
            WorldText("Title",null,"SALVAGE SQUAD",new Vector3(0,2.8f,1.2f),.115f,ink.color);
            var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.5f;
            camera.transform.position=new Vector3(0,7,-11);camera.transform.LookAt(new Vector3(0,.9f,0));
            camera.backgroundColor=track.color;camera.clearFlags=CameraClearFlags.SolidColor;camera.allowHDR=false;
            var rt=new RenderTexture(1200,620,24);var texture=new Texture2D(1200,620,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,620),0,0);texture.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Art-Kit.png")),texture.EncodeToPNG());
                Debug.Log("Art-Kit.png exported from actual project meshes.");
            }
            finally {RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }

        static Transform Robot()
        {
            var root = new GameObject("Wind-up Ranger").transform;
            var body = new GameObject("Body").transform; body.SetParent(root,false);
            Part("Torso",body,round,new Vector3(0,.58f,0),new Vector3(.64f,.59f,.47f),cream);
            Part("Helmet",body,ball,new Vector3(0,1.02f,.015f),new Vector3(.72f,.5f,.66f),teal);
            Part("Helmet ridge",body,round,new Vector3(0,1.24f,-.02f),new Vector3(.14f,.075f,.44f),gold);
            Part("Visor",body,round,new Vector3(0,1.04f,.3f),new Vector3(.56f,.22f,.13f),ink);
            for(int s=-1;s<=1;s+=2)
            {
                Part("Eye",body,ball,new Vector3(s*.13f,1.06f,.365f),new Vector3(.085f,.09f,.05f),glow);
                Part("Boot",body,round,new Vector3(s*.19f,.16f,.06f),new Vector3(.29f,.31f,.45f),ink);
                Part("Shoulder",body,ball,new Vector3(s*.36f,.69f,.025f),new Vector3(.3f,.31f,.34f),teal);
                Part("Grip",body,ball,new Vector3(s*.21f,.52f,.32f),new Vector3(.2f,.2f,.2f),cream);
                Part("Key wing",body,round,new Vector3(s*.18f,.66f,-.3f),new Vector3(.19f,.14f,.08f),gold);
                Part("Key hole",body,ball,new Vector3(s*.18f,.66f,-.35f),new Vector3(.085f,.065f,.025f),ink);
            }
            Part("Backpack",body,round,new Vector3(0,.63f,-.27f),new Vector3(.43f,.42f,.19f),gold);
            Part("Key hub",body,ball,new Vector3(0,.66f,-.34f),Vector3.one*.16f,gold);
            Part("Antenna",body,round,new Vector3(-.23f,1.31f,-.17f),new Vector3(.04f,.2f,.04f),ink);
            Part("Antenna tip",body,ball,new Vector3(-.23f,1.42f,-.17f),Vector3.one*.105f,gold);
            Part("Belt",body,round,new Vector3(0,.35f,-.005f),new Vector3(.63f,.1f,.49f),ink);
            var cannon = new GameObject("Cannon").transform; cannon.SetParent(body,false);
            cannon.localPosition=new Vector3(0,.55f,.36f);
            Part("Barrel",cannon,round,new Vector3(0,0,.12f),new Vector3(.25f,.25f,.61f),ink);
            Part("Collar",cannon,round,new Vector3(0,0,.37f),new Vector3(.33f,.32f,.14f),teal);
            Part("Bore",cannon,round,new Vector3(0,0,.445f),new Vector3(.16f,.16f,.035f),glow);
            Bake(cannon,"RangerCannon",new[]{ink,teal,glow});
            var sleeve=Part("Heavy sleeve",cannon,round,new Vector3(0,0,.31f),new Vector3(.37f,.35f,.21f),gold);sleeve.gameObject.SetActive(false);
            var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(body,false);
            muzzle.localPosition=new Vector3(0,.55f,.85f);
            var flash=Part("Muzzle flash",muzzle,ball,Vector3.zero,new Vector3(.2f,.19f,.33f),glow);
            flash.gameObject.SetActive(false);
            Bake(body,"RangerShell",new[]{cream,teal,ink,gold,glow},cannon,muzzle);
            return Prefab(root,"Ranger");
        }

        static Transform Enemies()
        {
            var forms=new Mesh[4];
            for(int k=0;k<4;k++)
            {
                var model=new GameObject("Enemy model").transform;
                bool heavy=k==1, charger=k==2, boss=k==3;
                float width=boss?1.3f:heavy?1.12f:charger?.72f:.89f;
                Part("Armored hull",model,boss||heavy?round:ball,new Vector3(0,.02f,0),new Vector3(width,boss?1.05f:.78f,.8f),rust);
                Part("Face plate",model,round,new Vector3(0,.1f,-.39f),new Vector3(width*.79f,.3f,.14f),ink);
                for(int s=-1;s<=1;s+=2)
                {
                    Part("Angry eye",model,round,new Vector3(s*width*.2f,.15f,-.475f),new Vector3(width*.2f,.095f,.04f),gold).localRotation=Quaternion.Euler(0,0,s*18);
                    Part("Crawler",model,round,new Vector3(s*width*.47f,-.38f,.02f),new Vector3(.25f,.3f,.87f),ink);
                    for(int z=-1;z<=1;z++) Part("Wheel",model,ball,new Vector3(s*width*.59f,-.4f,z*.26f),new Vector3(.09f,.19f,.19f),gold);
                    if(heavy||boss)
                    {
                        Part("Shoulder armor",model,round,new Vector3(s*width*.49f,.32f,0),new Vector3(.3f,.42f,.65f),rust);
                        if(!boss)
                        {
                            Part("Piston",model,round,new Vector3(s*.68f,-.02f,-.18f),new Vector3(.17f,.65f,.19f),ink);
                            Part("Crusher fist",model,round,new Vector3(s*.68f,-.2f,-.45f),new Vector3(.39f,.42f,.5f),rust);
                            Part("Safety band",model,round,new Vector3(s*.68f,-.03f,-.7f),new Vector3(.35f,.1f,.04f),gold);
                        }
                    }
                }
                Part("Jaw",model,round,new Vector3(0,-.19f,-.41f),new Vector3(width*.64f,.18f,.18f),ink);
                for(int t=-1;t<=1;t++) Part("Tooth",model,round,new Vector3(t*.17f,-.19f,-.51f),new Vector3(.1f,.13f,.04f),gold);
                if(charger)
                {
                    Part("Speed fin",model,round,new Vector3(0,.51f,.09f),new Vector3(.16f,.4f,.46f),gold).localRotation=Quaternion.Euler(-20,0,0);
                    Part("Ram nose",model,round,new Vector3(0,-.24f,-.57f),new Vector3(.59f,.16f,.31f),rust);
                }
                if(heavy||boss)
                {
                    Part("Tank lid",model,round,new Vector3(0,boss?.64f:.49f,.05f),new Vector3(width*.7f,.23f,.56f),ink);
                    Part("Reactor",model,ball,new Vector3(0,boss?.76f:.62f,.05f),new Vector3(.33f,.13f,.34f),gold);
                }
                if(boss)
                {
                    Part("Chimney",model,round,new Vector3(-.4f,.88f,.28f),new Vector3(.2f,.63f,.21f),ink);
                    Part("Chimney rim",model,round,new Vector3(-.4f,1.17f,.28f),new Vector3(.27f,.12f,.28f),gold);
                    Part("Crown center",model,round,new Vector3(0,.88f,-.03f),new Vector3(.14f,.2f,.13f),rust);
                    Part("Chest reactor",model,ball,new Vector3(0,-.03f,-.52f),new Vector3(.23f,.23f,.08f),gold);
                }
                forms[k]=Bake(model,"EnemyForm"+k,new[]{rust,ink,gold});
                Object.DestroyImmediate(model.gameObject);
            }
            var root=new GameObject("Rogue salvage machine").transform;
            var shell=new GameObject("Shell",typeof(MeshFilter),typeof(MeshRenderer)).transform;shell.SetParent(root,false);
            shell.GetComponent<MeshFilter>().sharedMesh=forms[0];shell.GetComponent<Renderer>().sharedMaterial=palette;
            var visual=root.gameObject.AddComponent<EnemyVisual>();visual.shell=shell.GetComponent<MeshFilter>();visual.forms=forms;
            visual.crusherArms=new Transform[2];
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                var arm=new GameObject("Crusher arm "+side).transform;arm.SetParent(root,false);arm.localPosition=new Vector3(side*.86f,.32f,0);
                Part("Piston",arm,round,new Vector3(0,-.34f,-.18f),new Vector3(.17f,.65f,.19f),ink);
                Part("Crusher fist",arm,round,new Vector3(0,-.52f,-.45f),new Vector3(.42f,.45f,.52f),rust);
                Part("Safety band",arm,round,new Vector3(0,-.35f,-.72f),new Vector3(.38f,.1f,.04f),gold);
                Bake(arm,"CrusherArm"+side,new[]{ink,rust,gold});visual.crusherArms[i]=arm;arm.gameObject.SetActive(false);
            }
            return Prefab(root,"RogueMachine");
        }

        [MenuItem("Salvage Squad/Art/Upgrade World and Interface")]
        public static void WorldAndInterface()
        {
            Prepare();var scene=EditorSceneManager.OpenScene(SliceSetup.ScenePath);
            var run=Object.FindFirstObjectByType<SliceRun>();
            run.gatePrefab=Gates();run.combat.projectilePrefab=Bolt();run.combat.burstPrefab=Burst();
            World(run);Interface(run);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("Dock, gates, UI and pooled effects upgraded; encounter balance preserved.");
        }

        static GateRow Gates()
        {
            var root=new GameObject("Power portals").transform;var gate=root.gameObject.AddComponent<GateRow>();
            for(int side=-1;side<=1;side+=2)
            {
                var frame=new GameObject(side<0?"Left choice":"Right choice").transform;frame.SetParent(root,false);frame.localPosition=new Vector3(side*2,0,0);
                var accent=side<0?teal:gold;
                Part("Threshold",frame,round,new Vector3(0,.08f,.06f),new Vector3(3.45f,.18f,.72f),ink);
                Part("Light strip",frame,round,new Vector3(0,.18f,-.21f),new Vector3(3.2f,.09f,.13f),accent);
                for(int p=-1;p<=1;p+=2)
                {
                    Part("Pillar",frame,round,new Vector3(p*1.61f,.69f,.08f),new Vector3(.23f,1.25f,.3f),accent);
                    Part("Pillar foot",frame,round,new Vector3(p*1.61f,.22f,.08f),new Vector3(.32f,.38f,.37f),ink);
                    Part("Cap",frame,ball,new Vector3(p*1.61f,1.49f,.1f),new Vector3(.29f,.29f,.29f),cream);
                }
                var sign=Part("Sign rim",frame,round,new Vector3(0,1.4f,.03f),new Vector3(3.42f,1.3f,.25f),accent);
                sign.localRotation=Quaternion.Euler(32,0,0);
                var inset=Part("Sign enamel",frame,round,new Vector3(0,1.44f,-.11f),new Vector3(3.18f,1.08f,.13f),ink);inset.localRotation=sign.localRotation;
                var label=WorldText("Gate value",frame,"+10",new Vector3(0,1.32f,-.28f),.14f,cream.color);
                var detail=WorldText("Gate category",frame,"RECRUIT",new Vector3(0,1.76f,.09f),.035f,accent.color);
                var strokes=new TextMesh[4];
                for(int i=0;i<4;i++)
                {
                    Vector3 offset=i==0?Vector3.left:i==1?Vector3.right:i==2?Vector3.up:Vector3.down;
                    var stroke=WorldText("Number outline "+i,label.transform,"+10",offset*.035f+Vector3.forward*.012f,.14f,ink.color);
                    stroke.transform.localRotation=Quaternion.identity;strokes[i]=stroke;
                }
                Bake(frame,"GateFrame"+side,new[]{ink,accent,cream},label.transform,detail.transform,inset);
                if(side<0){gate.leftFrame=frame;gate.leftLabel=label;gate.leftDetail=detail;gate.leftPlate=inset.GetComponent<Renderer>();gate.leftStroke=strokes;}
                else{gate.rightFrame=frame;gate.rightLabel=label;gate.rightDetail=detail;gate.rightPlate=inset.GetComponent<Renderer>();gate.rightStroke=strokes;}
            }
            return Prefab(root,"PowerGate").GetComponent<GateRow>();
        }
        static TextMesh WorldText(string name,Transform parent,string value,Vector3 position,float size,Color color)
        {
            var t=new GameObject(name,typeof(TextMesh)).GetComponent<TextMesh>();t.transform.SetParent(parent,false);
            t.transform.localPosition=position;t.transform.localRotation=Quaternion.Euler(32,0,0);
            t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=80;t.characterSize=size;t.color=color;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text=value;t.GetComponent<Renderer>().sharedMaterial=font.material;return t;
        }
        static Transform Bolt()
        {
            var root=new GameObject("Hot salvage bolt").transform;
            Part("Bolt core",root,ball,Vector3.zero,new Vector3(.095f,.085f,.52f),glow);
            return Prefab(root,"HotBolt");
        }
        static Transform Burst()
        {
            var root=new GameObject("Impact bloom").transform;
            Part("Core",root,ball,Vector3.zero,Vector3.one*.32f,glow);
            Part("Ring",root,ring,new Vector3(0,-.3f,0),Vector3.one,gold);
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;
                Part("Chip "+i,root,round,new Vector3(Mathf.Cos(angle),.2f,Mathf.Sin(angle))*.45f,new Vector3(.13f,.15f,.2f),i%2==0?gold:rust);
            }
            return Prefab(root,"ImpactBloom");
        }
        static void World(SliceRun run)
        {
            var old=GameObject.Find("Scrapline scenery");if(old)Object.DestroyImmediate(old);
            var previous=GameObject.Find("Polished dock");if(previous)Object.DestroyImmediate(previous);
            var trackRoot=GameObject.Find("Track").transform;
            var verges=new List<GameObject>();
            foreach(Transform item in trackRoot)
            {
                if(item.name.Contains("verge")){verges.Add(item.gameObject);continue;}
                var renderer=item.GetComponent<Renderer>();if(!renderer)continue;
                renderer.sharedMaterial=item.name=="Runway"?DeckMaterial():item.name.Contains("rail")?ink:slate;
                if(item.name=="Runway"){item.localScale=new Vector3(9.6f,item.localScale.y,120);item.localPosition=new Vector3(0,item.localPosition.y,40);}
                if(item.name.Contains("rail"))item.localPosition=new Vector3(Mathf.Sign(item.localPosition.x)*4.85f,item.localPosition.y,item.localPosition.z);
                if(item.name=="Lane dash")renderer.sharedMaterial=cream;
            }
            foreach(var verge in verges)Object.DestroyImmediate(verge);
            var scenery=new GameObject("Polished dock").transform;
            Part("Coolant canal",scenery,round,new Vector3(0,-.66f,40),new Vector3(160,.3f,160),water);
            for(int side=-1;side<=1;side+=2)
            {
                Part("Service deck",scenery,round,new Vector3(side*6.5f,-.29f,40),new Vector3(3.7f,.55f,120),slate);
                Part("Edge rubber",scenery,round,new Vector3(side*4.83f,.08f,40),new Vector3(.22f,.28f,120),ink);
                Part("Safety rail",scenery,round,new Vector3(side*4.84f,.21f,40),new Vector3(.13f,.09f,120),gold);
                for(int i=0;i<16;i++)
                {
                    float z=i*3.7f-5;bool crate=i%3!=1;
                    if(crate)
                    {
                        Part("Salvage crate",scenery,round,new Vector3(side*5.1f,.32f,z),new Vector3(1.35f,1.15f,1.5f),i%2==0?rust:teal);
                        Part("Crate lid",scenery,round,new Vector3(side*5.1f,.91f,z),new Vector3(1.42f,.17f,1.58f),slate);
                        for(int rib=-1;rib<=1;rib+=2)Part("Crate ribs",scenery,round,new Vector3(side*4.4f,.31f,z+rib*.45f),new Vector3(.08f,.8f,.1f),ink);
                    }
                    else
                    {
                        Part("Dock turbine",scenery,ball,new Vector3(side*5.1f,.48f,z),new Vector3(1.28f,1.25f,1.3f),slate);
                        Part("Turbine cap",scenery,round,new Vector3(side*5.1f,1.08f,z),new Vector3(.68f,.27f,.69f),teal);
                        Part("Intake",scenery,round,new Vector3(side*4.47f,.49f,z),new Vector3(.15f,.52f,.58f),ink);
                    }
                    Part("Dock light",scenery,round,new Vector3(side*4.59f,.25f,z+1.7f),new Vector3(.21f,.32f,.33f),teal);
                    Part("Lamp",scenery,ball,new Vector3(side*4.59f,.46f,z+1.7f),new Vector3(.16f,.12f,.16f),glow);
                    Part("Bollard",scenery,round,new Vector3(side*7.5f,.05f,z+1.3f),new Vector3(.38f,.68f,.4f),gold);
                }
            }
            Bake(scenery,"DockKit",new[]{water,slate,ink,gold,cream,rust,teal,glow});
            var light=GameObject.Find("Sun").GetComponent<Light>();light.color=new Color(1,.92f,.78f);light.intensity=1.3f;
            light.transform.rotation=Quaternion.Euler(42,-35,0);light.shadows=LightShadows.Soft;light.shadowStrength=.8f;
            RenderSettings.ambientLight=new Color(.42f,.48f,.55f);RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=34;RenderSettings.fogEndDistance=70;
            RenderSettings.fogColor=new Color(.31f,.38f,.42f);Camera.main.backgroundColor=RenderSettings.fogColor;
            Camera.main.orthographic=false;Camera.main.usePhysicalProperties=true;
            Camera.main.sensorSize=new Vector2(18,32);Camera.main.gateFit=Camera.GateFitMode.Horizontal;
            Camera.main.focalLength=44;Camera.main.transform.rotation=Quaternion.Euler(32,0,0);
            Camera.main.transform.position=new Vector3(0,.3f,6)-Camera.main.transform.forward*21;
            run.feedback.squad=run.squad;
            run.warning.GetComponent<Renderer>().sharedMaterial=rust;
            foreach(var r in run.warning.GetComponentsInChildren<Renderer>())if(r.transform!=run.warning)r.sharedMaterial=ink;
        }
        static void Interface(SliceRun run)
        {
            var sprite=PanelSprite();
            var header=run.safeArea.Find("Header").GetComponent<Image>();header.sprite=sprite;header.type=Image.Type.Sliced;header.color=ink.color;
            header.rectTransform.anchoredPosition=new Vector2(0,-12);header.rectTransform.sizeDelta=new Vector2(506,88);
            var mission=header.transform.Find("Mission").GetComponent<Text>();mission.text="01   /   SALVAGE DOCK";mission.fontSize=13;mission.color=gold.color;
            mission.rectTransform.anchoredPosition=new Vector2(78,-7);
            run.squadLabel.fontSize=34;run.squadLabel.rectTransform.anchoredPosition=new Vector2(78,-24);run.squadLabel.color=cream.color;
            var emblem=Decoration("Squad insignia",header.transform,IconSprite(false),teal.color,new Vector2(19,-18),new Vector2(47,48));emblem.raycastTarget=false;
            run.upgradeLabel.fontSize=13;run.upgradeLabel.color=new Color(.76f,.83f,.83f);run.upgradeLabel.rectTransform.anchoredPosition=new Vector2(21,-63);
            var bar=run.progress.transform.parent.GetComponent<Image>();bar.sprite=sprite;bar.type=Image.Type.Sliced;bar.color=slate.color;
            bar.rectTransform.anchoredPosition=new Vector2(21,7);bar.rectTransform.sizeDelta=new Vector2(464,6);
            run.progress.sprite=sprite;run.progress.color=teal.color;
            StyleButton(run.pauseButton,sprite,slate.color,cream.color);var pause=run.pauseButton.GetComponent<RectTransform>();
            Place(pause,new Vector2(1,1),new Vector2(-28,-28),new Vector2(47,47),new Vector2(1,1));
            run.pauseLabel.fontSize=19;
            var boss=run.bossBar.transform.parent.GetComponent<Image>();boss.sprite=sprite;boss.type=Image.Type.Sliced;boss.color=ink.color;
            boss.rectTransform.sizeDelta=new Vector2(350,14);boss.rectTransform.anchoredPosition=new Vector2(0,-180);
            run.bossBar.sprite=sprite;run.bossBar.color=rust.color;run.bossLabel.color=cream.color;run.bossLabel.fontSize=20;
            run.bossLabel.alignment=TextAnchor.MiddleCenter;run.bossLabel.rectTransform.anchoredPosition=new Vector2(0,31);
            var card=run.resultPanel.GetComponent<Image>();card.sprite=sprite;card.type=Image.Type.Sliced;card.color=ink.color;
            var shade=Decoration("Result shade",run.safeArea,sprite,new Color(.035f,.055f,.065f,.58f),Vector2.zero,Vector2.zero);
            shade.sprite=null;shade.rectTransform.anchorMin=Vector2.zero;shade.rectTransform.anchorMax=Vector2.one;
            shade.rectTransform.offsetMin=shade.rectTransform.offsetMax=Vector2.zero;
            // Move out first so rerunning the migration cannot swap the shade above the card.
            shade.transform.SetAsLastSibling();shade.transform.SetSiblingIndex(card.transform.GetSiblingIndex());run.resultShade=shade.gameObject;shade.gameObject.SetActive(false);
            card.rectTransform.sizeDelta=new Vector2(458,440);
            var rim=Decoration("Card rim",card.transform,sprite,slate.color,new Vector2(-4,4),new Vector2(466,448));rim.fillCenter=false;rim.pixelsPerUnitMultiplier=4;rim.transform.SetAsFirstSibling();
            Decoration("Reward seal",card.transform,IconSprite(true),gold.color,new Vector2(177,-26),new Vector2(104,104));
            var medal=card.transform.Find("Medal").GetComponent<Text>();medal.fontSize=52;medal.color=cream.color;
            Place(medal.rectTransform,new Vector2(.5f,1),new Vector2(0,-47),new Vector2(100,65),new Vector2(.5f,1));medal.transform.SetAsLastSibling();
            run.resultTitle.fontSize=40;run.resultTitle.color=gold.color;
            Place(run.resultTitle.rectTransform,new Vector2(.5f,1),new Vector2(0,-140),new Vector2(422,65),new Vector2(.5f,1));
            run.resultStats.fontSize=18;run.resultStats.lineSpacing=1.22f;run.resultStats.color=cream.color;
            Place(run.resultStats.rectTransform,new Vector2(.5f,1),new Vector2(0,-212),new Vector2(404,102),new Vector2(.5f,1));
            StyleButton(run.replayButton,sprite,teal.color,cream.color);run.replayButton.GetComponentInChildren<Text>().fontSize=22;
            Place(run.replayButton.GetComponent<RectTransform>(),new Vector2(.5f,1),new Vector2(0,-345),new Vector2(354,66),new Vector2(.5f,1));
            foreach(var button in new[]{run.soundButton,run.motionButton})
            {
                StyleButton(button,sprite,ink.color,cream.color);button.GetComponent<RectTransform>().sizeDelta=new Vector2(126,42);
                button.GetComponentInChildren<Text>().fontSize=12;
            }
            foreach(var text in run.safeArea.GetComponentsInChildren<Text>(true))
            {
                bool display=text.fontSize>=20;
                text.font=display?font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontStyle=FontStyle.Normal;
                var outline=text.GetComponent<Outline>();if(!outline&&display)outline=text.gameObject.AddComponent<Outline>();
                if(outline){outline.enabled=display;outline.effectColor=new Color(.07f,.1f,.13f,.75f);outline.effectDistance=new Vector2(1,-1);outline.useGraphicAlpha=true;}
            }
            run.hint.color=ink.color;run.hint.fontSize=18;
            var existing=run.safeArea.Find("Power popup");
            var popup=existing?existing.GetComponent<Text>():new GameObject("Power popup",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
            popup.transform.SetParent(run.safeArea,false);popup.font=font;popup.fontSize=34;popup.alignment=TextAnchor.MiddleCenter;popup.raycastTarget=false;
            Place(popup.rectTransform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(320,64),new Vector2(.5f,.5f));
            var popupOutline=popup.GetComponent<Outline>();if(!popupOutline)popupOutline=popup.gameObject.AddComponent<Outline>();
            popupOutline.effectColor=ink.color;popupOutline.effectDistance=new Vector2(2,-2);popupOutline.useGraphicAlpha=true;
            run.powerLabel=popup;popup.gameObject.SetActive(false);
            var toast=Decoration("Toast plate",run.safeArea,sprite,new Color(.98f,.94f,.84f,.96f),Vector2.zero,new Vector2(496,44)).transform;
            Place((RectTransform)toast,new Vector2(.5f,1),new Vector2(0,-108),new Vector2(496,38),new Vector2(.5f,1));
            run.hint.transform.SetParent(toast,false);run.hint.rectTransform.anchorMin=Vector2.zero;run.hint.rectTransform.anchorMax=Vector2.one;
            run.hint.rectTransform.offsetMin=run.hint.rectTransform.offsetMax=Vector2.zero;
            run.toastPlate=toast.gameObject;
        }
        static Material DeckMaterial()
        {
            const int size=256;var texture=new Texture2D(size,size,TextureFormat.RGB24,true);
            var random=new System.Random(714);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                bool seam=x%128<2||y%64<2;
                float grain=(float)random.NextDouble()*.035f;
                float shade=seam?.68f:.95f+grain;
                if(x%128==4||y%64==4)shade=1.04f;
                texture.SetPixel(x,y,track.color*shade);
            }
            texture.Apply();string path=Folder+"/DockDeck.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
            var mat=Material("Weathered Deck","FFFFFF",.12f);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetTextureScale("_BaseMap",new Vector2(1,3));return mat;
        }
        static void StyleButton(Button button,Sprite sprite,Color color,Color text)
        {
            var image=button.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.color=color;
            var label=button.GetComponentInChildren<Text>();label.color=text;label.alignment=TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.08f,1.08f,1.08f);
            colors.pressedColor=new Color(.75f,.85f,.9f);colors.fadeDuration=.08f;button.colors=colors;
            var shadow=button.transform.Find("Button depth");
            if(!shadow)
            {
                var depth=Decoration("Button depth",button.transform,sprite,ink.color,Vector2.zero,Vector2.zero);
                Place(depth.rectTransform,new Vector2(.5f,0),Vector2.zero,new Vector2(0,5),new Vector2(.5f,1));
                depth.rectTransform.anchorMin=Vector2.zero;depth.rectTransform.anchorMax=new Vector2(1,0);depth.transform.SetAsFirstSibling();
            }
            else {var depth=shadow.GetComponent<Image>();depth.sprite=sprite;depth.type=Image.Type.Sliced;}
        }
        static Image Decoration(string name,Transform parent,Sprite sprite,Color color,Vector2 pos,Vector2 size)
        {
            var existing=parent.Find(name);var image=existing?existing.GetComponent<Image>():new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.sprite=sprite;image.type=sprite.border==Vector4.zero?Image.Type.Simple:Image.Type.Sliced;
            image.color=color;image.raycastTarget=false;Place(image.rectTransform,new Vector2(0,1),pos,size,new Vector2(0,1));return image;
        }
        static void Place(RectTransform t,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot)
        {t.anchorMin=t.anchorMax=anchor;t.pivot=pivot;t.anchoredPosition=position;t.sizeDelta=size;}
        static Sprite PanelSprite()
        {
            return Sprite("EnamelPanel",64,(x,y)=>
            {
                var q=new Vector2(Mathf.Abs(x-31.5f)-17.5f,Mathf.Abs(y-31.5f)-17.5f);
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-13;
                float alpha=Mathf.Clamp01(.6f-d),shade=Mathf.Lerp(.79f,1,y/63f);
                if(d> -2)shade=Mathf.Min(1,shade+.15f);
                return new Color(shade,shade,shade,alpha);
            },new Vector4(17,17,17,17));
        }
        static Sprite IconSprite(bool seal)
        {
            return Sprite(seal?"GearSeal":"RangerBadge",96,(x,y)=>
            {
                float px=x-47.5f,py=y-47.5f,r=Mathf.Sqrt(px*px+py*py),a=Mathf.Atan2(py,px);
                float shape=seal?39+4*Mathf.Cos(a*12):Mathf.Max(Mathf.Abs(px)-27,Mathf.Abs(py)-22);
                float alpha=seal?Mathf.Clamp01(shape-r):Mathf.Clamp01(8-shape);
                if(!seal && py> -3 && py<8 && Mathf.Abs(px)>6 && Mathf.Abs(px)<18)alpha=0;
                return new Color(1,1,1,alpha);
            },Vector4.zero);
        }
        static Sprite Sprite(string name,int size,Func<int,int,Color> sample,Vector4 border)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=sample(x,y);
            texture.SetPixels(pixels);texture.Apply();string path=Folder+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.spriteBorder=border;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Material Material(string name,string hex,float smooth)
        {
            string path=Folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            ColorUtility.TryParseHtmlString("#"+hex,out var color);mat.color=color;
            mat.SetFloat("_Smoothness",smooth);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static Transform Part(string name,Transform parent,Mesh mesh,Vector3 pos,Vector3 scale,Material material)
        {
            var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);
            t.localPosition=pos;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;
            return t;
        }
        static Transform Prefab(Transform root,string name)
        {
            var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"/"+name+".prefab");
            Object.DestroyImmediate(root.gameObject);return asset.transform;
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            mesh.name=name;var path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh Bake(Transform root,string name,Material[] materials,params Transform[] keep)
        {
            var combined=new List<CombineInstance>();var copies=new List<Mesh>();
            var remove=new List<GameObject>();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                bool skip=false;foreach(var k in keep)if(f.transform==k||f.transform.IsChildOf(k))skip=true;
                if(skip)continue;
                var mat=f.GetComponent<Renderer>().sharedMaterial;int index=Array.IndexOf(materials,mat);
                if(index<0)throw new InvalidOperationException("Material outside shared art palette");
                int swatch=Array.IndexOf(swatches,mat);if(swatch<0)throw new InvalidOperationException("Missing palette swatch");
                var copy=Object.Instantiate(f.sharedMesh);var uv=new Vector2[copy.vertexCount];
                var vertices=copy.vertices;var bounds=copy.bounds;
                for(int i=0;i<uv.Length;i++)uv[i]=new Vector2((swatch+.5f)/swatches.Length,Mathf.Lerp(.08f,.92f,Mathf.InverseLerp(bounds.min.y,bounds.max.y,vertices[i].y)));
                copy.uv=uv;copies.Add(copy);
                combined.Add(new CombineInstance{mesh=copy,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});remove.Add(f.gameObject);
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combined.ToArray());mesh=SaveMesh(mesh,name);
            foreach(var copy in copies)Object.DestroyImmediate(copy);
            foreach(var item in remove)Object.DestroyImmediate(item);
            var shell=new GameObject("Shell",typeof(MeshFilter),typeof(MeshRenderer)).transform;shell.SetParent(root,false);
            shell.GetComponent<MeshFilter>().sharedMesh=mesh;shell.GetComponent<Renderer>().sharedMaterial=palette;return mesh;
        }
        static Mesh RoundedBox()
        {
            var verts=new List<Vector3>();var normals=new List<Vector3>();var tris=new List<int>();
            const int steps=4;const float radius=.16f;
            foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var u=Mathf.Abs(axis.y)>.5f?Vector3.right:Vector3.up;var v=Vector3.Cross(axis,u);int start=verts.Count;
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    var p=axis*.5f+u*((float)x/steps-.5f)+v*((float)y/steps-.5f);
                    var core=new Vector3(Mathf.Clamp(p.x,-.5f+radius,.5f-radius),Mathf.Clamp(p.y,-.5f+radius,.5f-radius),Mathf.Clamp(p.z,-.5f+radius,.5f-radius));
                    var n=(p-core).normalized;verts.Add(core+n*radius);normals.Add(n);
                }
                for(int y=0;y<steps;y++)for(int x=0;x<steps;x++)
                {int a=start+y*(steps+1)+x,b=a+1,c=a+steps+1;tris.AddRange(new[]{a,b,c,b,c+1,c});}
            }
            var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();return mesh;
        }
        static Mesh Ball()
        {
            const int sides=16,rows=10;var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int y=0;y<=rows;y++)for(int x=0;x<=sides;x++)
            {
                float a=y*Mathf.PI/rows,b=x*Mathf.PI*2/sides;var n=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b));
                vertices.Add(n*.5f);normals.Add(n);
            }
            for(int y=0;y<rows;y++)for(int x=0;x<sides;x++)
            {int a=y*(sides+1)+x,b=a+sides+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        static Mesh Ring()
        {
            const int count=32;var vertices=new Vector3[count*2];var normals=new Vector3[count*2];var triangles=new int[count*6];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                vertices[i*2]=direction*.5f;vertices[i*2+1]=direction*.42f;normals[i*2]=normals[i*2+1]=Vector3.up;
                int n=(i+1)%count*2,o=i*6;triangles[o]=i*2;triangles[o+1]=i*2+1;triangles[o+2]=n;
                triangles[o+3]=n;triangles[o+4]=i*2+1;triangles[o+5]=n+1;
            }
            return new Mesh{vertices=vertices,normals=normals,triangles=triangles};
        }
    }
}
