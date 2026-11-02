using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HamStartup
{
    /// <summary>Small case-insensitive INI reader/writer used for the app settings.</summary>
    public class IniFile
    {
        private readonly List<string> _sectionOrder = new List<string>();
        private readonly Dictionary<string, List<string>> _keyOrder =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _values =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static IniFile Load(string path)
        {
            IniFile ini = new IniFile();
            string[] lines = File.ReadAllLines(path);
            string section = "";
            ini.EnsureSection(section);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[')
                {
                    int end = line.IndexOf(']');
                    if (end > 0)
                    {
                        section = line.Substring(1, end - 1).Trim();
                        ini.EnsureSection(section);
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                ini.Set(section, key, value);
            }
            return ini;
        }

        private void EnsureSection(string section)
        {
            if (_values.ContainsKey(section)) return;
            _sectionOrder.Add(section);
            _values[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _keyOrder[section] = new List<string>();
        }

        public string Get(string section, string key, string def)
        {
            Dictionary<string, string> sec;
            if (!_values.TryGetValue(section, out sec)) return def;
            string value;
            if (!sec.TryGetValue(key, out value)) return def;
            return value;
        }

        public int GetInt(string section, string key, int def)
        {
            string text = Get(section, key, null);
            int value;
            if (text != null && int.TryParse(text.Trim(), out value)) return value;
            return def;
        }

        public bool GetBool(string section, string key, bool def)
        {
            string text = Get(section, key, null);
            if (text == null) return def;
            text = text.Trim();
            if (text.Equals("1", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("0", StringComparison.OrdinalIgnoreCase)) return false;
            if (text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (text.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            return def;
        }

        public void Set(string section, string key, string value)
        {
            EnsureSection(section);
            Dictionary<string, string> sec = _values[section];
            if (!sec.ContainsKey(key)) _keyOrder[section].Add(key);
            sec[key] = value ?? "";
        }

        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string section in _sectionOrder)
            {
                Dictionary<string, string> sec = _values[section];
                if (sec.Count == 0) continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append('[').Append(section).Append(']').AppendLine();
                foreach (string key in _keyOrder[section])
                {
                    sb.Append(key).Append('=').Append(sec[key]).AppendLine();
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// Edits individual keys inside one INI section while preserving every other line
    /// (JTDX.ini contains hundreds of unrelated settings we must not disturb).
    /// </summary>
    public static class IniSectionEditor
    {
        public static void UpdateKeys(string path, string section, Dictionary<string, string> updates)
        {
            if (updates == null || updates.Count == 0) return;
            string[] lines = File.ReadAllLines(path);
            List<string> output = new List<string>(lines.Length + updates.Count + 4);

            Dictionary<string, string> pending =
                new Dictionary<string, string>(updates, StringComparer.OrdinalIgnoreCase);
            bool sectionFound = false;
            bool inSection = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();

                if (trimmed.StartsWith("[") && trimmed.IndexOf(']') > 0)
                {
                    if (inSection)
                    {
                        AppendMissing(output, pending);
                        inSection = false;
                    }
                    int end = trimmed.IndexOf(']');
                    string name = trimmed.Substring(1, end - 1).Trim();
                    if (name.Equals(section, StringComparison.OrdinalIgnoreCase))
                    {
                        sectionFound = true;
                        inSection = true;
                    }
                    output.Add(raw);
                    continue;
                }

                if (inSection && trimmed.Length > 0 && trimmed[0] != ';' && trimmed[0] != '#')
                {
                    int eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        string key = trimmed.Substring(0, eq).Trim();
                        string replacement;
                        if (pending.TryGetValue(key, out replacement))
                        {
                            output.Add(key + "=" + replacement);
                            pending.Remove(key);
                            continue;
                        }
                    }
                }
                output.Add(raw);
            }

            if (inSection) AppendMissing(output, pending);

            if (!sectionFound)
            {
                if (output.Count > 0 && output[output.Count - 1].Trim().Length > 0) output.Add("");
                output.Add("[" + section + "]");
                foreach (KeyValuePair<string, string> kv in pending)
                {
                    output.Add(kv.Key + "=" + kv.Value);
                }
            }

            File.WriteAllLines(path, output.ToArray(), new UTF8Encoding(false));
        }

        private static void AppendMissing(List<string> output, Dictionary<string, string> pending)
        {
            foreach (KeyValuePair<string, string> kv in pending)
            {
                output.Add(kv.Key + "=" + kv.Value);
            }
            pending.Clear();
        }
    }
}
