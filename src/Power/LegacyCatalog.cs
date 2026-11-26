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
using System.Runtime.InteropServices;

namespace PowerOptionEditor
{
    static class LegacyCatalog
    {
        public static List<SchemeItem> LoadSchemes()
        {
            List<SchemeItem> list = new List<SchemeItem>();
            uint active;
            bool hasActive = Native.GetActivePwrScheme(out active);
            Native.EnumSchemesProc proc = delegate(uint index, uint nameSize, IntPtr name, uint descSize, IntPtr description, IntPtr policy, IntPtr param)
            {
                SchemeItem item = new SchemeItem();
                item.Legacy = true;
                item.LegacyIndex = index;
                item.Name = Marshal.PtrToStringUni(name);
                if (string.IsNullOrEmpty(item.Name)) item.Name = Ui.T("scheme") + index.ToString(CultureInfo.InvariantCulture);
                item.Description = Marshal.PtrToStringUni(description) ?? "";
                item.Active = hasActive && index == active;
                list.Add(item);
                return true;
            };
            Native.EnumPwrSchemes(proc, IntPtr.Zero);
            GC.KeepAlive(proc);
            return list;
        }

        static IntPtr ReadLegacyPolicy(uint index)
        {
            int size = Marshal.SizeOf(typeof(PowerPolicy));
            IntPtr mem = Marshal.AllocHGlobal(size);
            for (int i = 0; i < size; i++) Marshal.WriteByte(mem, i, 0);
            if (!Native.ReadPwrScheme(index, mem))
            {
                Marshal.FreeHGlobal(mem);
                return IntPtr.Zero;
            }
            return mem;
        }

        public static List<SettingRow> LoadSettings(uint index)
        {
            List<SettingRow> rows = new List<SettingRow>();
            IntPtr mem = ReadLegacyPolicy(index);
            if (mem == IntPtr.Zero) return rows;
            try
            {
                PowerPolicy policy = (PowerPolicy)Marshal.PtrToStructure(mem, typeof(PowerPolicy));
                AddTimeout(rows, 1, Ui.T("idle.timeout.seconds"), Ui.T("how.long.the.system.stays.idle.before.the.idle.action"), policy.User.IdleTimeoutAc, policy.User.IdleTimeoutDc);
                AddAction(rows, 2, Ui.T("idle.action"), Ui.T("action.when.the.idle.timer.expires"), policy.User.IdleAc.Action, policy.User.IdleDc.Action);
                AddByte(rows, 3, Ui.T("idle.sensitivity"), Ui.T("activity.level.that.counts.as.idle"), policy.User.IdleSensitivityAc, policy.User.IdleSensitivityDc);
                AddByte(rows, 4, Ui.T("processor.throttle.policy"), Ui.T("dynamic.processor.throttling.policy"), policy.User.ThrottlePolicyAc, policy.User.ThrottlePolicyDc);
                AddSleepState(rows, 5, Ui.T("maximum.sleep.state"), Ui.T("deepest.sleep.state.allowed"), policy.User.MaxSleepAc, policy.User.MaxSleepDc);
                AddTimeout(rows, 6, Ui.T("turn.off.display.seconds"), Ui.T("delay.before.turning.off.the.display.0.means.never"), policy.User.VideoTimeoutAc, policy.User.VideoTimeoutDc);
                AddTimeout(rows, 7, Ui.T("turn.off.hard.disk.seconds"), Ui.T("delay.before.turning.off.hard.disks.0.means.never"), policy.User.SpindownTimeoutAc, policy.User.SpindownTimeoutDc);
                AddByte(rows, 8, Ui.T("favor.fan.and.full.speed"), Ui.T("whether.passive.cooling.favors.the.fan.and.full.processor.speed.0.or.1"), policy.User.OptimizeForPowerAc, policy.User.OptimizeForPowerDc);
                AddByte(rows, 9, Ui.T("throttle.before.fan"), Ui.T("lowest.processor.throttle.before.the.fans.turn.on"), policy.User.FanThrottleToleranceAc, policy.User.FanThrottleToleranceDc);
                AddByte(rows, 10, Ui.T("forced.processor.throttle"), Ui.T("processor.throttle.imposed.by.the.system"), policy.User.ForcedThrottleAc, policy.User.ForcedThrottleDc);
                AddAction(rows, 11, Ui.T("power.button"), Ui.T("action.when.the.power.button.is.pressed"), policy.Machine.PowerButtonAc.Action, policy.Machine.PowerButtonDc.Action);
                AddAction(rows, 12, Ui.T("sleep.button"), Ui.T("action.when.the.sleep.button.is.pressed"), policy.Machine.SleepButtonAc.Action, policy.Machine.SleepButtonDc.Action);
                AddAction(rows, 13, Ui.T("lid.close"), Ui.T("action.when.the.lid.is.closed"), policy.Machine.LidCloseAc.Action, policy.Machine.LidCloseDc.Action);
                AddSleepState(rows, 14, Ui.T("minimum.sleep.state"), Ui.T("shallowest.sleep.state.used.when.idle"), policy.Machine.MinSleepAc, policy.Machine.MinSleepDc);
                AddTimeout(rows, 15, Ui.T("doze.timeout.seconds"), Ui.T("delay.before.dozing"), policy.Machine.DozeTimeoutAc, policy.Machine.DozeTimeoutDc);
                AddTimeout(rows, 16, Ui.T("hibernate.timeout.seconds"), Ui.T("delay.before.hibernation"), policy.Machine.DozeS4TimeoutAc, policy.Machine.DozeS4TimeoutDc);
            }
            finally { Marshal.FreeHGlobal(mem); }
            return rows;
        }

