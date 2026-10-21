using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GlaiveBurnTimeBoost
{
    [BepInPlugin("com.yourname.glaiveburntimeboost", "A2A Range Tuner", "1.9.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static ManualLogSource Log;
        public static readonly HashSet<int> ScaledIds = new HashSet<int>();

        private static readonly string[] LegacyKeys = new string[] { "Glaive percent", "Scythe percent", "Scimitar percent" };
        private static readonly string[] LegacyNames = new string[] { "AAM-21 Glaive", "AAM-29 Scythe", "AAM-36 Scimitar" };

        private readonly Dictionary<string, ConfigEntry<int>> percents = new Dictionary<string, ConfigEntry<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MissileSetting> byName = new Dictionary<string, MissileSetting>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> savedValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> sessionPercent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MissileSetting> missiles = new List<MissileSetting>();
        private readonly HashSet<int> scaledObjects = new HashSet<int>();
        private readonly HashSet<string> skipLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hiddenLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool watchStarted;
        private bool encyclopediaLoaded;
        private float encyclopediaAt = -1f;
        private float awakeAt;
        private float nextScan;
        private int scansDone;

        public bool HasScanned
        {
            get { return scansDone > 0; }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            awakeAt = Time.realtimeSinceStartup;

            LoadSavedRangeValues();
            BindSavedEntries();
            Config.Bind("Missiles", "Percentages", "", new ConfigDescription(
                "100 = original range. Restart the game after changing.",
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = RangeMenu.Draw,
                    HideSettingName = true,
                    HideDefaultButton = true,
                    Category = "Missiles",
                    Order = 1000
                }));

            Harmony harmony = new Harmony("com.yourname.glaiveburntimeboost");
            harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogWarning("A2A Range Tuner 1.9.0 AWAKE");
        }

        private void Update()
        {
            if (!watchStarted)
            {
                if (encyclopediaLoaded || Time.realtimeSinceStartup > awakeAt + 15f)
                {
                    watchStarted = true;
                    nextScan = 0f;
                }
                return;
            }
            if (Time.realtimeSinceStartup < nextScan)
                return;
            nextScan = Time.realtimeSinceStartup + 2f;
            Scan();
        }

        public void OnEncyclopediaLoaded()
        {
            encyclopediaLoaded = true;
            if (encyclopediaAt < 0f)
                encyclopediaAt = Time.realtimeSinceStartup;
            watchStarted = true;
        }

        public void CopyMissiles(string seeker, List<MissileSetting> dest)
        {
            dest.Clear();
            for (int i = 0; i < missiles.Count; i++)
            {
                if (missiles[i].Seeker == seeker)
                    dest.Add(missiles[i]);
            }
            dest.Sort(CompareSettings);
        }

        private static int CompareSettings(MissileSetting a, MissileSetting b)
        {
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private void Scan()
        {
            scansDone++;
            List<MissileOffer> found = MissileCatalog.FindAircraftMissiles();
            HashSet<string> foundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int before = missiles.Count;
            for (int i = 0; i < found.Count; i++)
            {
                foundNames.Add(found[i].Name);
                Track(found[i]);
            }
            if (missiles.Count != before)
                Log.LogInfo("Tracking " + missiles.Count.ToString(CultureInfo.InvariantCulture) + " aircraft missiles.");
            LogSkipped(foundNames);
            LogMissing();
            ApplyTracked();
        }

        private void Track(MissileOffer offer)
        {
            if (offer == null || string.IsNullOrEmpty(offer.Name) || byName.ContainsKey(offer.Name))
                return;
            if (offer.Seeker != "ARH" && offer.Seeker != "SARH" && offer.Seeker != "IR")
                return;

            bool isNew = !savedValues.ContainsKey(offer.Name) && !percents.ContainsKey(offer.Name);
            int starting = 100;
            int saved;
            if (savedValues.TryGetValue(offer.Name, out saved))
                starting = saved;
            ConfigEntry<int> entry = EnsureEntry(offer.Name, starting);

            MissileSetting setting = new MissileSetting();
            setting.Name = offer.Name;
            setting.Seeker = offer.Seeker;
            setting.Entry = entry;
            byName.Add(offer.Name, setting);
            missiles.Add(setting);
            if (isNew)
                Log.LogInfo("Added " + offer.Name + " [" + offer.Seeker + "] at " + entry.Value.ToString(CultureInfo.InvariantCulture) + "%.");
        }

        private void ApplyTracked()
        {
            for (int i = 0; i < missiles.Count; i++)
            {
                MissileSetting setting = missiles[i];
                int percent;
                if (!sessionPercent.TryGetValue(setting.Name, out percent))
                {
                    percent = setting.Entry.Value;
                    sessionPercent[setting.Name] = percent;
                }
                Apply(setting.Name, percent);
            }
        }

        private void Apply(string weaponName, int percent)
        {
            if (percent == 100 || percent <= 0)
                return;
            float mult = percent / 100f;
            WeaponInfo[] infos = Resources.FindObjectsOfTypeAll<WeaponInfo>();
            for (int i = 0; i < infos.Length; i++)
            {
                WeaponInfo weaponInfo = infos[i];
                if (weaponInfo == null || string.IsNullOrEmpty(weaponInfo.weaponName))
                    continue;
                if (!string.Equals(weaponInfo.weaponName.Trim(), weaponName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!scaledObjects.Add(weaponInfo.GetInstanceID()))
                    continue;

                TargetRequirements req = weaponInfo.targetRequirements;
                float oldMax = req.maxRange;
                req.maxRange = oldMax * mult;
                weaponInfo.targetRequirements = req;
                ScaledIds.Add(weaponInfo.GetInstanceID());
                Log.LogWarning(weaponInfo.name + " / " + weaponInfo.weaponName + " maxRange " + oldMax.ToString("0") + " -> " + req.maxRange.ToString("0") + " (x" + mult.ToString("0.00") + ")");

                if (weaponInfo.weaponPrefab == null)
                {
                    Log.LogWarning(weaponName + ": no weapon prefab");
                    continue;
                }
                Missile missile = weaponInfo.weaponPrefab.GetComponent<Missile>();
                if (missile == null)
                {
                    Log.LogWarning(weaponName + ": prefab has no Missile");
                    continue;
                }
                ScaleMotors(missile, mult, weaponName);
            }
        }

        private static void ScaleMotors(Missile missile, float mult, string label)
        {
            FieldInfo motorsField = typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo massField = typeof(Missile).GetField("mass", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (motorsField == null)
            {
                Log.LogWarning(label + ": no motors field");
                return;
            }
            Array motors = motorsField.GetValue(missile) as Array;
            if (motors == null)
            {
                Log.LogWarning(label + ": no motors field");
                return;
            }

            float fuelSum = 0f;
            for (int i = 0; i < motors.Length; i++)
            {
                object motor = motors.GetValue(i);
                if (motor == null)
                    continue;
                Type type = motor.GetType();
                SetMotorField(type, motor, "burnTime", mult, label, i);
                SetMotorField(type, motor, "fuelMass", mult, label, i);
                FieldInfo fuelField = type.GetField("fuelMass", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fuelField != null && fuelField.FieldType == typeof(float))
                    fuelSum += (float)fuelField.GetValue(motor);
                if (type.IsValueType)
                    motors.SetValue(motor, i);
            }

            if (massField != null && massField.FieldType == typeof(float) && fuelSum > 0f)
            {
                float mass = (float)massField.GetValue(missile);
                if (fuelSum > mass * 0.9f && mass > 1f)
                {
                    float grown = fuelSum / 0.75f;
                    massField.SetValue(missile, grown);
                    Log.LogWarning(label + " mass " + mass.ToString("0.0") + " -> " + grown.ToString("0.0") + " so dry mass stays positive (fuel " + fuelSum.ToString("0.0") + ")");
                }
            }
        }

        private static void SetMotorField(Type type, object motor, string fieldName, float mult, string label, int index)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(float))
                return;
            float oldValue = (float)field.GetValue(motor);
            field.SetValue(motor, oldValue * mult);
            Log.LogWarning(label + " motor[" + index + "] " + fieldName + " " + oldValue.ToString("0.00") + " -> " + (oldValue * mult).ToString("0.00"));
        }

        private void LogSkipped(HashSet<string> foundNames)
        {
            WeaponInfo[] infos = Resources.FindObjectsOfTypeAll<WeaponInfo>();
            for (int n = 0; n < infos.Length; n++)
            {
                string skippedSeeker;
                if (infos[n] == null || !MissileCatalog.TryDescribe(infos[n], out skippedSeeker))
                    continue;
                string rawName = infos[n].weaponName;
                string name = string.IsNullOrEmpty(rawName) ? "" : rawName.Trim();
                if (name.Length == 0 || foundNames.Contains(name))
                    continue;
                if (skipLogged.Add(name))
                    Log.LogInfo(name + " [" + skippedSeeker + "] skipped, not carried by an aircraft.");
            }
        }

        private void LogMissing()
        {
            bool settled = Time.realtimeSinceStartup > awakeAt + 30f;
            if (encyclopediaLoaded && encyclopediaAt >= 0f && Time.realtimeSinceStartup > encyclopediaAt + 20f)
                settled = true;
            if (!settled)
                return;
            HashSet<string> present = CollectLoadedWeaponNames();
            List<string> names = new List<string>(savedValues.Keys);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (present.Contains(name) || byName.ContainsKey(name))
                    continue;
                if (hiddenLogged.Add(name))
                    Log.LogInfo(name + " is not loaded, so it is hidden. Its saved percentage is kept.");
            }
        }

        private static HashSet<string> CollectLoadedWeaponNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            WeaponInfo[] infos = Resources.FindObjectsOfTypeAll<WeaponInfo>();
            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i] == null || string.IsNullOrEmpty(infos[i].weaponName))
                    continue;
                string name = infos[i].weaponName.Trim();
                if (name.Length > 0)
                    names.Add(name);
            }
            return names;
        }

        private void BindSavedEntries()
        {
            List<string> names = new List<string>(savedValues.Keys);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                EnsureEntry(name, savedValues[name]);
            }
        }

        private ConfigEntry<int> EnsureEntry(string weaponName, int value)
        {
            ConfigEntry<int> existing;
            if (percents.TryGetValue(weaponName, out existing))
                return existing;
            ConfigEntry<int> entry = Config.Bind("Range", weaponName, value, new ConfigDescription(
                "100 = original range. Restart the game after changing.",
                null,
                new ConfigurationManagerAttributes { Browsable = false }));
            savedValues[weaponName] = entry.Value;
            percents[weaponName] = entry;
            return entry;
        }

        private void LoadSavedRangeValues()
        {
            savedValues.Clear();
            Dictionary<string, int> saved = ReadRangeFile();
            foreach (KeyValuePair<string, int> pair in saved)
                savedValues[pair.Key] = pair.Value;
        }

        private Dictionary<string, int> ReadRangeFile()
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Config.ConfigFilePath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return result;
                string[] lines = File.ReadAllLines(path);
                string section = "";
                for (int i = 0; i < lines.Length; i++)
                {
                    string text = lines[i].Trim();
                    if (text.Length == 0 || text[0] == '#')
                        continue;
                    if (text[0] == '[' && text[text.Length - 1] == ']')
                    {
                        section = text.Substring(1, text.Length - 2).Trim();
                        continue;
                    }
                    if (section != "Range")
                        continue;
                    int eq = text.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string key = Unquote(text.Substring(0, eq).Trim());
                    string raw = Unquote(text.Substring(eq + 1).Trim());
                    int value;
                    if (key.Length == 0 || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        continue;
                    if (IsLegacyKey(key))
                    {
                        string mapped = MapLegacy(key);
                        if (mapped.Length > 0 && !result.ContainsKey(mapped))
                            result[mapped] = value;
                    }
                    else
                        result[key] = value;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("Could not read the range config: " + ex.Message);
            }
            return result;
        }

        private static string Unquote(string text)
        {
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                text = text.Substring(1, text.Length - 2);
                text = text.Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            return text.Trim();
        }

        private static bool IsLegacyKey(string key)
        {
            for (int i = 0; i < LegacyKeys.Length; i++)
            {
                if (string.Equals(LegacyKeys[i], key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string MapLegacy(string key)
        {
            for (int i = 0; i < LegacyKeys.Length; i++)
            {
                if (string.Equals(LegacyKeys[i], key, StringComparison.Ordinal))
                    return LegacyNames[i];
            }
            return "";
        }
    }

    public class MissileSetting
    {
        public string Name;
        public string Seeker;
        public ConfigEntry<int> Entry;
    }

    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[] { })]
    public static class EncyclopediaLoadedPatch
    {
        public static void Postfix()
        {
            if (Plugin.Instance != null)
                Plugin.Instance.OnEncyclopediaLoaded();
        }
    }

    // Property names are what BepInEx Configuration Manager looks for. No extra reference is required.
    public class ConfigurationManagerAttributes
    {
        public bool? Browsable;
        public bool? HideSettingName;
        public bool? HideDefaultButton;
        public string Category;
        public string DispName;
        public int? Order;
        public Action<ConfigEntryBase> CustomDrawer;
    }
}