using System;
using UnityEngine;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Declarative helper wrapper for the 10 stylized pixel-art particle types in CinematicUIParticleSystem.
    /// Exposes clean one-line triggers for directional bursts, energy sparks, speed streaks, data clouds,
    /// impact shards, and pixel dust.
    /// </summary>
    public static class UIParticleFX
    {
        private static CinematicUIParticleSystem System => CinematicUIParticleSystem.Instance;

        public static void Sparks(Vector2 position, Transform parent, Color color, int count = 4, float radius = 24f)
        {
            if (System != null) System.SpawnSparkBurst(position, parent, color, count, radius);
        }

        public static void Sparks(Vector3 worldPosition, Color color, int count = 4, float radius = 24f)
        {
            if (System != null) System.SpawnSparkBurst(worldPosition, color, count, radius);
        }

        public static void DirectionalSparks(Vector2 position, Transform parent, Vector2 direction, Color color, int count = 5, float speed = 150f)
        {
            if (System != null) System.SpawnDirectionalBurst(position, parent, color, direction, count, 45f, speed * 0.25f);
        }

        public static void DirectionalSparks(Vector3 worldPosition, Vector2 direction, Color color, int count = 5, float speed = 150f)
        {
            if (System != null) System.SpawnDirectionalBurst((Vector2)worldPosition, null, color, direction, count, 45f, speed * 0.25f);
        }

        public static void SpeedTrail(Vector2 from, Vector2 to, Transform parent, Color color, int count = 6)
        {
            if (System != null) System.SpawnEnergyTrail(from, parent, color, to - from);
        }

        public static void Shards(Vector2 position, Transform parent, Color color, int count = 6, float radius = 32f)
        {
            if (System != null) System.SpawnSparkBurst(position, parent, color, count, radius);
        }

        public static void Shards(Vector3 worldPosition, Color color, int count = 6, float radius = 32f)
        {
            if (System != null) System.SpawnSparkBurst(worldPosition, color, count, radius);
        }

        public static void DataParticles(Vector2 position, Transform parent, Color color, int count = 5)
        {
            if (System != null) System.SpawnDataFloat(position, parent, color, count, 35f);
        }

        public static void DataParticles(Vector3 worldPosition, Color color, int count = 5)
        {
            if (System != null) System.SpawnDataFloat(worldPosition, color, count, 35f);
        }

        public static void ScreenEdgeWave(RectTransform container, Color color, int countPerEdge = 4)
        {
            if (System != null) System.SpawnScreenEdgeWave(container, color);
        }

        public static void PixelDissolve(RectTransform target, Color color, int count = 16)
        {
            if (System != null && target != null) System.SpawnPixelDissolveShards(target, target.parent, color, count);
        }

        public static void GenericBurst(UIParticleType type, Vector2 position, Transform parent, Color color, int count = 4, float speed = 100f)
        {
            if (System == null) return;
            switch (type)
            {
                case UIParticleType.DataParticle:
                    System.SpawnDataFloat(position, parent, color, count, 35f);
                    break;
                case UIParticleType.EnergyBubble:
                    System.SpawnEnergyBubble(position, parent, color, 0.24f);
                    break;
                default:
                    System.SpawnSparkBurst(position, parent, color, count, speed * 0.25f);
                    break;
            }
        }
    }
}
