using System.Reflection;
using UnityModManagerNet;
using Harmony12;
using UnityEngine;
using System.Collections.Generic;

namespace FanService
{
    public class FanServiceMod
    {
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // Something
            return true; // If false the mod will show an error.
        }
    }
    
    [HarmonyPatch(typeof(ShunterLocoSimulation), "SimulateEngineTemp")]
    class ShunterLocoSimulation_SimulateEngineTemp_Patch
    {
        const float IdleTemp = 52f;

        static bool Prefix(ShunterLocoSimulation __instance, float delta)
        {
            if (__instance.engineOn)
            {
                if (__instance.engineRPM.value > 0f)
                {
                    __instance.engineTemp.AddNextValue(__instance.engineRPM.value * 12f * delta);
                }
                if (__instance.engineTemp.value < IdleTemp)
                {
                    __instance.engineTemp.AddNextValue(5f * delta);
                }
            }
            if (__instance.engineTemp.value <= __instance.engineTemp.min)
            {
                return false;
            }
            __instance.engineTemp.AddNextValue(-3f * delta);

            // Here are the changes:
            // If the fan is on, it's like the speed is at least 30 km/h
            float speed = 0f;
            bool fanOn = LocoControllerShunter_Awake_Patch.dict[__instance].GetFan();
            if (fanOn)
            {
                speed = 30f;
            }

            if (__instance.goingForward && __instance.engineTemp.value > IdleTemp)
            {
                speed = Mathf.Max(speed, __instance.speed.value);
            }
            if (speed > 0f)
            {
                __instance.engineTemp.AddNextValue(-1f * (speed / __instance.speed.max) * 4f * delta);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(LocoControllerShunter), "Awake")]
    class LocoControllerShunter_Awake_Patch
    {
        public static Dictionary<ShunterLocoSimulation, LocoControllerShunter> dict = new Dictionary<ShunterLocoSimulation, LocoControllerShunter>();

        static void Postfix(LocoControllerShunter __instance)
        {
            ShunterLocoSimulation sim = (ShunterLocoSimulation) Helper.Get(__instance, "sim");
            dict.Add(sim, __instance);
        }
    }

    [HarmonyPatch(typeof(LocoControllerBase), "OnDestroy")]
    class LocoControllerBase_OnDestroy_Patch
    {
        static void Postfix(LocoControllerBase __instance)
        {
            if (__instance is LocoControllerShunter)
            {
                ShunterLocoSimulation sim = (ShunterLocoSimulation)Helper.Get(__instance, "sim");
                LocoControllerShunter_Awake_Patch.dict.Remove(sim);
            }
        }
    }

    static class Helper
    {
        public static object Get<T>(T instance, string name)
        {
            return typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        }
    }
}
