using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace PowerOptionEditor
{
    sealed class ProfileItem
    {
        public Guid Guid;
        public string Name;
        public int Priority;
        public override string ToString() { return Name + " (" + Priority.ToString(CultureInfo.InvariantCulture) + ")"; }
    }

    static class ProfileCatalog
    {
        public static readonly Guid Processor = new Guid("54533251-82be-4824-96c1-47b60b740d00");
        const string Root = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\";

        public static List<ProfileItem> LoadProfiles()
        {
            List<ProfileItem> result = ProfileDefinitions.CreateProfiles();
            ApplyLocalPriorities(result);
            result.Sort(delegate(ProfileItem a, ProfileItem b) { return b.Priority.CompareTo(a.Priority); });
            return result;
        }

        static void ApplyLocalPriorities(List<ProfileItem> result)
        {
            using (RegistryKey events = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\Profile\Events\" + Processor.ToString("B")))
            {
                if (events != null) foreach (string child in events.GetSubKeyNames())
                {
                    Guid id;
                    if (!Guid.TryParse(child, out id)) continue;
                    ProfileItem item = result.Find(delegate(ProfileItem p) { return p.Guid == id; });
                    using (RegistryKey key = events.OpenSubKey(child))
                    {
                        if (key == null) continue;
                        if (item == null)
                        {
                            item = new ProfileItem { Guid = id, Name = key.GetValue("Name") as string ?? child };
                            result.Add(item);
                        }
                        object priority = key.GetValue("Pri");
                        if (priority is int) item.Priority = (int)priority;
                    }
                }
            }
        }

        public static bool HasAdjustableTargets(ProfileItem profile)
        {
            using (RegistryKey group = Registry.LocalMachine.OpenSubKey(Root + Processor.ToString("D")))
            {
                if (group == null) return false;
                foreach (string setting in group.GetSubKeyNames())
                {
                    Guid id;
                    if (!Guid.TryParse(setting, out id)) continue;
                    using (RegistryKey key = group.OpenSubKey(setting + "\\" + profile.Guid.ToString("D")))
                    {
                        if (key == null) continue;
                        foreach (string scheme in key.GetSubKeyNames())
                        {
                            if (!Guid.TryParse(scheme, out id)) continue;
                            using (RegistryKey values = key.OpenSubKey(scheme))
                            {
                                if (values != null && (values.GetValue("ProvAcSettingIndex") is int
                                    || values.GetValue("ProvDcSettingIndex") is int)) return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static string Path(SettingRow row, Guid profile, Guid scheme)
        {
            return Root + Processor.ToString("D") + "\\" + row.Setting.ToString("D") + "\\" + profile.ToString("D") + "\\" + scheme.ToString("D");
        }

        public static List<SchemeItem> LoadSchemes(ProfileItem profile, SchemeItem daily, List<SchemeItem> known)
        {
            List<SchemeItem> result = new List<SchemeItem>();
            using (RegistryKey group = Registry.LocalMachine.OpenSubKey(Root + Processor.ToString("D")))
            {
                if (group != null) foreach (string setting in group.GetSubKeyNames())
                using (RegistryKey key = group.OpenSubKey(setting + "\\" + profile.Guid.ToString("D")))
                {
                    if (key == null) continue;
                    foreach (string child in key.GetSubKeyNames())
                    {
                        Guid id;
                        if (!Guid.TryParse(child, out id) || result.Exists(delegate(SchemeItem s) { return s.Guid == id; })) continue;
                        SchemeItem item = known.Find(delegate(SchemeItem s) { return s.Guid == id; });
                        string name = item == null ? child : item.Name;
                        if (id == new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a"))
                            name = Ui.T("best.power.efficiency.overlay");
                        result.Add(new SchemeItem { Guid = id, Name = name });
                    }
                }
            }
            result.Sort(delegate(SchemeItem a, SchemeItem b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCulture); });
            if (result.Count == 0 && daily != null) result.Add(new SchemeItem { Guid = daily.Guid, Name = daily.Name });
            return result;
        }

        public static List<SettingRow> LoadSettings(ProfileItem profile, SchemeItem scheme, List<SettingRow> templates)
        {
            List<SettingRow> rows = new List<SettingRow>();
            foreach (SettingRow template in templates) rows.Add(template.CopyForProfile());
            foreach (SettingRow row in rows)
            {
                row.Profile = profile.Guid;
                row.HasAc = row.HasDc = row.HasAcDefault = row.HasDcDefault = false;
                row.Ac = row.Dc = 0;
                if (scheme == null) continue;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(Path(row, profile.Guid, scheme.Guid)))
                {
                    if (key == null) continue;
                    object ac = key.GetValue("ProvAcSettingIndex"), dc = key.GetValue("ProvDcSettingIndex");
                    row.HasAc = ac is int;
                    row.HasDc = dc is int;
                    if (row.HasAc) row.Ac = unchecked((uint)(int)ac);
                    if (row.HasDc) row.Dc = unchecked((uint)(int)dc);
                }
            }
            return rows;
        }

        public static uint WriteIndex(SchemeItem scheme, SettingRow row, bool ac, uint value)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(Path(row, row.Profile, scheme.Guid), true))
                {
                    string field = ac ? "ProvAcSettingIndex" : "ProvDcSettingIndex";
                    if (key == null || key.GetValueKind(field) != RegistryValueKind.DWord) return 2;
                    key.SetValue(field, unchecked((int)value), RegistryValueKind.DWord);
                    return Native.Ok;
                }
            }
            catch (UnauthorizedAccessException) { return Native.AccessDenied; }
            catch (System.Security.SecurityException) { return Native.AccessDenied; }
            catch (System.IO.IOException) { return 2; }
        }
    }
}
