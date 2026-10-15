using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace SalvageSquad
{
    public enum RunState { Running, Victory, Defeat }
    public sealed class SliceRun : MonoBehaviour
    {
        public LevelDefinition level;
        public LevelDefinition nextShift;
        public SquadController squad;
        public CombatSystem combat;
        public SliceFeedback feedback;
        public GateRow gatePrefab;
        public Transform warning, bossCrown;
        public Transform[] trackMarkers;
        public Text squadLabel, upgradeLabel, hint, resultTitle, resultStats, pauseLabel, bossLabel;
        public Image progress, bossBar;
        public RectTransform safeArea;
        public GameObject resultPanel;
        public GameObject toastPlate;
        public GameObject resultShade;
        public Text powerLabel;
        public Button replayButton, pauseButton, soundButton, motionButton;
        public RunState State { get; private set; }
        public bool IsPaused { get; private set; }
        public float Elapsed { get; private set; }
        public int GatesCollected { get; private set; }
        public int EncounterIndex { get; private set; }
        public float HazardRemaining => hazardTime;
        public int HazardLane => hazardLane;
        public bool BossStarted { get; private set; }
        GateRow[] gates;
        Vector3[] markerStarts;
        float hazardTime, nextBossAttack, toastTime, finishTime, hudTime, powerTime, hudPulse;
        int resultScore,resultBest,shownScore;
        int hazardLane, lastUnits;
        int bossDamage;
        int shownUnits = -1, shownDamage = -1, shownTier = -1;
        int shownPower=-1, shownBossSeconds=-1;
        Color bossBarColor;
        float shownRate = -1;
        Rect previousSafe;
        Text medal;
        Image rewardSeal;
        LevelDefinition firstShift;
        Text mission;
        Button pausedRestart;
        Vector2 cardSize;
        RectTransform cardRim;
        bool dragHint, bossEnraged, bossOpened;
        float bossTime;
        string defeatReason;
        string resultNext;
        Color squadColor;
        float hurtTime;
        bool powerPositive;
        CanvasGroup powerGroup;
        public void Awake()
        {
            string error = level ? level.Validate() : "Level missing";
            if (error != null) { Debug.LogError(error); enabled = false; return; }
            if (nextShift && nextShift.Validate() != null) { Debug.LogError(nextShift.Validate()); enabled = false; return; }
            firstShift = level;
            cardSize=resultPanel.GetComponent<RectTransform>().sizeDelta;
            cardRim=resultPanel.transform.Find("Card rim")?.GetComponent<RectTransform>();
            squadColor=squadLabel.color;
            resultStats.rectTransform.sizeDelta = new Vector2(resultStats.rectTransform.sizeDelta.x, 126);
            bossBarColor=bossBar.color;
            mission = safeArea.Find("Header/Mission")?.GetComponent<Text>();
            Application.targetFrameRate = 60;
            medal = resultPanel.transform.Find("Medal")?.GetComponent<Text>();
            rewardSeal = resultPanel.transform.Find("Reward seal")?.GetComponent<Image>();
            combat.Initialize(true); squad.Initialize(combat, 60); feedback.Initialize();
            PreparePowerPopup();
            int count = 0;
            foreach (var data in new[] { level, nextShift }) if (data)
            {
                int rows = 0; foreach (var row in data.encounters) if (row.kind == EncounterKind.Gate) rows++;
                count = Mathf.Max(count, rows);
            }
            gates = new GateRow[count];
            for (int i = 0; i < count; i++) { gates[i] = Instantiate(gatePrefab, transform); gates[i].ResetRow(); }
            combat.ShootableGates = gates;
            markerStarts = new Vector3[trackMarkers.Length];
            for (int i = 0; i < trackMarkers.Length; i++) markerStarts[i] = trackMarkers[i].position;
            pausedRestart=Instantiate(replayButton,resultPanel.transform);pausedRestart.name="Paused restart";
            var restartRect=pausedRestart.GetComponent<RectTransform>();
            restartRect.anchorMin=restartRect.anchorMax=restartRect.pivot=new Vector2(.5f,0);
            restartRect.anchoredPosition=new Vector2(0,12);restartRect.sizeDelta=new Vector2(200,66);
            pausedRestart.GetComponent<Image>().color=pauseButton.GetComponent<Image>().color;
            var restartText=pausedRestart.GetComponentInChildren<Text>();restartText.text="RESTART RUN";restartText.fontSize=16;
            pausedRestart.onClick=new Button.ButtonClickedEvent();pausedRestart.onClick.AddListener(ResetRun);
            replayButton.onClick.AddListener(() => { if(IsPaused)SetPaused(false);else Replay(); });
            pauseButton.onClick.AddListener(() => SetPaused(!IsPaused));
            soundButton.onClick.AddListener(() => { feedback.ToggleMute(); soundButton.GetComponentInChildren<Text>().text = feedback.Muted ? "SOUND OFF" : "SOUND ON"; });
            motionButton.onClick.AddListener(() => { feedback.ToggleMotion(); motionButton.GetComponentInChildren<Text>().text = feedback.ReducedMotion ? "MOTION LOW" : "MOTION ON"; });
            // Keep the artwork compact while making small controls easier to tap.
            foreach(var button in new[]{pauseButton,soundButton,motionButton})
            {
                var rect=button.GetComponent<RectTransform>();
                var hit=new GameObject("Touch target",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                hit.transform.SetParent(button.transform,false);hit.color=Color.clear;
                var area=hit.rectTransform;area.anchorMin=Vector2.zero;area.anchorMax=Vector2.one;
                var pad=new Vector2(Mathf.Max(0,(66-rect.rect.width)*.5f),Mathf.Max(0,(66-rect.rect.height)*.5f));
                area.offsetMin=-pad;area.offsetMax=pad;
                for(int i=0;i<squad.buttons.Length;i++)if(squad.buttons[i]==rect)squad.buttons[i]=area;
            }
            ResetRun(); ApplySafeArea();
        }
        public void ResetRun()
        {
            combat.Contact = (x, damage) => { if (Mathf.Abs(squad.transform.position.x - x) < 1.45f) DamageSquad(damage, "Machines reached your squad. Shoot their lane or steer clear."); };
            combat.ResetCombat(); squad.ResetSquad(); feedback.ResetFeedback();
            powerTime=hudPulse=hurtTime=0;shownScore=-1;squadLabel.color=squadColor;
            squadLabel.transform.localScale=Vector3.one;
            if(powerLabel)powerLabel.gameObject.SetActive(false);
            if(resultShade)resultShade.SetActive(false);
            foreach (var gate in gates) gate.ResetRow();
            for (int i = 0; i < trackMarkers.Length; i++) trackMarkers[i].position = markerStarts[i];
            Elapsed = hazardTime = finishTime = hudTime = 0; toastTime = 4;
            GatesCollected = EncounterIndex = 0; State = RunState.Running; BossStarted = false;
            dragHint = true; bossEnraged = bossOpened = false;bossBar.color=bossBarColor;shownBossSeconds=-1;
            defeatReason = "Choose recruits early. Avoid the striped warning lanes.";
            bossTime = level.deadline;
            foreach (var row in level.encounters) if (row.kind == EncounterKind.Boss) { bossTime = row.time; break; }
            if (mission) mission.text = level.title;
            warning.gameObject.SetActive(false); bossCrown.gameObject.SetActive(false);
            bossBar.transform.parent.gameObject.SetActive(false); resultPanel.SetActive(false);
            pauseButton.gameObject.SetActive(true); SetPaused(false);
            hint.text = "↔  DRAG TO STEER"; RefreshHUD();
            if (toastPlate) toastPlate.SetActive(true);
            resultPanel.transform.localScale = Vector3.one;
        }
        public void Replay()
        {
            if (State == RunState.Victory && nextShift) level = level == firstShift ? nextShift : firstShift;
            ResetRun();
        }
        public void SetPaused(bool paused)
        {
            if (State != RunState.Running) return;
            IsPaused = paused; squad.CancelDrag(); feedback.Pause(paused);combat.SetPaused(paused);
            pauseLabel.text = paused ? "▶" : "II";
            resultPanel.SetActive(paused);
            var size=cardSize+Vector2.up*(paused?60:0);resultPanel.GetComponent<RectTransform>().sizeDelta=size;
            if(cardRim)cardRim.sizeDelta=size+new Vector2(8,8);
            pausedRestart.gameObject.SetActive(paused);
            if(resultShade)resultShade.SetActive(paused);
            if(powerLabel)powerLabel.gameObject.SetActive(!paused&&powerTime>0);
            resultPanel.transform.localScale = Vector3.one;
            if (medal) medal.transform.localScale = Vector3.one;
            if (rewardSeal) rewardSeal.transform.localScale = Vector3.one;
            if (toastPlate) toastPlate.SetActive(!paused && toastTime > 0);
            replayButton.GetComponentInChildren<Text>().text = paused ? "RESUME RUN  ▶" : "PLAY AGAIN";
            if (paused)
            {
                resultTitle.text = "PAUSED"; resultStats.text = "Your squad is waiting.\nContinue your run below.";
                resultTitle.color = new Color(.94f,.91f,.82f); if (medal) medal.text = "II";
                if (rewardSeal) rewardSeal.color = new Color(.32f,.45f,.56f);
            }
        }
        void Update()
        {
            if (Screen.safeArea != previousSafe) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetRun();
            Advance(Mathf.Min(Time.deltaTime, .05f));
        }
        public void Advance(float dt)
        {
            if (IsPaused || dt <= 0) return;
            if (State == RunState.Victory)
                squad.transform.position = Vector3.MoveTowards(squad.transform.position, Vector3.zero, dt * 10);
            if (State != RunState.Running)
            {
                feedback.Tick(dt);
                float effectStep=Mathf.Min(dt,Mathf.Max(0,1.2f-finishTime));
                finishTime += dt;
                if (effectStep>0) combat.Tick(effectStep);
                float reveal = State == RunState.Victory ? .7f : .2f;
                if (finishTime >= reveal)
                {
                    resultPanel.SetActive(true);
                    if(resultShade)resultShade.SetActive(true);
                    if(State==RunState.Victory)
                    {
                        int score=Mathf.RoundToInt(resultScore*Mathf.SmoothStep(0,1,(finishTime-.7f)/.8f));
                        if(score!=shownScore){shownScore=score;resultStats.text=VictoryStats(score);}
                    }
                    float t = Mathf.Clamp01((finishTime - reveal) / .35f);
                    resultPanel.transform.localScale = Vector3.one * (feedback.ReducedMotion ? 1 : 1 - .15f * Mathf.Pow(1 - t, 2) * Mathf.Cos(t * 5));
                    // Let the card land first, then stamp the reward once; no idle pulsing.
                    float stamp = Mathf.Clamp01((finishTime - reveal - .1f) / .32f);
                    float sealScale = feedback.ReducedMotion || State != RunState.Victory ? 1 : 1 - Mathf.Pow(1 - stamp, 2) * Mathf.Cos(stamp * 4.5f);
                    if (medal) medal.transform.localScale = Vector3.one * sealScale;
                    if (rewardSeal) rewardSeal.transform.localScale = Vector3.one * sealScale;
                }
                return;
            }
            Elapsed += dt;
            combat.BossWindup=BossStarted&&hazardTime>0?Mathf.Clamp01(1-hazardTime/1.6f):0;
            squad.ReducedMotion = feedback.ReducedMotion;
            squad.Tick(dt); combat.Tick(dt);
            if (BossStarted && combat.BossDefeated) { Finish(true); return; }
            if (squad.UnitCount == 0) { Finish(false); return; }
            while (EncounterIndex < level.encounters.Length && level.encounters[EncounterIndex].time <= Elapsed)
                Spawn(level.encounters[EncounterIndex++]);
            foreach (var gate in gates)
            {
                gate.ReducedMotion=feedback.ReducedMotion;
                int before=squad.UnitCount;
                if (gate.Tick(dt, squad, out var choice))
                {
                    GatesCollected++; feedback.Play(choice.IsPositive);
                    if(hazardTime<=0){toastTime=0;hint.gameObject.SetActive(false);if(toastPlate)toastPlate.SetActive(false);}
                    int gain=squad.UnitCount-before;
                    if(gain<0)hurtTime=.45f;
                    string power=choice.kind==GateKind.Damage?"DAMAGE "+squad.Damage+"×":choice.kind==GateKind.FireRate?"FIRE RATE "+squad.FireRate.ToString("0.#")+"×":choice.Label;
                    ShowPower(gain!=0?(gain>0?"+":"")+gain+" SQUAD":power,choice.IsPositive);
                    combat.Pop(squad.transform.position + Vector3.forward);
                    if (squad.UnitCount == 0) { defeatReason = "That negative gate cost your remaining robots. Shoot it above zero or avoid its lane."; Finish(false); return; }
                }
            }
            if (hazardTime > 0)
            {
                hazardTime -= dt;
                warning.localScale = new Vector3(2.65f, .03f, 11 + Mathf.Sin(Elapsed * 18) * .25f);
                if (hazardTime <= 0)
                {
                    if (Mathf.Abs(squad.transform.position.x - hazardLane * 2.7f) <= 1.35f) DamageSquad(BossStarted ? bossDamage : 8, "The striped lane was hit. Move out before the warning ends.");
                    if(State!=RunState.Running)return;
                    combat.Pop(new Vector3(hazardLane * 2.7f, .5f, 1));
                    if(BossStarted)feedback.Impact();
                    warning.gameObject.SetActive(false);
                }
            }
            if (BossStarted)
            {
                bossBar.fillAmount = combat.BossHealth;
                if(!bossOpened&&!combat.BossArmored)
                {
                    bossOpened=true;bossBar.color=bossBarColor;bossLabel.text="SCRAP CRUSHER";
                    if(hazardTime<=0)Toast("ARMOR OPEN • FIRE!");
                }
                if (!bossEnraged && combat.BossHealth <= .5f)
                {
                    bossEnraged = true; if(shownBossSeconds<0)bossLabel.text = "CRUSHER • LAST STAND";
                    if (hazardTime <= 0) Toast("ARMOR CRACKED • FINISH IT!");
                    feedback.Play(true); combat.Pop(new Vector3(0, 1.5f, 9));
                }
                int seconds=Mathf.CeilToInt(level.deadline-Elapsed);
                if(seconds<=10&&seconds!=shownBossSeconds)
                {
                    shownBossSeconds=seconds;bossLabel.text=$"CRUSHER • {Mathf.Max(0,seconds)}s LEFT";
                }
                if (Elapsed >= nextBossAttack)
                {
                    Warn(Mathf.RoundToInt(squad.transform.position.x / 2.7f)); nextBossAttack = Elapsed + (bossEnraged ? 3.2f : 4);
                }
            }
            if (Elapsed >= level.deadline) { defeatReason = "The crusher held out. Try a different firepower route."; Finish(false); return; }
            foreach (var marker in trackMarkers)
            {
                marker.position += Vector3.back * (2.5f * dt);
                if (marker.position.z < -4) marker.position += Vector3.forward * 28;
            }
            toastTime -= dt;
            if(toastTime<=0&&!squad.HasSteered&&Elapsed<12) { hint.text="↔  DRAG TO CHOOSE A GATE";toastTime=.1f;dragHint=true; }
            hint.gameObject.SetActive(toastTime > 0 && !(dragHint && squad.HasSteered));
            if (toastPlate) toastPlate.SetActive(hint.gameObject.activeSelf);
            hudTime -= dt;
            if (hudTime <= 0 || lastUnits != squad.UnitCount) { RefreshHUD(); hudTime = .1f; }
            hudPulse=Mathf.Max(0,hudPulse-dt*3);squadLabel.transform.localScale=Vector3.one*(1+(feedback.ReducedMotion?0:hudPulse*.09f));
            hurtTime=Mathf.Max(0,hurtTime-dt);squadLabel.color=Color.Lerp(squadColor,new Color(1,.45f,.3f),Mathf.Clamp01(hurtTime*4));
            feedback.Tick(dt);
            if(powerLabel && powerTime>0)
            {
                powerTime=Mathf.Max(0,powerTime-dt);
                float age=1-powerTime;
                float powerScale=feedback.ReducedMotion?1:powerPositive?(age<.12f?Mathf.Lerp(.45f,1.12f,age/.12f):Mathf.Lerp(1.12f,1,Mathf.Clamp01((age-.12f)/.18f))):1+powerTime*powerTime*.18f;
                if(powerPositive)
                {
                    powerLabel.rectTransform.anchoredPosition=new Vector2(0,feedback.ReducedMotion?0:Mathf.Max(0,age-.65f)*45);
                    powerLabel.rectTransform.localRotation=Quaternion.Euler(0,0,feedback.ReducedMotion?0:Mathf.Lerp(-12,-5,Mathf.Clamp01(age/.16f)));
                }
                else
                {
                    Vector3 screen=feedback.view.WorldToScreenPoint(squad.transform.position+new Vector3(0,1.8f+(feedback.ReducedMotion?0:1-powerTime),1.4f));
                    if(RectTransformUtility.ScreenPointToWorldPointInRectangle(safeArea,screen,feedback.view,out var position))
                    {
                        Vector3 local=safeArea.InverseTransformPoint(position);
                        float half=powerLabel.rectTransform.rect.width*powerScale*.5f+10;
                        local.x=Mathf.Clamp(local.x,safeArea.rect.xMin+half,safeArea.rect.xMax-half);
                        powerLabel.transform.position=safeArea.TransformPoint(local);
                    }
                }
                powerGroup.alpha=Mathf.Min(1,powerTime*4);
                powerLabel.transform.localScale=Vector3.one*powerScale;
                powerLabel.gameObject.SetActive(powerTime>0);
            }
        }
        void Spawn(Encounter row)
        {
            switch (row.kind)
            {
                case EncounterKind.Gate:
                    foreach (var gate in gates) if (!gate.gameObject.activeSelf && !gate.Resolved)
                    {
                        gate.ReducedMotion=feedback.ReducedMotion; gate.Prepare(row.left, row.right, row.approachZ > 0 ? row.approachZ : 15,row.singleLaneGate?row.lane:2);
                        if(row.left.shootable||row.right.shootable)
                        {
                            Toast(row.singleLaneGate?"CLEAR THE GUARD • SHOOT − INTO +":!squad.HasSteered?"↔ DRAG • SHOOT − INTO +":row.left.IsPositive&&row.right.IsPositive?"SHOOT TO RECRUIT • OR CHOOSE FIREPOWER":"SHOOT − INTO + • TAKE THE REWARD");
                            if(GatesCollected==0)toastTime=(gate.transform.position.z-.6f)/2.5f;
                        }
                        break;
                    }
                    break;
                case EncounterKind.Wave:
                    for (int i = 0; i < row.count; i++)
                    {
                        bool concentrated = row.layout != WaveLayout.AcrossLanes;
                        float lane = row.layout == WaveLayout.FollowSquad ? squad.transform.position.x / 2.6f : row.lane;
                        float x = concentrated ? lane * 2.6f + (row.count > 2 ? (i % 3 - 1) * .65f : 0) : row.count > 2 ? (i % 3 - 1) * 2.6f : row.lane * 2.6f;
                        int rank = row.count > 2 ? i / 3 : i;
                        float spacing = row.count <= 2 ? Mathf.Max(1.2f, row.enemy.size * 1.5f) : concentrated ? .7f : 1.2f;
                        combat.SpawnTarget(x, row.enemy, (row.approachZ > 0 ? row.approachZ : 15) + rank * spacing);
                    }
                    break;
                case EncounterKind.Hazard: Warn(row.aimAtSquad ? Mathf.RoundToInt(squad.transform.position.x / 2.7f) : row.lane); break;
                case EncounterKind.Boss:
                    BossStarted = true; bossDamage = row.enemy.contactDamage; nextBossAttack = Elapsed + 4.2f;
                    bossLabel.text = "CRUSHER • ARMORED";bossBar.color=new Color(.35f,.65f,1);
                    combat.SpawnTarget(0, row.enemy, 9); bossCrown.gameObject.SetActive(true);
                    bossBar.transform.parent.gameObject.SetActive(true); Toast("SCRAP CRUSHER • AIM DOWN THE CENTER"); feedback.SetClimax(true);feedback.Play(false);
                    Warn(Mathf.RoundToInt(squad.transform.position.x/2.7f));
                    break;
            }
        }
        void Warn(int lane)
        {
            if(!BossStarted || combat.BossDefeated || State!=RunState.Running)return;
            hazardLane = Mathf.Clamp(lane, -1, 1); hazardTime = 1.6f;
            warning.position = new Vector3(hazardLane * 2.7f, .02f, 3.2f);
            warning.gameObject.SetActive(true); Toast("!  MOVE OUT OF THE STRIPED LANE");
            feedback.Warn();
        }
        public void DamageSquad(int amount, string reason = "Choose recruits early. Avoid the striped warning lanes.")
        {
            int before = squad.UnitCount;
            if (State != RunState.Running || (BossStarted && combat.BossDefeated) || !squad.LoseUnits(amount)) return;
            defeatReason = reason;
            int lost = before - squad.UnitCount;
            hurtTime=.45f;squadLabel.color=new Color(1,.45f,.3f);
            for(int i=Mathf.Min(squad.UnitCount,squad.bodies.Length);i<Mathf.Min(before,Mathf.Min(squad.bodies.Length,squad.UnitCount+3));i++)combat.Pop(squad.bodies[i].position+Vector3.up*.6f);
            feedback.Play(false);
            if (hazardTime <= 0) Toast("−" + lost + "  ROBOTS LOST");
            ShowPower("-"+lost+" SQUAD",false);
            if (squad.UnitCount == 0) Finish(false);
        }
        void Finish(bool victory)
        {
            if (State != RunState.Running) return;
            State = victory ? RunState.Victory : RunState.Defeat; squad.CancelDrag();
            hazardTime = 0; combat.BossWindup = 0;
            toastTime=powerTime=0;
            RefreshHUD();
            if (victory) progress.fillAmount = 1;
            warning.gameObject.SetActive(false); bossCrown.gameObject.SetActive(false);
            bossBar.transform.parent.gameObject.SetActive(false);
            foreach (var gate in gates) gate.gameObject.SetActive(false);
            combat.Contact = null;
            combat.StopActors();
            foreach (var flash in squad.flashes) flash.gameObject.SetActive(false);
            if (victory) for (int i = 0; i < 10; i++) combat.Pop(new Vector3((i % 5 - 2) * .8f, .4f, i / 5 * 1.5f));
            pauseButton.gameObject.SetActive(false); hint.gameObject.SetActive(false);
            if (toastPlate) toastPlate.SetActive(false);
            if(powerLabel)powerLabel.gameObject.SetActive(false);
            int score = combat.Salvaged * 10 + squad.UnitCount * 20;
            int best = PlayerPrefs.GetInt("SliceBest", 0);
            if (victory && score > best) { best = score; PlayerPrefs.SetInt("SliceBest", best); PlayerPrefs.Save(); }
            resultScore=score;resultBest=best;shownScore=-1;
            resultNext=victory&&nextShift?"\nNEXT: "+(level==firstShift?nextShift.title:firstShift.title):"";
            resultTitle.text = victory ? "VICTORY!" : squad.UnitCount>0?"TIME'S UP":"SQUAD LOST";
            resultTitle.color = victory ? new Color(1,.8f,.42f) : new Color(.95f,.48f,.3f);
            if (medal) medal.text = victory ? "★" : "×";
            if (rewardSeal) rewardSeal.color = victory ? new Color(1,.77f,.35f) : new Color(.97f,.42f,.27f);
            resultStats.supportRichText = true;
            resultStats.lineSpacing = 1.05f;
            resultStats.text = victory ? VictoryStats(score) : defeatReason;
            replayButton.GetComponentInChildren<Text>().text = victory && nextShift ? "NEXT SHIFT  →" : "TRY AGAIN";
            feedback.Play(victory, true);
        }
        string VictoryStats(int score) => $"<size=24>{squad.UnitCount} ROBOTS HOME  •  {Elapsed:0.0}s</size>\n<size=18>{combat.Salvaged} MACHINES RECYCLED</size>\n<size=28><color=#FFD06B>SCORE {score}</color></size>  <size=14>BEST {resultBest}</size><size=18>{resultNext}</size>";
        void Toast(string value) { dragHint = false; hint.text = value; toastTime = 2; hint.gameObject.SetActive(true); if (toastPlate) toastPlate.SetActive(true); }
        void PreparePowerPopup()
        {
            if(!powerLabel)return;
            powerGroup=powerLabel.gameObject.AddComponent<CanvasGroup>();powerGroup.blocksRaycasts=false;powerGroup.interactable=false;
            var outline=powerLabel.GetComponent<Outline>();if(outline)outline.effectDistance=new Vector2(3,-3);
            var shadow=powerLabel.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.025f,.065f,.09f,.8f);shadow.effectDistance=new Vector2(5,-7);
        }
        void ShowPower(string value,bool positive)
        {
            if(!powerLabel)return;
            powerPositive=positive;powerGroup.alpha=1;
            powerLabel.supportRichText=true;powerLabel.resizeTextForBestFit=positive;
            powerLabel.resizeTextMinSize=32;powerLabel.resizeTextMaxSize=78;powerLabel.fontSize=positive?78:34;
            powerLabel.rectTransform.sizeDelta=positive?new Vector2(Mathf.Min(440,safeArea.rect.width*.76f),140):new Vector2(320,64);
            powerLabel.rectTransform.anchoredPosition=Vector2.zero;powerLabel.rectTransform.localRotation=Quaternion.identity;
            powerLabel.transform.localScale=Vector3.one;
            powerLabel.text=positive?"<size=22>POWER UP!</size>\n"+value:value;powerLabel.color=positive?new Color(1,.85f,.3f):new Color(1,.45f,.3f);
            powerTime=1;powerLabel.gameObject.SetActive(true);
        }
        void RefreshHUD()
        {
            lastUnits = squad.UnitCount;
            if (shownUnits != squad.UnitCount)
            {
                shownUnits = squad.UnitCount;
                hudPulse=1;
                squadLabel.text = "SQUAD  " + squad.UnitCount.ToString("00");
            }
            int power=(int)System.Math.Min(int.MaxValue,System.Math.Round((double)squad.UnitCount*squad.Damage*squad.FireRate));
            if (shownPower!=power || shownDamage != squad.Damage || shownRate != squad.FireRate || shownTier != squad.WeaponTier)
            {
                shownPower=power;
                shownDamage = squad.Damage; shownRate = squad.FireRate; shownTier = squad.WeaponTier;
                upgradeLabel.text = $"POWER {power}   •   RATE {squad.FireRate:0.#}×   •   MK {squad.WeaponTier}";
            }
            progress.fillAmount = BossStarted ? .8f + .2f * (1 - combat.BossHealth) : .8f * Mathf.Clamp01(Elapsed / bossTime);
        }
        void ApplySafeArea()
        {
            previousSafe = Screen.safeArea;
            if (Screen.width < 1 || Screen.height < 1) return;
            safeArea.anchorMin = previousSafe.min / new Vector2(Screen.width, Screen.height);
            safeArea.anchorMax = previousSafe.max / new Vector2(Screen.width, Screen.height);
        }
        void OnApplicationPause(bool paused) { if (paused && feedback) SetPaused(true); }
        void OnApplicationFocus(bool focused) { if (!focused && squad) squad.CancelDrag(); }
    }
}
