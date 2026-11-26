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
using System.Runtime.InteropServices;

namespace PowerOptionEditor
{
    static partial class PowerCatalog
    {
        public static bool Modern;

        public static List<SchemeItem> LoadSchemes()
        {
            Modern = Native.ModernAvailable();
            if (!Modern) return LegacyCatalog.LoadSchemes();
            List<SchemeItem> list = new List<SchemeItem>();
            Guid active = Guid.Empty;
            bool hasActive = false;
            IntPtr activePtr;
            if (Native.PowerGetActiveScheme(IntPtr.Zero, out activePtr) == Native.Ok && activePtr != IntPtr.Zero)
            {
                active = (Guid)Marshal.PtrToStructure(activePtr, typeof(Guid));
                Native.LocalFree(activePtr);
                hasActive = true;
            }
            List<Guid> guids = Enumerate(null, null, Native.AccessScheme);
            for (int i = 0; i < guids.Count; i++)
            {
                SchemeItem item = new SchemeItem();
                item.Guid = guids[i];
                item.Name = Text(NativeKind.Friendly, guids[i], null, null);
                if (string.IsNullOrEmpty(item.Name)) item.Name = guids[i].ToString("B");
                item.Description = Text(NativeKind.Description, guids[i], null, null);
                item.Active = hasActive && guids[i] == active;
                list.Add(item);
            }
            return list;
        }

        public static List<SettingRow> LoadSettings(SchemeItem scheme)
        {
            if (scheme == null) return new List<SettingRow>();
            if (scheme.Legacy) return LegacyCatalog.LoadSettings(scheme.LegacyIndex);
            List<SettingRow> rows = new List<SettingRow>();
            List<Guid> groups = Enumerate(scheme.Guid, null, Native.AccessSubgroup);
            groups.Insert(0, Guid.Empty);
            for (int g = 0; g < groups.Count; g++)
            {
                Guid group = groups[g];
                Guid? groupArg = group == Guid.Empty ? (Guid?)null : group;
                string groupName = group == Guid.Empty ? Ui.T("plan.root") : Text(NativeKind.Friendly, null, group, null);
                if (string.IsNullOrEmpty(groupName)) groupName = group.ToString("B");
                List<Guid> settings = Enumerate(scheme.Guid, groupArg, Native.AccessSetting);
                for (int s = 0; s < settings.Count; s++)
                    rows.Add(ReadSetting(scheme.Guid, group, groupName, settings[s]));
            }
            AddRegistrySettings(rows, scheme.Guid);
            return rows;
        }

