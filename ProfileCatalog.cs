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

        // IDs and priorities are compatibility fallbacks; values always come from this computer.
        public static List<ProfileItem> LoadProfiles()
        {
            string[] ids = { "0da965dc-8fcf-4c0b-8efe-8dd5e7bc959a", "0aabb002-a307-447e-9b81-1d819df6c6d0", "d4140c81-ebba-4e60-8561-6918290359cd", "4569e601-272e-4869-bcab-1c6c03d7966f", "8bc6262c-c026-411d-ae3b-7e2f70811a13", "2e92e666-c3f6-42c3-89bd-94d40fabcde5", "ee1e4f72-e368-46b1-b3c6-5048b11c2dbd", "b025fe59-79f4-4e38-b142-bcc62c7ecc32", "33cc3a0d-45ee-43ca-86c4-695bfc9a313b", "336c7511-f109-4172-bb3a-3ea51f815ada", "0c3d5326-944b-4aab-8ad8-fe422a0e50e0", "a4a61b5f-f42c-4d23-b3ab-5c27df9f0f18", "c04a802d-2205-4910-ae98-3b51e3bb72f2" };
            string[] names = { Ui.T("低延迟", "低延遲", "Low latency"), Ui.T("持续性能", "持續效能", "Sustained performance"), Ui.T("游戏", "遊戲", "Game mode"), Ui.T("低功耗", "低功耗", "Low power"), Ui.T("待机", "待命", "Standby"), Ui.T("关屏", "關閉螢幕", "Screen off"), Ui.T("受限", "受限", "Constrained"), Ui.T("高耗电", "高耗電", "High drain"), Ui.T("实用程序", "公用程式", "Utility QoS"), Ui.T("效率模式", "效率模式", "Eco QoS"), Ui.T("多媒体", "多媒體", "Multimedia QoS"), Ui.T("入门性能", "入門效能", "Entry level performance"), Ui.T("后台", "背景", "Background") };
            int[] priorities = { 40, 35, 32, 30, 20, 19, 10, 5, 4, 3, 2, 1, 0 };
            List<ProfileItem> result = new List<ProfileItem>();
            for (int i = 0; i < ids.Length; i++)
                result.Add(new ProfileItem { Guid = new Guid(ids[i]), Name = names[i], Priority = priorities[i] });
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
            result.Sort(delegate(ProfileItem a, ProfileItem b) { return b.Priority.CompareTo(a.Priority); });
            return result;
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

        public static List<SchemeItem> LoadSchemes(ProfileItem profile, SchemeItem daily)
        {
            List<SchemeItem> result = new List<SchemeItem>();
            List<SchemeItem> known = FixedCatalog.LoadSchemes();
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
                            name = Ui.T("最佳能效（覆盖层）", "最佳能源效率（覆蓋層）", "Best power efficiency (overlay)");
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
