using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SalvageSquad
{
    public sealed class PrototypeRun : MonoBehaviour
    {
        public SquadController squad;
        public CombatSystem combat;
        public Text score, status, hint, pauseLabel;
        public Button restartButton, pauseButton;
        public RectTransform safeArea;
        public Transform[] trackMarkers;
        public bool IsPaused { get; private set; }
        public float Elapsed { get; private set; }
        Vector3[] markerStarts;
        float spawnTimer;
        int wave, lastSalvaged = -1, lastMissed = -1;
        Rect lastSafeArea;
        readonly float[] lanes = { 0, -2.5f, 2.5f, 0, 2.5f, -2.5f, 0, -2.5f };

        void Awake()
        {
            Application.targetFrameRate = 60;
            combat.Initialize();
            squad.Initialize(combat);
            markerStarts = new Vector3[trackMarkers.Length];
            for (int i = 0; i < trackMarkers.Length; i++) markerStarts[i] = trackMarkers[i].position;
            restartButton.onClick.AddListener(ResetRun);
            pauseButton.onClick.AddListener(() => SetPaused(!IsPaused));
            ResetRun();
            ApplySafeArea();
        }

        public void ResetRun()
        {
            combat.ResetCombat();
            squad.ResetSquad();
            Elapsed = wave = 0;
            spawnTimer = .25f;
            for (int i = 0; i < trackMarkers.Length; i++) trackMarkers[i].position = markerStarts[i];
            SetPaused(false);
            RefreshLabels();
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            squad.CancelDrag();
            pauseLabel.text = paused ? "RESUME" : "PAUSE";
            hint.text = paused ? "Ready when you are" : "DRAG LEFT / RIGHT";
            hint.gameObject.SetActive(paused || Elapsed < 7);
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetRun();
            if (IsPaused) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            Elapsed += dt;
            squad.Tick(dt);
            combat.Tick(dt);
            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                combat.SpawnTarget(lanes[wave++ % lanes.Length]);
                spawnTimer = 2f;
            }
            foreach (Transform marker in trackMarkers)
            {
                marker.position += Vector3.back * (combat.targetSpeed * dt);
                if (marker.position.z < -4) marker.position += Vector3.forward * 28;
            }
            hint.gameObject.SetActive(Elapsed < 7);
            if (combat.Salvaged != lastSalvaged || combat.Missed != lastMissed) RefreshLabels();
        }

        void RefreshLabels()
        {
            lastSalvaged = combat.Salvaged;
            lastMissed = combat.Missed;
            score.text = "SALVAGED  " + lastSalvaged.ToString("00");
            status.text = "SQUAD 05    /    MISSED " + lastMissed.ToString("00");
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            if (Screen.width == 0 || Screen.height == 0) return;
            safeArea.anchorMin = lastSafeArea.min / new Vector2(Screen.width, Screen.height);
            safeArea.anchorMax = lastSafeArea.max / new Vector2(Screen.width, Screen.height);
        }

        void OnApplicationPause(bool paused) { if (paused) SetPaused(true); }
        void OnApplicationFocus(bool focused) { if (!focused && squad) squad.CancelDrag(); }
    }
}
