using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SalvageSquad.Tests
{
    public sealed class PresentationTests
    {
        [Test]
        public void PanelSpriteKeepsBordersForReadableNineSliceCorners()
        {
            var panel=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Polished/EnamelPanel.png");
            Assert.That(panel,Is.Not.Null);
            Assert.That(panel.border.x,Is.GreaterThan(0));Assert.That(panel.border.y,Is.GreaterThan(0));
        }

        [Test]
        public void OriginalMechanicalClipsAreBoundedAndHaveClickFreeEndpoints()
        {
            foreach(float hz in new[]{72f,145f,360f})
            {
                var clip=CombatSystem.MakeMechanical("Test",.17f,hz,1);
                try
                {
                    var data=new float[clip.samples];Assert.That(clip.GetData(data,0),Is.True);
                    float peak=0;foreach(float sample in data){Assert.That(float.IsNaN(sample),Is.False);peak=Mathf.Max(peak,Mathf.Abs(sample));}
                    Assert.That(peak,Is.InRange(.1f,.95f));Assert.That(data[0],Is.Zero);Assert.That(Mathf.Abs(data[data.Length-1]),Is.LessThan(.001f));
                }
                finally{Object.DestroyImmediate(clip);}
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>CombatSystem.MakeMechanical("Invalid",float.NaN,100,1));
        }
        [Test]
        public void PooledEnemyCanReuseEverySilhouetteWithoutCreatingMaterials()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Polished/RogueMachine.prefab");
            Assert.That(prefab,Is.Not.Null,"Apply the character migration before testing.");
            var copy=Object.Instantiate(prefab);
            try
            {
                var visual=copy.GetComponent<EnemyVisual>();
                var renderer=visual.shell.GetComponent<Renderer>();var materials=renderer.sharedMaterials;
                Assert.That(visual.forms.Length,Is.EqualTo(4));
                for(int i=3;i>=0;i--)
                {
                    visual.shell.transform.localPosition=Vector3.one;
                    visual.Configure((EnemyKind)i);
                    Assert.That(visual.shell.sharedMesh,Is.SameAs(visual.forms[i]));
                    Assert.That(visual.shell.sharedMesh.subMeshCount,Is.EqualTo(1),"Palette atlas keeps every enemy to one material draw.");
                    Assert.That(visual.shell.transform.localPosition,Is.EqualTo(Vector3.zero));
                    CollectionAssert.AreEqual(materials,renderer.sharedMaterials);
                    visual.Tick(2,1);
                    foreach(var arm in visual.crusherArms)Assert.That(arm.gameObject.activeSelf,Is.EqualTo(i==3));
                    if(i==3){visual.Tick(2,0,1);Assert.That(Quaternion.Angle(visual.crusherArms[0].localRotation,Quaternion.identity),Is.GreaterThan(80));}
                }
                Assert.That(visual.forms[0],Is.Not.SameAs(visual.forms[3]));
            }
            finally {Object.DestroyImmediate(copy);}
        }
    }
}
