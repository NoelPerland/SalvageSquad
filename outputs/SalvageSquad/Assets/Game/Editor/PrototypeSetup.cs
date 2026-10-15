using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SalvageSquad.Editor
{
    public static class PrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/TrainingRange.unity";
        static Material teal, cream, dark, orange, yellow, road, stripe;
        static Font font;

        [MenuItem("Salvage Squad/Create Training Range")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) { Debug.Log("Training Range already exists; preserving scene edits."); return; }
            Directory.CreateDirectory("Assets/Game/Art");
            Directory.CreateDirectory("Assets/Game/Prefabs");
            AssetDatabase.Refresh();
            teal = Material("Teal", "43D9C2");
            cream = Material("Cream", "F0E8D1");
            dark = Material("Ink", "192F3D");
            orange = Material("Scrap", "EB7950");
            yellow = Material("Energy", "FFD777");
            road = Material("Track", "344B56");
            stripe = Material("Track Lines", "657B7D");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 12, -10);
            camera.transform.LookAt(new Vector3(0, 0, 7));
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 70;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("142B39");
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Light light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(45, -25, 0);
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.68f, .76f, .8f);
            RenderSettings.ambientMode = AmbientMode.Flat;

            Transform environment = new GameObject("Track").transform;
            Box("Runway", environment, new Vector3(0, -.18f, 10), new Vector3(8.5f, .3f, 46), road);
            Box("Left rail", environment, new Vector3(-4.35f, .1f, 10), new Vector3(.18f, .35f, 46), teal);
            Box("Right rail", environment, new Vector3(4.35f, .1f, 10), new Vector3(.18f, .35f, 46), teal);
            var markers = new List<Transform>();
            for (int i = 0; i < 14; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                    markers.Add(Box("Lane dash", environment, new Vector3(side * 1.4f, -.015f, i * 2), new Vector3(.045f, .02f, .8f), stripe));
            }
            for (int i = 0; i < 9; i++)
            {
                Box("Left verge", environment, new Vector3(-5.3f, -.3f, i * 4 - 4), new Vector3(1.3f, .45f, 3.5f), dark);
                Box("Right verge", environment, new Vector3(5.3f, -.3f, i * 4 - 4), new Vector3(1.3f, .45f, 3.5f), dark);
            }

            CombatSystem combat = new GameObject("Combat Pools").AddComponent<CombatSystem>();
            combat.projectilePrefab = ProjectilePrefab();
            combat.targetPrefab = TargetPrefab();
            combat.burstPrefab = BurstPrefab();
            SquadController squad = new GameObject("Squad").AddComponent<SquadController>();
            squad.bodies = new Transform[5]; squad.muzzles = new Transform[5]; squad.flashes = new Transform[5];
            Vector3[] slots = { new Vector3(-.82f, 0, .3f), new Vector3(0, 0, .3f), new Vector3(.82f, 0, .3f), new Vector3(-.41f, 0, -.6f), new Vector3(.41f, 0, -.6f) };
            for (int i = 0; i < slots.Length; i++) Robot(squad, i, slots[i]);

            PrototypeRun run = new GameObject("Run").AddComponent<PrototypeRun>();
            run.combat = combat; run.squad = squad; run.trackMarkers = markers.ToArray();
            BuildUI(run, camera);
            squad.buttons = new[] { run.restartButton.GetComponent<RectTransform>(), run.pauseButton.GetComponent<RectTransform>() };
            squad.uiCamera = camera;
            new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));

            PlayerSettings.companyName = "SalvageSquad";
            PlayerSettings.productName = "Salvage Squad";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            QualitySettings.vSyncCount = 0;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            AssetDatabase.DeleteAsset("Assets/TutorialInfo");
            AssetDatabase.DeleteAsset("Assets/Readme.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Training Range created. Open Assets/Scenes/TrainingRange.unity and press Play.");
        }

        [MenuItem("Salvage Squad/Build Windows Prototype")]
        public static void BuildWindows()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                File.Exists(SliceSetup.ScenePath) ? "../../WindowsSlice/SalvageSquad.exe" : "../../Windows/SalvageSquad.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = File.Exists(SliceSetup.ScenePath) ? new[] { SliceSetup.ScenePath, ScenePath } : new[] { ScenePath }, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
            Debug.Log("Windows build: " + path);
        }

        static void Robot(SquadController squad, int i, Vector3 slot)
        {
            Transform root = new GameObject("Robot " + (i + 1)).transform;
            root.SetParent(squad.transform, false); root.localPosition = slot;
            Transform body = new GameObject("Body").transform;
            body.SetParent(root, false); squad.bodies[i] = body;
            Box("Boot L", body, new Vector3(-.14f, .13f, 0), new Vector3(.21f, .24f, .37f), dark);
            Box("Boot R", body, new Vector3(.14f, .13f, 0), new Vector3(.21f, .24f, .37f), dark);
            Box("Torso", body, new Vector3(0, .43f, 0), new Vector3(.52f, .42f, .4f), teal);
            Box("Head", body, new Vector3(0, .83f, -.02f), new Vector3(.46f, .34f, .4f), cream);
            Box("Visor", body, new Vector3(0, .85f, .19f), new Vector3(.33f, .1f, .045f), dark);
            Box("Eye L", body, new Vector3(-.095f, .85f, .22f), new Vector3(.055f, .05f, .025f), yellow);
            Box("Eye R", body, new Vector3(.095f, .85f, .22f), new Vector3(.055f, .05f, .025f), yellow);
            Box("Cannon", body, new Vector3(0, .59f, .34f), new Vector3(.22f, .22f, .55f), dark);
            Box("Barrel band", body, new Vector3(0, .59f, .52f), new Vector3(.27f, .27f, .12f), cream);
            Box("Wind-up key", body, new Vector3(0, .57f, -.28f), new Vector3(.47f, .075f, .085f), yellow);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(body, false); muzzle.localPosition = new Vector3(0, .59f, .68f);
            squad.muzzles[i] = muzzle;
            squad.flashes[i] = Box("Muzzle flash", muzzle, Vector3.zero, new Vector3(.17f, .17f, .22f), yellow);
            squad.flashes[i].gameObject.SetActive(false);
        }

        static Transform ProjectilePrefab()
        {
            Transform root = new GameObject("Bolt").transform;
            Box("Energy", root, Vector3.zero, new Vector3(.065f, .065f, .45f), yellow);
            return SavePrefab(root, "Bolt");
        }

        static Transform TargetPrefab()
        {
            Transform root = new GameObject("Scrap target").transform;
            Box("Hull", root, Vector3.zero, new Vector3(1.03f, .85f, .85f), orange);
            Box("Helmet", root, new Vector3(0, .5f, 0), new Vector3(.8f, .16f, .75f), dark);
            Box("Plate", root, new Vector3(0, .05f, -.45f), new Vector3(.62f, .4f, .09f), dark);
            Box("Core", root, new Vector3(0, .05f, -.51f), new Vector3(.24f, .24f, .05f), yellow);
            Box("Skid L", root, new Vector3(-.4f, -.5f, 0), new Vector3(.28f, .2f, .95f), dark);
            Box("Skid R", root, new Vector3(.4f, -.5f, 0), new Vector3(.28f, .2f, .95f), dark);
            return SavePrefab(root, "ScrapTarget");
        }

        static Transform BurstPrefab()
        {
            Transform root = new GameObject("Burst").transform;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Box("Scrap", root, new Vector3(Mathf.Cos(angle), (i % 3) * .22f, Mathf.Sin(angle)) * .6f,
                    new Vector3(.16f, .16f, .25f), i % 2 == 0 ? orange : yellow);
            }
            return SavePrefab(root, "Burst");
        }

        static Transform SavePrefab(Transform root, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Game/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab.transform;
        }

        static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj.transform;
        }

        static Material Material(string name, string hex)
        {
            var value = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = Hex(hex) };
            value.SetFloat("_Smoothness", .22f);
            AssetDatabase.CreateAsset(value, "Assets/Game/Art/" + name + ".mat");
            return value;
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }

        static void BuildUI(PrototypeRun run, Camera camera)
        {
            Canvas canvas = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960); scaler.matchWidthOrHeight = .5f;
            RectTransform safe = Rect("Safe Area", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            run.safeArea = safe;
            Text title = Label("Title", safe, "SALVAGE\nSQUAD", 31, Hex("F0E8D1"));
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(20, -20), new Vector2(230, 78), new Vector2(0, 1));
            Text tag = Label("Subtitle", safe, "TRAINING RANGE  /  01", 12, Hex("43D9C2"));
            Place(tag.rectTransform, new Vector2(0, 1), new Vector2(22, -104), new Vector2(300, 22), new Vector2(0, 1));
            run.score = Label("Score", safe, "SALVAGED  00", 23, Color.white);
            Place(run.score.rectTransform, new Vector2(0, 1), new Vector2(22, -148), new Vector2(330, 32), new Vector2(0, 1));
            run.status = Label("Status", safe, "SQUAD 05", 12, Hex("AFC5CA"));
            Place(run.status.rectTransform, new Vector2(0, 1), new Vector2(23, -183), new Vector2(350, 24), new Vector2(0, 1));
            run.pauseButton = Button("Pause", safe, "PAUSE", Hex("203D4A"), Hex("F0E8D1"));
            Place(run.pauseButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-20, -24), new Vector2(92, 44), new Vector2(1, 1));
            run.pauseLabel = run.pauseButton.GetComponentInChildren<Text>();
            run.restartButton = Button("Restart", safe, "RESTART RUN", Hex("43D9C2"), Hex("142B39"));
            Place(run.restartButton.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(240, 54), new Vector2(.5f, 0));
            run.hint = Label("Hint", safe, "DRAG LEFT / RIGHT", 17, Hex("F0E8D1"));
            run.hint.alignment = TextAnchor.MiddleCenter;
            Place(run.hint.rectTransform, new Vector2(.5f, 0), new Vector2(0, 104), new Vector2(400, 34), new Vector2(.5f, 0));
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }
        static Text Label(string name, Transform parent, string text, int size, Color color)
        {
            Text label = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.fontStyle = FontStyle.Bold;
            label.text = text; label.color = color; label.raycastTarget = false;
            return label;
        }
        static Button Button(string name, Transform parent, string text, Color background, Color foreground)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>(); image.color = background;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Text label = Label("Label", rect, text, 14, foreground); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }
    }
}