        static void AddTimeout(List<SettingRow> rows, int field, string name, string description, uint ac, uint dc)
        {
            SettingRow row = BaseLegacy(field, name, description);
            row.HasAc = row.HasDc = true;
            row.Ac = ac;
            row.Dc = dc;
            row.Units = Ui.T("seconds");
            row.HasRange = true;
            row.Min = 0;
            row.Max = 999999;
            row.Increment = 1;
            rows.Add(row);
        }

        static void AddByte(List<SettingRow> rows, int field, string name, string description, byte ac, byte dc)
        {
            SettingRow row = BaseLegacy(field, name, description);
            row.HasAc = row.HasDc = true;
            row.Ac = ac;
            row.Dc = dc;
            row.HasRange = true;
            row.Min = 0;
            row.Max = 100;
            row.Increment = 1;
            rows.Add(row);
        }

        static void AddAction(List<SettingRow> rows, int field, string name, string description, int ac, int dc)
        {
            SettingRow row = BaseLegacy(field, name, description);
            row.HasAc = row.HasDc = true;
            row.Ac = (uint)ac;
            row.Dc = (uint)dc;
            string[] names = new string[] {
                Ui.T("none"),
                Ui.T("reserved"),
                Ui.T("sleep"),
                Ui.T("hibernate"),
                Ui.T("shut.down"),
                Ui.T("shut.down.and.restart"),
                Ui.T("shut.down.and.power.off"),
                Ui.T("eject")
            };
            for (uint i = 0; i < names.Length; i++)
            {
                ValueChoice choice = new ValueChoice();
                choice.Value = i;
                choice.Name = names[i];
                row.Choices.Add(choice);
            }
            rows.Add(row);
        }

        static void AddSleepState(List<SettingRow> rows, int field, string name, string description, int ac, int dc)
        {
            SettingRow row = BaseLegacy(field, name, description);
            row.HasAc = row.HasDc = true;
            row.Ac = (uint)ac;
            row.Dc = (uint)dc;
            string[] names = new string[] {
                Ui.T("unspecified"),
                Ui.T("working"),
                "S1", "S2",
                Ui.T("s3.sleep"),
                Ui.T("s4.hibernate"),
                Ui.T("shut.down")
            };
            for (uint i = 0; i < names.Length; i++)
            {
                ValueChoice choice = new ValueChoice();
                choice.Value = i;
                choice.Name = names[i];
                row.Choices.Add(choice);
            }
            rows.Add(row);
        }

