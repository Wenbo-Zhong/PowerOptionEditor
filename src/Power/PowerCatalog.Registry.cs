// Copyright (C) 2026 Wenbo Zhong
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, version 3 of the License.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace PowerOptionEditor
{
    static partial class PowerCatalog
    {
        static void ApplySchemeDefaults(Guid scheme, Guid group, Guid setting, SettingRow row)
        {
            if (group == Guid.Empty || setting == Guid.Empty || scheme == Guid.Empty) return;
            string path = string.Format(CultureInfo.InvariantCulture,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\{0:D}\{1:D}\DefaultPowerSchemeValues\{2:D}",
                group, setting, scheme);
            RegistryKey key = Registry.LocalMachine.OpenSubKey(path);
            if (key == null) return;
            using (key)
            {
                object ac = key.GetValue("ACSettingIndex");
                object dc = key.GetValue("DCSettingIndex");
                if (ac is int)
                {
                    row.HasAcDefault = true;
                    row.AcDefault = unchecked((uint)(int)ac);
                }
                if (dc is int)
                {
                    row.HasDcDefault = true;
                    row.DcDefault = unchecked((uint)(int)dc);
                }
            }
        }

        const string PowerRoot = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings";
        const string SchemeRoot = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";

        public struct UnavailableHideResult
        {
            public int Hidden;
            public bool AccessDenied;
        }

        /// <summary>
        /// Settings the power service does not expose, and that have no registry
        /// name, range, choices, defaults, or scheme value, are not on this machine.
        /// Collect candidates without changing power settings.
        /// </summary>
        public static List<string> FindUnavailable(Guid scheme, List<SettingRow> present)
        {
            SettingAvailability availability = LoadAvailability(scheme, present);
            return FindUnavailableSettings(availability);
        }

        sealed class SettingAvailability
        {
            public readonly HashSet<string> Settings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> GroupsWithSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> EnumeratedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SchemeValues;

            public bool HasSetting(Guid group, Guid setting)
            {
                string pair = PairKey(group, setting);
                return Settings.Contains(pair) || SchemeValues.Contains(pair);
            }
        }

        static SettingAvailability LoadAvailability(Guid scheme, List<SettingRow> present)
        {
            SettingAvailability availability = new SettingAvailability();
            if (present != null)
            {
                for (int i = 0; i < present.Count; i++)
                {
                    if (present[i] == null || present[i].Legacy) continue;
                    availability.Settings.Add(PairKey(present[i].Subgroup, present[i].Setting));
                    if (present[i].Subgroup != Guid.Empty)
                        availability.GroupsWithSettings.Add(present[i].Subgroup.ToString("D"));
                }
            }
            availability.SchemeValues = LoadSchemeValueKeys();
            List<Guid> groups = Enumerate(scheme, null, Native.AccessSubgroup);
            for (int i = 0; i < groups.Count; i++)
                availability.EnumeratedGroups.Add(groups[i].ToString("D"));
            return availability;
        }

        static List<string> FindUnavailableSettings(SettingAvailability availability)
        {
            List<string> pending = new List<string>();
            using (RegistryKey readRoot = Registry.LocalMachine.OpenSubKey(PowerRoot))
            {
                if (readRoot == null) return pending;
                string[] groupNames = readRoot.GetSubKeyNames();
                for (int g = 0; g < groupNames.Length; g++)
                {
                    Guid groupId;
                    if (!TryParseGuid(groupNames[g], out groupId)) continue;
                    using (RegistryKey group = readRoot.OpenSubKey(groupNames[g]))
                        if (group != null)
                            CollectUnavailableGroup(group, groupNames[g], groupId, availability, pending);
                }
            }
            return pending;
        }

        static void CollectUnavailableGroup(RegistryKey group, string groupName, Guid groupId,
            SettingAvailability availability, List<string> pending)
        {
            bool childGuid = false;
            bool anyRealChild = false;
            string[] children = group.GetSubKeyNames();
            for (int s = 0; s < children.Length; s++)
            {
                Guid settingId;
                if (!TryParseGuid(children[s], out settingId)) continue;
                childGuid = true;
                if (availability.HasSetting(groupId, settingId))
                {
                    anyRealChild = true;
                    continue;
                }
                using (RegistryKey setting = group.OpenSubKey(children[s]))
                {
                    if (HasRegistryIdentity(setting))
                    {
                        anyRealChild = true;
                        continue;
                    }
                    if (NeedsHiddenAttribute(setting))
                        pending.Add(groupName + "\\" + children[s]);
                }
            }
            bool groupKept = availability.EnumeratedGroups.Contains(groupId.ToString("D"))
                || availability.GroupsWithSettings.Contains(groupId.ToString("D"))
                || availability.HasSetting(Guid.Empty, groupId)
                || anyRealChild
                || (!childGuid && HasRegistryIdentity(group));
            if (!groupKept && NeedsHiddenAttribute(group))
                pending.Add(groupName);
        }

        public static UnavailableHideResult HideUnavailable(List<string> pending)
        {
            UnavailableHideResult result = new UnavailableHideResult();
            if (pending.Count == 0) return result;
            try
            {
                using (RegistryKey writeRoot = Registry.LocalMachine.OpenSubKey(PowerRoot, true))
                {
                    if (writeRoot == null)
                    {
                        result.AccessDenied = true;
                        return result;
                    }
                    for (int i = 0; i < pending.Count; i++)
                        TrySetHidden(writeRoot, pending[i], ref result);
                }
            }
            catch (UnauthorizedAccessException) { result.AccessDenied = true; }
            catch (System.Security.SecurityException) { result.AccessDenied = true; }
            return result;
        }

        static bool NeedsHiddenAttribute(RegistryKey key)
        {
            if (key == null) return false;
            object currentObj = key.GetValue("Attributes");
            if (currentObj != null && !(currentObj is int)) return false;
            uint current = currentObj is int ? unchecked((uint)(int)currentObj) : 0;
            uint updated = (current & ~2u) | Native.HideAttribute;
            return !(currentObj is int) || updated != current;
        }

        static void AddRegistrySettings(List<SettingRow> rows, Guid scheme)
        {
            HashSet<string> schemeValues = LoadSchemeValueKeys();
            RegistryKey root = Registry.LocalMachine.OpenSubKey(PowerRoot);
            if (root == null) return;
            using (root)
            {
                string[] groups = root.GetSubKeyNames();
                for (int g = 0; g < groups.Length; g++)
                {
                    Guid groupId;
                    if (!TryParseGuid(groups[g], out groupId)) continue;
                    RegistryKey groupKey = root.OpenSubKey(groups[g]);
                    if (groupKey == null) continue;
                    using (groupKey)
                    {
                        string groupName = Text(NativeKind.Friendly, null, groupId, null);
                        if (string.IsNullOrEmpty(groupName))
                        {
                            groupName = ExpandResource(groupKey.GetValue("FriendlyName") as string);
                            if (string.IsNullOrEmpty(groupName)) groupName = groupId.ToString("B");
                        }
                        bool childGuid = false;
                        string[] settings = groupKey.GetSubKeyNames();
                        for (int s = 0; s < settings.Length; s++)
                        {
                            Guid settingId;
                            if (!TryParseGuid(settings[s], out settingId)) continue;
                            childGuid = true;
                            RegistryKey settingKey = groupKey.OpenSubKey(settings[s]);
                            if (settingKey == null) continue;
                            using (settingKey)
                                MergeRegistrySetting(rows, scheme, groupId, groupName, settingId, settingKey, schemeValues);
                        }
                        if (!childGuid)
                            MergeRegistrySetting(rows, scheme, Guid.Empty, Ui.T("plan.root"), groupId, groupKey, schemeValues);
                    }
                }
            }
            AddSchemeOnlySettings(rows, scheme);
        }

        static void MergeRegistrySetting(List<SettingRow> rows, Guid scheme, Guid group, string groupName, Guid setting, RegistryKey key, HashSet<string> schemeValues)
        {
            bool inScheme = schemeValues.Contains(PairKey(group, setting));
            SettingRow row = FindRow(rows, group, setting);
            bool added = false;
            if (row == null)
            {
                if (!HasRegistryIdentity(key) && !inScheme) return;
                row = ReadSetting(scheme, group, groupName, setting);
                rows.Add(row);
                added = true;
            }
            OverlayIdentity(key, row);
            ApplyAttributes(key, row);
            ReadRegistryChoices(key, row);
            if (added && !inScheme && !IsMachineSetting(row))
                rows.Remove(row);
        }

        static void AddSchemeOnlySettings(List<SettingRow> rows, Guid scheme)
        {
            using (RegistryKey schemes = Registry.LocalMachine.OpenSubKey(SchemeRoot))
            {
                if (schemes == null) return;
                string[] schemeNames = schemes.GetSubKeyNames();
                for (int i = 0; i < schemeNames.Length; i++)
                {
                    Guid schemeId;
                    if (!TryParseGuid(schemeNames[i], out schemeId) || schemeId == Guid.Empty) continue;
                    using (RegistryKey schemeKey = schemes.OpenSubKey(schemeNames[i]))
                    {
                        if (schemeKey == null) continue;
                        string[] children = schemeKey.GetSubKeyNames();
                        for (int c = 0; c < children.Length; c++)
                        {
                            Guid childId;
                            if (!TryParseGuid(children[c], out childId)) continue;
                            using (RegistryKey child = schemeKey.OpenSubKey(children[c]))
                            {
                                if (child == null) continue;
                                if (HasIndexValue(child))
                                    EnsureListed(rows, scheme, Guid.Empty, Ui.T("plan.root"), childId);
                                string[] settings = child.GetSubKeyNames();
                                for (int s = 0; s < settings.Length; s++)
                                {
                                    Guid settingId;
                                    if (!TryParseGuid(settings[s], out settingId)) continue;
                                    using (RegistryKey settingKey = child.OpenSubKey(settings[s]))
                                    {
                                        if (settingKey != null && HasIndexValue(settingKey))
                                        {
                                            string groupName = Text(NativeKind.Friendly, null, childId, null);
                                            if (string.IsNullOrEmpty(groupName)) groupName = childId.ToString("B");
                                            EnsureListed(rows, scheme, childId, groupName, settingId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static void EnsureListed(List<SettingRow> rows, Guid scheme, Guid group, string groupName, Guid setting)
        {
            if (FindRow(rows, group, setting) != null) return;
            SettingRow row = ReadSetting(scheme, group, groupName, setting);
            using (RegistryKey key = OpenDefinition(group, setting))
            {
                if (key != null)
                {
                    OverlayIdentity(key, row);
                    ApplyAttributes(key, row);
                    ReadRegistryChoices(key, row);
                }
            }
            if (IsMachineSetting(row)) rows.Add(row);
        }

        static RegistryKey OpenDefinition(Guid group, Guid setting)
        {
            string path = group == Guid.Empty
                ? PowerRoot + "\\" + setting.ToString("D")
                : PowerRoot + "\\" + group.ToString("D") + "\\" + setting.ToString("D");
            return Registry.LocalMachine.OpenSubKey(path);
        }

        static SettingRow FindRow(List<SettingRow> rows, Guid group, Guid setting)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                SettingRow row = rows[i];
                if (row.Legacy || row.Setting != setting) continue;
                if (row.Subgroup == group) return row;
            }
            return null;
        }

        static bool IsMachineSetting(SettingRow row)
        {
            if (row.HasAc || row.HasDc || row.HasAcDefault || row.HasDcDefault || row.HasRange || row.Choices.Count > 0)
                return true;
            return !IsGuidName(row.Name);
        }

        static bool IsGuidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            Guid parsed;
            return TryParseGuid(name, out parsed);
        }

        static bool HasRegistryIdentity(RegistryKey key)
        {
            if (key == null) return false;
            if (!string.IsNullOrEmpty(ExpandResource(key.GetValue("FriendlyName") as string))) return true;
            if (!string.IsNullOrEmpty(ExpandResource(key.GetValue("Description") as string))) return true;
            if (key.GetValue("ValueMin") is int || key.GetValue("ValueMax") is int) return true;
            using (RegistryKey defaults = key.OpenSubKey("DefaultPowerSchemeValues"))
                if (defaults != null) return true;
            string[] names = key.GetSubKeyNames();
            for (int i = 0; i < names.Length; i++)
            {
                int ordinal;
                if (int.TryParse(names[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ordinal))
                    return true;
            }
            return false;
        }

        static void OverlayIdentity(RegistryKey key, SettingRow row)
        {
            if (IsGuidName(row.Name))
            {
                string name = ExpandResource(key.GetValue("FriendlyName") as string);
                if (!string.IsNullOrEmpty(name)) row.Name = name;
            }
            if (string.IsNullOrEmpty(row.Description))
            {
                string description = ExpandResource(key.GetValue("Description") as string);
                if (!string.IsNullOrEmpty(description)) row.Description = description;
            }
            if (!row.HasRange)
            {
                object min = key.GetValue("ValueMin");
                object max = key.GetValue("ValueMax");
                if (min is int && max is int)
                {
                    row.HasRange = true;
                    row.Min = unchecked((uint)(int)min);
                    row.Max = unchecked((uint)(int)max);
                    object inc = key.GetValue("ValueIncrement");
                    row.Increment = inc is int && (int)inc > 0 ? unchecked((uint)(int)inc) : 1;
                }
            }
        }

        static void ApplyAttributes(RegistryKey key, SettingRow row)
        {
            object attr = key.GetValue("Attributes");
            if (attr is int)
            {
                row.AttributesKnown = true;
                row.Hidden = (((uint)(int)attr) & Native.HideAttribute) != 0;
            }
        }

        static void ApplySchemeValue(Guid scheme, Guid group, Guid setting, SettingRow row)
        {
            string path = group == Guid.Empty
                ? SchemeRoot + "\\" + scheme.ToString("D") + "\\" + setting.ToString("D")
                : SchemeRoot + "\\" + scheme.ToString("D") + "\\" + group.ToString("D") + "\\" + setting.ToString("D");
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
            {
                if (key == null) return;
                object ac = key.GetValue("ACSettingIndex");
                object dc = key.GetValue("DCSettingIndex");
                if (!row.HasAc && ac is int)
                {
                    row.HasAc = true;
                    row.Ac = unchecked((uint)(int)ac);
                }
                if (!row.HasDc && dc is int)
                {
                    row.HasDc = true;
                    row.Dc = unchecked((uint)(int)dc);
                }
            }
        }

        static bool HasIndexValue(RegistryKey key)
        {
            return key.GetValue("ACSettingIndex") is int || key.GetValue("DCSettingIndex") is int;
        }

        static HashSet<string> LoadSchemeValueKeys()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (RegistryKey schemes = Registry.LocalMachine.OpenSubKey(SchemeRoot))
            {
                if (schemes == null) return set;
                string[] schemeNames = schemes.GetSubKeyNames();
                for (int i = 0; i < schemeNames.Length; i++)
                {
                    Guid schemeId;
                    if (!TryParseGuid(schemeNames[i], out schemeId) || schemeId == Guid.Empty) continue;
                    using (RegistryKey scheme = schemes.OpenSubKey(schemeNames[i]))
                    {
                        if (scheme == null) continue;
                        string[] children = scheme.GetSubKeyNames();
                        for (int c = 0; c < children.Length; c++)
                        {
                            Guid childId;
                            if (!TryParseGuid(children[c], out childId)) continue;
                            using (RegistryKey child = scheme.OpenSubKey(children[c]))
                            {
                                if (child == null) continue;
                                if (HasIndexValue(child)) set.Add(PairKey(Guid.Empty, childId));
                                string[] settings = child.GetSubKeyNames();
                                for (int s = 0; s < settings.Length; s++)
                                {
                                    Guid settingId;
                                    if (!TryParseGuid(settings[s], out settingId)) continue;
                                    using (RegistryKey setting = child.OpenSubKey(settings[s]))
                                        if (setting != null && HasIndexValue(setting))
                                            set.Add(PairKey(childId, settingId));
                                }
                            }
                        }
                    }
                }
            }
            return set;
        }

        static string PairKey(Guid group, Guid setting)
        {
            return group.ToString("D") + "|" + setting.ToString("D");
        }

        static void TrySetHidden(RegistryKey parent, string relativePath, ref UnavailableHideResult result)
        {
            try
            {
                using (RegistryKey key = parent.OpenSubKey(relativePath, true))
                {
                    if (key == null)
                    {
                        result.AccessDenied = true;
                        return;
                    }
                    if (!NeedsHiddenAttribute(key)) return;
                    object currentObj = key.GetValue("Attributes");
                    uint current = currentObj is int ? unchecked((uint)(int)currentObj) : 0;
                    uint updated = (current & ~2u) | Native.HideAttribute;
                    key.SetValue("Attributes", unchecked((int)updated), RegistryValueKind.DWord);
                    result.Hidden++;
                }
            }
            catch (UnauthorizedAccessException) { result.AccessDenied = true; }
            catch (System.Security.SecurityException) { result.AccessDenied = true; }
        }

        static void ReadRegistryChoices(RegistryKey key, SettingRow row)
        {
            string[] names = key.GetSubKeyNames();
            List<ValueChoice> choices = new List<ValueChoice>();
            for (int i = 0; i < names.Length; i++)
            {
                int ordinal;
                if (!int.TryParse(names[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ordinal)) continue;
                RegistryKey sub = key.OpenSubKey(names[i]);
                if (sub == null) continue;
                using (sub)
                {
                    object raw = sub.GetValue("SettingValue");
                    ValueChoice choice = new ValueChoice();
                    choice.Value = raw is int ? unchecked((uint)(int)raw) : (uint)ordinal;
                    choice.Name = ExpandResource(sub.GetValue("FriendlyName") as string);
                    if (string.IsNullOrEmpty(choice.Name))
                        choice.Name = ExpandResource(sub.GetValue("Description") as string);
                    choices.Add(choice);
                }
            }
            if (choices.Count == 0) return;
            choices.Sort(delegate(ValueChoice a, ValueChoice b) { return a.Value.CompareTo(b.Value); });
            row.Choices.Clear();
            for (int i = 0; i < choices.Count; i++) row.Choices.Add(choices[i]);
        }

        static string ExpandResource(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (!text.StartsWith("@", StringComparison.Ordinal)) return text;
            string query = text;
            string fallback = "";
            int semi = text.IndexOf(';');
            if (semi < 0)
            {
                int comma = text.LastIndexOf(',');
                if (comma > 0)
                {
                    string head = text.Substring(0, comma);
                    if (head.Length > 2 && char.IsDigit(head[head.Length - 1]))
                    {
                        query = head;
                        fallback = text.Substring(comma + 1);
                    }
                }
            }
            StringBuilder buffer = new StringBuilder(2048);
            try
            {
                int hr = Native.SHLoadIndirectString(query, buffer, (uint)buffer.Capacity, IntPtr.Zero);
                if (hr == 0 && buffer.Length > 0) return buffer.ToString();
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            if (fallback.Length > 0) return fallback;
            return text;
        }

        static bool TryParseGuid(string text, out Guid guid)
        {
            guid = Guid.Empty;
            if (string.IsNullOrEmpty(text)) return false;
            string formatted = text;
            if (text.Length == 32) formatted = text.Insert(20, "-").Insert(16, "-").Insert(12, "-").Insert(8, "-");
            return Guid.TryParse(formatted, out guid);
        }

    }
}
