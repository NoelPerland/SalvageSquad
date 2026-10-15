using System;
using NUnit.Framework;

namespace SalvageSquad.Tests
{
    public class CombatTests
    {
        [Test]
        public void DamageReportsDestructionOnlyOnceAndResetRestoresHealth()
        {
            var health = new HitPoints(3);
            Assert.That(health.Damage(1), Is.False);
            Assert.That(health.Current, Is.EqualTo(2));
            Assert.That(health.Damage(100), Is.True);
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.Damage(1), Is.False);
            health.Reset();
            Assert.That(health.Current, Is.EqualTo(3));
            Assert.That(health.IsDestroyed, Is.False);
        }

        [Test]
        public void InvalidHealthAndNegativeDamageAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HitPoints(0));
            var health = new HitPoints(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.Damage(-1));
            Assert.That(health.Current, Is.EqualTo(3));
        }

        [TestCase(0f, 0f, -8f, 3f, true)]
        [TestCase(2f, 0f, -8f, 3f, false)]
        [TestCase(0f, 0f, 2f, 4f, false)]
        [TestCase(0f, 0f, -8f, -2f, false)]
        public void SweptHitRespectsLaneAndCrossing(float x, float targetX, float start, float end, bool expected)
            => Assert.That(CombatSystem.CrossesTarget(x, targetX, start, end), Is.EqualTo(expected));
    }
}
