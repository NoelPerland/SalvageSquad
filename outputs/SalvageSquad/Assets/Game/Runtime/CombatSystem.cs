using UnityEngine;

namespace SalvageSquad
{
    public sealed class CombatSystem : MonoBehaviour
    {
        public Transform projectilePrefab;
        public Transform targetPrefab;
        public Transform burstPrefab;
        [Min(1)] public int targetHealth = 3;
        [Min(1)] public float bulletSpeed = 24f;
        [Min(0)] public float targetSpeed = 1.8f;
        public int Salvaged { get; private set; }
        public int Missed { get; private set; }
        public int ActiveProjectiles { get; private set; }
        public int ActiveTargets { get; private set; }
        public int ActiveEffects { get; private set; }
        public int TotalHits { get; private set; }
        public int TotalShots { get; private set; }
        public int DroppedTargets { get; private set; }
        public float ShotStrength = 1;
        public System.Action<float, int> Contact;
        public bool BossDefeated { get; private set; }
        public float BossHealth { get; private set; }
        public int DroppedShots { get; private set; }
        public GateRow[] ShootableGates;
        public float BossWindup;
        public const float BossArrivalDuration=1.6f;
        public float BossArrivalRemaining { get; private set; }
        public bool BossArmored => BossArrivalRemaining>0;

        sealed class Bullet { public Transform view; public Renderer renderer; public int damage; public float aimX; }
        sealed class Target
        {
            public Transform view;
            public Renderer hull;
            public EnemyVisual visual;
            public Color color;
            public HitPoints health;
            public float oldZ, flash;
            public EnemySettings settings;
            public float age, deathTime;
        }
        sealed class Burst
        {
            public Transform view, core, ring;
            public Transform[] chips;
            public Vector3[] chipStarts;
            public float life;
        }

        Bullet[] bullets;
        Target[] targets;
        readonly Burst[] bursts = new Burst[24];
        MaterialPropertyBlock properties;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        bool sliceMode;
        AudioSource sound, shots;
        AudioClip hitSound, salvageSound, cannonSound;
        AudioClip[] shotSounds;
        float soundCooldown, shotCooldown;
        int shotVariant;

        public void Initialize(bool slice = false)
        {
            bullets = new Bullet[slice ? 640 : 80];
            sliceMode = slice;
            targets = new Target[slice ? 64 : 12];
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < bullets.Length; i++)
            {
                var view = Make(projectilePrefab, "Bolt");
                bullets[i] = new Bullet { view = view, renderer = view.GetComponentInChildren<Renderer>() };
            }
            for (int i = 0; i < targets.Length; i++)
            {
                Transform view = Make(targetPrefab, "Scrap target");
                Renderer hull = view.GetComponentInChildren<Renderer>();
                targets[i] = new Target { view = view, hull = hull, visual = view.GetComponent<EnemyVisual>(),
                    color = hull.sharedMaterial.color, health = new HitPoints(targetHealth) };
            }
            for (int i = 0; i < bursts.Length; i++)
            {
                var view = Make(burstPrefab, "Scrap burst");
                var burst = new Burst { view = view, core = view.Find("Core"), ring = view.Find("Ring") };
                if (burst.core)
                {
                    burst.chips = new Transform[6]; burst.chipStarts = new Vector3[6];
                    for (int j = 0; j < 6; j++)
                    {
                        burst.chips[j] = view.Find("Chip " + j);
                        burst.chipStarts[j] = burst.chips[j].localPosition;
                    }
                }
                bursts[i] = burst;
            }
            sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 0; sound.priority = 130;
            shots = gameObject.AddComponent<AudioSource>(); shots.priority = 180;
            hitSound = MakeMechanical("Metal impact", .11f, 360, .35f);
            salvageSound = MakeMechanical("Scrap break", .27f, 95, .7f);
            cannonSound = MakeMechanical("Heavy salvage cannon", .17f, 72, 1);
            shotSounds = new AudioClip[3];
            for(int i=0;i<shotSounds.Length;i++)shotSounds[i]=MakeMechanical("Ranger shot "+i,.095f,145+i*7,.65f);
        }

        Transform Make(Transform prefab, string label)
        {
            Transform view = Instantiate(prefab, transform);
            view.name = label;
            view.gameObject.SetActive(false);
            return view;
        }

