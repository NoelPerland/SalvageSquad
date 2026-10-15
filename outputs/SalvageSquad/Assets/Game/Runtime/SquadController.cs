using UnityEngine;
using UnityEngine.InputSystem;

namespace SalvageSquad
{
    public sealed class SquadController : MonoBehaviour
    {
        public Transform[] bodies;
        public Transform[] muzzles;
        public Transform[] flashes;
        public RectTransform[] buttons;
        public Camera uiCamera;
        public float horizontalLimit = 2.65f;
        public float moveSpeed = 18f;
        public float shotInterval = .48f;
        public int UnitCount { get; private set; }
        public int VisibleCount => Mathf.Min(UnitCount, bodies.Length);
        public int Damage { get; private set; } = 1;
        public float FireRate { get; private set; } = 1;
        public int WeaponTier { get; private set; } = 1;
        public bool HasSteered { get; private set; }
        public bool ReducedMotion;
        public float DesiredX { get; private set; }
        CombatSystem combat;
        bool dragging, wasPressed;
        float lastPointerX, shotTimer, recoil;
        Transform[] roots;
        Transform[] cannons;
        Vector3[] cannonScales;
        Vector3[] cannonPositions, muzzlePositions;
        Transform[] heavySleeves;
        float[] shotTimers;
        double[] shotRemainders;
        float protection;
        float motionTime;
        bool formationMode;

        public void Initialize(CombatSystem system, int capacity = 5)
        {
            combat = system;
            formationMode = capacity > 5;
            int original = bodies.Length;
            System.Array.Resize(ref bodies, capacity);
            System.Array.Resize(ref muzzles, capacity);
            System.Array.Resize(ref flashes, capacity);
            roots = new Transform[capacity]; shotTimers = new float[capacity];
            shotRemainders = new double[capacity];
            cannons = new Transform[capacity]; cannonScales = new Vector3[capacity];
            cannonPositions = new Vector3[capacity]; muzzlePositions = new Vector3[capacity]; heavySleeves = new Transform[capacity];
            for (int i = 0; i < capacity; i++)
            {
                if (i >= original)
                {
                    roots[i] = Instantiate(bodies[0].parent, transform);
                    bodies[i] = roots[i].Find("Body");
                    muzzles[i] = bodies[i].Find("Muzzle");
                    flashes[i] = muzzles[i].Find("Muzzle flash");
                }
                else roots[i] = bodies[i].parent;
                cannons[i] = bodies[i].Find("Cannon");
                if (cannons[i]) cannonScales[i] = cannons[i].localScale;
                if (cannons[i]) { cannonPositions[i] = cannons[i].localPosition; heavySleeves[i] = cannons[i].Find("Heavy sleeve"); }
                muzzlePositions[i] = muzzles[i].localPosition;
            }
        }