        public static uint WriteIndex(SchemeItem scheme, SettingRow row, bool ac, uint value)
        {
            if (row.Profile != Guid.Empty) return ProfileCatalog.WriteIndex(scheme, row, ac, value);
            if (scheme.Legacy) return LegacyCatalog.WriteIndex(scheme, row, ac, value);
            using (GuidAlloc schemePtr = new GuidAlloc(scheme.Guid))
            using (GuidAlloc groupPtr = new GuidAlloc(row.Subgroup == Guid.Empty ? (Guid?)null : row.Subgroup))
            using (GuidAlloc settingPtr = new GuidAlloc(row.Setting))
            {
                if (ac) return Native.PowerWriteACValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, value);
                return Native.PowerWriteDCValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, value);
            }
        }

        public static bool TryReadIndex(SchemeItem scheme, SettingRow row, bool ac, out uint value)
        {
            value = 0;
            if (scheme == null || row == null || scheme.Legacy || row.Legacy) return false;
            using (GuidAlloc schemePtr = new GuidAlloc(scheme.Guid))
            using (GuidAlloc groupPtr = new GuidAlloc(row.Subgroup == Guid.Empty ? (Guid?)null : row.Subgroup))
            using (GuidAlloc settingPtr = new GuidAlloc(row.Setting))
            {
                uint read;
                uint rc = ac
                    ? Native.PowerReadACValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, out read)
                    : Native.PowerReadDCValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, out read);
                if (rc != Native.Ok) return false;
                value = read;
                return true;
            }
        }

        public static uint WriteHidden(SettingRow row, bool hidden)
        {
            using (GuidAlloc groupPtr = new GuidAlloc(row.Subgroup == Guid.Empty ? (Guid?)null : row.Subgroup))
            using (GuidAlloc settingPtr = new GuidAlloc(row.Setting))
            {
                uint attributes = Native.PowerReadSettingAttributes(groupPtr.Ptr, settingPtr.Ptr);
                if (hidden) attributes |= Native.HideAttribute;
                else attributes &= ~Native.HideAttribute;
                uint rc = Native.PowerWriteSettingAttributes(groupPtr.Ptr, settingPtr.Ptr, attributes);
                if (rc == Native.Ok)
                {
                    row.Hidden = hidden;
                    row.AttributesKnown = true;
                }
                return rc;
            }
        }

        public static uint Activate(SchemeItem scheme)
        {
            if (scheme.Legacy) return LegacyCatalog.Activate(scheme);
            using (GuidAlloc schemePtr = new GuidAlloc(scheme.Guid))
                return Native.PowerSetActiveScheme(IntPtr.Zero, schemePtr.Ptr);
        }

        static SettingRow ReadSetting(Guid scheme, Guid group, string groupName, Guid setting)
        {
            SettingRow row = new SettingRow();
            row.Subgroup = group;
            row.Setting = setting;
            row.GroupName = groupName;
            Guid? groupArg = group == Guid.Empty ? (Guid?)null : group;
            row.Name = Text(NativeKind.Friendly, null, groupArg, setting);
            if (string.IsNullOrEmpty(row.Name)) row.Name = setting.ToString("B");
            row.Description = Text(NativeKind.Description, null, groupArg, setting);
            row.Units = Text(NativeKind.Units, null, groupArg, setting);
            using (GuidAlloc schemePtr = new GuidAlloc(scheme))
            using (GuidAlloc groupPtr = new GuidAlloc(groupArg))
            using (GuidAlloc settingPtr = new GuidAlloc(setting))
            {
                ReadSettingValues(scheme, row, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr);
                ReadRangeAndAttributes(row, groupPtr.Ptr, settingPtr.Ptr);
                ReadChoices(row, groupPtr.Ptr, settingPtr.Ptr);
            }
            if (!row.HasAc || !row.HasDc)
                ApplySchemeValue(scheme, group, setting, row);
            return row;
        }

        static void ReadSettingValues(Guid scheme, SettingRow row, IntPtr schemePtr, IntPtr groupPtr, IntPtr settingPtr)
        {
            uint value;
            if (Native.PowerReadACValueIndex(IntPtr.Zero, schemePtr, groupPtr, settingPtr, out value) == Native.Ok)
            { row.HasAc = true; row.Ac = value; }
            if (Native.PowerReadDCValueIndex(IntPtr.Zero, schemePtr, groupPtr, settingPtr, out value) == Native.Ok)
            { row.HasDc = true; row.Dc = value; }
            if (Native.PowerReadACDefaultIndex(IntPtr.Zero, groupPtr, settingPtr, out value) == Native.Ok)
            { row.HasAcDefault = true; row.AcDefault = value; }
            if (Native.PowerReadDCDefaultIndex(IntPtr.Zero, groupPtr, settingPtr, out value) == Native.Ok)
            { row.HasDcDefault = true; row.DcDefault = value; }
            ApplySchemeDefaults(scheme, row.Subgroup, row.Setting, row);
        }

        static void ReadRangeAndAttributes(SettingRow row, IntPtr groupPtr, IntPtr settingPtr)
        {
            uint min, max, inc;
            if (Native.PowerReadValueMin(IntPtr.Zero, groupPtr, settingPtr, out min) == Native.Ok
                && Native.PowerReadValueMax(IntPtr.Zero, groupPtr, settingPtr, out max) == Native.Ok)
            {
                row.HasRange = true;
                row.Min = min;
                row.Max = max;
                row.Increment = Native.PowerReadValueIncrement(IntPtr.Zero, groupPtr, settingPtr, out inc) == Native.Ok ? inc : 1;
            }
            uint attributes = Native.PowerReadSettingAttributes(groupPtr, settingPtr);
            row.AttributesKnown = true;
            row.Hidden = (attributes & Native.HideAttribute) != 0;
        }

        static void ReadChoices(SettingRow row, IntPtr groupPtr, IntPtr settingPtr)
        {
            uint index = 0;
            while (index < 512)
            {
                uint size = 4;
                IntPtr buf = Marshal.AllocHGlobal(4);
                try
                {
                    uint rc = Native.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, groupPtr, Native.AccessPossible, index, buf, ref size);
                    if (rc != Native.Ok) break;
                    ValueChoice choice = new ValueChoice();
                    choice.Value = (uint)Marshal.ReadInt32(buf);
                    choice.Name = PossibleName(groupPtr, settingPtr, choice.Value);
                    if (string.IsNullOrEmpty(choice.Name))
                        choice.Name = PossibleName(groupPtr, settingPtr, index);
                    row.Choices.Add(choice);
                }
                finally { Marshal.FreeHGlobal(buf); }
                index++;
            }
        }

        static string PossibleName(IntPtr group, IntPtr setting, uint index)
        {
            uint size = 0;
            uint rc = Native.PowerReadPossibleFriendlyName(IntPtr.Zero, group, setting, index, IntPtr.Zero, ref size);
            if (size < 2 || (rc != Native.Ok && rc != Native.MoreData)) return "";
            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                rc = Native.PowerReadPossibleFriendlyName(IntPtr.Zero, group, setting, index, buf, ref size);
                if (rc != Native.Ok) return "";
                string text = Marshal.PtrToStringUni(buf);
                if (string.IsNullOrEmpty(text)) return "";
                return text.Trim().TrimEnd('\0');
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        enum NativeKind { Friendly, Description, Units }

        static string Text(NativeKind kind, Guid? scheme, Guid? group, Guid? setting)
        {
            using (GuidAlloc schemePtr = new GuidAlloc(scheme))
            using (GuidAlloc groupPtr = new GuidAlloc(group))
            using (GuidAlloc settingPtr = new GuidAlloc(setting))
            {
                uint size = 0;
                uint rc = TextRaw(kind, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, IntPtr.Zero, ref size);
                if (size < 2 || (rc != Native.Ok && rc != Native.MoreData)) return "";
                IntPtr buf = Marshal.AllocHGlobal((int)size);
                try
                {
                    rc = TextRaw(kind, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, buf, ref size);
                    if (rc != Native.Ok) return "";
                    string text = Marshal.PtrToStringUni(buf);
                    if (string.IsNullOrEmpty(text)) return "";
                    return text.Trim().TrimEnd('\0');
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }

        static uint TextRaw(NativeKind kind, IntPtr scheme, IntPtr group, IntPtr setting, IntPtr buffer, ref uint size)
        {
            if (kind == NativeKind.Friendly)
                return Native.PowerReadFriendlyName(IntPtr.Zero, scheme, group, setting, buffer, ref size);
            if (kind == NativeKind.Description)
                return Native.PowerReadDescription(IntPtr.Zero, scheme, group, setting, buffer, ref size);
            return Native.PowerReadValueUnitsSpecifier(IntPtr.Zero, group, setting, buffer, ref size);
        }

        static List<Guid> Enumerate(Guid? scheme, Guid? subgroup, uint access)
        {
            List<Guid> list = new List<Guid>();
            using (GuidAlloc schemePtr = new GuidAlloc(scheme))
            using (GuidAlloc subPtr = new GuidAlloc(subgroup))
            {
                uint index = 0;
                while (index < 4096)
                {
                    uint size = 16;
                    IntPtr buf = Marshal.AllocHGlobal(16);
                    try
                    {
                        uint rc = Native.PowerEnumerate(IntPtr.Zero, schemePtr.Ptr, subPtr.Ptr, access, index, buf, ref size);
                        if (rc != Native.Ok) break;
                        list.Add((Guid)Marshal.PtrToStructure(buf, typeof(Guid)));
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                    index++;
                }
            }
            return list;
        }

    }
}
