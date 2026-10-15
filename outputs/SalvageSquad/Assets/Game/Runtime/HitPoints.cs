using System;
using UnityEngine;

namespace SalvageSquad
{
    public struct HitPoints
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool IsDestroyed => Current == 0;

        public HitPoints(int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = Current = maximum;
        }

        public bool Damage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsDestroyed || amount == 0) return false;
            Current = Mathf.Max(0, Current - amount);
            return IsDestroyed;
        }

        public void Reset() => Current = Maximum;
    }
}
