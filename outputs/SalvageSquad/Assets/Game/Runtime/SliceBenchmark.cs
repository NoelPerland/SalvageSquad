#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace SalvageSquad
{
    // Development-only, opt-in stress run. No benchmark work occurs in normal play.
    public sealed class SliceBenchmark : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sliceBenchmark") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-sliceReview") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-sliceQA") >= 0)
            {
                // Hidden players may pause before a coroutine's first yield resumes.
                Application.runInBackground = true;
                new GameObject("Slice benchmark").AddComponent<SliceBenchmark>();
            }
        }
        IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-sliceQA")>=0){yield return QaReview();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-sliceReview")>=0){yield return Review();yield break;}
            yield return null;
            var run=FindFirstObjectByType<SliceRun>();
            if (!run) { Debug.LogError("Benchmark needs Scrapline scene."); Application.Quit(2); yield break; }
            run.enabled=false; Application.runInBackground=true;
            run.ResetRun();run.squad.SetCount(60);
            run.squad.Apply(new GateEffect{kind=GateKind.FireRate,value=3});
            run.squad.Apply(new GateEffect{kind=GateKind.Damage,value=4});
            run.squad.Apply(new GateEffect{kind=GateKind.Weapon,value=2});
            run.Advance(.02f); run.feedback.Tick(3); run.combat.Contact=null;
            EnemySettings enemy=null;
            foreach(var row in run.level.encounters)
                if(row.kind==EncounterKind.Wave && row.enemy.kind==EnemyKind.Swarm && (!enemy || row.enemy.health>enemy.health))enemy=row.enemy;
            if(!enemy){Debug.LogError("Benchmark needs a swarm encounter.");Application.Quit(2);yield break;}
            var timings=new FrameTiming[1];
            var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            double cpu=0,gpu=0,wall=0,maxCpu=0,maxGpu=0;long gcTotal=0;int samples=0,timingSamples=0,peakBullets=0,peakTargets=0;
            var stepWatch=new System.Diagnostics.Stopwatch();long allocated=0;
            for(int frame=0;frame<420;frame++)
            {
                if(frame%30==0)for(int i=0;i<12;i++)run.combat.SpawnTarget((i%3-1)*2.6f,enemy,15+i/3*1.2f);
                long before=GC.GetAllocatedBytesForCurrentThread();
                if(frame>=120)stepWatch.Start();
                run.squad.Tick(1f/60);run.combat.Tick(1f/60);
                if(frame>=120){stepWatch.Stop();allocated+=GC.GetAllocatedBytesForCurrentThread()-before;}
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                if(frame<120)continue;
                samples++;wall+=Time.unscaledDeltaTime;
                peakBullets=Mathf.Max(peakBullets,run.combat.ActiveProjectiles);peakTargets=Mathf.Max(peakTargets,run.combat.ActiveTargets);
                if(gc.Valid)gcTotal+=gc.LastValue;
                if(FrameTimingManager.GetLatestTimings(1,timings)>0)
                {
                    cpu+=timings[0].cpuFrameTime;gpu+=timings[0].gpuFrameTime;timingSamples++;
                    maxCpu=Math.Max(maxCpu,timings[0].cpuFrameTime);maxGpu=Math.Max(maxGpu,timings[0].gpuFrameTime);
                }
            }
            gc.Dispose();
            string gpuResult = gpu > 0 ? $"{gpu/Math.Max(1,timingSamples):0.00}/{maxGpu:0.00} ms" : "unavailable (API returned zero)";
            string report=$"Hardware: {SystemInfo.processorType}; {SystemInfo.graphicsDeviceName}\nResolution: {Screen.width}x{Screen.height}; Windows development player\nStress: 60 robots, Cannon II, 3x fire rate, {run.squad.Damage} damage, recurring 12-enemy waves; 120 warm-up + {samples} measured frames\nMean wall frame: {wall/samples*1000:0.00} ms\nFrameTiming samples: {timingSamples}; CPU mean/max: {cpu/Math.Max(1,timingSamples):0.00}/{maxCpu:0.00} ms; GPU mean/max: {gpuResult}\nGameplay Tick mean: {stepWatch.Elapsed.TotalMilliseconds/samples:0.000} ms; managed allocations inside measured gameplay ticks: {allocated} bytes\nWhole-frame GC mean (includes engine/UI/profiler): {(double)gcTotal/samples:0} bytes\nPeak bullets: {peakBullets}; peak enemies: {peakTargets}; dropped shots: {run.combat.DroppedShots}\n";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Performance.txt")),report);
            Debug.Log(report);Application.Quit(run.combat.DroppedShots==0?0:3);
        }
        // Real-time touch input, including delayed decisions and deliberately poor play.
        IEnumerator QaReview()
        {
            yield return null;
            var run=FindFirstObjectByType<SliceRun>();
            if(!run){Application.Quit(2);yield break;}
            bool hadBest=PlayerPrefs.HasKey("SliceBest");int best=PlayerPrefs.GetInt("SliceBest");
            var background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var touch=InputSystem.AddDevice<Touchscreen>();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../"));
            var report=new StringBuilder($"Visible real-time touch QA: {Screen.width}x{Screen.height}\n");
            bool failed=false;
            foreach(string profile in Array.IndexOf(Environment.GetCommandLineArgs(),"-sliceQADirector")>=0?new[]{"Greedy","Low"}:Array.IndexOf(Environment.GetCommandLineArgs(),"-sliceQAShort")>=0?new[]{"Novice","Bad"}:new[]{"Novice","Idle","Zigzag","Edge","Bad"})
            {
                run.ResetRun();var level=run.level;
                int objects=run.combat.GetComponentsInChildren<Transform>(true).Length;
                float start=Time.realtimeSinceStartup,warningStart=-1;int warningLane=0;
                Vector2 pointer=new Vector2(Screen.width*.5f,Screen.height*.18f);
                bool held=false,paused=false;int shot=0;
                float[] times={.3f,2,3.5f,4.2f,9.8f,17.8f,22.5f,30,38.5f,50,58,60.7f,62.5f,82,88};
                int lost=0,previous=5,peak=5;float firstKill=-1;
                var audio=new float[1024];float audioPeak=0,audioNext=0;double audioEnergy=0;int audioSamples=0;
                while(run.State==RunState.Running&&Time.realtimeSinceStartup-start<115)
                {
                    if(run.IsPaused)run.SetPaused(false);
                    float t=run.Elapsed,goal=0;
                    if(t>=audioNext)
                    {
                        audioNext=t+.1f;AudioListener.GetOutputData(audio,0);
                        foreach(float value in audio){audioPeak=Mathf.Max(audioPeak,Mathf.Abs(value));audioEnergy+=value*value;audioSamples++;}
                    }
                    if(run.HazardRemaining>0)
                    {
                        if(warningStart<0||warningLane!=run.HazardLane){warningStart=t;warningLane=run.HazardLane;}
                    }
                    else warningStart=-1;
                    if(profile=="Novice")
                    {
                        goal=t<4?2:t<10?-2:t<21?2:-2;
                        if(t<2.2f)goal=0;
                        if(run.BossStarted)goal=0;
                        if(warningStart>=0&&t-warningStart>.8f)goal=warningLane==0?2.6f:0;
                    }
                    else if(profile=="Greedy"||profile=="Low")
                    {
                        goal=profile=="Low"?(t<10?2:t<25.1f?-2:2):t<21?-2:t<25.1f?2:t<42?-2:2;
                        if(run.BossStarted)goal=0;
                        if(warningStart>=0&&t-warningStart>.35f)goal=warningLane==0?-2.6f:0;
                    }
                    else if(profile=="Zigzag")goal=Mathf.Sin(t*5.5f)*2.65f;
                    else if(profile=="Bad")goal=t<3.35f?2:-2;
                    else if(profile=="Edge")
                    {
                        goal=t<4?.025f:-.025f;
                        if(run.BossStarted)goal=0;
                        if(warningStart>=0&&t-warningStart>.35f)goal=warningLane==0?2.6f:0;
                    }
                    if(profile!="Idle")
                    {
                        if(!held)
                        {
                            pointer.x=Screen.width*.5f;held=true;
                            InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=pointer});
                        }
                        else
                        {
                            pointer.x+=Mathf.Clamp(goal-run.squad.DesiredX,-.35f,.35f)*Screen.width/9;
                            if(pointer.x<Screen.width*.06f||pointer.x>Screen.width*.94f)
                            {
                                held=false;
                                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=pointer});
                            }
                            else InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=pointer});
                        }
                    }
                    if(run.squad.UnitCount<previous)lost+=previous-run.squad.UnitCount;
                    previous=run.squad.UnitCount;peak=Mathf.Max(peak,previous);
                    if(firstKill<0&&run.combat.Salvaged>0)firstKill=t;
                    if(shot<times.Length&&t>=times[shot])ScreenCapture.CaptureScreenshot(Path.Combine(folder,$"QA-{profile}-{times[shot++].ToString("0.0",CultureInfo.InvariantCulture)}.png"));
                    if(!paused&&t>12)
                    {
                        InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=pointer});held=false;
                        run.pauseButton.onClick.Invoke();float elapsed=run.Elapsed;int count=run.squad.UnitCount;
                        yield return new WaitForSecondsRealtime(.35f);
                        failed|=!run.IsPaused||run.Elapsed!=elapsed||run.squad.UnitCount!=count||!run.resultPanel.activeSelf;
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder,$"QA-{profile}-Pause.png"));
                        yield return null;
                        run.replayButton.onClick.Invoke();failed|=run.IsPaused||run.Elapsed!=elapsed||run.squad.UnitCount!=count;paused=true;
                    }
                    yield return null;
                }
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=pointer});
                yield return new WaitForSecondsRealtime(1.8f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,$"QA-{profile}-{run.State}.png"));
                yield return new WaitForSecondsRealtime(.15f);
                if(run.squad.UnitCount<previous)lost+=previous-run.squad.UnitCount;
                report.AppendFormat(CultureInfo.InvariantCulture,"{0}: {1} {2:0.00}s; first kill {3:0.00}s; {4} gates; peak {5}, survivors {6}, losses {7}; {8} kills; dropped shots/enemies {9}/{10}; steered {11}.\n",profile,run.State,run.Elapsed,firstKill,run.GatesCollected,peak,run.squad.UnitCount,lost,run.combat.Salvaged,run.combat.DroppedShots,run.combat.DroppedTargets,run.squad.HasSteered);
                report.AppendFormat(CultureInfo.InvariantCulture,"Boss health remaining: {0:0.000}.\n",run.combat.BossHealth);
                report.AppendFormat(CultureInfo.InvariantCulture,"Mixed-audio sampled peak/RMS: {0:0.000}/{1:0.000} (zero means unavailable); not a listening assessment.\n",audioPeak,Math.Sqrt(audioEnergy/Math.Max(1,audioSamples)));
                failed|=audioPeak>=1;
                failed|=run.State==RunState.Running||run.combat.DroppedShots!=0||run.combat.DroppedTargets!=0;
                if(profile=="Novice"||profile=="Greedy"||profile=="Low")failed|=run.State!=RunState.Victory||!run.squad.HasSteered;
                if(profile=="Idle")failed|=run.State!=RunState.Defeat;
                if(profile=="Bad")failed|=run.State!=RunState.Defeat;
                bool victory=run.State==RunState.Victory;
                run.replayButton.onClick.Invoke();yield return null;
                failed|=run.State!=RunState.Running||run.squad.UnitCount!=5||run.GatesCollected!=0||run.IsPaused||run.resultPanel.activeSelf||objects!=run.combat.GetComponentsInChildren<Transform>(true).Length;
                failed|=victory?run.level==level:run.level!=level;
                // Keep profiles comparable on the first authored shift.
                run.level=level;
                File.WriteAllText(Path.Combine(folder,"QA-Checks.txt"),report.ToString());
            }
            InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;
            if(hadBest)PlayerPrefs.SetInt("SliceBest",best);else PlayerPrefs.DeleteKey("SliceBest");PlayerPrefs.Save();
            report.Append($"Pause/retry/reset/pool/drop checks: {(failed?"FAILED":"PASSED")}. Automated touch; human enjoyment/audio judgement still required.\n");
            File.WriteAllText(Path.Combine(folder,"QA-Checks.txt"),report.ToString());Debug.Log(report);Application.Quit(failed?3:0);
        }
        // Actual development-player replay: normal frame loop/audio/rendering, never accelerated simulation.
        IEnumerator Review()
        {
            yield return null;
            var run=FindFirstObjectByType<SliceRun>();
            if(!run){Debug.LogError("Review needs Scrapline.");Application.Quit(2);yield break;}
            bool hadBest=PlayerPrefs.HasKey("SliceBest");int best=PlayerPrefs.GetInt("SliceBest");
            bool rightRoute=Array.IndexOf(Environment.GetCommandLineArgs(),"-sliceRouteRight")>=0;
            string basePrefix=rightRoute?"ReviewRight-":Screen.height>1000?"ReviewTall-":"Review-";
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../"));
            run.ResetRun();int objects=run.combat.GetComponentsInChildren<Transform>(true).Length;
            var report=new StringBuilder();bool failed=false;
            for(int shift=0;shift<2;shift++)
            {
                string prefix=shift==0?basePrefix:basePrefix+"Next-";
                var level=run.level;float start=Time.realtimeSinceStartup;
                float[] captureTimes={.85f,7,10.1f,17.8f,22.3f,30.3f,35.3f,39.5f,50.4f,61.7f};string[] names={"Shootable","Gates","Growth","Pressure","LaneGate","Cannon","Charge","LanePressure","Power","Boss"};int shot=0;
                bool paused=false,lastStand=false;float firstKill=-1;int collected=0,sample=0;
                var timeline=new StringBuilder("time,units,damage,rate,tier,gates,kills,enemies,shots,hits,boss_health\n");
                var gateTimes=new StringBuilder();
                while(run.State==RunState.Running && Time.realtimeSinceStartup-start<110)
                {
                    // Review must survive desktop focus/minimize pauses; normal play still pauses normally.
                    if(run.IsPaused)run.SetPaused(false);
                    float choice=(rightRoute?-2:2)*(shift==0?-1:1);
                    float x=run.HazardRemaining>0?run.HazardLane==0?-2.6f:0:run.BossStarted?0:choice;
                    run.squad.SetDesiredX(x);
                    if(firstKill<0&&run.combat.Salvaged>0)firstKill=run.Elapsed;
                    if(run.GatesCollected!=collected){collected=run.GatesCollected;gateTimes.AppendFormat(CultureInfo.InvariantCulture," {0:0.00}s({1} units)",run.Elapsed,run.squad.UnitCount);}
                    if(run.Elapsed>=sample)
                    {
                        sample++;
                        timeline.AppendFormat(CultureInfo.InvariantCulture,"{0:0.00},{1},{2},{3:0.00},{4},{5},{6},{7},{8},{9},{10:0.000}\n",run.Elapsed,run.squad.UnitCount,run.squad.Damage,run.squad.FireRate,run.squad.WeaponTier,run.GatesCollected,run.combat.Salvaged,run.combat.ActiveTargets,run.combat.TotalShots,run.combat.TotalHits,run.combat.BossHealth);
                    }
                    if(shot<captureTimes.Length&&run.Elapsed>=captureTimes[shot])ScreenCapture.CaptureScreenshot(Path.Combine(folder,prefix+names[shot++]+".png"));
                    if(!lastStand&&run.BossStarted&&run.combat.BossHealth<.4f){lastStand=true;ScreenCapture.CaptureScreenshot(Path.Combine(folder,prefix+"LastStand.png"));}
                    if(!paused&&run.Elapsed>20)
                    {
                        run.SetPaused(true);float elapsed=run.Elapsed;
                        yield return new WaitForSecondsRealtime(.25f);
                        failed|=run.Elapsed!=elapsed;run.SetPaused(false);paused=true;
                    }
                    yield return null;
                }
                float duration=run.Elapsed;int drops=run.combat.DroppedShots;
                bool won=run.State==RunState.Victory;
                failed|=!won||duration<60||duration>90||drops!=0||run.combat.DroppedTargets!=0||run.GatesCollected<6||run.GatesCollected>8||!paused;
                report.AppendFormat(CultureInfo.InvariantCulture,"{0}: {1}; {2:0.00}s; first kill {3:0.00}s; {4} gates; {5} survivors; {6} kills; {7} shots dropped, {8} enemies dropped.\nGate pickups:{9}\n",level.title,run.State,duration,firstKill,run.GatesCollected,run.squad.UnitCount,run.combat.Salvaged,drops,run.combat.DroppedTargets,gateTimes);
                File.WriteAllText(Path.Combine(folder,prefix+"Timeline.csv"),timeline.ToString());
                yield return new WaitForSecondsRealtime(1.8f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,prefix+(won?"Victory":"Defeat")+".png"));yield return new WaitForSecondsRealtime(.15f);
                run.replayButton.onClick.Invoke();yield return null;
                failed|=run.State!=RunState.Running||run.squad.UnitCount!=5||run.GatesCollected!=0||objects!=run.combat.GetComponentsInChildren<Transform>(true).Length;
                if(won&&run.nextShift)failed|=run.level==level;
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,prefix+"Reset.png"));yield return new WaitForSecondsRealtime(.15f);
                if(!won)break;
            }
            if(hadBest)PlayerPrefs.SetInt("SliceBest",best);else PlayerPrefs.DeleteKey("SliceBest");PlayerPrefs.Save();
            report.Insert(0,$"Actual player replay: {(failed?"FAILED":"PASSED")}; {Screen.width}x{Screen.height}; two shifts, pause/reset/pool checks.\n");
            report.Append("Automated steering; human touch/audio/enjoyment not verified.\n");
            File.WriteAllText(Path.Combine(folder,basePrefix+"Checks.txt"),report.ToString());Debug.Log(report);Application.Quit(failed?3:0);
        }
    }
}
#endif


