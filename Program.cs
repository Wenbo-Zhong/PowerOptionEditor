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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace PowerOptionEditor
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
            Dpi.Enable();
            if (HasArg(args, "/dump"))
            {
                string path = ArgValue(args, "/dump");
                if (string.IsNullOrEmpty(path))
                    path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "power-settings-dump.txt");
                System.IO.File.WriteAllText(path, BuildDump(), new UTF8Encoding(true));
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, Ui.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot-error.txt"), ex.ToString());
            }
        }

        static bool HasArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        static string BuildDump()
        {
            StringBuilder sb = new StringBuilder();
            List<SchemeItem> schemes = FixedCatalog.LoadSchemes();
            sb.AppendLine(FixedCatalog.Modern ? "modern" : "legacy");
            sb.AppendLine("schemes=" + schemes.Count.ToString(CultureInfo.InvariantCulture));
            SchemeItem current = null;
            for (int i = 0; i < schemes.Count; i++)
            {
                if (schemes[i].Active) current = schemes[i];
                sb.AppendLine((schemes[i].Active ? "* " : "  ") + schemes[i].Name);
            }
            if (current == null && schemes.Count > 0) current = schemes[0];
            if (current == null) return sb.ToString();
            List<SettingRow> rows = FixedCatalog.LoadSettings(current);
            int bare = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Choices.Count == 0 && rows[i].HasAc) bare++;
            sb.AppendLine("settings=" + rows.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("rows-without-choice-list=" + bare.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < rows.Count; i++)
            {
                SettingRow row = rows[i];
                sb.AppendLine(row.GroupName + " | " + row.Name + " | AC=" + row.AcText + " | DC=" + row.DcText + " | hide=" + (row.Hidden ? "1" : "0"));
            }
            return sb.ToString();
        }
    }

    static class Dpi
    {
        const int LOGPIXELSX = 88;

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int index);
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        public static int Scale = 96;

        public static void Enable()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4. Missing on older Windows.
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
                    SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                try { SetProcessDPIAware(); }
                catch (EntryPointNotFoundException) { }
            }
            IntPtr hdc = GetDC(IntPtr.Zero);
            int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
            ReleaseDC(IntPtr.Zero, hdc);
            if (dpi < 96) dpi = 96;
            Scale = dpi;
        }

        public static int S(int pxAt96)
        {
            return (int)Math.Round(pxAt96 * (Scale / 96.0));
        }
    }

    static class Native
    {
        public const uint Ok = 0;
        public const uint MoreData = 234;
        public const uint NoMoreItems = 259;
        public const uint AccessDenied = 5;

        public const uint AccessPossible = 4;
        public const uint AccessScheme = 16;
        public const uint AccessSubgroup = 17;
        public const uint AccessSetting = 18;
        public const uint HideAttribute = 1;
        public const uint RegDword = 4;

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint FormatMessage(uint flags, IntPtr source, uint messageId, uint languageId, StringBuilder buffer, uint size, IntPtr args);
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SHLoadIndirectString(string source, StringBuilder output, uint outputChars, IntPtr reserved);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint flags, uint index, IntPtr buffer, ref uint bufferSize);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadFriendlyName(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, IntPtr buffer, ref uint bufferSize);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadDescription(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, IntPtr buffer, ref uint bufferSize);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadPossibleFriendlyName(IntPtr root, IntPtr subgroup, IntPtr setting, uint possibleIndex, IntPtr buffer, ref uint bufferSize);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadValueUnitsSpecifier(IntPtr root, IntPtr subgroup, IntPtr setting, IntPtr buffer, ref uint bufferSize);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadACValueIndex(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadDCValueIndex(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerWriteACValueIndex(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerWriteDCValueIndex(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadACDefaultIndex(IntPtr root, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadDCDefaultIndex(IntPtr root, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadValueMin(IntPtr root, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadValueMax(IntPtr root, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadValueIncrement(IntPtr root, IntPtr subgroup, IntPtr setting, out uint value);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadSettingAttributes(IntPtr subgroup, IntPtr setting);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerWriteSettingAttributes(IntPtr subgroup, IntPtr setting, uint attributes);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(IntPtr userRoot, out IntPtr activePolicyGuid);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(IntPtr userRoot, IntPtr schemeGuid);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadACValue(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, out uint type, IntPtr buffer, ref uint bufferSize);
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerReadDCValue(IntPtr root, IntPtr scheme, IntPtr subgroup, IntPtr setting, out uint type, IntPtr buffer, ref uint bufferSize);

        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool GetActivePwrScheme(out uint index);
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool ReadPwrScheme(uint index, IntPtr policy);
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool WritePwrScheme(ref uint index, string name, string description, IntPtr policy);
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SetActivePwrScheme(uint index, IntPtr globalPolicy, IntPtr policy);
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool EnumPwrSchemes(EnumSchemesProc proc, IntPtr param);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.U1)]
        public delegate bool EnumSchemesProc(uint index, uint nameSize, IntPtr name, uint descSize, IntPtr description, IntPtr policy, IntPtr param);

        public static bool ModernAvailable()
        {
            IntPtr lib = LoadLibrary("powrprof.dll");
            if (lib == IntPtr.Zero) return false;
            return GetProcAddress(lib, "PowerEnumerate") != IntPtr.Zero;
        }

        public static string ErrorText(uint code)
        {
            StringBuilder sb = new StringBuilder(512);
            uint n = FormatMessage(0x1000 | 0x200, IntPtr.Zero, code, 0, sb, (uint)sb.Capacity, IntPtr.Zero);
            if (n == 0) return Ui.T("error") + code.ToString(CultureInfo.InvariantCulture);
            return sb.ToString().Trim();
        }
    }

    sealed class GuidAlloc : IDisposable
    {
        public readonly IntPtr Ptr;
        public GuidAlloc(Guid? value)
        {
            if (!value.HasValue)
            {
                Ptr = IntPtr.Zero;
                return;
            }
            Ptr = Marshal.AllocHGlobal(16);
            Marshal.StructureToPtr(value.Value, Ptr, false);
        }
        public void Dispose()
        {
            if (Ptr != IntPtr.Zero) Marshal.FreeHGlobal(Ptr);
        }
    }

    sealed class ValueChoice
    {
        public uint Value;
        public string Name;
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name)) return Value.ToString(CultureInfo.InvariantCulture);
            return Name + "  (" + Value.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }

    sealed class SettingRow
    {
        public bool Legacy;
        public Guid Profile;
        public Guid ProfileScheme;
        public int LegacyField;
        public Guid Subgroup;
        public Guid Setting;
        public string GroupName = "";
        public string Name = "";
        public string Description = "";
        public string Units = "";
        public uint Ac, Dc, AcDefault, DcDefault;
        public bool HasAc, HasDc, HasAcDefault, HasDcDefault;
        public bool Hidden;
        public bool AttributesKnown;
        public bool HasRange;
        public uint Min, Max, Increment;
        public bool UnsupportedValue;
        public readonly List<ValueChoice> Choices = new List<ValueChoice>();

        public SettingRow CopyForProfile()
        {
            // Choice definitions are read-only after discovery. Values and UI state
            // belong to each independent scenario row.
            return (SettingRow)MemberwiseClone();
        }

        public string AcText { get { return Format(HasAc, Ac); } }
        public string DcText { get { return Format(HasDc, Dc); } }
        public string AcDefaultText { get { return Profile == Guid.Empty ? Format(HasAcDefault, AcDefault) : ""; } }
        public string DcDefaultText { get { return Profile == Guid.Empty ? Format(HasDcDefault, DcDefault) : ""; } }

        string Format(bool has, uint value)
        {
            if (!has) return Profile != Guid.Empty ? Ui.T("not.overridden") : "";
            if (Profile != Guid.Empty && value == 0 && (Setting == new Guid("75b0ae3f-bce0-45a7-8c89-c9611c25e100") || Setting == new Guid("75b0ae3f-bce0-45a7-8c89-c9611c25e101")))
                return Ui.T("unlimited");
            for (int i = 0; i < Choices.Count; i++)
            {
                if (Choices[i].Value == value)
                    return Choices[i].ToString();
            }
            string number = value.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(Units)) number = number + " " + Units;
            if (Choices.Count > 0) return number + Ui.T("not.in.the.choice.list");
            return number;
        }
    }

    sealed class SchemeItem
    {
        public bool Legacy;
        public Guid Guid;
        public uint LegacyIndex;
        public string Name = "";
        public string Description = "";
        public bool Active;
        public override string ToString()
        {
            return Active ? Name + Ui.T("active") : Name;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct PowerActionPolicy
    {
        public int Action;
        public uint Flags;
        public uint EventCode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct UserPowerPolicy
    {
        public uint Revision;
        public PowerActionPolicy IdleAc;
        public PowerActionPolicy IdleDc;
        public uint IdleTimeoutAc;
        public uint IdleTimeoutDc;
        public byte IdleSensitivityAc;
        public byte IdleSensitivityDc;
        public byte ThrottlePolicyAc;
        public byte ThrottlePolicyDc;
        public int MaxSleepAc;
        public int MaxSleepDc;
        public uint Reserved0;
        public uint Reserved1;
        public uint VideoTimeoutAc;
        public uint VideoTimeoutDc;
        public uint SpindownTimeoutAc;
        public uint SpindownTimeoutDc;
        public byte OptimizeForPowerAc;
        public byte OptimizeForPowerDc;
        public byte FanThrottleToleranceAc;
        public byte FanThrottleToleranceDc;
        public byte ForcedThrottleAc;
        public byte ForcedThrottleDc;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct MachinePowerPolicy
    {
        public uint Revision;
        public PowerActionPolicy PowerButtonAc;
        public PowerActionPolicy PowerButtonDc;
        public PowerActionPolicy SleepButtonAc;
        public PowerActionPolicy SleepButtonDc;
        public PowerActionPolicy LidCloseAc;
        public PowerActionPolicy LidCloseDc;
        public int MinSleepAc;
        public int MinSleepDc;
        public int ReducedLatencySleepAc;
        public int ReducedLatencySleepDc;
        public uint DozeTimeoutAc;
        public uint DozeTimeoutDc;
        public uint DozeS4TimeoutAc;
        public uint DozeS4TimeoutDc;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct PowerPolicy
    {
        public UserPowerPolicy User;
        public MachinePowerPolicy Machine;
    }

    static class Catalog
    {
        public static bool Modern;

        public static List<SchemeItem> LoadSchemes()
        {
            Modern = Native.ModernAvailable();
            if (Modern) return LoadModernSchemes();
            return LoadLegacySchemes();
        }

        public static List<SettingRow> LoadSettings(SchemeItem scheme)
        {
            if (scheme == null) return new List<SettingRow>();
            if (!scheme.Legacy) return LoadModernSettings(scheme.Guid);
            return LoadLegacySettings(scheme.LegacyIndex);
        }

        public static string DumpText()
        {
            StringBuilder sb = new StringBuilder();
            List<SchemeItem> schemes = LoadSchemes();
            sb.AppendLine(Modern ? "modern" : "legacy");
            sb.AppendLine("schemes=" + schemes.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < schemes.Count; i++)
            {
                SchemeItem scheme = schemes[i];
                sb.AppendLine((scheme.Active ? "* " : "  ") + scheme.Name + " " + scheme.Guid.ToString("B"));
            }
            SchemeItem current = null;
            for (int i = 0; i < schemes.Count; i++)
                if (schemes[i].Active) current = schemes[i];
            if (current == null && schemes.Count > 0) current = schemes[0];
            if (current == null) return sb.ToString();
            List<SettingRow> rows = LoadSettings(current);
            sb.AppendLine("settings=" + rows.Count.ToString(CultureInfo.InvariantCulture));
            int emptyChoicesWithValue = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                SettingRow row = rows[i];
                if (row.Choices.Count == 0 && row.HasAc) emptyChoicesWithValue++;
                sb.AppendLine(row.GroupName + " | " + row.Name + " | AC=" + row.AcText + " | DC=" + row.DcText + " | hide=" + (row.Hidden ? "1" : "0"));
            }
            sb.AppendLine("rows-without-choice-list=" + emptyChoicesWithValue.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static List<SchemeItem> LoadModernSchemes()
        {
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
            List<Guid> guids = EnumGuids(IntPtr.Zero, IntPtr.Zero, Native.AccessScheme);
            for (int i = 0; i < guids.Count; i++)
            {
                SchemeItem item = new SchemeItem();
                item.Guid = guids[i];
                item.Name = ReadText(NativeReadKind.Friendly, guids[i], null, null);
                if (string.IsNullOrEmpty(item.Name)) item.Name = guids[i].ToString("B");
                item.Description = ReadText(NativeReadKind.Description, guids[i], null, null);
                item.Active = hasActive && guids[i] == active;
                list.Add(item);
            }
            return list;
        }

        static List<SettingRow> LoadModernSettings(Guid scheme)
        {
            List<SettingRow> rows = new List<SettingRow>();
            List<Guid> groups = EnumGuids(PtrOf(scheme), IntPtr.Zero, Native.AccessSubgroup);
            groups.Insert(0, Guid.Empty);
            for (int g = 0; g < groups.Count; g++)
            {
                Guid group = groups[g];
                string groupName = group == Guid.Empty ? Ui.T("plan.root") : ReadText(NativeReadKind.Friendly, null, group, null);
                if (string.IsNullOrEmpty(groupName)) groupName = group.ToString("B");
                List<Guid> settings = EnumGuids(PtrOf(scheme), PtrOf(group == Guid.Empty ? (Guid?)null : group), Native.AccessSetting);
                for (int s = 0; s < settings.Count; s++)
                    rows.Add(ReadModernSetting(scheme, group, groupName, settings[s]));
            }
            return rows;
        }

        static IntPtr PtrOf(Guid guid)
        {
            // Callers that need a stable pointer use GuidAlloc. This helper is unused for enum.
            return IntPtr.Zero;
        }

        static IntPtr PtrOf(Guid? guid)
        {
            return IntPtr.Zero;
        }

        static SettingRow ReadModernSetting(Guid scheme, Guid group, string groupName, Guid setting)
        {
            SettingRow row = new SettingRow();
            row.Subgroup = group;
            row.Setting = setting;
            row.GroupName = groupName;
            Guid? groupArg = group == Guid.Empty ? (Guid?)null : group;
            row.Name = ReadText(NativeReadKind.Friendly, null, groupArg, setting);
            if (string.IsNullOrEmpty(row.Name)) row.Name = setting.ToString("B");
            row.Description = ReadText(NativeReadKind.Description, null, groupArg, setting);
            row.Units = ReadText(NativeReadKind.Units, null, groupArg, setting);
            uint value;
            using (GuidAlloc schemePtr = new GuidAlloc(scheme))
            using (GuidAlloc groupPtr = new GuidAlloc(groupArg))
            using (GuidAlloc settingPtr = new GuidAlloc(setting))
            {
                if (Native.PowerReadACValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                {
                    row.HasAc = true;
                    row.Ac = value;
                }
                if (Native.PowerReadDCValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                {
                    row.HasDc = true;
                    row.Dc = value;
                }
                if (Native.PowerReadACDefaultIndex(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                {
                    row.HasAcDefault = true;
                    row.AcDefault = value;
                }
                if (Native.PowerReadDCDefaultIndex(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                {
                    row.HasDcDefault = true;
                    row.DcDefault = value;
                }
                uint min, max, inc;
                bool hasMin = Native.PowerReadValueMin(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out min) == Native.Ok;
                bool hasMax = Native.PowerReadValueMax(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out max) == Native.Ok;
                bool hasInc = Native.PowerReadValueIncrement(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out inc) == Native.Ok;
                if (hasMin && hasMax)
                {
                    row.HasRange = true;
                    row.Min = min;
                    row.Max = max;
                    row.Increment = hasInc ? inc : 1;
                }
                uint attributes = Native.PowerReadSettingAttributes(groupPtr.Ptr, settingPtr.Ptr);
                row.AttributesKnown = true;
                row.Hidden = (attributes & Native.HideAttribute) != 0;
                ReadChoices(groupPtr.Ptr, settingPtr.Ptr, row);
                if (!row.HasAc)
                    row.UnsupportedValue = !TryReadRaw(true, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, row);
            }
            return row;
        }

        static bool TryReadRaw(bool ac, IntPtr scheme, IntPtr group, IntPtr setting, SettingRow row)
        {
            uint size = 0;
            uint type;
            uint rc = ac
                ? Native.PowerReadACValue(IntPtr.Zero, scheme, group, setting, out type, IntPtr.Zero, ref size)
                : Native.PowerReadDCValue(IntPtr.Zero, scheme, group, setting, out type, IntPtr.Zero, ref size);
            if (size == 0 || (rc != Native.Ok && rc != Native.MoreData)) return false;
            if (type == Native.RegDword && size >= 4)
            {
                IntPtr buf = Marshal.AllocHGlobal((int)size);
                try
                {
                    rc = ac
                        ? Native.PowerReadACValue(IntPtr.Zero, scheme, group, setting, out type, buf, ref size)
                        : Native.PowerReadDCValue(IntPtr.Zero, scheme, group, setting, out type, buf, ref size);
                    if (rc != Native.Ok) return false;
                    uint value = (uint)Marshal.ReadInt32(buf);
                    if (ac) { row.HasAc = true; row.Ac = value; }
                    else { row.HasDc = true; row.Dc = value; }
                    return true;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            row.Description = (row.Description ?? "") + Ui.T("this.value.is.not.a.dword.it.is.shown.but.cannot.be.written.here");
            return true;
        }

        static void ReadChoices(IntPtr group, IntPtr setting, SettingRow row)
        {
            uint index = 0;
            while (index < 512)
            {
                uint size = 4;
                IntPtr buf = Marshal.AllocHGlobal(4);
                try
                {
                    uint rc = Native.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, group, Native.AccessPossible, index, buf, ref size);
                    if (rc == Native.NoMoreItems || rc != Native.Ok) break;
                    uint value = (uint)Marshal.ReadInt32(buf);
                    ValueChoice choice = new ValueChoice();
                    choice.Value = value;
                    choice.Name = ReadPossibleName(group, setting, value);
                    if (string.IsNullOrEmpty(choice.Name))
                        choice.Name = ReadPossibleName(group, setting, index);
                    row.Choices.Add(choice);
                }
                finally { Marshal.FreeHGlobal(buf); }
                index++;
            }
        }

        static string ReadPossibleName(IntPtr group, IntPtr setting, uint index)
        {
            uint size = 0;
            uint rc = Native.PowerReadPossibleFriendlyName(IntPtr.Zero, group, setting, index, IntPtr.Zero, ref size);
            if (size < 2 || (rc != Native.Ok && rc != Native.MoreData)) return "";
            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                rc = Native.PowerReadPossibleFriendlyName(IntPtr.Zero, group, setting, index, buf, ref size);
                if (rc != Native.Ok) return "";
                return Clean(Marshal.PtrToStringUni(buf));
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        enum NativeReadKind { Friendly, Description, Units }

        static string ReadText(NativeReadKind kind, Guid? scheme, Guid? group, Guid? setting)
        {
            using (GuidAlloc schemePtr = new GuidAlloc(scheme))
            using (GuidAlloc groupPtr = new GuidAlloc(group))
            using (GuidAlloc settingPtr = new GuidAlloc(setting))
            {
                uint size = 0;
                uint rc = ReadTextRaw(kind, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, IntPtr.Zero, ref size);
                if (size < 2 || (rc != Native.Ok && rc != Native.MoreData)) return "";
                IntPtr buf = Marshal.AllocHGlobal((int)size);
                try
                {
                    rc = ReadTextRaw(kind, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, buf, ref size);
                    if (rc != Native.Ok) return "";
                    return Clean(Marshal.PtrToStringUni(buf));
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }

        static uint ReadTextRaw(NativeReadKind kind, IntPtr scheme, IntPtr group, IntPtr setting, IntPtr buffer, ref uint size)
        {
            if (kind == NativeReadKind.Friendly)
                return Native.PowerReadFriendlyName(IntPtr.Zero, scheme, group, setting, buffer, ref size);
            if (kind == NativeReadKind.Description)
                return Native.PowerReadDescription(IntPtr.Zero, scheme, group, setting, buffer, ref size);
            return Native.PowerReadValueUnitsSpecifier(IntPtr.Zero, group, setting, buffer, ref size);
        }

        static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Trim().TrimEnd('\0').Replace("\r\n", " ").Replace('\n', ' ');
        }

        static List<Guid> EnumGuids(IntPtr scheme, IntPtr subgroup, uint access)
        {
            // scheme/subgroup here are already pointers only when non-null is required.
            // Overload below allocates.
            List<Guid> list = new List<Guid>();
            uint index = 0;
            while (index < 4096)
            {
                uint size = 16;
                IntPtr buf = Marshal.AllocHGlobal(16);
                try
                {
                    uint rc = Native.PowerEnumerate(IntPtr.Zero, scheme, subgroup, access, index, buf, ref size);
                    if (rc == Native.NoMoreItems) break;
                    if (rc != Native.Ok) break;
                    Guid guid = (Guid)Marshal.PtrToStructure(buf, typeof(Guid));
                    list.Add(guid);
                }
                finally { Marshal.FreeHGlobal(buf); }
                index++;
            }
            return list;
        }

        static List<Guid> EnumGuids(GuidAlloc scheme, Guid? subgroup, uint access)
        {
            using (GuidAlloc sub = new GuidAlloc(subgroup))
                return EnumGuids(scheme == null ? IntPtr.Zero : scheme.Ptr, sub.Ptr, access);
        }

        static List<Guid> EnumGuids(IntPtr ignoredSchemeMarker, IntPtr ignored, uint access, bool unused)
        {
            return new List<Guid>();
        }

        // Real entry used by LoadModernSettings. The IntPtr overload above is correct when pointers are built by caller.
        static List<Guid> EnumChild(Guid? scheme, Guid? subgroup, uint access)
        {
            using (GuidAlloc schemePtr = new GuidAlloc(scheme))
            using (GuidAlloc subPtr = new GuidAlloc(subgroup))
                return EnumGuids(schemePtr.Ptr, subPtr.Ptr, access);
        }

        static List<SchemeItem> LoadModernSchemesFixed()
        {
            return LoadModernSchemes();
        }

        public static uint WriteIndex(SchemeItem scheme, SettingRow row, bool ac, uint value)
        {
            if (row.Profile != Guid.Empty) return ProfileCatalog.WriteIndex(scheme, row, ac, value);
            if (scheme.Legacy) return WriteLegacy(scheme, row, ac, value);
            using (GuidAlloc schemePtr = new GuidAlloc(scheme.Guid))
            using (GuidAlloc groupPtr = new GuidAlloc(row.Subgroup == Guid.Empty ? (Guid?)null : row.Subgroup))
            using (GuidAlloc settingPtr = new GuidAlloc(row.Setting))
            {
                if (ac) return Native.PowerWriteACValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, value);
                return Native.PowerWriteDCValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, value);
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
            if (scheme.Legacy)
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
            using (GuidAlloc schemePtr = new GuidAlloc(scheme.Guid))
                return Native.PowerSetActiveScheme(IntPtr.Zero, schemePtr.Ptr);
        }

        static List<SchemeItem> LoadLegacySchemes()
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

        static List<SettingRow> LoadLegacySettings(uint index)
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

        static uint WriteLegacy(SchemeItem scheme, SettingRow row, bool ac, uint value)
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

    // Fix LoadModernSchemes/Settings to use EnumChild. The methods above call EnumGuids with PtrOf that returns Zero.
    // The actual calls are corrected in the partial rewrite below by replacing method bodies via the wrapper class methods
    // invoked from MainForm. Catalog.LoadSchemes uses LoadModernSchemes which uses EnumGuids(IntPtr.Zero...) — WRONG.
    // I will patch this after compile test if dump shows 0 schemes. Writing a corrected Catalog section is done
    // by redefining load path in ModernCatalog.

    sealed class MainForm : Form
    {
        readonly ComboBox _schemes = new ComboBox();
        readonly ComboBox _profiles = new ComboBox();
        readonly ComboBox _profileSchemes = new ComboBox();
        readonly ThemeTabs _mode = new ThemeTabs();
        readonly AppTheme _theme = new AppTheme();
        bool _themeUpdateQueued;
        readonly TableLayoutPanel _pageContent = new TableLayoutPanel();
        readonly List<Control> _dailyControls = new List<Control>();
        bool SceneMode { get { return _mode.SelectedIndex == 1; } }
        readonly TextBox _search = new TextBox();
        readonly ListView _grid = new BufferedListView();
        readonly TextBox _description = new TextBox();
        readonly Label _meta = new Label();
        readonly ComboBox _acList = new ComboBox();
        readonly ComboBox _dcList = new ComboBox();
        readonly TextBox _acBox = new TextBox();
        readonly TextBox _dcBox = new TextBox();
        readonly CheckBox _hidden = new CheckBox();
        readonly Button _apply = new ThemeButton();
        readonly Button _defaults = new ThemeButton();
        readonly Label _status = new Label();
        readonly Label _pending = new Label();
        readonly List<SettingRow> _rows = new List<SettingRow>();
        List<SettingRow> _processorTemplates;
        readonly Dictionary<string, List<SettingRow>> _dailyCache = new Dictionary<string, List<SettingRow>>();
        readonly Dictionary<string, int[]> _columnWidths = new Dictionary<string, int[]>();
        readonly Dictionary<string, GridPosition> _gridPositions = new Dictionary<string, GridPosition>();
        string _boundViewKey;
        bool _profilesLoaded;
        bool _layoutQueued;

        sealed class GridPosition
        {
            public Guid Setting, Scheme;
            public int TopIndex;
        }
        readonly Panel _bar = new Panel();
        readonly Panel _editorRow = new Panel();
        readonly Panel _actionRow = new Panel();
        readonly Panel _acHost = new Panel();
        readonly Panel _dcHost = new Panel();
        Label _acCaption;
        Label _dcCaption;
        bool _filling;
        bool _arrangingBar;
        bool _arrangingEditors;
        bool _arrangingActions;

        public MainForm()
        {
            Text = Ui.Title;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Dpi.S(1100), Dpi.S(720));
            MinimumSize = new Size(Dpi.S(860), Dpi.S(560));

            TableLayoutPanel root = _pageContent;
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(Dpi.S(8));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(198)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(24)));


            _bar.Dock = DockStyle.Top;
            _bar.AutoSize = false;
            _bar.SizeChanged += delegate { ArrangeBar(); };
            _bar.Margin = new Padding(0, 0, 0, Dpi.S(6));
            root.Controls.Add(_bar, 0, 0);

            _mode.TabPages.Add(Ui.T("daily"));
            _mode.TabPages.Add(Ui.T("scenarios"));
            _mode.Dock = DockStyle.Fill;
            Controls.Add(_mode);
            _mode.TabPages[0].Controls.Add(root);
            // Only block the scenario page on legacy systems; disabling the
            // container would also disable every daily setting inside it.
            _mode.Selecting += delegate(object sender, TabControlCancelEventArgs e)
            {
                if (e.TabPageIndex == 1 && !FixedCatalog.Modern) e.Cancel = true;
                else SaveGridPosition();
            };
            _mode.SelectedIndexChanged += delegate { ReloadProfiles(); };

            _profiles.DropDownStyle = _profileSchemes.DropDownStyle = ComboBoxStyle.DropDownList;
            _profiles.Width = Dpi.S(180);
            _profileSchemes.Width = Dpi.S(240);
            _profiles.Visible = _profileSchemes.Visible = false;
            _profiles.SelectedIndexChanged += delegate { ReloadProfileSchemes(); };
            _profileSchemes.SelectedIndexChanged += delegate { ReloadSettings(); };
            AddBar(_profiles);
            // Scheme overrides are listed together in the scenario grid.
            // Keep this selector internal so the toolbar only needs the scenario menu.
            Label planCaption = MakeLabel(Ui.T("power.plan"));
            AddBar(planCaption);
            _dailyControls.Add(planCaption);
            _dailyControls.Add(_schemes);
            _schemes.DropDownStyle = ComboBoxStyle.DropDownList;
            _schemes.Width = Dpi.S(280);
            _schemes.SelectedIndexChanged += delegate { ReloadSettings(); };
            AddBar(_schemes);

            Button activate = MakeButton(Ui.T("set.active"));
            activate.Click += delegate { OnActivate(); };
            AddBar(activate);

            Button refresh = MakeButton(Ui.T("refresh"));
            refresh.Click += delegate { ReloadSchemes(true); if (SceneMode) ReloadProfiles(); };
            AddBar(refresh);

            AddBar(MakeLabel(Ui.T("search")));
            _search.Width = Dpi.S(180);
            _search.TextChanged += delegate { BindGrid(); };
            AddBar(_search);

            Button export = MakeButton(Ui.T("export"));
            export.Click += delegate { OnExport(); };
            AddBar(export);

            Button import = MakeButton(Ui.T("import"));
            import.Click += delegate { OnImport(); };
            AddBar(import);
            Button unhideAll = MakeButton(Ui.T("unhide.all.hidden.options"));
            unhideAll.Click += delegate { OnUnhideAll(); };
            unhideAll.Enabled = Native.ModernAvailable();
            AddBar(unhideAll);
            _dailyControls.Add(unhideAll);
            _dailyControls.Add(activate);
            _dailyControls.Add(export);
            _dailyControls.Add(import);
            _dailyControls.Add(_hidden);
            _dailyControls.Add(_defaults);

            Button cpl = MakeButton(Ui.T("power.options"));
            cpl.Click += delegate
            {
                try { Process.Start("control.exe", "powercfg.cpl"); }
                catch (Exception ex) { Report(ex.Message); }
            };
            AddBar(cpl);

            Button admin = MakeButton(Ui.T("restart.as.administrator"));
            admin.Click += delegate { RelaunchAdmin(); };
            AddBar(admin);
            ArrangeBar();

            _grid.Dock = DockStyle.Fill;
            _grid.View = View.Details;
            _grid.FullRowSelect = true;
            _grid.GridLines = true;
            _grid.HideSelection = false;
            _grid.MultiSelect = false;
            _grid.ShowItemToolTips = true;
            ImageList spacer = new ImageList();
            spacer.ImageSize = new Size(1, Math.Max(Dpi.S(20), Font.Height + Dpi.S(6)));
            _grid.SmallImageList = spacer;
            AddColumn(Ui.T("group"));
            AddColumn(Ui.T("setting"));
            AddColumn(Ui.T("ac"));
            AddColumn(Ui.T("battery"));
            AddColumn(Ui.T("default.ac"));
            AddColumn(Ui.T("default.battery"));
            AddColumn(Ui.T("hidden"));
            AddColumn(Ui.T("units"));
            _grid.SelectedIndexChanged += delegate { if (!_filling) ShowSelected(); if (_theme.Dark) _grid.Invalidate(); };
            _grid.DoubleClick += delegate { OpenRegistry(); };
            root.Controls.Add(_grid, 0, 1);

            TableLayoutPanel detail = new TableLayoutPanel();
            detail.Dock = DockStyle.Fill;
            detail.ColumnCount = 1;
            detail.RowCount = 5;
            detail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(detail, 0, 2);

            _description.Multiline = true;
            _description.ReadOnly = true;
            _description.ScrollBars = ScrollBars.Vertical;
            _description.Dock = DockStyle.Fill;
            _description.Margin = new Padding(0, 0, 0, Dpi.S(4));
            detail.Controls.Add(_description, 0, 0);

            _meta.Dock = DockStyle.Fill;
            _meta.AutoSize = true;
            _meta.AutoEllipsis = true;
            _meta.TextAlign = ContentAlignment.MiddleLeft;
            _meta.Margin = new Padding(0, 0, 0, Dpi.S(4));
            detail.Controls.Add(_meta, 0, 1);

            _editorRow.Dock = DockStyle.Top;
            _editorRow.Margin = new Padding(0, Dpi.S(2), 0, Dpi.S(4));
            _editorRow.Resize += delegate { ArrangeEditors(); };
            detail.Controls.Add(_editorRow, 0, 2);
            _acCaption = MakeLabel(Ui.T("ac"));
            _dcCaption = MakeLabel(Ui.T("battery"));
            _acCaption.AutoSize = false;
            _dcCaption.AutoSize = false;
            _acCaption.TextAlign = ContentAlignment.MiddleRight;
            _dcCaption.TextAlign = ContentAlignment.MiddleRight;
            _acList.DropDownStyle = ComboBoxStyle.DropDownList;
            _dcList.DropDownStyle = ComboBoxStyle.DropDownList;
            _editorRow.Controls.Add(_acCaption);
            _editorRow.Controls.Add(_acList);
            _editorRow.Controls.Add(_acBox);
            _editorRow.Controls.Add(_dcCaption);
            _editorRow.Controls.Add(_dcList);
            _editorRow.Controls.Add(_dcBox);
            _acBox.TextChanged += delegate { UpdatePendingChanges(); };
            _dcBox.TextChanged += delegate { UpdatePendingChanges(); };
            _acList.SelectedIndexChanged += delegate { UpdatePendingChanges(); };
            _dcList.SelectedIndexChanged += delegate { UpdatePendingChanges(); };
            _hidden.CheckedChanged += delegate { UpdatePendingChanges(); };

            _pending.Dock = DockStyle.Fill;
            _pending.AutoSize = true;
            _pending.MinimumSize = new Size(0, Dpi.S(30));
            _pending.Font = new Font(Font, FontStyle.Bold);
            _pending.TextAlign = ContentAlignment.MiddleLeft;
            _pending.BorderStyle = BorderStyle.FixedSingle;
            _pending.Padding = new Padding(Dpi.S(4));
            _pending.Margin = new Padding(0, 0, 0, Dpi.S(4));
            detail.Controls.Add(_pending, 0, 3);

            _actionRow.Dock = DockStyle.Top;
            _actionRow.Margin = new Padding(0, Dpi.S(2), 0, 0);
            _actionRow.Resize += delegate { ArrangeActions(); };
            detail.Controls.Add(_actionRow, 0, 4);
            _hidden.Text = Ui.T("hide.in.power.options");
            _hidden.AutoSize = true;
            _hidden.Margin = Padding.Empty;
            _apply.Text = Ui.T("write.values");
            _apply.AutoSize = true;
            _apply.Margin = Padding.Empty;
            _apply.Click += delegate { OnApply(false); };
            _defaults.Text = Ui.T("restore.defaults");
            _defaults.AutoSize = true;
            _defaults.Margin = Padding.Empty;
            _defaults.Click += delegate { OnApply(true); };
            Button copy = MakeButton(Ui.T("copy.guid"));
            copy.Click += delegate { CopyGuid(); };
            Button reg = MakeButton(Ui.T("open.registry"));
            reg.Click += delegate { OpenRegistry(); };
            _actionRow.Controls.Add(_hidden);
            _actionRow.Controls.Add(_apply);
            _actionRow.Controls.Add(_defaults);
            _actionRow.Controls.Add(copy);
            _actionRow.Controls.Add(reg);

            _status.Dock = DockStyle.Fill;
            _status.AutoEllipsis = true;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_status, 0, 3);

            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.F)
                {
                    _search.Focus();
                    _search.SelectAll();
                    e.Handled = true;
                }
            };

            Shown += delegate { RefreshTheme(); ArrangeBar(); ArrangeEditors(); ArrangeActions(); ReloadSchemes(false); };
        }

        void SaveGridPosition()
        {
            if (_boundViewKey == null || _grid.Items.Count == 0) return;
            SettingRow row = SelectedRow();
            if (row == null) return;
            _gridPositions[_boundViewKey] = new GridPosition { Setting = row.Setting, Scheme = row.ProfileScheme,
                TopIndex = _grid.TopItem == null ? 0 : _grid.TopItem.Index };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            QueueLayoutRepair();
        }

        void QueueLayoutRepair()
        {
            if (_layoutQueued || !IsHandleCreated || IsDisposed || WindowState == FormWindowState.Minimized) return;
            _layoutQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                _layoutQueued = false;
                if (IsDisposed || WindowState == FormWindowState.Minimized) return;
                _pageContent.SuspendLayout();
                ArrangeBar();
                ArrangeEditors();
                ArrangeActions();
                _pageContent.ResumeLayout(true);
                _pageContent.PerformLayout();
                _grid.Invalidate();
            });
        }

        void RefreshTheme()
        {
            if (_theme.Apply(this)) UpdatePendingChanges();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _theme.ApplyTitleBar(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Theme, preference and high-contrast broadcasts can arrive in a burst.
            if ((m.Msg == 0x001A || m.Msg == 0x031A || m.Msg == 0x0015)
                && IsHandleCreated && !IsDisposed && !_themeUpdateQueued)
            {
                _themeUpdateQueued = true;
                BeginInvoke((MethodInvoker)delegate
                {
                    _themeUpdateQueued = false;
                    if (!IsDisposed) RefreshTheme();
                });
            }
        }

        Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Margin = Padding.Empty;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        Button MakeButton(string text)
        {
            Button button = new ThemeButton();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = Padding.Empty;
            return button;
        }

        void AddBar(Control control)
        {
            control.Margin = Padding.Empty;
            _bar.Controls.Add(control);
        }

        void ArrangeBar()
        {
            if (_arrangingBar || WindowState == FormWindowState.Minimized || _bar.ClientSize.Width < Dpi.S(200)) return;
            _arrangingBar = true;
            int gap = Dpi.S(8);
            int x = 0;
            int maxH = 0;
            for (int i = 0; i < _bar.Controls.Count; i++)
            {
                Control control = _bar.Controls[i];
                if (control.Visible && control.Height > maxH) maxH = control.Height;
            }
            if (maxH < 1) maxH = Font.Height + Dpi.S(8);
            int y = 0;
            for (int i = 0; i < _bar.Controls.Count; i++)
            {
                Control control = _bar.Controls[i];
                if (!control.Visible) continue;
                if (x > 0 && x + control.Width > _bar.ClientSize.Width) { x = 0; y += maxH + gap; }
                control.Left = x;
                control.Top = y + Math.Max(0, (maxH - control.Height) / 2);
                x += control.Width + gap;
            }
            _bar.Height = y + maxH;
            _arrangingBar = false;
        }

        void ArrangeEditors()
        {
            if (_arrangingEditors || _acCaption == null || WindowState == FormWindowState.Minimized || _editorRow.ClientSize.Width < Dpi.S(200)) return;
            _arrangingEditors = true;
            int pairGap = Dpi.S(6);
            int splitGap = Dpi.S(28);
            int width = _editorRow.ClientSize.Width;
            if (width < Dpi.S(200)) width = Dpi.S(640);
            int editH = _acList.Height;
            if (editH < Font.Height) editH = Font.Height + Dpi.S(8);
            int acLabel = TextRenderer.MeasureText(_acCaption.Text, _acCaption.Font, Size.Empty, TextFormatFlags.NoPadding).Width + Dpi.S(4);
            int dcLabel = TextRenderer.MeasureText(_dcCaption.Text, _dcCaption.Font, Size.Empty, TextFormatFlags.NoPadding).Width + Dpi.S(4);
            int editorW = Math.Max(Dpi.S(80), (width - acLabel - dcLabel - pairGap * 2 - splitGap) / 2);
            int x = 0;
            _acCaption.SetBounds(x, 0, acLabel, editH);
            x += acLabel + pairGap;
            _acList.SetBounds(x, 0, editorW, editH);
            _acBox.SetBounds(x, 0, editorW, editH);
            x += editorW + splitGap;
            _dcCaption.SetBounds(x, 0, dcLabel, editH);
            x += dcLabel + pairGap;
            _dcList.SetBounds(x, 0, editorW, editH);
            _dcBox.SetBounds(x, 0, editorW, editH);
            _editorRow.Height = editH;
            _arrangingEditors = false;
        }

        void ArrangeActions()
        {
            if (_arrangingActions || WindowState == FormWindowState.Minimized) return;
            _arrangingActions = true;
            int gap = Dpi.S(8);
            int x = 0;
            int maxH = 0;
            for (int i = 0; i < _actionRow.Controls.Count; i++)
            {
                Control control = _actionRow.Controls[i];
                if (!control.Visible) continue;
                if (control.Height > maxH) maxH = control.Height;
            }
            if (maxH < 1) maxH = Font.Height + Dpi.S(8);
            for (int i = 0; i < _actionRow.Controls.Count; i++)
            {
                Control control = _actionRow.Controls[i];
                if (!control.Visible) continue;
                control.Left = x;
                control.Top = Math.Max(0, (maxH - control.Height) / 2);
                x += control.Width + gap;
            }
            _actionRow.Height = maxH;
            _arrangingActions = false;
        }

        static void PlaceMiddle(Control control, int x, int width, int rowH)
        {
            control.Left = x;
            control.Width = width;
            control.Top = Math.Max(0, (rowH - control.Height) / 2);
        }

        void AddColumn(string text)
        {
            ColumnHeader header = new ColumnHeader();
            header.Text = text;
            header.Width = Dpi.S(80);
            _grid.Columns.Add(header);
        }

        void FitColumns()
        {
            string key = (_boundViewKey ?? "") + "|" + _search.Text;
            int[] cached;
            if (_columnWidths.TryGetValue(key, out cached))
            {
                for (int c = 0; c < cached.Length; c++) _grid.Columns[c].Width = cached[c];
                return;
            }
            int[] widths = new int[_grid.Columns.Count];
            int pad = Dpi.S(18);
            for (int c = 0; c < _grid.Columns.Count; c++)
            {
                if (SceneMode && c >= 4 && c <= 6) { _grid.Columns[c].Width = 0; continue; }
                int width = TextRenderer.MeasureText(_grid.Columns[c].Text, _grid.Font).Width + pad;
                for (int i = 0; i < _grid.Items.Count; i++)
                {
                    if (c >= _grid.Items[i].SubItems.Count) continue;
                    int itemWidth = TextRenderer.MeasureText(_grid.Items[i].SubItems[c].Text, _grid.Font).Width + pad;
                    if (itemWidth > width) width = itemWidth;
                }
                _grid.Columns[c].Width = width;
                widths[c] = width;
            }
            _columnWidths[key] = widths;
        }

        void ReloadSchemes(bool keep)
        {
            _processorTemplates = null;
            _dailyCache.Clear();
            _columnWidths.Clear();
            _profilesLoaded = false;
            Guid previous = Guid.Empty;
            uint legacy = 0;
            bool wasLegacy = false;
            SchemeItem selected = _schemes.SelectedItem as SchemeItem;
            if (selected != null)
            {
                previous = selected.Guid;
                legacy = selected.LegacyIndex;
                wasLegacy = selected.Legacy;
            }
            List<SchemeItem> schemes;
            try { schemes = FixedCatalog.LoadSchemes(); }
            catch (Exception ex)
            {
                Report(ex.Message);
                return;
            }
            _filling = true;
            _schemes.Items.Clear();
            int index = 0;
            for (int i = 0; i < schemes.Count; i++)
            {
                _schemes.Items.Add(schemes[i]);
                if (schemes[i].Active) index = i;
                if (keep && wasLegacy == schemes[i].Legacy && ((wasLegacy && schemes[i].LegacyIndex == legacy) || (!wasLegacy && schemes[i].Guid == previous)))
                    index = i;
            }
            _filling = false;
            if (_schemes.Items.Count > 0) _schemes.SelectedIndex = index;
            else Report(Ui.T("no.power.plans.were.found"));
            Text = FixedCatalog.Modern
                ? Ui.Title
                : Ui.Title + Ui.T("legacy.power.schemes.windows.server.2003");
        }

        void ReloadProfiles()
        {
            _pageContent.SuspendLayout();
            _mode.SelectedTab.Controls.Add(_pageContent);
            _profiles.Visible = SceneMode;
            foreach (Control control in _dailyControls) control.Visible = !SceneMode;
            _pageContent.ResumeLayout(true);
            QueueLayoutRepair();
            if (!SceneMode) { ReloadSettings(); return; }
            if (_profilesLoaded) { ReloadProfileSchemes(); return; }
            try
            {
                Guid previous = _profiles.SelectedItem is ProfileItem ? ((ProfileItem)_profiles.SelectedItem).Guid : Guid.Empty;
                _filling = true;
                _profiles.Items.Clear();
                foreach (ProfileItem profile in ProfileCatalog.LoadProfiles())
                    if (ProfileCatalog.HasAdjustableTargets(profile)) _profiles.Items.Add(profile);
                int selected = 0;
                for (int i = 0; i < _profiles.Items.Count; i++)
                    if (((ProfileItem)_profiles.Items[i]).Guid == previous) selected = i;
                if (_profiles.Items.Count > 0) _profiles.SelectedIndex = selected;
                _profilesLoaded = true;
            }
            catch (Exception ex) { Report(ex.Message); }
            finally { _filling = false; }
            ReloadProfileSchemes();
        }

        void ReloadProfileSchemes()
        {
            if (_filling || !SceneMode) return;
            ProfileItem profile = _profiles.SelectedItem as ProfileItem;
            SchemeItem daily = _schemes.SelectedItem as SchemeItem;
            SchemeItem previous = _profileSchemes.SelectedItem as SchemeItem;
            try
            {
                _filling = true;
                _profileSchemes.Items.Clear();
                if (profile != null) foreach (SchemeItem scheme in ProfileCatalog.LoadSchemes(profile, daily)) _profileSchemes.Items.Add(scheme);
                int selected = 0;
                for (int i = 0; i < _profileSchemes.Items.Count; i++)
                    if (((SchemeItem)_profileSchemes.Items[i]).Guid == (previous != null ? previous.Guid : daily != null ? daily.Guid : Guid.Empty)) selected = i;
                if (_profileSchemes.Items.Count > 0) _profileSchemes.SelectedIndex = selected;
            }
            catch (Exception ex) { Report(ex.Message); }
            finally { _filling = false; }
            ReloadSettings();
        }

        void ReloadSettings()
        {
            if (_filling) return;
            SchemeItem scheme = _schemes.SelectedItem as SchemeItem;
            UseWaitCursor = true;
            try
            {
                SaveGridPosition();
                _rows.Clear();
                _boundViewKey = null;
                if (scheme != null)
                {
                    ProfileItem profile = _profiles.SelectedItem as ProfileItem;
                    if (SceneMode && profile != null)
                    {
                        _boundViewKey = "profile:" + profile.Guid.ToString();
                        if (_processorTemplates == null) CacheProcessorTemplates(FixedCatalog.LoadSettings(scheme));
                        foreach (SchemeItem target in _profileSchemes.Items)
                        {
                            List<SettingRow> overrides = ProfileCatalog.LoadSettings(profile, target, _processorTemplates);
                            foreach (SettingRow row in overrides)
                            {
                                if (!row.HasAc && !row.HasDc) continue;
                                row.ProfileScheme = target.Guid;
                                if (_profileSchemes.Items.Count > 1) row.GroupName += " — " + target.Name;
                                _rows.Add(row);
                            }
                        }
                    }
                    else if (!SceneMode)
                    {
                        _boundViewKey = "daily:" + (scheme.Legacy ? scheme.LegacyIndex.ToString(CultureInfo.InvariantCulture) : scheme.Guid.ToString());
                        List<SettingRow> cached;
                        if (!_dailyCache.TryGetValue(_boundViewKey, out cached))
                        {
                            cached = FixedCatalog.LoadSettings(scheme);
                            _dailyCache[_boundViewKey] = cached;
                        }
                        _rows.AddRange(cached);
                        CacheProcessorTemplates(_rows);
                    }
                }
                BindGrid();
                Report(Ui.T("loaded") + _rows.Count.ToString(CultureInfo.InvariantCulture) + Ui.T("settings")
                    + (FixedCatalog.Modern ? "" : Ui.T("this.system.has.no.power.interface.from.vista.onward.so.legacy.schemes")));
            }
            catch (Exception ex) { Report(ex.Message); }
            finally { UseWaitCursor = false; }
        }

        void CacheProcessorTemplates(List<SettingRow> rows)
        {
            _processorTemplates = new List<SettingRow>();
            foreach (SettingRow row in rows)
                if (row.Subgroup == ProfileCatalog.Processor) _processorTemplates.Add(row.CopyForProfile());
        }

        void BindGrid()
        {
            string query = (_search.Text ?? "").Trim();
            List<SettingRow> view = new List<SettingRow>();
            for (int i = 0; i < _rows.Count; i++)
            {
                SettingRow row = _rows[i];
                if (query.Length == 0 || Contains(row, query)) view.Add(row);
            }
            view.Sort(delegate(SettingRow a, SettingRow b)
            {
                int group = string.Compare(a.GroupName, b.GroupName, StringComparison.CurrentCultureIgnoreCase);
                if (group != 0) return group;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            _filling = true;
            _grid.BeginUpdate();
            GridPosition position = null;
            try
            {
                _grid.Items.Clear();
                for (int i = 0; i < view.Count; i++)
                {
                    SettingRow row = view[i];
                    ListViewItem item = new ListViewItem(row.GroupName);
                    item.SubItems.Add(row.Name);
                    item.SubItems.Add(row.AcText);
                    item.SubItems.Add(row.DcText);
                    item.SubItems.Add(row.AcDefaultText);
                    item.SubItems.Add(row.DcDefaultText);
                    item.SubItems.Add(row.AttributesKnown ? (row.Hidden ? Ui.T("yes") : Ui.T("no")) : "");
                    item.SubItems.Add(row.Units ?? "");
                    item.ToolTipText = row.Description ?? "";
                    item.Tag = row;
                    _grid.Items.Add(item);
                }
                FitColumns();
                int selectedIndex = 0;
                if (_boundViewKey != null && _gridPositions.TryGetValue(_boundViewKey, out position))
                {
                    for (int i = 0; i < _grid.Items.Count; i++)
                    {
                        SettingRow row = (SettingRow)_grid.Items[i].Tag;
                        if (row.Setting == position.Setting && row.ProfileScheme == position.Scheme) { selectedIndex = i; break; }
                    }
                }
                if (_grid.Items.Count > 0) _grid.Items[selectedIndex].Selected = true;
            }
            finally { _grid.EndUpdate(); _filling = false; }
            if (position != null && _grid.Items.Count > 0) _grid.TopItem = _grid.Items[Math.Min(position.TopIndex, _grid.Items.Count - 1)];
            ShowSelected();
        }

        static bool Contains(SettingRow row, string query)
        {
            return IndexOf(row.GroupName, query) || IndexOf(row.Name, query) || IndexOf(row.Description, query)
                || IndexOf(row.AcText, query) || IndexOf(row.DcText, query) || IndexOf(row.Units, query);
        }

        static bool IndexOf(string text, string query)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        SettingRow SelectedRow()
        {
            if (_grid.SelectedItems.Count == 0) return null;
            return _grid.SelectedItems[0].Tag as SettingRow;
        }

        void ShowSelected()
        {
            SettingRow row = SelectedRow();
            _filling = true;
            try
            {
                bool has = row != null;
                _apply.Enabled = has && !row.UnsupportedValue && (row.HasAc || row.HasDc);
                _defaults.Enabled = has && !row.Legacy && (row.HasAcDefault || row.HasDcDefault);
                _hidden.Enabled = has && row.AttributesKnown && !row.Legacy && row.Profile == Guid.Empty;
                _description.Text = has ? (row.Description ?? "") + (row.Profile != Guid.Empty ? Ui.T("scenario.override.missing.values.use.daily.settings.changes.take.effec") : "") : "";
                if (!has)
                {
                    _meta.Text = "";
                    _acList.Visible = _dcList.Visible = false;
                    _acBox.Visible = _dcBox.Visible = true;
                    _acBox.Text = _dcBox.Text = "";
                    _acBox.Enabled = _dcBox.Enabled = false;
                    _hidden.Checked = false;
                    return;
                }
                _hidden.Checked = row.Hidden;
                StringBuilder meta = new StringBuilder();
                if (row.Legacy) meta.Append(Ui.T("legacy.field"));
                else meta.Append(row.Setting.ToString("B"));
                if (row.HasRange)
                {
                    meta.Append(Ui.T("range"));
                    meta.Append(row.Min.ToString(CultureInfo.InvariantCulture));
                    meta.Append(" – ");
                    meta.Append(row.Max.ToString(CultureInfo.InvariantCulture));
                    if (row.Increment > 1)
                    {
                        meta.Append(Ui.T("increment"));
                        meta.Append(row.Increment.ToString(CultureInfo.InvariantCulture));
                    }
                }
                if (row.Choices.Count == 0 && (row.HasAc || row.HasDc))
                    meta.Append(Ui.T("no.predefined.choices.edit.the.raw.value"));
                _meta.Text = meta.ToString();
                BindEditor(_acList, _acBox, row, row.HasAc, row.Ac);
                BindEditor(_dcList, _dcBox, row, row.HasDc, row.Dc);
            }
            finally { _filling = false; UpdatePendingChanges(); }
        }

        void UpdatePendingChanges()
        {
            if (_filling) return;
            SettingRow row = SelectedRow();
            uint ac, dc;
            bool acChanged = row != null && !row.UnsupportedValue && row.HasAc
                && (!TryReadEditor(_acList, _acBox, true, row.Ac, out ac) || ac != row.Ac);
            bool dcChanged = row != null && !row.UnsupportedValue && row.HasDc
                && (!TryReadEditor(_dcList, _dcBox, true, row.Dc, out dc) || dc != row.Dc);
            bool hiddenChanged = row != null && _hidden.Enabled && _hidden.Checked != row.Hidden;
            bool changed = acChanged || dcChanged || hiddenChanged;
            _pending.Text = changed
                ? Ui.T("changes.have.not.been.written.click.write.values.to.save")
                : Ui.T("no.pending.changes");
            _pending.BackColor = changed ? _theme.Pending : _theme.Surface;
            _pending.ForeColor = changed ? _theme.PendingText : _theme.Text;
            _acBox.BackColor = _acList.BackColor = acChanged ? _theme.Pending : _theme.Input;
            _dcBox.BackColor = _dcList.BackColor = dcChanged ? _theme.Pending : _theme.Input;
            _acBox.ForeColor = _acList.ForeColor = acChanged ? _theme.PendingText : _theme.InputText;
            _dcBox.ForeColor = _dcList.ForeColor = dcChanged ? _theme.PendingText : _theme.InputText;
            _hidden.BackColor = hiddenChanged ? _theme.Pending : _theme.Surface;
            _hidden.ForeColor = hiddenChanged ? _theme.PendingText : _theme.Text;
            _apply.Text = changed
                ? Ui.T("write.values.pending")
                : Ui.T("write.values");
            _apply.UseVisualStyleBackColor = !changed && !_theme.Dark;
            _apply.BackColor = changed ? _theme.Pending : _theme.Surface;
            _apply.ForeColor = changed ? _theme.PendingText : _theme.Text;
            Text = (FixedCatalog.Modern ? Ui.Title : Ui.Title + Ui.T("legacy.power.schemes.windows.server.2003"))
                + (changed ? Ui.T("unsaved.changes") : "");
            ArrangeActions();
        }

        static void BindEditor(ComboBox list, TextBox box, SettingRow row, bool has, uint value)
        {
            list.Items.Clear();
            if (!has || row.UnsupportedValue)
            {
                list.Visible = false;
                box.Visible = true;
                box.Text = row.Profile != Guid.Empty && !has ? Ui.T("not.overridden") : "";
                box.Enabled = false;
                return;
            }
            if (row.Choices.Count > 0)
            {
                box.Visible = false;
                list.Visible = true;
                list.Enabled = true;
                int select = -1;
                for (int i = 0; i < row.Choices.Count; i++)
                {
                    list.Items.Add(row.Choices[i]);
                    if (row.Choices[i].Value == value) select = i;
                }
                if (select < 0)
                {
                    ValueChoice extra = new ValueChoice();
                    extra.Value = value;
                    extra.Name = Ui.T("current.unlisted.value");
                    list.Items.Insert(0, extra);
                    select = 0;
                }
                list.SelectedIndex = select;
            }
            else
            {
                list.Visible = false;
                box.Visible = true;
                box.Enabled = true;
                box.Text = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        void OnUnhideAll()
        {
            if (SceneMode || !FixedCatalog.Modern) return;
            SchemeItem scheme = _schemes.SelectedItem as SchemeItem;
            if (scheme == null) return;
            int changed = 0, failed = 0;
            uint firstError = Native.Ok;
            UseWaitCursor = true;
            try
            {
                // Reload the complete daily catalog: search must not limit this operation.
                foreach (SettingRow row in FixedCatalog.LoadSettings(scheme))
                {
                    if (row.Legacy || !row.AttributesKnown || !row.Hidden) continue;
                    uint rc = FixedCatalog.WriteHidden(row, false);
                    if (rc == Native.Ok) changed++;
                    else { failed++; if (firstError == Native.Ok) firstError = rc; }
                }
                _dailyCache.Clear();
                _columnWidths.Clear();
                _processorTemplates = null;
                ReloadSettings();
                Report(Ui.T("unhidden") + changed.ToString(CultureInfo.InvariantCulture)
                    + Ui.T("failed") + failed.ToString(CultureInfo.InvariantCulture)
                    + (failed > 0 ? ". " + Explain(firstError) : ""));
            }
            catch (Exception ex) { Report(ex.Message); }
            finally { UseWaitCursor = false; }
        }

        void OnApply(bool useDefault)
        {
            if (_filling) return;
            SchemeItem scheme = (SceneMode ? _profileSchemes : _schemes).SelectedItem as SchemeItem;
            SettingRow row = SelectedRow();
            if (scheme == null || row == null) return;
            if (row.Profile != Guid.Empty) scheme = new SchemeItem { Guid = row.ProfileScheme };
            uint ac, dc;
            if (useDefault)
            {
                if (!row.HasAcDefault && !row.HasDcDefault) return;
                ac = row.HasAcDefault ? row.AcDefault : row.Ac;
                dc = row.HasDcDefault ? row.DcDefault : row.Dc;
            }
            else if (!TryReadEditor(_acList, _acBox, row.HasAc, row.Ac, out ac) || !TryReadEditor(_dcList, _dcBox, row.HasDc, row.Dc, out dc))
            {
                Report(Ui.T("enter.an.integer.from.0.to.4294967295"));
                return;
            }
            if (row.Profile != Guid.Empty && row.HasRange &&
                ((row.HasAc && (ac < row.Min || ac > row.Max || (row.Increment > 1 && (ac - row.Min) % row.Increment != 0))) ||
                 (row.HasDc && (dc < row.Min || dc > row.Max || (row.Increment > 1 && (dc - row.Min) % row.Increment != 0)))))
            {
                Report(Ui.T("value.is.outside.the.range.or.does.not.match.the.increment"));
                return;
            }
            uint rc = Native.Ok;
            if (row.HasAc) rc = FixedCatalog.WriteIndex(scheme, row, true, ac);
            if (rc == Native.Ok && row.HasDc) rc = FixedCatalog.WriteIndex(scheme, row, false, dc);
            if (rc != Native.Ok)
            {
                Report(Explain(rc));
                return;
            }
            if (_hidden.Enabled && _hidden.Checked != row.Hidden)
            {
                rc = FixedCatalog.WriteHidden(row, _hidden.Checked);
                if (rc != Native.Ok)
                {
                    Report(Ui.T("values.were.written.but.changing.the.hidden.flag.failed") + Explain(rc));
                    return;
                }
            }
            row.Ac = ac;
            row.Dc = dc;
            row.HasAc = row.HasAc || useDefault;
            RefreshRow(row);
            Report(Ui.T("wrote") + row.Name + Ui.T("text") + (row.Profile != Guid.Empty ? Ui.T("takes.effect.the.next.time.this.scenario.is.entered") : ""));
        }

        static bool TryReadEditor(ComboBox list, TextBox box, bool has, uint current, out uint value)
        {
            value = current;
            if (!has) return true;
            if (list.Visible)
            {
                ValueChoice choice = list.SelectedItem as ValueChoice;
                if (choice == null) return false;
                value = choice.Value;
                return true;
            }
            return uint.TryParse((box.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        void RefreshRow(SettingRow row)
        {
            _columnWidths.Clear();
            for (int i = 0; i < _grid.Items.Count; i++)
            {
                if (!object.ReferenceEquals(_grid.Items[i].Tag, row)) continue;
                ListViewItem item = _grid.Items[i];
                item.SubItems[2].Text = row.AcText;
                item.SubItems[3].Text = row.DcText;
                item.SubItems[4].Text = row.AcDefaultText;
                item.SubItems[5].Text = row.DcDefaultText;
                item.SubItems[6].Text = row.AttributesKnown ? (row.Hidden ? Ui.T("yes") : Ui.T("no")) : "";
                item.SubItems[7].Text = row.Units ?? "";
                break;
            }
            ShowSelected();
        }

        void OnActivate()
        {
            SchemeItem scheme = _schemes.SelectedItem as SchemeItem;
            if (scheme == null) return;
            uint rc = FixedCatalog.Activate(scheme);
            if (rc != Native.Ok) { Report(Explain(rc)); return; }
            Report(Ui.T("the.active.power.plan.was.changed"));
            ReloadSchemes(true);
        }

        void OnExport()
        {
            if (!FixedCatalog.Modern)
            {
                Report(Ui.T("legacy.power.schemes.cannot.be.exported.in.this.format"));
                return;
            }
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = Ui.T("power.backup.xml.xml");
            dialog.FileName = "PowerBackup.xml";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                int count = PlanFile.Export(dialog.FileName, _rows);
                Report(Ui.T("exported") + count.ToString(CultureInfo.InvariantCulture) + Ui.T("settings"));
            }
            catch (Exception ex) { Report(ex.Message); }
        }

        void OnImport()
        {
            if (!FixedCatalog.Modern)
            {
                Report(Ui.T("legacy.power.schemes.cannot.be.imported.from.this.format"));
                return;
            }
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = Ui.T("power.backup.xml.xml");
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                int written;
                int skipped;
                uint error;
                int failed = PlanFile.Import(dialog.FileName, out written, out skipped, out error);
                _dailyCache.Clear();
                _columnWidths.Clear();
                _processorTemplates = null;
                ReloadSettings();
                if (error != Native.Ok)
                    Report(Ui.T("import.stopped") + Explain(error));
                else
                    Report(Ui.T("imported.wrote")
                        + written.ToString(CultureInfo.InvariantCulture)
                        + Ui.T("skipped")
                        + skipped.ToString(CultureInfo.InvariantCulture)
                        + Ui.T("failed.08926bbc")
                        + failed.ToString(CultureInfo.InvariantCulture) + ".");
            }
            catch (Exception ex) { Report(ex.Message); }
        }

        void CopyGuid()
        {
            SettingRow row = SelectedRow();
            if (row == null || row.Legacy) return;
            try
            {
                Clipboard.SetText(row.Setting.ToString("B"));
                Report(Ui.T("copied.the.setting.guid"));
            }
            catch (Exception ex) { Report(ex.Message); }
        }

        void OpenRegistry()
        {
            SchemeItem scheme = (SceneMode ? _profileSchemes : _schemes).SelectedItem as SchemeItem;
            SettingRow row = SelectedRow();
            if (scheme == null || row == null || row.Legacy || scheme.Legacy) return;
            string sub = row.Subgroup == Guid.Empty ? "" : row.Subgroup.ToString("B") + "\\";
            string path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\"
                + scheme.Guid.ToString("B") + "\\" + sub + row.Setting.ToString("B");
            if (row.Profile != Guid.Empty) path = "HKEY_LOCAL_MACHINE\\" + ProfileCatalog.Path(row, row.Profile, row.ProfileScheme);
            try
            {
                string prefix = Ui.T("computer");
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                {
                    if (key != null) key.SetValue("LastKey", prefix + "\\" + path);
                }
                Process.Start("regedit.exe");
            }
            catch (Exception ex) { Report(ex.Message); }
        }

        void RelaunchAdmin()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Application.ExecutablePath;
                info.UseShellExecute = true;
                info.Verb = "runas";
                Process.Start(info);
                Close();
            }
            catch (Exception ex) { Report(ex.Message); }
        }

        static string Explain(uint rc)
        {
            if (rc == Native.AccessDenied)
                return Ui.T("access.denied.use.restart.as.administrator.and.try.again");
            return Native.ErrorText(rc);
        }

        void Report(string text)
        {
            _status.Text = text ?? "";
        }
    }

    /// <summary>
    /// Load path that passes real GUID pointers. Catalog.LoadModern* used a stub and is not called.
    /// </summary>
    static class FixedCatalog
    {
        public static bool Modern;

        public static List<SchemeItem> LoadSchemes()
        {
            Modern = Native.ModernAvailable();
            if (!Modern) return InvokeLegacySchemes();
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
            if (scheme.Legacy) return InvokeLegacySettings(scheme.LegacyIndex);
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
            if (scheme.Legacy || row.Legacy) return Catalog.WriteIndex(scheme, row, ac, value);
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
            return Catalog.WriteHidden(row, hidden);
        }

        public static uint Activate(SchemeItem scheme)
        {
            return Catalog.Activate(scheme);
        }

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

        static void AddRegistrySettings(List<SettingRow> rows, Guid scheme)
        {
            RegistryKey root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings");
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
                        if (string.IsNullOrEmpty(groupName)) groupName = groupId.ToString("B");
                        string[] settings = groupKey.GetSubKeyNames();
                        for (int s = 0; s < settings.Length; s++)
                        {
                            Guid settingId;
                            if (!TryParseGuid(settings[s], out settingId)) continue;
                            RegistryKey settingKey = groupKey.OpenSubKey(settings[s]);
                            if (settingKey == null) continue;
                            using (settingKey)
                            {
                                SettingRow row = null;
                                for (int i = 0; i < rows.Count; i++)
                                {
                                    if (rows[i].Setting == settingId) { row = rows[i]; break; }
                                }
                                if (row == null)
                                {
                                    row = ReadSetting(scheme, groupId, groupName, settingId);
                                    rows.Add(row);
                                }
                                object attr = settingKey.GetValue("Attributes");
                                if (attr is int)
                                {
                                    row.AttributesKnown = true;
                                    row.Hidden = (((uint)(int)attr) & Native.HideAttribute) != 0;
                                }
                                ReadRegistryChoices(settingKey, row);
                            }
                        }
                    }
                }
            }
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
                uint value;
                if (Native.PowerReadACValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                { row.HasAc = true; row.Ac = value; }
                if (Native.PowerReadDCValueIndex(IntPtr.Zero, schemePtr.Ptr, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                { row.HasDc = true; row.Dc = value; }
                if (Native.PowerReadACDefaultIndex(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                { row.HasAcDefault = true; row.AcDefault = value; }
                if (Native.PowerReadDCDefaultIndex(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out value) == Native.Ok)
                { row.HasDcDefault = true; row.DcDefault = value; }
                ApplySchemeDefaults(scheme, group, setting, row);
                uint min, max, inc;
                if (Native.PowerReadValueMin(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out min) == Native.Ok
                    && Native.PowerReadValueMax(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out max) == Native.Ok)
                {
                    row.HasRange = true;
                    row.Min = min;
                    row.Max = max;
                    row.Increment = Native.PowerReadValueIncrement(IntPtr.Zero, groupPtr.Ptr, settingPtr.Ptr, out inc) == Native.Ok ? inc : 1;
                }
                uint attributes = Native.PowerReadSettingAttributes(groupPtr.Ptr, settingPtr.Ptr);
                row.AttributesKnown = true;
                row.Hidden = (attributes & Native.HideAttribute) != 0;
                uint index = 0;
                while (index < 512)
                {
                    uint size = 4;
                    IntPtr buf = Marshal.AllocHGlobal(4);
                    try
                    {
                        uint rc = Native.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, groupPtr.Ptr, Native.AccessPossible, index, buf, ref size);
                        if (rc != Native.Ok) break;
                        ValueChoice choice = new ValueChoice();
                        choice.Value = (uint)Marshal.ReadInt32(buf);
                        choice.Name = PossibleName(groupPtr.Ptr, settingPtr.Ptr, choice.Value);
                        if (string.IsNullOrEmpty(choice.Name))
                            choice.Name = PossibleName(groupPtr.Ptr, settingPtr.Ptr, index);
                        row.Choices.Add(choice);
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                    index++;
                }
            }
            return row;
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

        static List<SchemeItem> InvokeLegacySchemes()
        {
            // Reuse Catalog by forcing its private loader through the public LoadSchemes only when Modern is false.
            // Catalog.LoadSchemes checks ModernAvailable again. That is correct for legacy.
            return Catalog.LoadSchemes();
        }

        static List<SettingRow> InvokeLegacySettings(uint index)
        {
            SchemeItem fake = new SchemeItem();
            fake.Legacy = true;
            fake.LegacyIndex = index;
            // Catalog.LoadSettings uses legacy branch only when scheme.Legacy is true, and does not depend on Modern.
            return Catalog.LoadSettings(fake);
        }
    }

    static class PlanFile
    {
        public static int Export(string path, List<SettingRow> rows)
        {
            List<SchemeItem> schemes = FixedCatalog.LoadSchemes();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new UTF8Encoding(true);
            int count = 0;
            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("root");
                Guid openGroup = Guid.Empty;
                bool groupOpen = false;
                for (int i = 0; i < rows.Count; i++)
                {
                    SettingRow row = rows[i];
                    if (row.Legacy) continue;
                    if (!groupOpen || row.Subgroup != openGroup)
                    {
                        if (groupOpen) writer.WriteEndElement();
                        writer.WriteStartElement("subgroup");
                        writer.WriteAttributeString("guid", row.Subgroup.ToString("D"));
                        openGroup = row.Subgroup;
                        groupOpen = true;
                    }
                    writer.WriteStartElement("setting");
                    writer.WriteAttributeString("guid", row.Setting.ToString("D"));
                    writer.WriteAttributeString("name", row.Name ?? "");
                    for (int s = 0; s < schemes.Count; s++)
                    {
                        if (schemes[s].Legacy) continue;
                        uint value;
                        if (FixedCatalog.TryReadIndex(schemes[s], row, true, out value))
                        {
                            writer.WriteStartElement("acindex");
                            writer.WriteAttributeString("scheme", schemes[s].Guid.ToString("D"));
                            writer.WriteAttributeString("value", value.ToString(CultureInfo.InvariantCulture));
                            writer.WriteEndElement();
                        }
                    }
                    for (int s = 0; s < schemes.Count; s++)
                    {
                        if (schemes[s].Legacy) continue;
                        uint value;
                        if (FixedCatalog.TryReadIndex(schemes[s], row, false, out value))
                        {
                            writer.WriteStartElement("dcindex");
                            writer.WriteAttributeString("scheme", schemes[s].Guid.ToString("D"));
                            writer.WriteAttributeString("value", value.ToString(CultureInfo.InvariantCulture));
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                    count++;
                }
                if (groupOpen) writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            return count;
        }

        public static int Import(string path, out int written, out int skipped, out uint error)
        {
            written = 0;
            skipped = 0;
            error = Native.Ok;
            int failed = 0;
            List<SchemeItem> schemes = FixedCatalog.LoadSchemes();
            XmlDocument document = new XmlDocument();
            document.Load(path);
            XmlNodeList settings = document.SelectNodes("/root/subgroup/setting");
            if (settings == null) return 0;
            for (int i = 0; i < settings.Count; i++)
            {
                XmlElement setting = settings[i] as XmlElement;
                if (setting == null) continue;
                XmlElement subgroup = setting.ParentNode as XmlElement;
                Guid subgroupId;
                Guid settingId;
                if (subgroup == null || !Guid.TryParse(subgroup.GetAttribute("guid"), out subgroupId) || !Guid.TryParse(setting.GetAttribute("guid"), out settingId))
                {
                    failed++;
                    continue;
                }
                SettingRow row = new SettingRow();
                row.Subgroup = subgroupId;
                row.Setting = settingId;
                XmlNodeList values = setting.ChildNodes;
                for (int n = 0; n < values.Count; n++)
                {
                    XmlElement index = values[n] as XmlElement;
                    if (index == null) continue;
                    bool ac = index.Name == "acindex";
                    bool dc = index.Name == "dcindex";
                    if (!ac && !dc) continue;
                    Guid schemeId;
                    uint value;
                    if (!Guid.TryParse(index.GetAttribute("scheme"), out schemeId) || !uint.TryParse(index.GetAttribute("value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    {
                        failed++;
                        continue;
                    }
                    SchemeItem scheme = null;
                    for (int s = 0; s < schemes.Count; s++)
                    {
                        if (!schemes[s].Legacy && schemes[s].Guid == schemeId) { scheme = schemes[s]; break; }
                    }
                    if (scheme == null)
                    {
                        skipped++;
                        continue;
                    }
                    uint rc = FixedCatalog.WriteIndex(scheme, row, ac, value);
                    if (rc == Native.Ok) written++;
                    else
                    {
                        failed++;
                        error = rc;
                        if (rc == Native.AccessDenied) return failed;
                    }
                }
            }
            return failed;
        }
    }
}
