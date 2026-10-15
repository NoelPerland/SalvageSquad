using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SalvageSquad.Editor
{
    // Explicit tuning migration; preserves the scene's working art and component references.
    public static class PlayFeelTuning
    {
        const string Folder = "Assets/Game/Slice/";
        [MenuItem("Salvage Squad/Apply First-Minute Tuning")]
        public static void Apply()
        {
            var swarm = AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder + "Swarm.asset");
            var heavy = AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder + "Heavy.asset");
            var charger = AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder + "Charger.asset");
            var boss = AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder + "Crusher.asset");
            var warmup = AssetDatabase.LoadAssetAtPath<EnemySettings>(Folder + "Warmup.asset");
            if (!warmup)
            {
                warmup = ScriptableObject.CreateInstance<EnemySettings>();
                AssetDatabase.CreateAsset(warmup, Folder + "Warmup.asset");
            }
            warmup.kind = EnemyKind.Swarm; warmup.health = 1; warmup.speed = 1.8f;
            warmup.contactDamage = 0; warmup.size = swarm.size; warmup.color = swarm.color;
            EditorUtility.SetDirty(warmup);
            swarm.health = 3; heavy.health = 100; charger.health = 12; boss.health = 3700;
            foreach (var enemy in new[] { swarm, heavy, charger, boss }) EditorUtility.SetDirty(enemy);
            foreach (bool alternate in new[] { false, true })
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Folder + (alternate ? "ReverseShift.asset" : "FirstShift.asset"));
                var rows = new List<Encounter>();
                void Wave(float t, EnemySettings enemy, int count, WaveLayout layout = WaveLayout.AcrossLanes, int lane = 0, float z = 15)
                    => rows.Add(new Encounter { time = t, kind = EncounterKind.Wave, enemy = enemy, count = count, layout = layout, lane = alternate ? -lane : lane, approachZ = z });
                void Gate(float t, GateEffect left, GateEffect right, float z = 15)
                    => rows.Add(new Encounter { time = t, kind = EncounterKind.Gate, left = alternate ? right : left, right = alternate ? left : right, approachZ = z });
                void Hazard(float t, int lane, bool aimed = false) => rows.Add(new Encounter { time = t, kind = EncounterKind.Hazard, lane = alternate ? -lane : lane, aimAtSquad = aimed });
                GateEffect Effect(GateKind kind, float value) => new GateEffect { kind = kind, value = value };
                Wave(.1f, warmup, 9, WaveLayout.AcrossLanes, z: 6);
                Gate(.7f, new GateEffect { kind = GateKind.Add, value = -3, shootable = true, maxValue = 10 }, Effect(GateKind.Add, 8), 8);
                Wave(3.9f, swarm, 9, WaveLayout.FollowSquad, z: 14);
                Gate(4.5f, Effect(GateKind.Multiply, 2), Effect(GateKind.Damage, 3), 12);
                Wave(9.4f, swarm, 15, WaveLayout.FollowSquad, z: 14);
                Wave(12.5f, charger, 3, WaveLayout.AcrossLanes, z: 22);
                Wave(14, heavy, 1, WaveLayout.Focused, -1, 14);
                Gate(14.5f, Effect(GateKind.FireRate, 1.8f), Effect(GateKind.Add, 10));
                Hazard(17.5f, 0, true);
                Wave(20.6f, swarm, 18, WaveLayout.FollowSquad, z: 14);
                Wave(23, heavy, 1, WaveLayout.Focused, 1, 12);
                Gate(23, Effect(GateKind.Weapon, 2), Effect(GateKind.Damage, 2));
                Hazard(25.4f, 0, true);
                Wave(29.1f, swarm, 24, WaveLayout.FollowSquad, z: 14);
                Wave(31, charger, 6, WaveLayout.AcrossLanes, z: 22);
                Gate(34.2f, new GateEffect { kind = GateKind.Add, value = 2, shootable = true, maxValue = 15, shotsPerIncrement = 15 }, Effect(GateKind.Damage, 2), 12);
                Hazard(35.8f, -1);
                Wave(39.1f, swarm, 24, WaveLayout.FollowSquad, z: 14);
                Wave(41, heavy, 2, WaveLayout.Focused, 1, 14);
                Gate(43, Effect(GateKind.Add, 5), Effect(GateKind.FireRate, 1.5f));
                Hazard(44.4f, 0, true);
                Wave(49.1f, swarm, 30, WaveLayout.FollowSquad, z: 14);
                Wave(51.5f, charger, alternate ? 9 : 6, WaveLayout.AcrossLanes, z: 22);
                Wave(53.5f, swarm, 18, WaveLayout.FollowSquad, z: 14);
                rows.Add(new Encounter { time = 56, kind = EncounterKind.Boss, enemy = boss });
                rows.Sort((a, b) => a.time.CompareTo(b.time));
                level.title = alternate ? "02   /   CROSS SHIFT" : "01   /   SALVAGE DOCK";
                level.deadline = 90; level.encounters = rows.ToArray();
                string error = level.Validate();
                if (error != null) throw new System.InvalidOperationException(error);
                EditorUtility.SetDirty(level);
            }
            var scene = EditorSceneManager.OpenScene(SliceSetup.ScenePath);
            var run = Object.FindFirstObjectByType<SliceRun>();
            run.nextShift = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Folder + "ReverseShift.asset");
            run.squad.shotInterval = .36f; run.combat.bulletSpeed = 32;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("First-minute tuning saved. Existing presentation preserved; two authored shifts linked.");
        }
    }
}