        public static Vector3 FormationSlot(int index, int count)
        {
            int columns = Mathf.Min(6, Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(count))));
            int row = index / columns;
            int inRow = Mathf.Min(columns, count - row * columns);
            return new Vector3((index % columns - (inRow - 1) * .5f) * .64f, 0, .5f - row * 1f);
        }

        public void SetCount(int count)
        {
            int before = VisibleCount;
            UnitCount = Mathf.Max(0,count);
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].gameObject.SetActive(i < UnitCount);
                if (formationMode && i >= before && i < UnitCount)
                {
                    roots[i].localPosition = FormationSlot(i, VisibleCount) + Vector3.up * (ReducedMotion ? 0 : .8f);
                    roots[i].localScale = Vector3.one * (ReducedMotion ? .8f : .35f);
                    shotTimers[i] = (i % 6) * shotInterval / (6 * Mathf.Min(3, FireRate));
                    shotRemainders[i] = 0;
                }
            }
        }

        public bool LoseUnits(int amount)
        {
            if (amount <= 0 || protection > 0 || UnitCount == 0) return false;
            SetCount(UnitCount - amount); protection = .65f;
            return true;
        }

        public void Apply(GateEffect effect)
        {
            switch (effect.kind)
            {
                case GateKind.Add:
                case GateKind.Multiply: SetCount(effect.ResultCount(UnitCount)); break;
                case GateKind.Damage: Damage = (int)System.Math.Clamp(System.Math.Round(Damage * (double)effect.value), 1, int.MaxValue); break;
                case GateKind.FireRate: FireRate = (float)System.Math.Min(float.MaxValue, FireRate * (double)effect.value); break;
                case GateKind.Weapon:
                    int tier = (int)System.Math.Clamp(System.Math.Round(effect.value), 1, int.MaxValue);
                    int gain = Mathf.Max(0, tier - WeaponTier);
                    Damage = (int)System.Math.Min(int.MaxValue, (long)Damage + gain);
                    FireRate = (float)System.Math.Min(float.MaxValue, FireRate * (1 + .15 * gain));
                    WeaponTier = Mathf.Max(WeaponTier, tier);
                    break;
            }
        }

        public void ResetSquad()
        {
            transform.position = Vector3.zero;
            DesiredX = shotTimer = recoil = 0;
            protection = 0; Damage = WeaponTier = 1; FireRate = 1; HasSteered = false;
            motionTime = 0;
            SetCount(5);
            for (int i = 0; i < roots.Length; i++)
            {
                shotRemainders[i] = 0;
                shotTimers[i] = (i % 6) * shotInterval / 6;
                if (formationMode)
                {
                    roots[i].localPosition = FormationSlot(i, Mathf.Max(5, UnitCount));
                    roots[i].localScale = Vector3.one * .8f;
                }
                if (cannons[i]) cannons[i].localScale = cannonScales[i];
                if (cannons[i]) cannons[i].localPosition = cannonPositions[i];
                if (heavySleeves[i]) heavySleeves[i].gameObject.SetActive(false);
                muzzles[i].localPosition = muzzlePositions[i];
                bodies[i].localPosition = Vector3.zero;
            }
            CancelDrag();
            foreach (Transform body in bodies) body.localRotation = Quaternion.identity;
            foreach (Transform flash in flashes) flash.gameObject.SetActive(false);
        }

        public void SetDesiredX(float x) => DesiredX = Mathf.Clamp(x, -horizontalLimit, horizontalLimit);
        public void CancelDrag() { dragging = wasPressed = false; }

        public void Tick(float dt)
        {
            bool pressed = false;
            Vector2 pointer = Vector2.zero;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                pressed = true;
                pointer = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.isPressed;
                pointer = Mouse.current.position.ReadValue();
            }
            if (pressed && !wasPressed)
            {
                dragging = true;
                foreach (RectTransform button in buttons)
                    if (button.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(button, pointer, uiCamera)) dragging = false;
                lastPointerX = pointer.x;
            }
            if (pressed && dragging)
            {
                if (Mathf.Abs(pointer.x - lastPointerX) > 2) HasSteered = true;
                SetDesiredX(DesiredX + (pointer.x - lastPointerX) / Mathf.Max(1, Screen.width) * 9f);
                lastPointerX = pointer.x;
            }
            if (!pressed) dragging = false;
            wasPressed = pressed;

            float previousX = transform.position.x;
            transform.position = new Vector3(Mathf.MoveTowards(previousX, DesiredX, moveSpeed * dt), 0, 0);
            shotTimer -= dt;
            recoil = Mathf.Max(0, recoil - dt * 10f);
            protection = Mathf.Max(0, protection - dt);
            if (formationMode)
            {
                motionTime += dt;
                float positionBlend = 1 - Mathf.Exp(-12 * dt), scaleBlend = 1 - Mathf.Exp(-10 * dt);
                float formationLean = ReducedMotion ? 0 : Mathf.Clamp((transform.position.x - previousX) / Mathf.Max(dt, .001f) * -1.5f, -15, 15);
                combat.ShotStrength = Mathf.Lerp(.65f, 1.25f, UnitCount / 60f);
                float firingRate = Mathf.Min(3, FireRate);
                for (int i = 0; i < VisibleCount; i++)
                {
                    roots[i].localPosition = Vector3.Lerp(roots[i].localPosition, FormationSlot(i, VisibleCount), positionBlend);
                    roots[i].localScale = Vector3.Lerp(roots[i].localScale, Vector3.one * .8f, scaleBlend);
                    shotTimers[i] -= dt;
                    muzzles[i].localPosition = muzzlePositions[i] + (WeaponTier >= 2 ? Vector3.forward * .19f : Vector3.zero);
                    if (shotTimers[i] <= 0)
                    {
                        // Extra recruits share the fixed visuals; total volley power still grows per recruit.
                        int represented=UnitCount/VisibleCount+(i<UnitCount%VisibleCount?1:0);
                        // Keep projectile load bounded; excess fire rate becomes equivalent shot damage.
                        double power=Damage*(double)represented*(FireRate/firingRate)+shotRemainders[i];
                        int shotDamage=(int)System.Math.Min(int.MaxValue,System.Math.Floor(power));
                        shotRemainders[i]=power>=int.MaxValue?0:power-shotDamage;
                        combat.Fire(muzzles[i].position, shotDamage, WeaponTier >= 2, transform.position.x);
                        shotTimers[i] += shotInterval / firingRate;
                    }
                    float kick = Mathf.Clamp01(1 - (shotInterval / firingRate - shotTimers[i]) * 12);
                    bodies[i].localRotation = Quaternion.Euler(ReducedMotion ? 0 : -kick * 4, 0, formationLean);
                    bodies[i].localPosition = Vector3.up * (ReducedMotion ? 0 : Mathf.Sin(motionTime * 12 + i * 1.7f) * .028f);
                    flashes[i].gameObject.SetActive(kick > .6f);
                    muzzles[i].localScale = Vector3.one * (WeaponTier >= 2 ? 1.6f : 1);
                    if (cannons[i]) cannons[i].localScale = cannonScales[i] * (WeaponTier >= 2 ? 1.4f : 1);
                    if (cannons[i]) cannons[i].localPosition = cannonPositions[i] - Vector3.forward * (kick * .07f);
                    if (heavySleeves[i]) heavySleeves[i].gameObject.SetActive(WeaponTier >= 2);
                }
                return;
            }
            if (shotTimer <= 0)
            {
                foreach (Transform muzzle in muzzles) combat.Fire(muzzle.position, 1);
                shotTimer += shotInterval;
                recoil = 1;
            }
            float lean = Mathf.Clamp((transform.position.x - previousX) / Mathf.Max(dt, .001f) * -1.5f, -15, 15);
            foreach (Transform body in bodies)
                body.localRotation = Quaternion.Lerp(body.localRotation, Quaternion.Euler(recoil * -7, 0, lean), 15 * dt);
            foreach (Transform flash in flashes)
                flash.gameObject.SetActive(recoil > .65f);
        }
    }
}
