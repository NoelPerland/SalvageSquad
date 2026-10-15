using System;
using UnityEngine;

namespace SalvageSquad
{
    public enum GateKind { Add, Multiply, Damage, FireRate, Weapon }
    [Serializable]
    public struct GateEffect
    {
        public GateKind kind;
        public float value;
        public bool shootable;
        public bool uncapped;
        public float maxValue;
        [Min(0)] public int shotsPerIncrement; // Zero keeps existing one-hit recruit gates.
        public bool CanCharge => shootable && (uncapped || value < maxValue);
        public float ChargeStep => kind == GateKind.FireRate ? .1f : 1;
        public bool IsPositive => kind == GateKind.Add ? value >= 0 : kind == GateKind.Weapon || value >= 1;
        public string Label => kind switch {
            GateKind.Add => (value >= 0 ? "+" : "-") + Mathf.Abs(value).ToString("0"),
            GateKind.Multiply => "×" + value.ToString("0.#"),
            GateKind.Damage => "DMG ×" + value.ToString("0.#"),
            GateKind.FireRate => "RATE ×" + value.ToString("0.#"),
            _ => "MK " + value.ToString("0")
        };
        public int ResultCount(int count)
        {
            double result = kind == GateKind.Add ? count + System.Math.Round(value, System.MidpointRounding.ToEven)
                : kind == GateKind.Multiply ? System.Math.Floor(count * (double)value) : count;
            return (int)System.Math.Clamp(result, 0, int.MaxValue);
        }
    }
    public enum EncounterKind { Wave, Gate, Hazard, Boss }
    public enum WaveLayout { AcrossLanes, Focused, FollowSquad }
    [Serializable]
    public sealed class Encounter
    {
        [Min(0)] public float time;
        public EncounterKind kind;
        [Range(-1, 1)] public int lane;
        [Min(1)] public int count = 1;
        public EnemySettings enemy;
        [Min(0)] public float approachZ;
        public WaveLayout layout;
        public bool aimAtSquad;
        public bool singleLaneGate;
        public GateEffect left, right;
    }
    [CreateAssetMenu(menuName = "Salvage Squad/Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public string title = "THE SCRAPLINE";
        [Min(60)] public float deadline = 90;
        public Encounter[] encounters = Array.Empty<Encounter>();
        public string Validate()
        {
            if (float.IsNaN(deadline) || float.IsInfinity(deadline) || deadline < 60 || encounters == null) return "Level needs a finite deadline of at least 60 seconds and an encounter list.";
            float last = -1;
            int count = 5;
            bool unboundedPath = false;
            foreach (var row in encounters)
            {
                if (row == null) return "Encounter row missing.";
                if (row.time < last || row.time < 0 || row.time >= deadline) return "Encounter times must be ordered and inside the run.";
                last = row.time;
                if (float.IsNaN(row.time) || float.IsInfinity(row.time) || float.IsNaN(row.approachZ) || float.IsInfinity(row.approachZ) || row.approachZ < 0) return "Encounter timing and approach distance must be finite and nonnegative.";
                if (!Enum.IsDefined(typeof(WaveLayout), row.layout) || row.count < 1) return "Wave layout and count must be valid.";
                if ((row.kind == EncounterKind.Wave || row.kind == EncounterKind.Boss) && !row.enemy) return "Enemy settings missing.";
                if (row.kind != EncounterKind.Gate) continue;
                if (row.singleLaneGate && (row.lane < -1 || row.lane > 1)) return "Single gate lane must be inside the track.";
                unboundedPath |= row.left.uncapped || (!row.singleLaneGate && row.right.uncapped);
                foreach (var effect in new[] { row.left, row.right })
                {
                    if (!Enum.IsDefined(typeof(GateKind), effect.kind) || float.IsNaN(effect.value) || float.IsInfinity(effect.value)) return "Gate effect must be finite and known.";
                    if (effect.kind != GateKind.Add && effect.value <= 0) return "Gate multipliers must be positive.";
                    if (effect.uncapped && !effect.shootable) return "Uncapped gates must be shootable.";
                    if (effect.shootable && !effect.uncapped && (float.IsNaN(effect.maxValue) || float.IsInfinity(effect.maxValue) || effect.maxValue < effect.value)) return "Shootable gate needs a finite cap above its starting value.";
                    if (effect.shotsPerIncrement < 0) return "Gate charge cost cannot be negative.";
                    double maximum=effect.shootable?effect.maxValue:effect.value;
                    double result=effect.kind==GateKind.Add?count+maximum:effect.kind==GateKind.Multiply?count*maximum:count;
                    if(!unboundedPath && result>60)return "Gate path exceeds 60 visible units.";
                }
                var left = row.left; var right = row.right;
                if (left.shootable && !left.uncapped) left.value = left.maxValue;
                if (right.shootable && !right.uncapped) right.value = right.maxValue;
                count = Mathf.Max(left.ResultCount(count), right.ResultCount(count));
                if (!unboundedPath && count > 60) return "Gate path exceeds 60 visible units.";
            }
            return null;
        }
    }
}