        public void ResetCombat()
        {
            foreach (Bullet bullet in bullets) bullet.view.gameObject.SetActive(false);
            foreach (Target target in targets)
            {
                target.view.gameObject.SetActive(false);
                target.health.Reset();
                target.flash = target.deathTime = 0;
                target.hull.SetPropertyBlock(null);
                if (target.visual) target.hull.SetPropertyBlock(null, 0);
            }
            foreach (Burst burst in bursts) burst.view.gameObject.SetActive(false);
            Salvaged = Missed = TotalHits = TotalShots = DroppedTargets = ActiveProjectiles = ActiveTargets = ActiveEffects = 0;
            BossDefeated = false; BossHealth = 0; DroppedShots = 0;
            BossArrivalRemaining=0;
            soundCooldown = shotCooldown = BossWindup = 0; shotVariant = 0;
            sound.Stop(); shots.Stop();
            ShotStrength = 1;
        }
        public void SetPaused(bool paused)
        {
            if(paused){sound.Pause();shots.Pause();}else{sound.UnPause();shots.UnPause();}
        }

        public bool Fire(Vector3 origin, int damage, bool heavy = false, float aimX=float.NaN)
        {
            foreach (Bullet bullet in bullets)
            {
                if (bullet.view.gameObject.activeSelf) continue;
                bullet.view.position = origin;
                bullet.renderer.enabled = !sliceMode || origin.z >= 1.2f;
                bullet.damage = damage;
                bullet.aimX=float.IsNaN(aimX)?origin.x:aimX;
                bullet.view.localScale = Vector3.one * Mathf.Min(1.8f, 1 + (damage - 1) * .2f);
                bullet.view.gameObject.SetActive(true);
                ActiveProjectiles++;
                TotalShots++;
                // A bounded firing bed conveys a crowd without creating one voice per soldier.
                if(shotCooldown <= 0)
                {
                    bool powerful = heavy || damage >= 4;
                    shots.PlayOneShot(powerful ? cannonSound : shotSounds[shotVariant++ % shotSounds.Length], (powerful ? .23f : .16f) * ShotStrength);
                    shotCooldown = powerful ? .095f : .075f;
                }
                return true;
            }
            DroppedShots++;
            return false;
        }

        public bool SpawnTarget(float x, EnemySettings settings = null, float z = 15)
        {
            foreach (Target target in targets)
            {
                if (target.view.gameObject.activeSelf) continue;
                target.settings = settings;
                if (target.visual) target.visual.Configure(settings ? settings.kind : EnemyKind.Swarm);
                target.health = new HitPoints(settings ? settings.health : targetHealth);
                target.age = target.deathTime = 0; target.view.rotation = Quaternion.identity;
                if (settings && settings.kind == EnemyKind.Boss) { BossHealth = 1;BossArrivalRemaining=BossArrivalDuration; }
                target.flash = 0;
                target.hull.SetPropertyBlock(null);
                if (target.visual) target.hull.SetPropertyBlock(null, 0);
                target.view.localScale = Vector3.one;
                target.view.position = new Vector3(x, .7f, z);
                target.oldZ = z;
                target.view.gameObject.SetActive(true);
                ActiveTargets++;
                return true;
            }
            DroppedTargets++;
            return false;
        }

        // Relative motion keeps fast bullets from tunnelling through approaching targets.
        public static bool CrossesTarget(float bulletX, float targetX, float relativeStart, float relativeEnd)
            => Mathf.Abs(bulletX - targetX) <= .62f && relativeStart <= .55f && relativeEnd >= -.55f;

        public void StopActors()
        {
            foreach (var bullet in bullets) bullet.view.gameObject.SetActive(false);
            foreach (var target in targets) if(target.deathTime<=0)target.view.gameObject.SetActive(false);
            shots.Stop();
            ActiveProjectiles = ActiveTargets = 0;
        }

