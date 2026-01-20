using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using math = Unity.Mathematics.math;
using System.Text;
using System.Diagnostics;

namespace SB4b1.GameMath
{
    public struct GameRng
    {
        private Unity.Mathematics.Random rng;

        public GameRng(uint seed)
        {
            rng = new Unity.Mathematics.Random(seed);
        }

        public int Range(int min, int max)
        {
            return rng.NextInt(min, max);
        }

        public float Range(float min, float max)
        {
            return rng.NextFloat(min, max);
        }

        public bool Chance(float chance01)
        {
            return rng.NextFloat() < chance01;
        }

        public Unity.Mathematics.Random GetState()
        {
            return rng;
        }
    }

    public static class CombatMath
    {
        public static float CalculateDamage(
            float attack,
            float defense,
            float multiplier = 1f)
        {
            float mitigated = attack - defense;
            return math.max(1f, mitigated * multiplier);
        }

        public static float CriticalDamage(
            float baseDamage,
            float critMultiplier)
        {
            return baseDamage * critMultiplier;
        }

        public static bool RollChance(float chance01, ref Unity.Mathematics.Random rng)
        {
            return rng.NextFloat() < chance01;
        }

        public static float DamageOverTime(
            float baseDamage,
            int stacks,
            float tickMultiplier)
        {
            return baseDamage * stacks * tickMultiplier;
        }
    }
}


namespace SB4b1.Logging
{
    public static class GLogger // Game Logger
    {
        public static bool EnableLogs = true;
        public static bool EnableDevLogs = true;

        private static readonly StringBuilder sb = new(128);

        public static void Log(object tag, object sender, object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.Log(Format(tag, sender, message));
        }

        public static void Dev(object message)
        {
            if (!EnableDevLogs) return;
            UnityEngine.Debug.Log(message);
        }

        public static void Warning(object tag, object sender, object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.LogWarning(Format(tag, sender, message));
        }

        public static void Error(object tag, object sender, object message)
        {
            UnityEngine.Debug.LogError(Format(tag, sender, message));
        }

        private static string Format(object tag, object sender, object message)
        {
            sb.Clear();
            sb.Append('[').Append(tag).Append('/').Append(sender).Append("] ");
            sb.Append(message);
            return sb.ToString();
        }
    }
}

namespace SB4b1.Logging
{
    public static class ELogger // Editor Logger
    {
        [Conditional("UNITY_EDITOR")]
        public static void Log(object message)
        {
            UnityEngine.Debug.Log(Format(message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Log(object sender, object message)
        {
            UnityEngine.Debug.Log(Format(sender, message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Log(object tag, object sender, object message)
        {
            UnityEngine.Debug.Log(Format(tag, sender, message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Warning(object message)
        {
            UnityEngine.Debug.LogWarning(Format(message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Warning(object sender, object message)
        {
            UnityEngine.Debug.LogWarning(Format(sender, message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Warning(object tag, object sender, object message)
        {
            UnityEngine.Debug.LogWarning(Format(tag, sender, message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Error(object message)
        {
            UnityEngine.Debug.LogError(Format(message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Error(object sender, object message)
        {
            UnityEngine.Debug.LogError(Format(sender, message));
        }

        [Conditional("UNITY_EDITOR")]
        public static void Error(object tag, object sender, object message)
        {
            UnityEngine.Debug.LogError(Format(tag, sender, message));
        }

        private static string Format(object message)
        {
            return $"[{message}";
        }

        private static string Format(object sender, object message)
        {
            return $"[{sender}] {message}";
        }

        private static string Format(object tag, object sender, object message)
        {
            return $"[{tag}/{sender}] {message}";
        }
    }
}

namespace SB4b1.Utils
{
    public static class RandomUtil
    {
        public static int Range(int min, int max) =>
            UnityEngine.Random.Range(min, max);

        public static float Range(float min, float max) =>
            UnityEngine.Random.Range(min, max);

        public static T Pick<T>(IList<T> list)
        {
            if (list == null || list.Count == 0)
                return default;

            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static T PickWeighted<T>(IList<T> items, IList<int> weights)
        {
            if (items == null || weights == null ||
                items.Count != weights.Count || items.Count == 0)
                return default;

            int total = 0;
            for (int i = 0; i < weights.Count; i++)
                total += Mathf.Max(0, weights[i]);

            int roll = UnityEngine.Random.Range(0, total);
            int sum = 0;

            for (int i = 0; i < items.Count; i++)
            {
                sum += Mathf.Max(0, weights[i]);
                if (roll < sum)
                    return items[i];
            }

            return items[0];
        }
    }
}

namespace SB4b1.Utils
{
    public static class VectorUtil
    {
        public static Vector2 DirectionFromAngle(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        public static float AngleFromDirection(Vector2 dir)
        {
            dir.Normalize();
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            return angle < 0 ? angle + 360f : angle;
        }

        public static Vector3 RandomDirection2D()
        {
            float angle = UnityEngine.Random.Range(0f, 360f);
            return DirectionFromAngle(angle);
        }

        public static float AngleFromDirectionXZ(Vector3 dir)
        {
            dir.Normalize();
            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            return angle < 0 ? angle + 360f : angle;
        }
    }
}

namespace SB4b1.ComponentUtils
{
    public static class ComponentUtil
    {
        public static GameObject GetChildGameObjectByTag(GameObject parent, string tag)
        {
            foreach (Transform child in parent.transform)
            {
                if (child.gameObject.CompareTag(tag))
                    return child.gameObject;

                GameObject found = GetChildGameObjectByTag(child.gameObject, tag);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}


