using UnityEngine;
namespace SalvageSquad
{
    public enum EnemyKind { Swarm, Heavy, Charger, Boss }
    [CreateAssetMenu(menuName = "Salvage Squad/Enemy")]
    public sealed class EnemySettings : ScriptableObject
    {
        public EnemyKind kind;
        [Min(1)] public int health = 5;
        [Min(0)] public float speed = 2.5f;
        [Min(0)] public int contactDamage = 3;
        [Min(.2f)] public float size = 1;
        public Color color = new Color(1, .4f, .2f);
    }
}
