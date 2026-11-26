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

namespace PowerOptionEditor
{
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
}
