using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace GlaiveBurnTimeBoost
{
    public static class RangeMenu
    {
        private static readonly List<MissileSetting> buffer = new List<MissileSetting>();
        private static readonly Dictionary<string, string> drafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool openArh;
        private static bool openSarh;
        private static bool openIr;

        public static void Draw(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("100 = original range. A change takes effect after you restart the game.");
            GUILayout.Space(6f);
            DrawSeeker("ARH", "Active radar", ref openArh);
            GUILayout.Space(4f);
            DrawSeeker("SARH", "Semi-active radar", ref openSarh);
            GUILayout.Space(4f);
            DrawSeeker("IR", "Infrared", ref openIr);
            GUILayout.EndVertical();
        }

        private static void DrawSeeker(string seeker, string tip, ref bool open)
        {
            Plugin plugin = Plugin.Instance;
            int count = 0;
            if (plugin != null && plugin.HasScanned)
            {
                plugin.CopyMissiles(seeker, buffer);
                count = buffer.Count;
            }
            else
                buffer.Clear();

            string arrow = open ? "v" : ">";
            string title = arrow + "   " + seeker + "    (" + count.ToString(CultureInfo.InvariantCulture) + ")";
            if (GUILayout.Button(new GUIContent(title, tip), GUILayout.Height(26f), GUILayout.ExpandWidth(true)))
                open = !open;
            if (!open)
                return;

            if (plugin == null || !plugin.HasScanned)
            {
                GUILayout.Label("    Searching...");
                return;
            }
            if (buffer.Count == 0)
            {
                GUILayout.Label("    None loaded.");
                return;
            }
            for (int i = 0; i < buffer.Count; i++)
                DrawPercent(buffer[i]);
        }

        private static void DrawPercent(MissileSetting setting)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            GUILayout.Label(setting.Name, GUILayout.ExpandWidth(true));
            string current;
            if (!drafts.TryGetValue(setting.Name, out current))
                current = setting.Entry.Value.ToString(CultureInfo.InvariantCulture);
            string edited = GUILayout.TextField(current, GUILayout.Width(52f));
            if (!string.Equals(edited, current, StringComparison.Ordinal))
            {
                drafts[setting.Name] = edited;
                int parsed;
                string trimmed = edited.Trim();
                if (trimmed.Length > 0 && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed != setting.Entry.Value)
                    setting.Entry.Value = parsed;
            }
            GUILayout.Label("%", GUILayout.Width(18f));
            GUILayout.EndHorizontal();
        }
    }
}