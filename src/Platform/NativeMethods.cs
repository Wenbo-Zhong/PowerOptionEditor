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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerOptionEditor
{
    static class Native
    {
        public const uint Ok = 0;
        public const uint MoreData = 234;
        public const uint AccessDenied = 5;

        public const uint AccessPossible = 4;
        public const uint AccessScheme = 16;
        public const uint AccessSubgroup = 17;
        public const uint AccessSetting = 18;
        public const uint HideAttribute = 1;

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
}
