using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace SalvageSquad.Editor
{
    public static class SliceSetup
    {
        public const string ScenePath = "Assets/Scenes/Scrapline.unity";
        static Material teal, cream, dark, orange, yellow, road;
        static Font font;
        const string Folder = "Assets/Game/Slice";
        [MenuItem("Salvage Squad/Create Scrapline Slice")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) { Debug.Log("Scrapline exists; preserving edits."); return; }
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            teal = Load("Teal"); cream = Load("Cream"); dark = Load("Ink"); orange = Load("Scrap"); yellow = Load("Energy"); road = Load("Track");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var scene = EditorSceneManager.OpenScene(PrototypeSetup.ScenePath);
            var old = Object.FindFirstObjectByType<PrototypeRun>();
            var combat = old.combat; var squad = old.squad; var markers = old.trackMarkers;
            Object.DestroyImmediate(old.safeArea.GetComponentInParent<Canvas>().gameObject);
            Object.DestroyImmediate(old.gameObject);
            foreach (var body in squad.bodies) Object.DestroyImmediate(body.parent.gameObject);
            squad.bodies = new Transform[5]; squad.muzzles = new Transform[5]; squad.flashes = new Transform[5];
            Transform robot = Robot();
            for (int i = 0; i < 5; i++)
            {
                Transform root = (Transform)PrefabUtility.InstantiatePrefab(robot, squad.transform);
                root.localPosition = SquadController.FormationSlot(i, 5); root.localScale = Vector3.one * .65f;
                squad.bodies[i] = root.Find("Body"); squad.muzzles[i] = squad.bodies[i].Find("Muzzle");
                squad.flashes[i] = squad.muzzles[i].Find("Muzzle flash");
            }
            combat.targetPrefab = Enemy();
            var run = new GameObject("Scrapline Run").AddComponent<SliceRun>();
            run.squad = squad; run.combat = combat; run.trackMarkers = markers;
            run.level = Level(false); Level(true);
            run.gatePrefab = Gate();
            var camera = Camera.main;
            camera.transform.position = new Vector3(0, 15, -12);
            camera.transform.LookAt(new Vector3(0, 0, 6)); camera.orthographicSize = 10.4f;
            run.feedback = run.gameObject.AddComponent<SliceFeedback>(); run.feedback.view = camera;
            run.warning = Part("Warning lane", null, PrimitiveType.Cube, new Vector3(0,.02f,3.2f), new Vector3(2.65f,.03f,11), orange);
            for (int i = 0; i < 9; i++)
                Part("Warning stripe", run.warning, PrimitiveType.Cube, new Vector3(0,.8f,(i-4)*.105f), new Vector3(.9f,.1f,.018f), dark).localRotation = Quaternion.Euler(0,20,0);
            run.warning.gameObject.SetActive(false);
            run.bossCrown = new GameObject("Crusher arms").transform;
            run.bossCrown.position = new Vector3(0,0,9);
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Piston", run.bossCrown, PrimitiveType.Cylinder, new Vector3(side*1.9f,1,0), new Vector3(.6f,1.1f,.6f), dark);
                Part("Crusher fist", run.bossCrown, PrimitiveType.Cube, new Vector3(side*1.9f,.65f,-.7f), new Vector3(.9f,.9f,1.3f), orange);
                Part("Warning band", run.bossCrown, PrimitiveType.Cube, new Vector3(side*1.9f,1.13f,-.7f), new Vector3(.95f,.09f,.4f), yellow);
            }
            run.bossCrown.gameObject.SetActive(false);
            Environment(); BuildUI(run, camera);
            squad.buttons = new[] { run.replayButton.GetComponent<RectTransform>(), run.pauseButton.GetComponent<RectTransform>(), run.soundButton.GetComponent<RectTransform>(), run.motionButton.GetComponent<RectTransform>() };
            squad.uiCamera = camera;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true), new EditorBuildSettingsScene(PrototypeSetup.ScenePath,true) };
            AssetDatabase.SaveAssets(); Debug.Log("Scrapline created with authored main/alternate data.");
        }
        static EnemySettings EnemyData(string name, EnemyKind kind, int hp, float speed, int damage, float size, Color color)
        {
            var asset = ScriptableObject.CreateInstance<EnemySettings>();
            asset.kind=kind; asset.health=hp; asset.speed=speed; asset.contactDamage=damage; asset.size=size; asset.color=color;
            AssetDatabase.CreateAsset(asset, Folder+"/"+name+".asset"); return asset;
        }
        static LevelDefinition Level(bool alternate)
        {
            EnemySettings swarm, heavy, charger, boss;
            if (!alternate)
            {
                swarm=EnemyData("Swarm",EnemyKind.Swarm,5,2.2f,2,.8f,orange.color);
                heavy=EnemyData("Heavy",EnemyKind.Heavy,90,1.7f,7,1.3f,new Color(.72f,.25f,.13f));
                charger=EnemyData("Charger",EnemyKind.Charger,14,2.4f,4,.7f,yellow.color);
                boss=EnemyData("Crusher",EnemyKind.Boss,2400,1,18,2.4f,orange.color);
            }
            else
            {
                swarm=AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder+"/Swarm.asset"); heavy=AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder+"/Heavy.asset");
                charger=AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder+"/Charger.asset"); boss=AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder+"/Crusher.asset");
            }
            var rows=new List<Encounter>();
            void Wave(float t,EnemySettings e,int n,int lane=0) => rows.Add(new Encounter { time=t,kind=EncounterKind.Wave,enemy=e,count=n,lane=alternate ? -lane : lane });
            void GateAt(float t,GateKind a,float av,GateKind b,float bv) => rows.Add(new Encounter { time=t,kind=EncounterKind.Gate,left=new GateEffect {kind=a,value=av},right=new GateEffect{kind=b,value=bv} });
            Wave(.4f,swarm,1); Wave(2,swarm,1,-1);
            GateAt(2,GateKind.Add,10,GateKind.Add,8);
            Wave(10,swarm,6); GateAt(12,GateKind.Multiply,2,GateKind.Add,15);
            Wave(18,swarm,9); Wave(22,heavy,1,-1);
            GateAt(23,GateKind.Damage,2,GateKind.FireRate,1.5f);
            rows.Add(new Encounter {time=31,kind=EncounterKind.Hazard,lane=alternate ? -1 : 1});
            Wave(31,charger,3); Wave(34,swarm,9);
            GateAt(34,GateKind.Weapon,2,GateKind.Damage,2);
            Wave(41,heavy,1,1); GateAt(42,GateKind.Add,10,GateKind.Multiply,1.5f);
            Wave(46,charger,3); GateAt(49,GateKind.Add,10,GateKind.FireRate,1.5f);
            Wave(50,swarm,12); Wave(53,swarm,15); Wave(56,swarm,12);
            rows.Add(new Encounter {time=61,kind=EncounterKind.Boss,enemy=boss});
            rows.Sort((a,b)=>a.time.CompareTo(b.time));
            var level=ScriptableObject.CreateInstance<LevelDefinition>(); level.title=alternate?"SCRAPLINE / REVERSE SHIFT":"SCRAPLINE / FIRST SHIFT";
            level.encounters=rows.ToArray(); level.deadline=90;
            AssetDatabase.CreateAsset(level,Folder+(alternate?"/ReverseShift.asset":"/FirstShift.asset")); return level;
        }
        static Transform Robot()
        {
            var root=new GameObject("Salvager").transform;
            var body=new GameObject("Body").transform; body.SetParent(root,false);
            Part("Shell",body,PrimitiveType.Capsule,new Vector3(0,.56f,0),new Vector3(.65f,.4f,.53f),cream);
            Part("Helmet",body,PrimitiveType.Sphere,new Vector3(0,.96f,0),new Vector3(.65f,.55f,.57f),teal);
            Part("Visor",body,PrimitiveType.Cube,new Vector3(0,.97f,.24f),new Vector3(.45f,.17f,.12f),dark);
            Part("Eyes",body,PrimitiveType.Cube,new Vector3(0,.98f,.31f),new Vector3(.3f,.06f,.03f),yellow);
            for(int s=-1;s<=1;s+=2)
            {
                Part("Boot",body,PrimitiveType.Cube,new Vector3(s*.2f,.14f,0),new Vector3(.26f,.25f,.46f),dark);
                Part("Shoulder",body,PrimitiveType.Sphere,new Vector3(s*.37f,.57f,.06f),new Vector3(.28f,.3f,.3f),teal);
            }
            Part("Key",body,PrimitiveType.Cube,new Vector3(0,.6f,-.38f),new Vector3(.58f,.09f,.12f),yellow);
            Part("Key hub",body,PrimitiveType.Sphere,new Vector3(0,.6f,-.34f),Vector3.one*.2f,dark);
            var cannon=Part("Cannon",body,PrimitiveType.Cube,new Vector3(0,.59f,.38f),new Vector3(.26f,.25f,.65f),dark);
            Part("Band",body,PrimitiveType.Cube,new Vector3(0,.59f,.63f),new Vector3(.31f,.3f,.1f),teal);
            var muzzle=new GameObject("Muzzle").transform; muzzle.SetParent(body,false); muzzle.localPosition=new Vector3(0,.59f,.75f);
            Part("Muzzle flash",muzzle,PrimitiveType.Sphere,Vector3.zero,new Vector3(.24f,.2f,.3f),yellow).gameObject.SetActive(false);
            Combine(body,"RobotMesh",cannon,muzzle);
            return Save(root,"Salvager");
        }
        static Transform Enemy()
        {
            var root=new GameObject("Scrap drone").transform;
            Part("Hull",root,PrimitiveType.Sphere,Vector3.zero,new Vector3(1,.85f,.85f),orange);
            Part("Jaw",root,PrimitiveType.Cube,new Vector3(0,-.23f,-.35f),new Vector3(.8f,.26f,.3f),dark);
            Part("Eye",root,PrimitiveType.Cube,new Vector3(0,.12f,-.42f),new Vector3(.55f,.15f,.12f),yellow);
            for(int side=-1;side<=1;side+=2)
                Part("Tread",root,PrimitiveType.Capsule,new Vector3(side*.45f,-.35f,0),new Vector3(.28f,.3f,.6f),dark).localRotation=Quaternion.Euler(90,0,0);
            Combine(root,"EnemyMesh"); return Save(root,"ScrapDrone");
        }
        static GateRow Gate()
        {
            var root=new GameObject("Gate row").transform; var gate=root.gameObject.AddComponent<GateRow>();
            for(int side=-1;side<=1;side+=2)
            {
                var frame=new GameObject(side<0?"Left choice":"Right choice").transform; frame.SetParent(root,false); frame.localPosition=new Vector3(side*2,0,0);
                Material mat=side<0?teal:yellow;
                Part("Base",frame,PrimitiveType.Cube,new Vector3(0,.05f,0),new Vector3(3.5f,.1f,.8f),mat);
                for(int p=-1;p<=1;p+=2) Part("Post",frame,PrimitiveType.Cube,new Vector3(p*1.65f,.55f,0),new Vector3(.1f,1.1f,.12f),mat);
                Part("Sign",frame,PrimitiveType.Cube,new Vector3(0,1.2f,0),new Vector3(3.45f,.85f,.15f),dark);
                var label=new GameObject("Gate value",typeof(TextMesh)).GetComponent<TextMesh>(); label.transform.SetParent(frame,false);
                label.transform.localPosition=new Vector3(0,1.27f,-.12f); label.transform.localRotation=Quaternion.Euler(38,0,0);
                label.font=font; label.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.fontSize=64; label.characterSize=.14f; label.color=mat.color; label.text="+10";
                if(side<0) {gate.leftFrame=frame;gate.leftLabel=label;} else {gate.rightFrame=frame;gate.rightLabel=label;}
            }
            return Save(root,"GateRow").GetComponent<GateRow>();
        }
        static void Environment()
        {
            var root=new GameObject("Scrapline scenery").transform;
            for(int i=0;i<10;i++) for(int side=-1;side<=1;side+=2)
            {
                float z=i*3-2;
                Part("Machine housing",root,PrimitiveType.Cube,new Vector3(side*5.25f,.3f,z),new Vector3(1,.9f,1.3f),dark);
                Part("Status lamp",root,PrimitiveType.Cube,new Vector3(side*4.7f,.65f,z),new Vector3(.1f,.16f,.4f),teal);
                Part("Scrap drum",root,PrimitiveType.Cylinder,new Vector3(side*5.7f,.8f,z+1.1f),new Vector3(.65f,.6f,.65f),i%2==0?road:orange);
            }
        }
        static void BuildUI(SliceRun run,Camera camera)
        {
            var canvas=new GameObject("Slice HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(540,960);scaler.matchWidthOrHeight=0;
            run.safeArea=Rect("Safe area",canvas.transform); var safe=run.safeArea;
            var header=Panel("Header",safe,new Color(.055f,.12f,.16f,.96f));Place(header.rectTransform,new Vector2(.5f,1),new Vector2(0,0),new Vector2(540,126),new Vector2(.5f,1));
            Label("Mission",header.transform,"01  /  THE SCRAPLINE",14,teal.color,new Vector2(22,-16),new Vector2(370,22));
            run.squadLabel=Label("Squad count",header.transform,"SQUAD 05",32,cream.color,new Vector2(22,-44),new Vector2(370,42));
            run.upgradeLabel=Label("Upgrades",header.transform,"DMG 1   •   RATE 1×   •   MK 1",12,cream.color,new Vector2(24,-88),new Vector2(390,22));
            var bar=Panel("Run progress",header.transform,dark.color);Place(bar.rectTransform,new Vector2(0,0),new Vector2(22,3),new Vector2(496,5),Vector2.zero);
            run.progress=Fill(bar.transform,teal.color);
            run.pauseButton=Button("Pause",safe,"II",new Vector2(474, -34),new Vector2(44,44),dark.color);run.pauseLabel=run.pauseButton.GetComponentInChildren<Text>();
            var boss=Panel("Boss health",safe,dark.color);Place(boss.rectTransform,new Vector2(.5f,1),new Vector2(0,-165),new Vector2(350,12),new Vector2(.5f,1));
            run.bossBar=Fill(boss.transform,orange.color);run.bossLabel=Label("Boss name",boss.transform,"SCRAP CRUSHER",14,cream.color,new Vector2(0,23),new Vector2(350,24));
            run.hint=Label("Prompt",safe,"↔  DRAG TO STEER",18,cream.color,Vector2.zero,new Vector2(500,44));
            Place(run.hint.rectTransform,new Vector2(.5f,0),new Vector2(0,112),new Vector2(500,44),new Vector2(.5f,0));run.hint.alignment=TextAnchor.MiddleCenter;
            var result=Panel("Result card",safe,new Color(.055f,.12f,.16f,.98f));Place(result.rectTransform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(470,340),new Vector2(.5f,.5f));run.resultPanel=result.gameObject;
            run.resultTitle=Label("Result",result.transform,"MISSION COMPLETE",27,teal.color,new Vector2(25,-30),new Vector2(420,45));run.resultTitle.alignment=TextAnchor.MiddleCenter;
            run.resultStats=Label("Stats",result.transform,"",19,cream.color,new Vector2(25,-100),new Vector2(420,115));run.resultStats.alignment=TextAnchor.MiddleCenter;
            run.replayButton=Button("Replay",result.transform,"PLAY AGAIN",new Vector2(85,-240),new Vector2(300,62),teal.color);
            run.soundButton=Button("Sound",safe,"SOUND ON",new Vector2(20,-910),new Vector2(115,32),dark.color);
            run.motionButton=Button("Motion",safe,"MOTION ON",new Vector2(398,-910),new Vector2(122,32),dark.color);
            Place(run.soundButton.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(20,18),new Vector2(115,32),Vector2.zero);
            Place(run.motionButton.GetComponent<RectTransform>(),new Vector2(1,0),new Vector2(-20,18),new Vector2(122,32),new Vector2(1,0));
        }
        static Image Fill(Transform parent,Color color) { var image=Panel("Fill",parent,color);image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillAmount=0;return image; }

        [MenuItem("Salvage Squad/Finish Slice Presentation")]
        public static void FinishPresentation()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var run = Object.FindFirstObjectByType<SliceRun>();
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (!sprite) throw new System.Exception("Built-in UI sprite unavailable");
            run.progress.sprite = run.bossBar.sprite = sprite;
            foreach (var button in new[] { run.replayButton, run.pauseButton, run.soundButton, run.motionButton })
            { var image=button.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced; }
            var card=run.resultPanel.GetComponent<Image>();card.sprite=sprite;card.type=Image.Type.Sliced;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card.rectTransform.sizeDelta=new Vector2(470,410);
            if(!run.resultPanel.transform.Find("Medal"))
            {
                var medal=Label("Medal",run.resultPanel.transform,"★",54,new Color(1,.82f,.4f),new Vector2(20,-14),new Vector2(430,65));
                medal.alignment=TextAnchor.MiddleCenter;
            }
            run.resultTitle.rectTransform.anchoredPosition=new Vector2(25,-92);
            run.resultStats.rectTransform.anchoredPosition=new Vector2(25,-155);
            run.replayButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(85,-320);
            PlayerSettings.enableFrameTimingStats=true;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        static RectTransform Rect(string name,Transform parent) {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static Image Panel(string name,Transform parent,Color color) {var i=Rect(name,parent).gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
        static Text Label(string name,Transform parent,string text,int size,Color color,Vector2 pos,Vector2 bounds)
        { var t=Rect(name,parent).gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.fontStyle=FontStyle.Bold;t.color=color;t.raycastTarget=false;Place(t.rectTransform,new Vector2(0,1),pos,bounds,new Vector2(0,1));return t; }
        static Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size,Color color)
        {var img=Panel(name,parent,color);img.raycastTarget=true;Place(img.rectTransform,new Vector2(0,1),pos,size,new Vector2(0,1));var b=img.gameObject.AddComponent<Button>();b.targetGraphic=img;var l=Label("Label",img.transform,text,14,cream.color,Vector2.zero,size);l.alignment=TextAnchor.MiddleCenter;if(color==teal.color)l.color=dark.color;return b;}
        static void Place(RectTransform r,Vector2 anchor,Vector2 pos,Vector2 size,Vector2 pivot) {r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;}
        static Material Load(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/"+name+".mat");
        static Transform Part(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material mat)
        {var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        static Transform Save(Transform root,string name)
        {var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"/"+name+".prefab");Object.DestroyImmediate(root.gameObject);return prefab.transform;}
        // Bake reusable parts by material once; 60 robots must not carry hundreds of separate part renderers.
        static void Combine(Transform root,string name,params Transform[] keep)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var remove=new List<GameObject>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                bool skip=false;foreach(var k in keep)if(filter.transform==k||filter.transform.IsChildOf(k))skip=true;
                if(skip)continue;
                var mat=filter.GetComponent<Renderer>().sharedMaterial;
                if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();
                groups[mat].Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});remove.Add(filter.gameObject);
            }
            int index=0;
            foreach(var group in groups)
            {
                var mesh=new Mesh {name=name+index};mesh.CombineMeshes(group.Value.ToArray());AssetDatabase.CreateAsset(mesh,Folder+"/"+name+index+".asset"); index++;
                var go=new GameObject("Shell "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=group.Key;
            }
            foreach(var go in remove)Object.DestroyImmediate(go);
        }
    }
}

