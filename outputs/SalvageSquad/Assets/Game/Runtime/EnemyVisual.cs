using UnityEngine;

namespace SalvageSquad
{
    // One pooled renderer swaps authored silhouettes; enemy rules stay in CombatSystem.
    public sealed class EnemyVisual : MonoBehaviour
    {
        public MeshFilter shell;
        public Mesh[] forms;
        public Transform[] crusherArms;
        EnemyKind kind;

        public void Configure(EnemyKind value)
        {
            kind = value;
            shell.sharedMesh = forms[(int)value];
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localRotation = Quaternion.identity;
            if(crusherArms!=null)foreach(var arm in crusherArms){arm.gameObject.SetActive(kind==EnemyKind.Boss);arm.localRotation=Quaternion.identity;}
        }

        public void Tick(float age, float hit, float attack = 0)
        {
            float bob = kind == EnemyKind.Boss ? .015f : .035f;
            shell.transform.localPosition = new Vector3(0, Mathf.Sin(age * 9) * bob, hit * .1f);
            float windup = kind == EnemyKind.Charger && age < 1.2f ? -12 : 0;
            shell.transform.localRotation = Quaternion.Euler(windup + hit * 11 - attack * 7, 0, Mathf.Sin(age * 6) * 2);
            if(kind==EnemyKind.Boss && crusherArms!=null)foreach(var arm in crusherArms)
                arm.localRotation=Quaternion.Euler(Mathf.SmoothStep(0,1,attack)*85,0,0);
        }
    }
}
