using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GlaiveBurnTimeBoost
{
    [HarmonyPatch(typeof(HUDMissileState), "CalcWeaponRange")]
    public static class HudRangePatch
    {
        private static readonly FieldInfo MaxRangeField = AccessTools.Field(typeof(HUDMissileState), "maxRange");
        private static readonly FieldInfo NoEscapeField = AccessTools.Field(typeof(HUDMissileState), "noEscapeRange");
        private static readonly FieldInfo LastCalcField = AccessTools.Field(typeof(HUDMissileState), "lastWeaponRangeCalc");
        private static readonly FieldInfo PrefabField = AccessTools.Field(typeof(HUDMissileState), "prefabMissile");
        private static readonly FieldInfo AircraftField = AccessTools.Field(typeof(HUDMissileState), "aircraft");
        private static readonly FieldInfo StationField = AccessTools.Field(typeof(HUDMissileState), "weaponStation");
        private static readonly FieldInfo FarTargetField = AccessTools.Field(typeof(HUDMissileState), "farTarget");
        private static readonly FieldInfo MaxDistField = AccessTools.Field(typeof(HUDMissileState), "maxTargetDist");
        private static readonly FieldInfo MaxSpeedField = AccessTools.Field(typeof(HUDMissileState), "maxTargetSpeed");
        private static readonly FieldInfo WeaponInfoField = AccessTools.Field(typeof(HUDWeaponState), "weaponInfo");
        private static readonly FieldInfo TargetListField = AccessTools.Field(typeof(HUDWeaponState), "targetList");

        private static readonly HashSet<int> Logged = new HashSet<int>();

        public static void Prefix(HUDMissileState __instance, out bool __state)
        {
            __state = false;
            if (LastCalcField == null || StationField == null || TargetListField == null)
                return;

            float last = (float)LastCalcField.GetValue(__instance);
            if (Time.timeSinceLevelLoad - last < 1f)
                return;

            WeaponStation station = StationField.GetValue(__instance) as WeaponStation;
            List<Unit> targets = TargetListField.GetValue(__instance) as List<Unit>;
            if (station == null || station.Ammo == 0 || targets == null || targets.Count == 0)
                return;

            __state = true;
        }

        public static void Postfix(HUDMissileState __instance, bool __state)
        {
            if (!__state)
                return;
            if (MaxRangeField == null || NoEscapeField == null || PrefabField == null || AircraftField == null || WeaponInfoField == null)
                return;

            WeaponInfo weaponInfo = WeaponInfoField.GetValue(__instance) as WeaponInfo;
            Missile prefab = PrefabField.GetValue(__instance) as Missile;
            Aircraft aircraft = AircraftField.GetValue(__instance) as Aircraft;
            if (weaponInfo == null || prefab == null || aircraft == null)
                return;
            if (!Plugin.ScaledIds.Contains(weaponInfo.GetInstanceID()))
                return;

            float cardMax = weaponInfo.targetRequirements.maxRange;
            if (cardMax < 1f)
                return;

            float ignored;
            float reference = prefab.CalcRange(0f, 0f, 0f, 10000f, 0f, out ignored);
            if (float.IsNaN(reference) || float.IsInfinity(reference) || reference < 100f)
            {
                Plugin.Log.LogWarning(weaponInfo.weaponName + " kinematic reference was unusable (" + reference + "). Leaving the game's HUD number.");
                return;
            }

            float targetAltitude = 0f;
            Unit farTarget = FarTargetField.GetValue(__instance) as Unit;
            if (farTarget != null && aircraft.NetworkHQ != null && aircraft.NetworkHQ.TryGetKnownPosition(farTarget, out GlobalPosition known))
                targetAltitude = known.y;

            float launchAltitude = ((Unit)aircraft).GlobalPosition().y;
            float targetDist = (float)MaxDistField.GetValue(__instance);
            float targetSpeed = (float)MaxSpeedField.GetValue(__instance);
            float noEscape;
            float live = prefab.CalcRange(aircraft.speed, launchAltitude, targetAltitude, targetDist, targetSpeed, out noEscape);
            if (float.IsNaN(live) || float.IsInfinity(live) || live <= 0f)
                return;

            float scale = cardMax / reference;
            MaxRangeField.SetValue(__instance, live * scale);
            NoEscapeField.SetValue(__instance, Mathf.Max(0f, noEscape * scale));

            if (Logged.Add(weaponInfo.GetInstanceID()))
            {
                Plugin.Log.LogWarning(
                    weaponInfo.weaponName + " HUD anchored to card max " + cardMax.ToString("0") + " m " +
                    "(kinematic reference " + reference.ToString("0") + " m, scale x" + scale.ToString("0.00") + "). " +
                    "First shot: launch " + launchAltitude.ToString("0") + " m, target " + targetAltitude.ToString("0") + " m, tape " + (live * scale).ToString("0") + " m.");
            }
        }
    }
}