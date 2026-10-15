using NUnit.Framework;
using UnityEngine;
namespace SalvageSquad.Tests
{
    public class SliceLogicTests
    {
        [Test]
        public void EveryAuthoredNumericPowerupIsShootableAndUncapped()
        {
            foreach(string name in new[]{"FirstShift","ReverseShift"})
            {
                var level=UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Slice/"+name+".asset");
                Assert.That(level.Validate(),Is.Null);
                var kinds=new System.Collections.Generic.HashSet<GateKind>();
                foreach(var row in level.encounters)
                {
                    if(row.kind!=EncounterKind.Gate)continue;
                    foreach(var effect in row.singleLaneGate?new[]{row.left}:new[]{row.left,row.right})
                    {
                        Assert.That(effect.shootable&&effect.uncapped,Is.True,$"{name} at {row.time}: {effect.Label}");
                        Assert.That(effect.CanCharge,Is.True);kinds.Add(effect.kind);
                    }
                }
                Assert.That(kinds.Count,Is.EqualTo(5));
            }
        }
        [Test]
        public void BothAuthoredLevelsKeepAllSixtyFourGatePathsValidWithUncappedRewards()
        {
            foreach (string name in new[] { "FirstShift", "ReverseShift" })
            {
                var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Slice/" + name + ".asset");
                Assert.That(level.Validate(), Is.Null);
                for (int charged = 0; charged < 2; charged++) for (int mask = 0; mask < 64; mask++)
                {
                    int units = 5, rowIndex = 0, bosses = 0;
                    foreach (var row in level.encounters)
                    {
                        if (row.kind == EncounterKind.Boss) bosses++;
                        if (row.kind != EncounterKind.Gate || row.singleLaneGate) continue;
                        var effect = (mask & (1 << rowIndex++)) == 0 ? row.left : row.right;
                        if (charged == 1 && effect.shootable) effect.value = effect.uncapped ? 150 : effect.maxValue;
                        if (units > 0) units = effect.ResultCount(units);
                        Assert.That(units, Is.GreaterThanOrEqualTo(0));
                        Assert.That(Mathf.Min(60,units),Is.InRange(0,60));
                    }
                    Assert.That(rowIndex, Is.EqualTo(6)); Assert.That(bosses, Is.EqualTo(1));
                }
            }
        }
        [TestCase(-1.1f,0)] [TestCase(-1.09f,-1)] [TestCase(0,-1)] [TestCase(1.09f,-1)] [TestCase(1.1f,1)]
        public void GateSelectionRequiresCommittingToASide(float x,int expected) => Assert.That(GateRow.Choice(x),Is.EqualTo(expected));
        [TestCase(GateKind.Add,10,5,15)] [TestCase(GateKind.Add,-7,5,0)] [TestCase(GateKind.Multiply,2,15,30)] [TestCase(GateKind.Multiply,1.5f,29,43)]
        public void GateGrowthMath(GateKind kind,float value,int before,int expected)
            => Assert.That(new GateEffect{kind=kind,value=value}.ResultCount(before),Is.EqualTo(expected));
        [Test]
        public void SixtyFormationSlotsAreBoundedAndDistinct()
        {
            var slots=new System.Collections.Generic.HashSet<Vector3>();
            for(int i=0;i<60;i++)
            {
                var slot=SquadController.FormationSlot(i,60);
                Assert.That(Mathf.Abs(slot.x),Is.LessThanOrEqualTo(1.61f));
                Assert.That(slot.z,Is.GreaterThan(-8.6f)); Assert.That(slots.Add(slot),Is.True);
            }
        }
        [TestCase(20f)] [TestCase(float.MaxValue)]
        public void OversizedGatePathIsRejected(float multiplier)
        {
            var level=ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                level.encounters=new[]{new Encounter{kind=EncounterKind.Gate,left=new GateEffect{kind=GateKind.Multiply,value=multiplier},right=new GateEffect{kind=GateKind.Add,value=1}}};
                Assert.That(level.Validate(),Does.Contain("60"));
            }
            finally {Object.DestroyImmediate(level);}
        }
        [Test]
        public void ShootableGateCapAndNonFiniteValuesAreValidated()
        {
            var level=ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var effect=new GateEffect{kind=GateKind.Add,value=-5,shootable=true,maxValue=100};
                level.encounters=new[]{new Encounter{kind=EncounterKind.Gate,left=effect,right=new GateEffect{kind=GateKind.Add,value=1}}};
                Assert.That(level.Validate(),Does.Contain("60"));
                level.encounters[0].left.maxValue=float.NaN;
                Assert.That(level.Validate(),Does.Contain("finite"));
                level.encounters[0].left.maxValue=-6;
                Assert.That(level.Validate(),Does.Contain("cap"));
                level.encounters[0].left.shootable=false;
                Assert.That(level.Validate(),Is.Null);
                Assert.That(effect.Label,Is.EqualTo("-5"));
            }
            finally{Object.DestroyImmediate(level);}
        }
        [Test]
        public void UncappedRecruitGatesValidateAndCountMathCannotWrap()
        {
            var level=ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var effect=new GateEffect{kind=GateKind.Add,value=-20,shootable=true,uncapped=true};
                level.encounters=new[]{new Encounter{kind=EncounterKind.Gate,left=effect,singleLaneGate=true,lane=1}};
                Assert.That(level.Validate(),Is.Null);Assert.That(effect.CanCharge,Is.True);
                level.encounters[0].left.kind=GateKind.Damage;Assert.That(level.Validate(),Is.Not.Null);
                Assert.That(new GateEffect{kind=GateKind.Add,value=150}.ResultCount(5),Is.EqualTo(155));
                Assert.That(new GateEffect{kind=GateKind.Add,value=float.MaxValue}.ResultCount(5),Is.EqualTo(int.MaxValue));
                Assert.That(new GateEffect{kind=GateKind.Multiply,value=2}.ResultCount(int.MaxValue),Is.EqualTo(int.MaxValue));
            }
            finally{Object.DestroyImmediate(level);}
        }
        [Test]
        public void InvalidEncounterTimingLayoutAndChargeCostsAreRejected()
        {
            var level=ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var row=new Encounter{kind=EncounterKind.Gate,left=new GateEffect{kind=GateKind.Add,value=1},right=new GateEffect{kind=GateKind.Add,value=1}};
                level.encounters=new[]{row};Assert.That(level.Validate(),Is.Null);
                row.time=float.NaN;Assert.That(level.Validate(),Is.Not.Null);row.time=0;
                row.approachZ=float.PositiveInfinity;Assert.That(level.Validate(),Is.Not.Null);row.approachZ=0;
                row.layout=(WaveLayout)99;Assert.That(level.Validate(),Is.Not.Null);row.layout=WaveLayout.AcrossLanes;
                row.left.shotsPerIncrement=-1;Assert.That(level.Validate(),Is.Not.Null);row.left.shotsPerIncrement=15;
                level.deadline=float.NaN;Assert.That(level.Validate(),Is.Not.Null);level.deadline=90;
                level.encounters=new Encounter[]{null};Assert.That(level.Validate(),Is.Not.Null);
            }
            finally{Object.DestroyImmediate(level);}
        }
        [Test]
        public void EarlyFirepowerChoiceCanOneShotThePayoffEnemy()
        {
            var swarm=UnityEditor.AssetDatabase.LoadAssetAtPath<EnemySettings>("Assets/Game/Slice/Swarm.asset");
            foreach(string name in new[]{"FirstShift","ReverseShift"})
            {
                var level=UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Slice/"+name+".asset");
                bool found=false;
                foreach(var row in level.encounters)
                {
                    if(row.kind!=EncounterKind.Gate||row.time>10)continue;
                    foreach(var effect in new[]{row.left,row.right})if(effect.kind==GateKind.Damage)
                    {
                        found=true;Assert.That(new HitPoints(swarm.health).Damage(Mathf.RoundToInt(effect.value)),Is.True);
                    }
                }
                Assert.That(found,Is.True);
            }
        }
    }
}
