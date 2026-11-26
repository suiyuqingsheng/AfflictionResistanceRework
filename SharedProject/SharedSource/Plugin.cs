using System.Collections.Generic;
using System.Reflection;
using Barotrauma;
using Barotrauma.Plugins;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace AfflictionResistanceRework;

public partial class Plugin : IBarotraumaPlugin
{
    public static readonly IDebugConsole DebugConsole = PluginServiceProvider.GetService<IDebugConsole>();

    private const string harmony_id = "AfflictionResistanceRework";
    private Harmony? harmonyInstance = null;

    public void Init()
    {
        harmonyInstance = new Harmony(harmony_id);
        harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        DebugConsole.NewMessage("AfflictionResistanceRework loaded!", Color.Lime);
        InitProjectSpecific();
    }

    public partial void InitProjectSpecific();

    public void Dispose()
    {
        harmonyInstance?.UnpatchSelf();
        harmonyInstance = null;
        DebugConsole.NewMessage("AfflictionResistanceRework disposed!", Color.Lime);
    }

    public void OnContentLoaded()
    {
    }

    // 统一所有抗性计算方式为伤害倍率相乘，最后用1-最后的伤害的倍率就是最终的抗性，在实际使用抗性的地方都是1-抗性，所以最后就是倍率
    // 如果有完全减免的也可以支持，而且不会导致出现负数和超过100%的抗性
    [HarmonyPatch(typeof(CharacterHealth), "GetResistance")]
    public class GetResistancePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CharacterHealth __instance, ref float __result, AfflictionPrefab afflictionPrefab, LimbType limbType)
        {
            // 获取伤害抗性倍率初始值
            float resistanceMultiplier = __instance.Character.GetAbilityResistance(afflictionPrefab);

            foreach (var affliction in __instance.GetAllAfflictions())
            {
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