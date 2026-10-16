using UnityEngine;
namespace SalvageSquad
{
    public sealed class SliceFeedback : MonoBehaviour
    {
        public Camera view;
        public SquadController squad;
        public bool ReducedMotion { get; private set; }
        public bool Muted { get; private set; }
        AudioSource effects, music;
        AudioClip gate, hurt, win, loop, warning, impact, slide, skid;
        Vector3 home;
        float homeSize;
        float shake;
        float movementCooldown;
        float distance = 22, focusZ = 6;
        public void Initialize()
        {
            home = view.transform.localPosition;
            homeSize = view.orthographicSize;
            effects = gameObject.AddComponent<AudioSource>();
            music = gameObject.AddComponent<AudioSource>();
            effects.priority=70;music.priority=220;
            gate = MakeTone("Brass power-up", .46f, 330, true);
            hurt = CombatSystem.MakeMechanical("Armor loss", .3f, 82, 1);
            impact = CombatSystem.MakeMechanical("Crusher floor strike", .22f, 58, 1);
            slide = CombatSystem.MakeMechanical("Robot slide", .16f, 145, .18f);
            skid = CombatSystem.MakeMechanical("Robot skid", .13f, 185, .4f);
            warning = MakeTone("Lane warning", .25f, 210, false);
            win = MakeTone("Run complete", 1.25f, 390, true);
            loop = MakeMusic(); music.clip = loop; music.loop = true; music.volume = .075f;
            music.Play();
        }
        public void ToggleMute() { Muted = !Muted; AudioListener.volume = Muted ? 0 : 1; }
        public void ToggleMotion() { ReducedMotion = !ReducedMotion; shake = 0; ApplyCamera(); }
        public void Play(bool positive, bool finale = false)
        {
            // Give the boss kill weight even when the ordinary hit channel is throttled.
            if (finale && positive) effects.PlayOneShot(impact, .55f);
            effects.PlayOneShot(finale && positive ? win : positive ? gate : hurt, .35f);
            if(finale)music.Stop();
            shake = finale ? .35f : positive ? .09f : .18f;
        }
        public void Warn() => effects.PlayOneShot(warning,.3f);
        public void Impact() { effects.PlayOneShot(impact,.24f); shake=Mathf.Max(shake,.07f); }
        public void Slide() { if(movementCooldown>0)return;effects.PlayOneShot(slide,.1f);movementCooldown=.09f; }
        public void Skid() { if(movementCooldown>0)return;effects.PlayOneShot(skid,.13f);movementCooldown=.13f; }
        public void SetClimax(bool boss) { music.pitch=boss?1.06f:1;music.volume=boss?.09f:.075f; }
        public void Tick(float dt)
        {
            shake = Mathf.MoveTowards(shake, 0, dt);
            movementCooldown=Mathf.Max(0,movementCooldown-dt);
            if (squad && !view.orthographic)
            {
                float growth = Mathf.Clamp01((squad.UnitCount - 5) / 55f);
                // Create space ahead of the formation's reflow; settle gently after losses.
                float blend = 1 - Mathf.Exp(-dt * (Mathf.Lerp(22,31.5f,growth)>distance?18:2));
                distance = Mathf.Lerp(distance, Mathf.Lerp(22, 31.5f, growth), blend);
                focusZ = Mathf.Lerp(focusZ, Mathf.Lerp(6, .8f, growth), blend);
            }
            ApplyCamera();
        }
        void ApplyCamera()
        {
            Vector3 offset = ReducedMotion ? Vector3.zero : new Vector3(Mathf.Sin(shake * 151), Mathf.Cos(shake * 117), 0) * shake * .15f;
            if (squad && !view.orthographic)
            {
                view.transform.rotation = Quaternion.Euler(32, 0, 0);
                view.transform.position = new Vector3(squad.transform.position.x * .4f, .3f, focusZ) - view.transform.forward * distance + offset;
                // Frame both separated choices and the outer robots during formation growth.
                view.focalLength = 42 * (ReducedMotion ? 1 : 1 + shake * .04f);
            }
            else { view.transform.localPosition = home + offset; view.orthographicSize = homeSize; }
        }
        public void Pause(bool paused)
        {
            if (paused) { music.Pause(); effects.Pause(); }
            else { music.UnPause(); effects.UnPause(); }
        }
        public void ResetFeedback() { shake = movementCooldown = 0; distance = 22; focusZ = 6; ApplyCamera(); effects.Stop(); SetClimax(false); music.time = 0; if(!music.isPlaying)music.Play(); }
        static AudioClip MakeTone(string name, float duration, float hz, bool rise)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(duration * rate)];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Min(1, t * 200) * Mathf.Pow(1 - t / duration, 2);
                int step = Mathf.Min(3, (int)(t / duration * 4));
                float note = rise ? (step == 0 ? 1 : step == 1 ? 1.25992f : step == 2 ? 1.49831f : 2) : 1 - t * 1.5f;
                // Continuous phase avoids clicks when the positive major arpeggio changes notes.
                phase += hz * note * Mathf.PI * 2 / rate;
                float fundamental = Mathf.Sin(phase);
                float overtone = (Mathf.Sin(phase * 2.01f)+Mathf.Sin(phase*3.97f)) * .14f;
                float latch=Mathf.Sin(i*1.713f)*Mathf.Sin(i*.413f)*Mathf.Exp(-t*50)*.12f;
                samples[i] = (fundamental * .55f + overtone + latch) * envelope * .6f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        static AudioClip MakeMusic()
        {
            const int rate = 22050;
            var samples = new float[rate * 16];
            float[] notes = { 130.81f, 196, 164.81f, 261.63f, 110, 164.81f, 220, 196, 146.83f, 220, 196, 293.66f, 130.81f,196,261.63f,196 };
            var random=new System.Random(810);float noise=0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, beat = t % .5f;
                int n = (int)(t * 2) % notes.Length;
                float envelope = Mathf.Min(1, beat * 100) * Mathf.Exp(-beat * 7);
                float pluck = (Mathf.Sin(2 * Mathf.PI * notes[n] * 2 * beat) * .28f + Mathf.Sin(2 * Mathf.PI * notes[n] * 4.01f * beat) * .11f) * envelope;
                float kick = Mathf.Sin(2 * Mathf.PI * (70 * beat + .012f * (1 - Mathf.Exp(-beat * 140)))) * Mathf.Exp(-beat * 24) * (n % 2 == 0 ? .27f : .08f);
                float bass = Mathf.Sin(2 * Mathf.PI * notes[n] * .5f * beat) * envelope * .25f;
                noise=Mathf.Lerp(noise,(float)random.NextDouble()*2-1,.4f);
                float hat = noise * Mathf.Exp(-(t % .25f) * 70) * .12f;
                samples[i] = (pluck + bass + kick + hat)*Mathf.Min(1,(16-t)*100);
            }
            var clip = AudioClip.Create("Scrapline pulse — original", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        void OnDestroy()
        {
            AudioListener.volume = 1;
            if (gate) Destroy(gate); if (hurt) Destroy(hurt); if (win) Destroy(win); if (loop) Destroy(loop);
            if(warning)Destroy(warning);
            if(impact)Destroy(impact);
            if(slide)Destroy(slide);if(skid)Destroy(skid);
        }
    }
}