        public void Tick(float dt)
        {
            BossArrivalRemaining=Mathf.Max(0,BossArrivalRemaining-dt);
            soundCooldown -= dt;
            shotCooldown -= dt;
            foreach (Target target in targets)
            {
                if (!target.view.gameObject.activeSelf) continue;
                if(target.deathTime>0)
                {
                    target.deathTime=Mathf.Max(0,target.deathTime-dt);
                    float t=1-target.deathTime/.75f;
                    target.view.rotation=Quaternion.Euler(t*18,0,-t*28);
                    target.view.position=new Vector3(target.view.position.x,.7f-t*.6f,target.view.position.z);
                    target.view.localScale=Vector3.one*target.settings.size*(1-t*.18f);
                    if(target.deathTime<=0)target.view.gameObject.SetActive(false);
                    continue;
                }
                target.oldZ = target.view.position.z;
                target.age += dt;
                float speed = target.settings ? target.settings.speed : targetSpeed;
                if (target.settings && target.settings.kind == EnemyKind.Charger)
                    speed *= target.age < 1.2f ? .2f : 2.2f;
                bool boss = target.settings && target.settings.kind == EnemyKind.Boss;
                if (!boss || target.view.position.z > 9) target.view.position += Vector3.back * (speed * dt);
                target.flash = Mathf.Max(0, target.flash - dt * 8);
                float size = target.settings ? target.settings.size : 1;
                target.view.localScale = Vector3.one * size * (1 + target.flash * (target.visual ? .075f : .16f));
                Color baseColor = target.settings ? target.settings.color : target.color;
                if (target.settings && target.settings.kind == EnemyKind.Charger && target.age < 1.2f)
                    baseColor = Color.Lerp(baseColor, Color.white, Mathf.PingPong(target.age * 6, 1));
                properties.SetColor(BaseColor, Color.Lerp(baseColor, Color.white, target.flash * .55f));
                if (target.visual)
                {
                    properties.SetColor(BaseColor, boss&&BossArmored?new Color(.35f,.65f,1):Color.white);
                    float glow = target.flash * .5f;
                    if (target.settings && target.settings.kind == EnemyKind.Charger && target.age < 1.2f)
                        glow = Mathf.Max(glow, Mathf.PingPong(target.age * 6, 1) * .2f);
                    properties.SetColor(EmissionColor, Color.white * glow);
                    target.hull.SetPropertyBlock(properties, 0);
                    target.visual.Tick(target.age, target.flash, boss ? BossWindup : 0);
                }
                else target.hull.SetPropertyBlock(properties);
                if (target.view.position.z < (target.settings ? .55f : -2.5f))
                {
                    target.view.gameObject.SetActive(false);
                    ActiveTargets--;
                    Missed++;
                    if (target.settings) Contact?.Invoke(target.view.position.x, target.settings.contactDamage);
                }
            }

            foreach (Bullet bullet in bullets)
            {
                if (!bullet.view.gameObject.activeSelf) continue;
                Vector3 previous = bullet.view.position;
                // Reach the far end of the visible track while keeping projectile lifetime bounded.
                float range = sliceMode ? 22 : 24;
                float endZ = previous.z + bulletSpeed * dt;
                bullet.view.position = new Vector3(previous.x, previous.y, Mathf.Min(endZ, range));
                if (sliceMode && !bullet.renderer.enabled && bullet.view.position.z >= 1.2f) bullet.renderer.enabled = true;
                Target nearest = null;
                float closest = float.PositiveInfinity;
                foreach (Target target in targets)
                {
                    if (!target.view.gameObject.activeSelf || target.health.IsDestroyed) continue;
                    float radius = target.settings ? .62f * target.settings.size : .62f;
                    if (Mathf.Abs(previous.x - target.view.position.x) > radius ||
                        previous.z - target.oldZ > radius || bullet.view.position.z - target.view.position.z < -radius) continue;
                    float travel = bullet.view.position.z - previous.z - (target.view.position.z - target.oldZ);
                    float fraction = travel > 0 ? Mathf.Max(0, target.oldZ - radius - previous.z) / travel : 0;
                    if (fraction >= closest) continue;
                    closest = fraction;
                    nearest = target;
                }
                GateRow gateHit = null;
                if (ShootableGates != null) foreach (var gate in ShootableGates)
                    if(gate.ShotCrossing(previous.x, previous.z, bullet.view.position.z, dt, out float fraction, bullet.aimX) && fraction < closest)
                    { gateHit = gate; closest = fraction; }
                if(gateHit)
                {
                    bool turnedPositive=gateHit.Charge(previous.x);
                    if(turnedPositive)Pop(new Vector3(previous.x,.9f,gateHit.transform.position.z));
                    bullet.view.gameObject.SetActive(false); ActiveProjectiles--;
                    if(turnedPositive || soundCooldown<=0){sound.PlayOneShot(turnedPositive?salvageSound:hitSound,turnedPositive?.24f:.11f);soundCooldown=.07f;}
                }
                else if (nearest != null)
                {
                    bool boss = nearest.settings && nearest.settings.kind == EnemyKind.Boss;
                    bool destroyed = nearest.health.Damage(boss&&BossArmored?0:bullet.damage);
                    TotalHits++;
                    nearest.flash = 1;
                    if (boss) BossHealth = (float)nearest.health.Current / nearest.health.Maximum;
                    if (destroyed)
                    {
                        Pop(nearest.view.position);
                        if(!boss)nearest.view.gameObject.SetActive(false);
                        ActiveTargets--;
                        Salvaged++;
                        if (boss)
                        {
                            BossDefeated = true;
                            nearest.deathTime=.75f;
                            properties.SetColor(BaseColor,new Color(.45f,.45f,.45f));properties.SetColor(EmissionColor,Color.black);
                            nearest.hull.SetPropertyBlock(properties,0);
                            for (int i = 0; i < 8; i++) Pop(nearest.view.position + new Vector3((i % 3 - 1) * 1.2f, i * .15f, (i / 3 - 1) * .8f));
                        }
                    }
                    if (soundCooldown <= 0)
                    {
                        sound.PlayOneShot(destroyed ? salvageSound : hitSound, destroyed ? .28f : .13f);
                        soundCooldown = .06f;
                    }
                    bullet.view.gameObject.SetActive(false);
                    ActiveProjectiles--;
                }
                else if (endZ >= range)
                {
                    bullet.view.gameObject.SetActive(false);
                    ActiveProjectiles--;
                }
            }

            foreach (Burst burst in bursts)
            {
                if (!burst.view.gameObject.activeSelf) continue;
                burst.life -= dt;
                if (burst.core)
                {
                    float t = Mathf.Clamp01(1 - burst.life / .5f);
                    burst.core.localScale = Vector3.one * (.65f * Mathf.Pow(1 - t, 2));
                    burst.ring.localScale = Vector3.one * (1 + t * 2.8f);
                    burst.ring.gameObject.SetActive(t < .45f);
                    for (int i = 0; i < burst.chips.Length; i++)
                    {
                        burst.chips[i].localPosition = burst.chipStarts[i] * (1 + t * 3) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .7f);
                        burst.chips[i].localScale = new Vector3(.13f,.15f,.2f) * (1 - t);
                        burst.chips[i].localRotation = Quaternion.Euler(t * 270, i * 60, t * 150);
                    }
                }
                else
                {
                    burst.view.localScale = Vector3.one * Mathf.Max(0, (1 - burst.life / .35f) * 1.5f);
                    burst.view.Rotate(0, dt * 190, dt * 80);
                }
                if (burst.life <= 0)
                {
                    burst.view.gameObject.SetActive(false);
                    ActiveEffects--;
                }
            }
        }

        public void Pop(Vector3 position)
        {
            foreach (Burst burst in bursts)
            {
                if (burst.view.gameObject.activeSelf) continue;
                burst.view.position = position;
                burst.view.rotation = Quaternion.identity;
                burst.view.localScale = burst.core ? Vector3.one : Vector3.zero;
                burst.life = burst.core ? .5f : .35f;
                if (burst.core)
                {
                    burst.ring.localPosition = new Vector3(0, .03f - position.y, 0);
                    burst.ring.gameObject.SetActive(true);
                    burst.core.localScale = Vector3.one * .65f;
                    for (int i = 0; i < burst.chips.Length; i++)
                    {
                        burst.chips[i].localPosition = burst.chipStarts[i];
                        burst.chips[i].localScale = new Vector3(.13f,.15f,.2f);
                    }
                }
                burst.view.gameObject.SetActive(true);
                ActiveEffects++;
                break;
            }
        }

        public static AudioClip MakeMechanical(string label, float duration, float frequency, float weight)
        {
            if(!(duration>0)||float.IsInfinity(duration)||!(frequency>0)||float.IsInfinity(frequency)||!(weight>=0&&weight<=1))throw new System.ArgumentOutOfRangeException(null,"Sound parameters must be finite, positive, and weight between zero and one.");
            const int rate = 22050;
            float[] data = new float[Mathf.CeilToInt(rate * duration)];
            var random=new System.Random(417+(int)frequency);float filtered=0,phase=0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Min(1,t*600)*Mathf.Pow(1-t/duration,2);
                float noise=(float)random.NextDouble()*2-1;filtered=Mathf.Lerp(filtered,noise,.28f);
                phase += frequency*(1+.8f*Mathf.Exp(-t*55))*2*Mathf.PI/rate;
                float body=Mathf.Sin(phase)*Mathf.Exp(-t*24)*weight*.6f;
                float crack=(noise*.25f+filtered*.5f)*Mathf.Exp(-t*65);
                float metal=Mathf.Sin(2*Mathf.PI*frequency*2.71f*t)*Mathf.Exp(-t*18)*.14f;
                data[i]=Mathf.Clamp((body+crack+metal)*envelope,-.95f,.95f);
            }
            AudioClip clip = AudioClip.Create(label, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (hitSound) Destroy(hitSound);
            if (salvageSound) Destroy(salvageSound);
            if(cannonSound)Destroy(cannonSound);
            if(shotSounds!=null)foreach(var clip in shotSounds)if(clip)Destroy(clip);
        }
    }
}
