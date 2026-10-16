using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SalvageSquad.Tests
{
    public class SliceRunTests
    {
        [UnityTest]
        public IEnumerator HealthBarsShowDelayedLossThenCatchUpAndReset()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            run.squad.SetCount(20);run.Advance(.02f);
            run.DamageSquad(10);run.Advance(.02f);
            Capture("Feel-Squad-Hit");
            Assert.That(run.SquadHealth.fillAmount,Is.InRange(.5f,.99f));
            Assert.That(run.SquadHealthTrail.fillAmount,Is.GreaterThan(run.SquadHealth.fillAmount));
            Assert.That(run.SquadHealthTrail.color,Is.Not.EqualTo(run.SquadHealth.color));
            for(int i=0;i<80;i++)run.Advance(.02f);
            Assert.That(run.SquadHealth.fillAmount,Is.EqualTo(.5f).Within(.002f));
            Assert.That(run.SquadHealthTrail.fillAmount,Is.EqualTo(.5f).Within(.002f));
            run.feedback.ToggleMotion();run.squad.SetCount(20);run.Advance(.02f);
            run.DamageSquad(10);run.Advance(.02f);
            Assert.That(run.SquadHealth.fillAmount,Is.EqualTo(.5f));
            Assert.That(run.SquadHealthTrail.fillAmount,Is.EqualTo(.5f));
            run.ResetRun();
            Assert.That(run.SquadHealth.fillAmount,Is.EqualTo(1));
            Assert.That(run.SquadHealthTrail.fillAmount,Is.EqualTo(1));
            Assert.That(run.BossHealthTrail.fillAmount,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator BossHealthShowsDamageBeforeTrailingLoss()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var original=run.level;var trial=Object.Instantiate(original);
            var boss=ScriptableObject.CreateInstance<EnemySettings>();
            boss.kind=EnemyKind.Boss;boss.health=1000;boss.size=2;boss.speed=0;
            trial.encounters=new[]{new Encounter{time=.1f,kind=EncounterKind.Boss,enemy=boss}};trial.deadline=20;
            try
            {
                run.level=trial;run.ResetRun();run.Advance(.1f);run.Advance(1.61f);
                Assert.That(run.combat.BossArmored,Is.False);
                run.combat.Fire(new Vector3(0,.6f,8),300);run.Advance(.06f);
                Capture("Feel-Boss-Hit");
                Assert.That(run.combat.BossHealth,Is.LessThan(.75f));
                Assert.That(run.bossBar.fillAmount,Is.GreaterThan(run.combat.BossHealth));
                Assert.That(run.BossHealthTrail.fillAmount,Is.GreaterThan(run.bossBar.fillAmount));
                for(int i=0;i<60;i++)run.Advance(.02f);
                Assert.That(run.BossHealthTrail.fillAmount,Is.LessThan(.8f));
                run.ResetRun();Assert.That(run.BossHealthTrail.fillAmount,Is.EqualTo(1));
            }
            finally { run.level=original;run.ResetRun();Object.Destroy(trial);Object.Destroy(boss); }
        }
        int previousBest;
        bool hadBest;
        [SetUp] public void PreserveBest() { hadBest=PlayerPrefs.HasKey("SliceBest");previousBest=PlayerPrefs.GetInt("SliceBest"); }
        [TearDown] public void RestoreBest() { if(hadBest)PlayerPrefs.SetInt("SliceBest",previousBest);else PlayerPrefs.DeleteKey("SliceBest");PlayerPrefs.Save(); }

        [UnityTest]
        public IEnumerator UpgradeAnnouncementStaysCenteredPausesAndRespectsReducedMotion()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            int objects=run.safeArea.GetComponentsInChildren<Transform>(true).Length;
            var gate=run.combat.ShootableGates[0];
            var reward=new GateEffect{kind=GateKind.Add,value=20};
            run.squad.SetDesiredX(2);run.squad.Tick(.2f);
            gate.Prepare(reward,reward,.7f);run.Advance(.05f);run.Advance(.12f);
            Assert.That(run.powerLabel.gameObject.activeSelf,Is.True);Assert.That(run.powerLabel.text,Does.Contain("POWER UP!"));
            Assert.That(run.powerLabel.rectTransform.anchoredPosition.x,Is.Zero);
            Assert.That(run.hint.text,Does.Not.Contain("POWER UP"),"Onboarding may remain, but the old banner must not duplicate the upgrade.");
            var group=run.powerLabel.GetComponent<CanvasGroup>();Assert.That(group.blocksRaycasts,Is.False);
            Assert.That(run.powerLabel.GetComponentsInChildren<UnityEngine.UI.Image>(true),Is.Empty,"The announcement must have no decorative lines.");
            run.squad.SetDesiredX(2.6f);run.Advance(.1f);
            Assert.That(run.powerLabel.rectTransform.anchoredPosition.x,Is.Zero);
            var pose=run.powerLabel.rectTransform.anchoredPosition;float alpha=group.alpha;
            run.SetPaused(true);run.Advance(2);Assert.That(run.powerLabel.gameObject.activeSelf,Is.False);
            Assert.That(group.alpha,Is.EqualTo(alpha));Assert.That(run.powerLabel.rectTransform.anchoredPosition,Is.EqualTo(pose));
            run.SetPaused(false);Assert.That(run.powerLabel.gameObject.activeSelf,Is.True);
            run.Advance(1.1f);Assert.That(run.powerLabel.gameObject.activeSelf,Is.False);
            run.ResetRun();if(!run.feedback.ReducedMotion)run.feedback.ToggleMotion();
            run.squad.SetDesiredX(2);run.squad.Tick(.2f);
            gate.Prepare(reward,reward,.7f);run.Advance(.05f);
            Assert.That(run.powerLabel.transform.localScale,Is.EqualTo(Vector3.one));Assert.That(run.powerLabel.transform.localRotation,Is.EqualTo(Quaternion.identity));
            Assert.That(run.safeArea.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
            run.feedback.ToggleMotion();run.ResetRun();Assert.That(run.powerLabel.gameObject.activeSelf,Is.False);
        }

        [UnityTest]
        public IEnumerator WideSquadCannotFarmBothGatesFromMiddleAndCollectsOnlyCommittedSide()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            foreach(float x in new[]{0f,-2f,2f})
            {
                run.ResetRun();run.squad.SetCount(60);run.squad.transform.position=new Vector3(x,0,0);run.squad.SetDesiredX(x);
                var gate=run.combat.ShootableGates[0];
                gate.Prepare(new GateEffect{kind=GateKind.Add,value=10,shootable=true,uncapped=true},new GateEffect{kind=GateKind.Add,value=20,shootable=true,uncapped=true},6);
                for(int i=0;i<40;i++){run.squad.Tick(.02f);run.combat.Tick(.02f);}
                if(x==0){Assert.That(gate.leftLabel.text,Is.EqualTo("+10"));Assert.That(gate.rightLabel.text,Is.EqualTo("+20"));}
                if(x<0){Assert.That(gate.leftLabel.text,Is.Not.EqualTo("+10"));Assert.That(gate.rightLabel.text,Is.EqualTo("+20"));}
                if(x>0){Assert.That(gate.leftLabel.text,Is.EqualTo("+10"));Assert.That(gate.rightLabel.text,Is.Not.EqualTo("+20"));}
                gate.transform.position=Vector3.zero;
                Assert.That(gate.Tick(.02f,run.squad,out _),Is.EqualTo(x!=0));
                int units=run.squad.UnitCount;Assert.That(units,x==0?Is.EqualTo(60):Is.GreaterThan(60));
                Assert.That(gate.Tick(.02f,run.squad,out _),Is.False);Assert.That(run.squad.UnitCount,Is.EqualTo(units));
            }
            run.ResetRun();
        }

        [UnityTest]
        public IEnumerator EveryNumericGateGrowsFromProjectilesAndAppliesItsDisplayedReward()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            foreach(GateKind kind in System.Enum.GetValues(typeof(GateKind)))
            {
                run.ResetRun();run.squad.SetDesiredX(-2);run.squad.Tick(.15f);run.combat.ResetCombat();
                var gate=run.combat.ShootableGates[0];
                float value=kind==GateKind.Add?-1:kind==GateKind.FireRate?1.5f:2;
                gate.Prepare(new GateEffect{kind=kind,value=value,shootable=true,uncapped=true,maxValue=2,shotsPerIncrement=2},default,6);
                string before=gate.leftLabel.text;
                run.combat.Fire(new Vector3(-2,.6f,0),1);run.combat.Tick(.4f);
                Assert.That(gate.leftLabel.text,Is.EqualTo(before),"Partial shot charge must be retained.");
                run.combat.Fire(new Vector3(-2,.6f,0),1);run.combat.Tick(.4f);
                string[] labels={"+0","×3","×3","×"+1.6f.ToString("0.#"),"MK 3"};
                Assert.That(gate.leftLabel.text,Is.EqualTo(labels[(int)kind]));
                Assert.That(gate.ShotCrossing(-2,0,8,.1f,out _),Is.True,"Uncapped rewards must keep accepting shots.");
                gate.transform.position=Vector3.zero;Assert.That(gate.Tick(.02f,run.squad,out _),Is.True);
                if(kind==GateKind.Add)Assert.That(run.squad.UnitCount,Is.EqualTo(5));
                if(kind==GateKind.Multiply)Assert.That(run.squad.UnitCount,Is.EqualTo(15));
                if(kind==GateKind.Damage)Assert.That(run.squad.Damage,Is.EqualTo(3));
                if(kind==GateKind.FireRate)Assert.That(run.squad.FireRate,Is.EqualTo(1.6f).Within(.001f));
                if(kind==GateKind.Weapon){Assert.That(run.squad.WeaponTier,Is.EqualTo(3));Assert.That(run.squad.Damage,Is.EqualTo(3));}
            }
            run.ResetRun();
        }
        [UnityTest]
        public IEnumerator ExcessFireRateAndHigherWeaponTiersKeepTheirPowerWithoutExtraProjectiles()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var enemy=ScriptableObject.CreateInstance<EnemySettings>();enemy.health=2;enemy.speed=0;enemy.size=8;
            foreach(float rate in new[]{3f,6f})
            {
                run.ResetRun();run.squad.SetCount(1);run.squad.Apply(new GateEffect{kind=GateKind.FireRate,value=rate});
                run.combat.SpawnTarget(0,enemy,3);run.squad.Tick(.02f);run.combat.Tick(.2f);
                Assert.That(run.combat.TotalShots,Is.EqualTo(1));
                Assert.That(run.combat.Salvaged,Is.EqualTo(rate==6?1:0),"Extra fire rate must increase delivered damage.");
            }
            run.ResetRun();run.squad.Apply(new GateEffect{kind=GateKind.Weapon,value=3});run.squad.Tick(.02f);
            Assert.That(run.squad.WeaponTier,Is.EqualTo(3));Assert.That(run.squad.Damage,Is.EqualTo(3));
            Assert.That(run.squad.bodies[0].Find("Cannon/Heavy sleeve").gameObject.activeSelf,Is.True);
            run.squad.Apply(new GateEffect{kind=GateKind.Damage,value=float.MaxValue});
            run.squad.Apply(new GateEffect{kind=GateKind.Damage,value=2});Assert.That(run.squad.Damage,Is.EqualTo(int.MaxValue));
            run.ResetRun();Assert.That(run.squad.Damage,Is.EqualTo(1));Assert.That(run.squad.FireRate,Is.EqualTo(1));Assert.That(run.squad.WeaponTier,Is.EqualTo(1));
            Object.Destroy(enemy);
        }
        [UnityTest]
        public IEnumerator BossKilledOnWarningImpactFrameCannotDamageTheSquad()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var original=run.level;var trial=Object.Instantiate(original);
            var boss=ScriptableObject.CreateInstance<EnemySettings>();boss.kind=EnemyKind.Boss;boss.health=1000000;boss.size=2;boss.contactDamage=18;
            trial.encounters=new[]{new Encounter{time=.1f,kind=EncounterKind.Boss,enemy=boss},new Encounter{time=1.8f,kind=EncounterKind.Hazard,lane=0}};
            try
            {
                run.level=trial;run.ResetRun();run.squad.SetCount(3);
                run.Advance(.1f);run.squad.SetDesiredX(2.6f);run.Advance(1.61f);run.Advance(.1f);
                run.squad.SetDesiredX(0);run.Advance(1.4f);
                Assert.That(run.HazardRemaining,Is.InRange(.01f,.2f));Assert.That(run.combat.BossArmored,Is.False);
                int units=run.squad.UnitCount;Assert.That(units,Is.EqualTo(3));
                run.combat.Fire(new Vector3(0,.6f,8),int.MaxValue);run.Advance(.2f);
                Assert.That(run.combat.BossDefeated,Is.True);Assert.That(run.State,Is.EqualTo(RunState.Victory));
                Assert.That(run.squad.UnitCount,Is.EqualTo(units));Assert.That(run.HazardRemaining,Is.Zero);
                Assert.That(run.combat.BossWindup,Is.Zero);Assert.That(run.warning.gameObject.activeSelf,Is.False);
                run.feedback.ToggleMotion();
                run.DamageSquad(100);run.Advance(.8f);Assert.That(run.squad.UnitCount,Is.EqualTo(units));
                Assert.That(run.resultPanel.transform.Find("Reward seal").localScale,Is.EqualTo(Vector3.one),"Reduced motion skips the medal stamp.");
                run.feedback.ToggleMotion();run.Advance(1.2f);
            }
            finally{run.level=original;run.ResetRun();Object.Destroy(trial);Object.Destroy(boss);}
        }

        [UnityTest]
        public IEnumerator FullSquadFitsBothPhonePortraitRatiosAtSteeringLimits()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();run.squad.SetCount(60);
            Assert.That(run.resultShade.transform.GetSiblingIndex(),Is.LessThan(run.resultPanel.transform.GetSiblingIndex()),"Dimmer must stay behind the reward card after repeated art migrations.");
            var camera=run.feedback.view;Assert.That(camera.orthographic,Is.False);
            var previous=camera.targetTexture;
            foreach(int height in new[]{960,1170})
            {
                var rt=new RenderTexture(540,height,24);camera.targetTexture=rt;
                foreach(float x in new[]{-2.65f,2.65f})
                {
                    run.squad.SetDesiredX(x);
                    for(int i=0;i<180;i++){run.squad.Tick(.02f);run.combat.Tick(.02f);run.feedback.Tick(.02f);}
                    for(int i=0;i<60;i++)
                    {
                        Vector3 foot=camera.WorldToViewportPoint(run.squad.bodies[i].parent.position);
                        Vector3 head=camera.WorldToViewportPoint(run.squad.bodies[i].parent.position+Vector3.up*1.15f);
                        Assert.That(foot.x,Is.InRange(.02f,.98f));Assert.That(foot.y,Is.GreaterThan(.08f));
                        Assert.That(head.y,Is.LessThan(.9f));
                    }
                }
                camera.targetTexture=previous;Object.Destroy(rt);
            }
            run.DamageSquad(1);run.Advance(.02f);
            var corners=new Vector3[4];run.powerLabel.rectTransform.GetWorldCorners(corners);
            foreach(var corner in corners)Assert.That(camera.WorldToScreenPoint(corner).x,Is.InRange(0,Screen.width));
            run.ResetRun();Assert.That(run.powerLabel.gameObject.activeSelf,Is.False);Assert.That(run.resultShade.activeSelf,Is.False);
        }
        [UnityTest]
        public IEnumerator RobotBodiesStayInsideCameraAtAllSquadSizesAndLimits()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var camera=run.feedback.view;var previous=camera.targetTexture;
            foreach(int height in new[]{960,1170})
            {
                var rt=new RenderTexture(540,height,24);camera.targetTexture=rt;
                try
                {
                    foreach(int count in new[]{5,10,13,17,26,30,60})foreach(float x in new[]{-2.65f,2.65f})
                    {
                        run.ResetRun();run.squad.SetCount(count);run.squad.SetDesiredX(x);
                        for(int i=0;i<180;i++){run.squad.Tick(.02f);run.combat.Tick(.02f);run.feedback.Tick(.02f);}
                        for(int i=0;i<count;i++)foreach(var renderer in run.squad.bodies[i].GetComponentsInChildren<Renderer>())
                        {
                            if(!renderer.gameObject.activeInHierarchy)continue;
                            var bounds=renderer.bounds;
                            for(int corner=0;corner<8;corner++)
                            {
                                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                                var screen=camera.WorldToViewportPoint(point);
                                Assert.That(screen.x,Is.InRange(.01f,.99f),$"{count} robots at x={x}: {renderer.name} clipped horizontally.");
                                Assert.That(screen.y,Is.InRange(.07f,.89f),$"{count} robots at x={x}: {renderer.name} clipped vertically.");
                            }
                        }
                    }
                }
                finally { camera.targetTexture=previous;Object.Destroy(rt); }
            }
        }
        [UnityTest]
        public IEnumerator AuthoredGrowthKeepsRobotsFramedDuringJoinAnimation()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var camera=run.feedback.view;var previous=camera.targetTexture;
            var rt=new RenderTexture(540,1170,24);camera.targetTexture=rt;
            try
            {
                foreach(float x in new[]{-2.65f,2.65f})
                {
                    run.ResetRun();run.squad.SetDesiredX(x);
                    for(int i=0;i<100;i++){run.squad.Tick(.02f);run.combat.Tick(.02f);run.feedback.Tick(.02f);}
                    foreach(int count in new[]{15,30,40,55,60})
                    {
                        run.squad.SetCount(count);run.feedback.Play(true);
                        for(int frame=0;frame<30;frame++)
                        {
                            run.squad.Tick(.02f);run.combat.Tick(.02f);run.feedback.Tick(.02f);
                            for(int i=0;i<count;i++)foreach(var renderer in run.squad.bodies[i].GetComponentsInChildren<Renderer>())
                            {
                                if(!renderer.gameObject.activeInHierarchy)continue;
                                var bounds=renderer.bounds;
                                for(int corner=0;corner<8;corner++)
                                {
                                    var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                                    var screen=camera.WorldToViewportPoint(point);
                                    Assert.That(screen.x,Is.InRange(0,1),$"Growth to {count}, frame {frame}: {renderer.name} clipped horizontally.");
                                    Assert.That(screen.y,Is.InRange(.05f,.9f),$"Growth to {count}, frame {frame}: {renderer.name} clipped vertically.");
                                }
                            }
                        }
                    }
                }
            }
            finally { camera.targetTexture=previous;Object.Destroy(rt); }
        }
        [UnityTest]
        public IEnumerator ShootableGateUsesNearestHitCapsAndResetsWithoutMutatingData()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline"); yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var gate=run.combat.ShootableGates[0];
            var effect=new GateEffect{kind=GateKind.Add,value=-5,shootable=true,maxValue=10};
            var safe=new GateEffect{kind=GateKind.Add,value=8};
            gate.Prepare(effect,safe);gate.transform.position=Vector3.forward*5;
            var enemy=ScriptableObject.CreateInstance<EnemySettings>();enemy.health=100;enemy.speed=0;
            run.combat.SpawnTarget(-2,enemy,2);
            run.combat.Fire(new Vector3(-2,.6f,0),1);run.combat.Tick(.4f);
            Assert.That(run.combat.TotalHits,Is.EqualTo(1));Assert.That(gate.leftLabel.text,Is.EqualTo("-5"));
            run.combat.ResetCombat();run.combat.SpawnTarget(-2,enemy,9);
            run.combat.Fire(new Vector3(-2,.6f,0),1);run.combat.Tick(.4f);
            Assert.That(run.combat.TotalHits,Is.Zero,"Gate must intercept before the farther enemy.");
            Assert.That(gate.leftLabel.text,Is.EqualTo("-4"));Assert.That(run.combat.ActiveProjectiles,Is.Zero);
            for(int i=0;i<30;i++)gate.Charge(-2);
            Assert.That(gate.leftLabel.text,Is.EqualTo("+10"));
            run.combat.Fire(new Vector3(-2,.6f,0),1);run.combat.Tick(.4f);
            Assert.That(run.combat.TotalHits,Is.EqualTo(1),"A fully charged gate lets shots through.");
            Assert.That(effect.value,Is.EqualTo(-5),"Runtime charge must not mutate level data.");
            gate.ResetRow();gate.Prepare(effect,safe);Assert.That(gate.leftLabel.text,Is.EqualTo("-5"));
            run.combat.ResetCombat();run.squad.transform.position=Vector3.left*2;run.squad.SetDesiredX(-2);
            run.DamageSquad(1);gate.transform.position=Vector3.zero;run.Advance(.02f);
            Assert.That(run.State,Is.EqualTo(RunState.Defeat),"Chosen losses bypass contact invulnerability and zero units ends the run.");
            run.ResetRun();Assert.That(run.combat.ShootableGates[0].gameObject.activeSelf,Is.False);
            Object.Destroy(enemy);
        }
        [UnityTest]
        public IEnumerator GateProgressAndPowerExplainRewardsWithoutAccumulatingObjects()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var gate=run.combat.ShootableGates[0];var reward=new GateEffect{kind=GateKind.Add,value=-1,shootable=true,uncapped=true,shotsPerIncrement=4};
            gate.Prepare(reward,default,10);
            Assert.That(gate.leftDetail.text,Does.Contain("LOSE ROBOTS"));
            var fill=gate.leftFrame.Find("Shot progress fill");int objects=gate.GetComponentsInChildren<Transform>(true).Length;
            float empty=fill.localScale.x;gate.Charge(-2);Assert.That(fill.localScale.x,Is.GreaterThan(empty));
            for(int i=0;i<3;i++)gate.Charge(-2);
            Assert.That(gate.leftDetail.text,Does.Contain("SAFE PASS"));
            for(int i=0;i<4;i++)gate.Charge(-2);
            Assert.That(gate.leftDetail.text,Does.Contain("ADD ROBOTS"));
            for(int i=0;i<3;i++){gate.ResetRow();gate.Prepare(reward,default);}
            Assert.That(gate.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
            Assert.That(fill.localScale.x,Is.EqualTo(empty));
            run.squad.SetCount(120);run.Advance(.02f);
            Assert.That(run.upgradeLabel.text,Does.Contain("POWER 120"));
            run.ResetRun();Assert.That(run.upgradeLabel.text,Does.Contain("POWER 5"));
            var original=run.level;var timed=Object.Instantiate(original);
            timed.encounters=new Encounter[0];timed.deadline=60;run.level=timed;run.ResetRun();run.Advance(60);
            Assert.That(run.State,Is.EqualTo(RunState.Defeat));Assert.That(run.squad.UnitCount,Is.EqualTo(5));
            Assert.That(run.resultTitle.text,Is.EqualTo("TIME'S UP"));
            run.level=original;run.ResetRun();Object.Destroy(timed);
        }
        [UnityTest]
        public IEnumerator BossArrivalSignalsArmorThenOpensAndResetsCleanly()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var boss=ScriptableObject.CreateInstance<EnemySettings>();boss.kind=EnemyKind.Boss;boss.health=100;boss.size=2;boss.speed=0;
            run.combat.SpawnTarget(0,boss,9);run.combat.Fire(Vector3.zero,10000);run.combat.Tick(.4f);
            Assert.That(run.combat.BossArmored,Is.True);Assert.That(run.combat.BossHealth,Is.EqualTo(1));
            run.SetPaused(true);float remaining=run.combat.BossArrivalRemaining;run.Advance(2);
            Assert.That(run.combat.BossArrivalRemaining,Is.EqualTo(remaining));run.SetPaused(false);
            run.combat.Tick(1.21f);Assert.That(run.combat.BossArmored,Is.False);
            run.combat.Fire(Vector3.zero,10000);run.combat.Tick(.4f);Assert.That(run.combat.BossDefeated,Is.True);
            run.ResetRun();Assert.That(run.combat.BossArrivalRemaining,Is.Zero);Assert.That(run.combat.BossDefeated,Is.False);
            Object.Destroy(boss);
        }
        [UnityTest]
        public IEnumerator BossDeadlineIsVisibleAndFreezesWhilePaused()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var original=run.level;var trial=Object.Instantiate(original);
            var boss=ScriptableObject.CreateInstance<EnemySettings>();boss.kind=EnemyKind.Boss;boss.health=1000000;boss.size=2;
            trial.deadline=12;trial.encounters=new[]{new Encounter{time=.1f,kind=EncounterKind.Boss,enemy=boss}};
            run.level=trial;run.ResetRun();run.squad.SetCount(60);
            for(int i=0;i<105;i++)run.Advance(.02f);
            Assert.That(run.bossLabel.text,Does.Contain("10s LEFT"));
            run.SetPaused(true);run.Advance(5);Assert.That(run.bossLabel.text,Does.Contain("10s LEFT"));
            run.SetPaused(false);for(int i=0;i<50;i++)run.Advance(.02f);
            Assert.That(run.bossLabel.text,Does.Contain("9s LEFT"));
            run.ResetRun();Assert.That(run.bossBar.transform.parent.gameObject.activeSelf,Is.False);
            run.level=original;run.ResetRun();Object.Destroy(trial);Object.Destroy(boss);
        }
        [UnityTest]
        public IEnumerator GateFramesStayFixedAndNumbersVisibleThroughApproachAndSteering()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var camera=run.feedback.view;var previous=camera.targetTexture;
            foreach(int height in new[]{960,1170})
            {
                var rt=new RenderTexture(540,height,24);camera.targetTexture=rt;
                try
                {
                    foreach(float x in new[]{-2.65f,2.65f})
                    {
                        run.ResetRun();run.squad.SetDesiredX(x);
                        for(int i=0;i<90;i++){run.squad.Tick(.02f);run.feedback.Tick(.02f);}
                        var gate=run.combat.ShootableGates[0];
                        gate.Prepare(new GateEffect{kind=GateKind.Add,value=-20,shootable=true,uncapped=true},new GateEffect{kind=GateKind.Add,value=150},15);
                        var leftPosition=gate.leftFrame.localPosition;var rightPosition=gate.rightFrame.localPosition;
                        var leftScale=gate.leftFrame.localScale;var rightScale=gate.rightFrame.localScale;
                        while(gate.transform.position.z>1)
                        {
                            run.squad.SetDesiredX(-run.squad.DesiredX);run.squad.Tick(.02f);
                            gate.Tick(.02f,run.squad,out _);
                            Assert.That(gate.leftFrame.localPosition,Is.EqualTo(leftPosition));Assert.That(gate.rightFrame.localPosition,Is.EqualTo(rightPosition));
                            Assert.That(gate.leftFrame.localScale,Is.EqualTo(leftScale));Assert.That(gate.rightFrame.localScale,Is.EqualTo(rightScale));
                        }
                        gate.transform.position=new Vector3(0,0,.8f);
                        gate.Tick(.02f,run.squad,out _);
                        Capture("Gate-Spacing-"+height+(x<0?"-Left":"-Right"),540,height);
                        Assert.That(gate.ShotCrossing(gate.leftFrame.position.x,0,3,.02f,out _),Is.True);
                        Assert.That(gate.ShotCrossing(-3.4f,0,3,.02f,out _),Is.False,"Shots outside the approaching sign must pass through.");
                        foreach(var label in new[]{gate.leftLabel,gate.rightLabel})
                        {
                            var bounds=label.GetComponent<Renderer>().bounds;
                            foreach(float bx in new[]{bounds.min.x,bounds.max.x})foreach(float by in new[]{bounds.min.y,bounds.max.y})foreach(float bz in new[]{bounds.min.z,bounds.max.z})
                                Assert.That(camera.WorldToViewportPoint(new Vector3(bx,by,bz)).x,Is.InRange(.01f,.99f),$"Never hide {label.text}: height={height}, squad={x}, bounds={bounds}.");
                        }
                    }
                }
                finally {camera.targetTexture=previous;Object.Destroy(rt);}
            }
        }
        [UnityTest]
        public IEnumerator UncappedLaneGateChangesColorKeepsGrowingAndCanBeDodged()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var gate=run.combat.ShootableGates[0];
            var effect=new GateEffect{kind=GateKind.Add,value=-20,shootable=true,uncapped=true,shotsPerIncrement=1};
            gate.Prepare(effect,default,8,1);
            var tint=new MaterialPropertyBlock();gate.leftPlate.GetPropertyBlock(tint);
            Color negative=tint.GetColor("_BaseColor");
            Assert.That(negative.b,Is.GreaterThan(negative.r));
            Assert.That(gate.rightFrame.gameObject.activeSelf,Is.False);
            Assert.That(gate.ShotCrossing(-2,0,10,.02f,out _),Is.False);
            Assert.That(gate.ShotCrossing(2.6f,0,10,.02f,out _),Is.True);
            int crossings=0;
            for(int i=0;i<150;i++)if(gate.Charge(2.6f))crossings++;
            Assert.That(crossings,Is.EqualTo(1));Assert.That(gate.leftLabel.text,Is.EqualTo("+130"));
            gate.leftPlate.GetPropertyBlock(tint);Color positive=tint.GetColor("_BaseColor");
            Assert.That(positive.r,Is.GreaterThan(positive.b));
            Assert.That(gate.ShotCrossing(2.6f,0,10,.02f,out _),Is.True,"High rewards must still accept shots.");
            gate.transform.position=Vector3.zero;
            Assert.That(gate.Tick(.02f,run.squad,out _),Is.False,"Staying outside the lane skips its reward.");
            Assert.That(run.squad.UnitCount,Is.EqualTo(5));Assert.That(gate.gameObject.activeSelf,Is.False);
            gate.ResetRow();gate.Prepare(effect,default,8,0);
            Assert.That(gate.leftLabel.text,Is.EqualTo("-20"));Assert.That(effect.value,Is.EqualTo(-20));
            for(int i=0;i<150;i++)gate.Charge(0);
            gate.transform.position=Vector3.zero;
            Assert.That(gate.Tick(.02f,run.squad,out _),Is.True);
            Assert.That(run.squad.UnitCount,Is.EqualTo(135));Assert.That(run.squad.VisibleCount,Is.EqualTo(60));
            Assert.That(gate.Tick(.02f,run.squad,out _),Is.False);
            run.squad.Tick(.5f);run.DamageSquad(5);
            Assert.That(run.squad.UnitCount,Is.EqualTo(130));
            Assert.That(run.squad.bodies.Length,Is.EqualTo(60));
            run.ResetRun();Assert.That(run.squad.UnitCount,Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator RecruitsAboveVisualPoolRetainVolleyPowerAndNoObjectsAccumulate()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            int objects=run.squad.GetComponentsInChildren<Transform>(true).Length;
            var enemy=ScriptableObject.CreateInstance<EnemySettings>();enemy.health=3;enemy.speed=0;enemy.size=8;
            run.squad.SetCount(180);run.combat.SpawnTarget(0,enemy,3);
            run.squad.Tick(.02f);run.combat.Tick(.2f);
            Assert.That(run.combat.Salvaged,Is.EqualTo(1),"Each visible shot represents three recruits.");
            Assert.That(run.squad.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
            Assert.That(run.combat.DroppedShots,Is.Zero);run.ResetRun();Object.Destroy(enemy);
        }
        [UnityTest]
        public IEnumerator OnlyLiveBossCanStartLargeAttacksAndIgnoringItsWarningHurts()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline"); yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var original=run.level;var trial=Object.Instantiate(original);
            var heavy=ScriptableObject.CreateInstance<EnemySettings>();heavy.kind=EnemyKind.Heavy;heavy.health=1000000;
            var boss=ScriptableObject.CreateInstance<EnemySettings>();boss.kind=EnemyKind.Boss;boss.health=1000000;boss.contactDamage=18;
            trial.encounters=new[]{new Encounter{time=.1f,kind=EncounterKind.Hazard},new Encounter{time=.2f,kind=EncounterKind.Wave,enemy=heavy,approachZ=14},new Encounter{time=.4f,kind=EncounterKind.Boss,enemy=boss}};
            try
            {
                run.level=trial;run.ResetRun();run.Advance(.1f);run.Advance(.2f);
                Assert.That(run.BossStarted,Is.False);Assert.That(run.HazardRemaining,Is.Zero);Assert.That(run.warning.gameObject.activeSelf,Is.False);
                run.Advance(.11f);Assert.That(run.BossStarted,Is.True);Assert.That(run.HazardRemaining,Is.GreaterThan(0));
                for(int i=0;i<100&&run.State==RunState.Running;i++)run.Advance(.02f);
                Assert.That(run.State,Is.EqualTo(RunState.Defeat),"Only the living boss's warned attack should hit the idle squad.");
            }
            finally{run.level=original;run.ResetRun();Object.Destroy(trial);Object.Destroy(heavy);Object.Destroy(boss);}
        }

        [UnityTest]
        public IEnumerator AlternateLevelAndRightGateRouteCanWin()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
