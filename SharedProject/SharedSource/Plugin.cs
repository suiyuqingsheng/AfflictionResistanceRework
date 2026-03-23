using System;
using Barotrauma;
using HarmonyLib;
using System.Reflection;

namespace AfflictionResistanceRework
{
    partial class AfflictionResistanceRework : IAssemblyPlugin
    {
        const string harmony_id = "Affliction Resistance Rework";
        public Harmony? harmonyInstance;
        public void Initialize()
        {
            harmonyInstance = new Harmony(harmony_id);
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            LuaCsLogger.Log("Affliction Resistance Rework loaded!");
        }

        public void OnLoadCompleted()
        {
        }

        public void PreInitPatching()
        {
        }

        public void Dispose()
        {
            harmonyInstance?.UnpatchSelf();
            harmonyInstance = null;
            LuaCsLogger.Log("Affliction Resistance Rework disposed!");
        }

        // 统一所有抗性计算方式为伤害倍率相乘，最后用1-最后的伤害的倍率就是最终的抗性，在实际使用抗性的地方都是1-抗性，所以最后就是倍率
        // 如果有完全减免的也可以支持，而且不会导致出现负数和超过100%的抗性
        // 还得是假鱼会算
        // 天赋的值是伤害倍率，aff抗性是减免多少
        // 假如天赋给的倍率是0.8 0.9各免伤15% 10%,aff抗性0.1 0.2免伤10% 20%
        // 天赋倍率返回值1.7，aff合计抗性是0.3
        // return 1 - ((1 - resistance) * abilityResistanceMultiplier);
        // 1-( (1-0.3) * 1.7) = 1- (0.7 * 1.7) = 1- 1.19 = -0.19 最后抗性是-19%
        // Math.Min(newAffliction.Prefab.MaxStrength, newAffliction.Strength * (100.0f / MaxVitality) * (1f - GetResistance(newAffliction.Prefab, limbType)))
        // 加在肢体上的伤害=原始strength * (100.0f / MaxVitality) * (1- (-0.19)) = 1.19倍原始的伤害
        // 只改GetResistance
        // resistanceMultiplier = 1.7 * (1-0.1) * (1-0.2) = 1.7 * 0.9 * 0.8 = 1.224
        // __result = 1 - resistanceMultiplier = 1 - 1.224 = -0.224 最后抗性是-22.4%
        // 加上改GetAbilityResistance
        // resistanceMultiplier = 0.72 * (1-0.1) * (1-0.2) = 0.72 * 0.9 * 0.8 = 0.5184
        // __result = 1 - resistanceMultiplier = 1 - 0.5184 = 0.4816 最后抗性是48.16%

        // 假如天赋给的倍率是0.1 0.1各免伤90% 90%,aff抗性0.8 0.8免伤80% 80%
        // 天赋倍率返回值0.2，aff合计抗性是1.6
        // return 1 - ((1 - resistance) * abilityResistanceMultiplier);
        // 1-( (1-1.6) * 0.2) = 1- (-0.6 * 0.2) = 1- (-0.12) = 1.12 最后抗性是112%
        // 只改GetResistance的话
        // resistanceMultiplier = 0.2 * (1-0.8) * (1-0.8) = 0.2 * 0.2 * 0.2 = 0.008
        // __result = 1 - resistanceMultiplier = 1 - 0.008 = 0.992 最后抗性是99.2%
        // 加上改GetAbilityResistance的话
        // resistanceMultiplier = 0.01 * (1-0.8) * (1-0.8) = 0.01 * 0.2 * 0.2 = 0.0004
        // __result = 1 - resistanceMultiplier = 1 - 0.0004 = 0.9996 最后抗性是99.96%

        // 修改所有抗性计算方式为倍率相乘，最后用1-乘以的伤害的倍率就是最终的抗性，如果有完全减免的也可以支持，而且不会导致出现负数和超过100%的抗性
        [HarmonyPatch(typeof(Character), "GetAbilityResistance", new Type[] { typeof(Identifier) })]
        public class GetAbilityResistanceByIdentifierPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Character __instance, ref float __result, Identifier resistanceId)
            {
                float resistanceMultiplier = 1f; // 乘法初始值为1
                bool hadResistance = false;

                foreach (var (key, value) in __instance.abilityResistances)
                {
                    if (key.ResistanceIdentifier == resistanceId)
                    {
                        resistanceMultiplier *= value; // 使用乘法代替加法
                        hadResistance = true;
                    }
                }

                // NOTE: 抗性在这里是作为乘数处理的，因此 1.0 相当于 0% 的抗性。
                __result = hadResistance ? Math.Max(0, resistanceMultiplier) : 1f;

                // 跳过原方法执行
                return false;
            }
        }

        [HarmonyPatch(typeof(Character), "GetAbilityResistance", new Type[] { typeof(AfflictionPrefab) })]
        public class GetAbilityResistanceByAfflictionPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Character __instance, ref float __result, AfflictionPrefab affliction)
            {
                float resistanceMultiplier = 1f; // 乘法初始值为1
                bool hadResistance = false;

                foreach (var (key, value) in __instance.abilityResistances)
                {
                    if (key.ResistanceIdentifier == affliction.AfflictionType ||
                        key.ResistanceIdentifier == affliction.Identifier)
                    {
                        resistanceMultiplier *= value; // 使用乘法代替加法
                        hadResistance = true;
                    }
                }

                // NOTE: 抗性在这里是作为乘数处理的，因此 1.0 相当于 0% 的抗性。
                __result = hadResistance ? Math.Max(0, resistanceMultiplier) : 1f;

                // 跳过原方法执行
                return false;
            }
        }

        [HarmonyPatch(typeof(CharacterHealth), "GetResistance")]
        public class GetResistancePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(CharacterHealth __instance, ref float __result, AfflictionPrefab afflictionPrefab, LimbType limbType)
            {
                // 获取伤害抗性倍率初始值
                float resistanceMultiplier = __instance.Character.GetAbilityResistance(afflictionPrefab);

                foreach (var kvp in __instance.afflictions)
                {
                    var affliction = kvp.Key;
                    // 使用乘法而不是加法，乘以伤害倍率
                    resistanceMultiplier *= 1.0f - affliction.GetResistance(afflictionPrefab.Identifier, limbType);
                }

                // 返回最终结果
                __result = 1 - resistanceMultiplier;

                // 返回 false 以跳过原始方法的执行
                return false;
            }
        }
    }
}
