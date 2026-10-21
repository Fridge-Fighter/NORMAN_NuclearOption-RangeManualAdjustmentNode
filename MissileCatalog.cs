using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GlaiveBurnTimeBoost
{
    public class MissileOffer
    {
        public string Name;
        public string Seeker;
        public bool Checked;
    }

    public static class MissileCatalog
    {
        public static List<MissileOffer> FindAircraftMissiles()
        {
            HashSet<string> carried = CollectAircraftWeaponNames();
            Dictionary<string, MissileOffer> found = new Dictionary<string, MissileOffer>(StringComparer.OrdinalIgnoreCase);
            WeaponInfo[] infos = Resources.FindObjectsOfTypeAll<WeaponInfo>();
            for (int i = 0; i < infos.Length; i++)
            {
                WeaponInfo info = infos[i];
                string seeker;
                if (!TryDescribe(info, out seeker))
                    continue;
                string name = info.weaponName.Trim();
                if (name.Length == 0 || !carried.Contains(name))
                    continue;
                if (!found.ContainsKey(name))
                {
                    MissileOffer offer = new MissileOffer();
                    offer.Name = name;
                    offer.Seeker = seeker;
                    offer.Checked = true;
                    found.Add(name, offer);
                }
            }

            List<MissileOffer> list = new List<MissileOffer>(found.Values);
            list.Sort(CompareNames);
            return list;
        }

        private static int CompareNames(MissileOffer a, MissileOffer b)
        {
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static HashSet<string> CollectAircraftWeaponNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            WeaponManager[] managers = Resources.FindObjectsOfTypeAll<WeaponManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                WeaponManager manager = managers[i];
                if (manager == null || manager.hardpointSets == null)
                    continue;
                if (!IsOnAircraft(manager))
                    continue;
                for (int s = 0; s < manager.hardpointSets.Length; s++)
                {
                    HardpointSet set = manager.hardpointSets[s];
                    if (set == null || set.weaponOptions == null)
                        continue;
                    for (int w = 0; w < set.weaponOptions.Count; w++)
                    {
                        WeaponMount mount = set.weaponOptions[w];
                        if (mount == null || mount.info == null || string.IsNullOrEmpty(mount.info.weaponName))
                            continue;
                        names.Add(mount.info.weaponName.Trim());
                    }
                }
            }
            return names;
        }

        private static FieldInfo aircraftField;

        private static bool IsOnAircraft(WeaponManager manager)
        {
            if (manager == null)
                return false;
            if (aircraftField == null)
                aircraftField = typeof(WeaponManager).GetField("aircraft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (aircraftField != null)
            {
                UnityEngine.Object aircraft = aircraftField.GetValue(manager) as UnityEngine.Object;
                if (aircraft != null)
                    return true;
            }
            Transform transform = manager.transform;
            while (transform != null)
            {
                if (transform.GetComponent<Aircraft>() != null)
                    return true;
                transform = transform.parent;
            }
            return false;
        }

        public static bool TryDescribe(WeaponInfo info, out string seeker)
        {
            seeker = null;
            if (info == null || !info.missile || info.weaponPrefab == null)
                return false;
            if (string.IsNullOrEmpty(info.weaponName))
                return false;

            RoleIdentity role = info.effectiveness;
            if (role.antiAir <= 0f || role.antiAir < role.antiSurface || role.antiAir < role.antiRadar)
                return false;

            MissileSeeker[] seekers = info.weaponPrefab.GetComponentsInChildren<MissileSeeker>(true);
            if (seekers == null)
                return false;
            string infrared = null;
            for (int i = 0; i < seekers.Length; i++)
            {
                if (seekers[i] == null)
                    continue;
                string type = seekers[i].GetSeekerType();
                if (type == "ARH" || type == "SARH")
                {
                    seeker = type;
                    return true;
                }
                if (type == "IR")
                    infrared = type;
            }
            if (infrared != null)
            {
                seeker = infrared;
                return true;
            }
            return false;
        }

        public static bool IsRadar(string seeker)
        {
            return seeker == "ARH" || seeker == "SARH";
        }
    }
}