#if UNITY_EDITOR
            run.level=UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Slice/ReverseShift.asset");
#endif
            run.ResetRun();
            for(int i=0;i<4600 && run.State==RunState.Running;i++)
            {
                float x = Steering(run, 2);
                run.squad.SetDesiredX(x);run.Advance(.02f);
                if(i%200==0)yield return null;
            }
            Debug.Log($"Alternate route: {run.Elapsed:0.00}s, {run.State}, units={run.squad.UnitCount}, gates={run.GatesCollected}");
            Assert.That(run.State,Is.EqualTo(RunState.Victory));Assert.That(run.GatesCollected,Is.InRange(6,8));
            Assert.That(run.combat.DroppedShots,Is.Zero);
        }
        [UnityTest]
        public IEnumerator GrowthSingleGatePauseDefeatAndCompleteReset()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline"); yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            int objects=run.combat.GetComponentsInChildren<Transform>(true).Length;
            run.squad.Apply(new GateEffect{kind=GateKind.Add,value=10});Assert.That(run.squad.UnitCount,Is.EqualTo(15));
            run.squad.Apply(new GateEffect{kind=GateKind.Multiply,value=2});Assert.That(run.squad.UnitCount,Is.EqualTo(30));
            run.squad.Apply(new GateEffect{kind=GateKind.Damage,value=2});
            run.squad.Apply(new GateEffect{kind=GateKind.FireRate,value=1.5f});
            run.squad.Apply(new GateEffect{kind=GateKind.Weapon,value=2});
            Assert.That(run.squad.Damage,Is.EqualTo(3));Assert.That(run.squad.FireRate,Is.EqualTo(1.725f).Within(.001f));
            run.squad.SetDesiredX(2);run.squad.Tick(.2f);
            var gate=Object.Instantiate(run.gatePrefab);
            gate.Prepare(new GateEffect{kind=GateKind.Add,value=1},new GateEffect{kind=GateKind.Add,value=2});
            gate.transform.position=Vector3.zero;
            Assert.That(gate.Tick(.02f,run.squad,out _),Is.True); Assert.That(run.squad.UnitCount,Is.EqualTo(32));
            Assert.That(gate.Tick(.02f,run.squad,out _),Is.False);
            gate.Tick(.3f,run.squad,out _);Assert.That(gate.gameObject.activeSelf,Is.False);
            Assert.That(run.squad.UnitCount,Is.EqualTo(32),"Collect animation must not apply a second reward.");
            gate.ResetRow();gate.Prepare(new GateEffect{kind=GateKind.Add,value=1},new GateEffect{kind=GateKind.Add,value=2});
            Assert.That(gate.leftFrame.gameObject.activeSelf && gate.rightFrame.gameObject.activeSelf,Is.True);
            Object.Destroy(gate.gameObject);
            run.DamageSquad(2);run.DamageSquad(2);Assert.That(run.squad.UnitCount,Is.EqualTo(30),"Invulnerability prevents overlapping losses.");
            run.SetPaused(true);run.Advance(1);Assert.That(run.Elapsed,Is.Zero);run.SetPaused(false);
            run.Advance(.7f);run.DamageSquad(100);Assert.That(run.State,Is.EqualTo(RunState.Defeat));
            for(int n=0;n<3;n++)
            {
                run.ResetRun();Assert.That(run.State,Is.EqualTo(RunState.Running));Assert.That(run.squad.UnitCount,Is.EqualTo(5));
                Assert.That(run.squad.Damage,Is.EqualTo(1));Assert.That(run.squad.FireRate,Is.EqualTo(1));Assert.That(run.squad.WeaponTier,Is.EqualTo(1));
                Assert.That(run.GatesCollected,Is.Zero);Assert.That(run.EncounterIndex,Is.Zero);Assert.That(run.HazardRemaining,Is.Zero);
                Assert.That(run.combat.ActiveTargets+run.combat.ActiveProjectiles+run.combat.ActiveEffects,Is.Zero);
                Assert.That(run.combat.Contact,Is.Not.Null);
                Assert.That(run.combat.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));run.Advance(.02f);
            }
            var lethal = ScriptableObject.CreateInstance<EnemySettings>();
            lethal.health=10000;lethal.speed=20;lethal.contactDamage=100;
            run.combat.SpawnTarget(0,lethal,.7f);run.Advance(.02f);
            Assert.That(run.State,Is.EqualTo(RunState.Defeat));
            Assert.That(run.combat.ActiveTargets,Is.Zero,"Contact defeat must not decrement an already-cleared pool.");
            Object.Destroy(lethal);
        }
        [UnityTest]
        public IEnumerator AuthoredRunCanWinAndRestartWithStablePools()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            bool gateImage=false,powerImage=false,bossImage=false,negativeImage=false,growthImage=false;
            var timer=new System.Diagnostics.Stopwatch();
            int steps=0;
            for(int i=0;i<4600 && run.State==RunState.Running;i++)
            {
                float x = Steering(run, -2);
                run.squad.SetDesiredX(x);
                timer.Start();run.Advance(.02f);timer.Stop();steps++;
                if(!negativeImage && run.Elapsed>.85f){Capture("Slice-Shootable");negativeImage=true;}
                if(!growthImage && run.Elapsed>10.1f){Capture("Slice-Growth");growthImage=true;}
                if(!gateImage && run.Elapsed>7){Capture("Slice-Gates");gateImage=true;}
                if(!powerImage && run.Elapsed>54){Capture("Slice-Power");Capture("Slice-Tall",540,1170);powerImage=true;}
                if(!bossImage && run.Elapsed>57.7f){Capture("Slice-Boss");bossImage=true;}
                if(i%100==0)yield return null;
            }
            Debug.Log($"Slice simulation: {run.Elapsed:0.00}s, {run.State}, units={run.squad.UnitCount}, gates={run.GatesCollected}, mean Advance CPU={timer.Elapsed.TotalMilliseconds/steps:0.000}ms, dropped shots={run.combat.DroppedShots}");
            Assert.That(run.GatesCollected,Is.InRange(6,8));Assert.That(run.State,Is.EqualTo(RunState.Victory));
            Assert.That(run.Elapsed,Is.InRange(60,90));Assert.That(run.combat.DroppedShots,Is.Zero);
            run.squad.transform.position=Vector3.left*2.65f;run.Advance(.3f);
            Assert.That(run.squad.transform.position,Is.EqualTo(Vector3.zero),"Gather surviving robots under their reward after a dodge.");
            run.Advance(.5f);
            var seal=run.resultPanel.transform.Find("Reward seal");
            Assert.That(seal.localScale.x,Is.LessThan(.1f),"Reward stamp waits for the card entrance.");
            Capture("Polish-Medal-Start");
            run.Advance(.18f);Assert.That(seal.localScale.x,Is.GreaterThan(1),"Medal stamp has a brief overshoot.");
            Capture("Polish-Medal-Stamp");
            run.Advance(1.02f);Capture("Slice-Victory");
            Assert.That(seal.localScale,Is.EqualTo(Vector3.one));
            Assert.That(run.resultStats.preferredHeight,Is.LessThanOrEqualTo(run.resultStats.rectTransform.rect.height),"Victory stats and next-shift line must fit without clipping.");
            int row=run.EncounterIndex;run.Advance(2);Assert.That(run.EncounterIndex,Is.EqualTo(row));
            run.ResetRun();Assert.That(run.combat.BossDefeated,Is.False);Assert.That(run.BossStarted,Is.False);
            run.SetPaused(true);Assert.That(seal.localScale,Is.EqualTo(Vector3.one));run.SetPaused(false);
        }
        static float Steering(SliceRun run, float choice)
            => run.HazardRemaining > 0 ? run.HazardLane == 0 ? -2.6f : 0 : run.BossStarted ? 0 : choice;

        [UnityTest]
        public IEnumerator FirstTenSecondsRewardSteeringAndVictoryOpensDifferentShift()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var first=run.level;
            for(int i=0;i<500;i++){run.squad.SetDesiredX(-2);run.Advance(.02f);}
            Assert.That(run.GatesCollected,Is.EqualTo(2));Assert.That(run.squad.UnitCount,Is.GreaterThanOrEqualTo(20));
            Assert.That(run.combat.Salvaged,Is.GreaterThanOrEqualTo(8),"Upgrades must have immediate targets to overpower.");
            Assert.That(run.powerLabel.gameObject.activeSelf,Is.True,"Steering must not hide the centered upgrade announcement.");
            bool warned=false;
            for(int i=0;i<4500&&run.State==RunState.Running;i++)
            {
                run.squad.SetDesiredX(Steering(run,-2));run.Advance(.02f);
                if(!warned&&run.HazardRemaining>0)
                {
                    warned=true;string instruction=run.hint.text;int units=run.squad.UnitCount;
                    run.DamageSquad(1);Assert.That(run.squad.UnitCount,Is.EqualTo(units-1));
                    Assert.That(run.hint.text,Is.EqualTo(instruction),"A damage toast must not hide an active dodge warning.");
                }
                if(i%200==0)yield return null;
            }
            Assert.That(warned,Is.True);
            Assert.That(run.State,Is.EqualTo(RunState.Victory));
            run.Advance(1.6f);Assert.That(run.replayButton.GetComponentInChildren<UnityEngine.UI.Text>().text,Does.Contain("NEXT SHIFT"));
            run.replayButton.onClick.Invoke();Assert.That(run.level,Is.Not.SameAs(first));Assert.That(run.squad.UnitCount,Is.EqualTo(5));
            run.DamageSquad(100,"Test failure");run.Advance(.21f);
            Assert.That(run.resultPanel.activeSelf,Is.True);Assert.That(run.resultStats.text,Is.EqualTo("Test failure"));
            Assert.That(run.squadLabel.text,Does.EndWith("00"),"Defeat must not leave a stale living-squad count.");
            var retry=run.level;run.replayButton.onClick.Invoke();Assert.That(run.level,Is.SameAs(retry));
            Assert.That(run.GatesCollected,Is.Zero);Assert.That(run.combat.TotalShots,Is.Zero);
        }
        [UnityTest]
        public IEnumerator ChargedRecruitNeedsSustainedFireAndResetsItsPartialCharge()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var gate=run.combat.ShootableGates[0];
            var effect=new GateEffect{kind=GateKind.Add,value=2,shootable=true,maxValue=15,shotsPerIncrement=15};
            gate.Prepare(effect,new GateEffect{kind=GateKind.Damage,value=2},12);
            gate.Tick(.01f,run.squad,out _);
            Assert.That(gate.leftDetail.text,Does.Not.StartWith("▼"));Assert.That(gate.rightDetail.text,Does.Not.StartWith("▼"));
            run.squad.transform.position=Vector3.left*2;
            gate.Charge(-2);gate.Tick(.01f,run.squad,out _);
            Assert.That(gate.leftLabel.transform.localScale.x,Is.GreaterThan(1),"Partial hits need visible feedback before a number increases.");
            Assert.That(gate.leftDetail.text,Does.StartWith("▼"));Assert.That(gate.rightDetail.text,Does.Not.StartWith("▼"));
            gate.ReducedMotion=true;gate.Tick(.01f,run.squad,out _);Assert.That(gate.leftLabel.transform.localScale,Is.EqualTo(Vector3.one));
            gate.ReducedMotion=false;gate.Prepare(effect,new GateEffect{kind=GateKind.Damage,value=2},12);
            for(int i=0;i<14;i++)gate.Charge(-2);Assert.That(gate.leftLabel.text,Is.EqualTo("+2"));
            gate.Charge(-2);Assert.That(gate.leftLabel.text,Is.EqualTo("+3"));
            gate.Charge(-2);gate.ResetRow();gate.Prepare(effect,effect);
            for(int i=0;i<14;i++)gate.Charge(-2);Assert.That(gate.leftLabel.text,Is.EqualTo("+2"));
        }
        [UnityTest]
        public IEnumerator FatalHazardCannotReopenPopupsOverDefeatCard()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var original=run.level;var trial=Object.Instantiate(original);
            var boss=ScriptableObject.CreateInstance<EnemySettings>();boss.kind=EnemyKind.Boss;boss.health=1000000;boss.contactDamage=18;
            trial.encounters=new[]{new Encounter{time=.1f,kind=EncounterKind.Boss,enemy=boss}};
            try
            {
                run.level=trial;run.ResetRun();run.squad.SetCount(1);run.Advance(.1f);
                Assert.That(run.HazardRemaining,Is.GreaterThan(0));
                for(int i=0;i<100&&run.State==RunState.Running;i++)run.Advance(.02f);
                Assert.That(run.State,Is.EqualTo(RunState.Defeat));Assert.That(run.resultStats.text,Does.Contain("striped lane"));
                run.Advance(1);Assert.That(run.resultPanel.activeSelf,Is.True);
                Assert.That(run.SquadHealth.fillAmount,Is.LessThan(.01f));
                Assert.That(run.SquadHealthTrail.fillAmount,Is.LessThan(.2f));
                Assert.That(run.powerLabel.gameObject.activeSelf,Is.False);Assert.That(run.toastPlate.activeSelf,Is.False);Assert.That(run.hint.gameObject.activeSelf,Is.False);
                Assert.That(run.squadLabel.text,Does.EndWith("00"));
            }
            finally{run.level=original;run.ResetRun();Object.Destroy(trial);Object.Destroy(boss);}
        }
        [UnityTest]
        public IEnumerator PausePrimaryActionResumesAndSecondaryRestartResetsSameShift()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            for(int i=0;i<100;i++)run.Advance(.02f);
            var level=run.level;float elapsed=run.Elapsed;
            run.squad.SetCount(20);run.pauseButton.onClick.Invoke();
            var restart=run.resultPanel.transform.Find("Paused restart").GetComponent<UnityEngine.UI.Button>();
            Assert.That(restart.gameObject.activeInHierarchy,Is.True);
            Canvas.ForceUpdateCanvases();
            var primaryCorners=new Vector3[4];var restartCorners=new Vector3[4];
            run.replayButton.GetComponent<RectTransform>().GetWorldCorners(primaryCorners);restart.GetComponent<RectTransform>().GetWorldCorners(restartCorners);
            Assert.That(primaryCorners[0].y,Is.GreaterThan(restartCorners[1].y),"Pause buttons must not overlap.");
            Assert.That(restart.GetComponent<RectTransform>().rect.height,Is.GreaterThanOrEqualTo(66));
            Assert.That(run.replayButton.GetComponentInChildren<UnityEngine.UI.Text>().text,Does.Contain("RESUME"));
            run.replayButton.onClick.Invoke();
            Assert.That(run.IsPaused,Is.False);Assert.That(run.Elapsed,Is.EqualTo(elapsed));Assert.That(run.squad.UnitCount,Is.EqualTo(20));
            Assert.That(restart.gameObject.activeSelf,Is.False);
            run.pauseButton.onClick.Invoke();restart.onClick.Invoke();
            Assert.That(run.IsPaused,Is.False);Assert.That(run.Elapsed,Is.Zero);Assert.That(run.squad.UnitCount,Is.EqualTo(5));
            Assert.That(run.level,Is.SameAs(level));Assert.That(run.resultPanel.activeSelf,Is.False);
        }
        [UnityTest]
        public IEnumerator SteeringInstructionSurvivesHesitationAndDamageResetRestoresHud()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var color=run.squadLabel.color;
            for(int i=0;i<160;i++)run.Advance(.02f);
            Assert.That(run.hint.text,Does.Contain("DRAG"));Assert.That(run.hint.text,Does.Contain("SHOOT"));
            Assert.That(run.hint.gameObject.activeSelf,Is.True);
            for(int i=0;i<180;i++)run.Advance(.02f);
            Assert.That(run.hint.text,Does.Contain("DRAG"));Assert.That(run.hint.gameObject.activeSelf,Is.True);
            run.DamageSquad(1);Assert.That(run.squadLabel.color,Is.Not.EqualTo(color));
            Assert.That(run.combat.ActiveEffects,Is.GreaterThan(0));
            run.ResetRun();Assert.That(run.squadLabel.color,Is.EqualTo(color));
            Assert.That(run.squadLabel.text,Does.EndWith("05"));
        }
        [UnityTest]
        public IEnumerator ShootingAndSteeringStayConsistentAcrossFrameRates()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            int min=int.MaxValue,max=0;
            foreach(int fps in new[]{30,60,120})
            {
                run.ResetRun();run.squad.SetDesiredX(2);
                run.squad.Tick(1f/fps);
                float lean=Mathf.DeltaAngle(0,run.squad.bodies[0].localEulerAngles.z);
                Assert.That(lean,Is.InRange(-15,-1),"Visual lean eases in while movement remains immediate.");
                for(int i=1;i<fps*6;i++){run.squad.Tick(1f/fps);run.combat.Tick(1f/fps);}
                min=Mathf.Min(min,run.combat.TotalShots);max=Mathf.Max(max,run.combat.TotalShots);
                Assert.That(run.squad.transform.position.x,Is.EqualTo(2).Within(.001f));
                Assert.That(run.combat.DroppedShots,Is.Zero);
            }
            Assert.That(max-min,Is.LessThanOrEqualTo(5),"Only the final boundary volley may differ, never accumulated frame rounding.");
            run.squad.ReducedMotion=true;run.squad.SetDesiredX(-2);run.squad.Tick(.02f);
            Assert.That(run.squad.bodies[0].localRotation,Is.EqualTo(Quaternion.identity));
            Assert.That(run.squad.bodies[0].localPosition,Is.EqualTo(Vector3.zero));
        }
        [UnityTest]
        public IEnumerator SmallerFirepowerBuildCanWinBothAuthoredShifts()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var first=run.level;
            foreach(bool alternate in new[]{false,true})
            {
                run.level=alternate?run.nextShift:first;run.ResetRun();
                for(int i=0;i<4500&&run.State==RunState.Running;i++)
                {
                    run.squad.SetDesiredX(Steering(run,alternate?-2:2));run.Advance(.02f);
                    if(i%200==0)yield return null;
                }
                Debug.Log($"Firepower route ({run.level.title}): {run.State} at {run.Elapsed:0.00}s; units={run.squad.UnitCount}, damage={run.squad.Damage}, rate={run.squad.FireRate:0.00}");
                Assert.That(run.State,Is.EqualTo(RunState.Victory));Assert.That(run.Elapsed,Is.InRange(60,90));
                Assert.That(run.GatesCollected,Is.InRange(6,8));Assert.That(run.combat.DroppedShots+run.combat.DroppedTargets,Is.Zero);
            }
        }
        [UnityTest]
        public IEnumerator ShotsReachDistantTargetsLastLongerAndStillExpireAtFixedRange()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var enemy=ScriptableObject.CreateInstance<EnemySettings>();enemy.health=100;enemy.speed=0;enemy.size=1;
            run.ResetRun();run.combat.SpawnTarget(0,enemy,18);
            run.combat.Fire(new Vector3(0,.6f,11.9f),1);run.combat.Tick(5f/run.combat.bulletSpeed);
            Assert.That(run.combat.ActiveProjectiles,Is.EqualTo(1),"Shots must continue beyond the old12-unit limit.");
            run.combat.Tick(2f/run.combat.bulletSpeed);Assert.That(run.combat.TotalHits,Is.EqualTo(1),"Distant enemies must be hittable.");
            run.ResetRun();run.combat.Fire(new Vector3(0,.6f,0),1);run.combat.Tick(14f/run.combat.bulletSpeed);
            Assert.That(run.combat.ActiveProjectiles,Is.EqualTo(1));
            run.combat.Tick(10f/run.combat.bulletSpeed);Assert.That(run.combat.ActiveProjectiles,Is.Zero,"Missed shots must return to the pool.");
            foreach(float dt in new[]{1f/120,1f/60,1f/30,.4f})
            {
                run.ResetRun();run.combat.SpawnTarget(0,enemy,23.5f);
                run.combat.Fire(new Vector3(0,.6f,21.9f),1);run.combat.Tick(dt);
                Assert.That(run.combat.TotalHits,Is.Zero);Assert.That(run.combat.ActiveProjectiles,Is.Zero);
            }
            Object.Destroy(enemy);
        }
        [UnityTest]
        public IEnumerator MixedArmyAndRapidFireChoicesRemainViable()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            foreach(int mask in new[]{4,58})
            {
                run.ResetRun();int peak=5;
                for(int i=0;i<4500&&run.State==RunState.Running;i++)
                {
                    float choice=(mask&(1<<Mathf.Min(5,run.GatesCollected)))==0?-2:2;
                    run.squad.SetDesiredX(Steering(run,choice));run.Advance(.02f);peak=Mathf.Max(peak,run.squad.UnitCount);
                    if(i%200==0)yield return null;
                }
                Debug.Log($"Mixed route {mask}: {run.State} at {run.Elapsed:0.00}s; peak={peak}, survivors={run.squad.UnitCount}");
                Assert.That(run.State,Is.EqualTo(RunState.Victory));Assert.That(run.Elapsed,Is.InRange(60,90));
                if(mask==4)Assert.That(peak,Is.GreaterThanOrEqualTo(50),"Contacts can remove units on a viable maximum-recruit path.");
                Assert.That(run.combat.DroppedShots+run.combat.DroppedTargets,Is.Zero);
            }
        }
        [UnityTest]
        public IEnumerator TwoHeavyEnemiesHaveSeparateReadablePositions()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var original=run.level;var trial=Object.Instantiate(original);
            var heavy=ScriptableObject.CreateInstance<EnemySettings>();heavy.kind=EnemyKind.Heavy;heavy.health=100;heavy.speed=0;heavy.size=1.3f;
            trial.encounters=new[]{new Encounter{time=.1f,kind=EncounterKind.Wave,enemy=heavy,count=2,lane=1,layout=WaveLayout.Focused,approachZ=14}};
            run.level=trial;run.ResetRun();
            for(int i=0;i<6;i++)run.Advance(.02f);
            var actors=run.combat.GetComponentsInChildren<EnemyVisual>();
            Assert.That(actors.Length,Is.EqualTo(2));
            Assert.That(Mathf.Abs(actors[0].transform.position.z-actors[1].transform.position.z),Is.GreaterThanOrEqualTo(1.3f));
            run.level=original;run.ResetRun();Object.Destroy(trial);Object.Destroy(heavy);
        }
        static void Capture(string name, int width = 540, int height = 960)
        {
            var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(width,height,24);var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../../"+name+".png")),tex.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;Object.Destroy(rt);Object.Destroy(tex);}
        }
    }
}