        static SettingRow BaseLegacy(int field, string name, string description)
        {
            SettingRow row = new SettingRow();
            row.Legacy = true;
            row.LegacyField = field;
            row.GroupName = Ui.T("legacy.power.scheme");
            row.Name = name;
            row.Description = description + Ui.T("these.fields.come.from.the.windows.server.2003.power.scheme");
            return row;
        }

        public static uint WriteIndex(SchemeItem scheme, SettingRow row, bool ac, uint value)
        {
            IntPtr mem = ReadLegacyPolicy(scheme.LegacyIndex);
            if (mem == IntPtr.Zero) return 1;
            try
            {
                PowerPolicy policy = (PowerPolicy)Marshal.PtrToStructure(mem, typeof(PowerPolicy));
                ApplyLegacyField(ref policy, row.LegacyField, ac, value);
                Marshal.StructureToPtr(policy, mem, false);
                bool ok = Native.WritePwrScheme(ref scheme.LegacyIndex, scheme.Name, scheme.Description ?? "", mem);
                if (!ok) return (uint)Marshal.GetLastWin32Error();
                if (scheme.Active)
                    Native.SetActivePwrScheme(scheme.LegacyIndex, IntPtr.Zero, mem);
                return Native.Ok;
            }
            finally { Marshal.FreeHGlobal(mem); }
        }

        public static uint Activate(SchemeItem scheme)
        {
            IntPtr policy = ReadLegacyPolicy(scheme.LegacyIndex);
            if (policy == IntPtr.Zero) return 1;
            try
            {
                bool ok = Native.SetActivePwrScheme(scheme.LegacyIndex, IntPtr.Zero, policy);
                return ok ? Native.Ok : (uint)Marshal.GetLastWin32Error();
            }
            finally { Marshal.FreeHGlobal(policy); }
        }

        static void ApplyLegacyField(ref PowerPolicy policy, int field, bool ac, uint value)
        {
            byte b = (byte)Math.Min(value, 255);
            int n = (int)value;
            switch (field)
            {
                case 1: if (ac) policy.User.IdleTimeoutAc = value; else policy.User.IdleTimeoutDc = value; break;
                case 2: if (ac) policy.User.IdleAc.Action = n; else policy.User.IdleDc.Action = n; break;
                case 3: if (ac) policy.User.IdleSensitivityAc = b; else policy.User.IdleSensitivityDc = b; break;
                case 4: if (ac) policy.User.ThrottlePolicyAc = b; else policy.User.ThrottlePolicyDc = b; break;
                case 5: if (ac) policy.User.MaxSleepAc = n; else policy.User.MaxSleepDc = n; break;
                case 6: if (ac) policy.User.VideoTimeoutAc = value; else policy.User.VideoTimeoutDc = value; break;
                case 7: if (ac) policy.User.SpindownTimeoutAc = value; else policy.User.SpindownTimeoutDc = value; break;
                case 8: if (ac) policy.User.OptimizeForPowerAc = b; else policy.User.OptimizeForPowerDc = b; break;
                case 9: if (ac) policy.User.FanThrottleToleranceAc = b; else policy.User.FanThrottleToleranceDc = b; break;
                case 10: if (ac) policy.User.ForcedThrottleAc = b; else policy.User.ForcedThrottleDc = b; break;
                case 11: if (ac) policy.Machine.PowerButtonAc.Action = n; else policy.Machine.PowerButtonDc.Action = n; break;
                case 12: if (ac) policy.Machine.SleepButtonAc.Action = n; else policy.Machine.SleepButtonDc.Action = n; break;
                case 13: if (ac) policy.Machine.LidCloseAc.Action = n; else policy.Machine.LidCloseDc.Action = n; break;
                case 14: if (ac) policy.Machine.MinSleepAc = n; else policy.Machine.MinSleepDc = n; break;
                case 15: if (ac) policy.Machine.DozeTimeoutAc = value; else policy.Machine.DozeTimeoutDc = value; break;
                case 16: if (ac) policy.Machine.DozeS4TimeoutAc = value; else policy.Machine.DozeS4TimeoutDc = value; break;
            }
        }
    }
}
