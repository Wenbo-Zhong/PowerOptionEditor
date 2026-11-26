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
using System.Xml;

namespace PowerOptionEditor
{
    static class PlanFile
    {
        public static int Export(string path, List<SettingRow> rows)
        {
            List<SchemeItem> schemes = PowerCatalog.LoadSchemes();
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
                    ExportSetting(writer, row, schemes);
                    count++;
                }
                if (groupOpen) writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            return count;
        }

        static void ExportSetting(XmlWriter writer, SettingRow row, List<SchemeItem> schemes)
        {
            writer.WriteStartElement("setting");
            writer.WriteAttributeString("guid", row.Setting.ToString("D"));
            writer.WriteAttributeString("name", row.Name ?? "");
            ExportIndices(writer, row, schemes, true);
            ExportIndices(writer, row, schemes, false);
            writer.WriteEndElement();
        }

        static void ExportIndices(XmlWriter writer, SettingRow row, List<SchemeItem> schemes, bool ac)
        {
            for (int s = 0; s < schemes.Count; s++)
            {
                if (schemes[s].Legacy) continue;
                uint value;
                if (!PowerCatalog.TryReadIndex(schemes[s], row, ac, out value)) continue;
                writer.WriteStartElement(ac ? "acindex" : "dcindex");
                writer.WriteAttributeString("scheme", schemes[s].Guid.ToString("D"));
                writer.WriteAttributeString("value", value.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
        }

        public static int Import(string path, out int written, out int skipped, out uint error)
        {
            written = 0;
            skipped = 0;
            error = Native.Ok;
            int failed = 0;
            List<SchemeItem> schemes = PowerCatalog.LoadSchemes();
            XmlDocument document = new XmlDocument();
            document.Load(path);
            XmlNodeList settings = document.SelectNodes("/root/subgroup/setting");
            if (settings == null) return 0;
            for (int i = 0; i < settings.Count; i++)
            {
                XmlElement setting = settings[i] as XmlElement;
                if (setting == null) continue;
                SettingRow row = ReadSettingIdentity(setting);
                if (row == null)
                {
                    failed++;
                    continue;
                }
                if (!ImportIndices(setting, row, schemes, ref written, ref skipped, ref failed, ref error))
                    return failed;
            }
            return failed;
        }

        static SettingRow ReadSettingIdentity(XmlElement setting)
        {
            XmlElement subgroup = setting.ParentNode as XmlElement;
            Guid subgroupId;
            Guid settingId;
            if (subgroup == null || !Guid.TryParse(subgroup.GetAttribute("guid"), out subgroupId)
                || !Guid.TryParse(setting.GetAttribute("guid"), out settingId)) return null;
            return new SettingRow { Subgroup = subgroupId, Setting = settingId };
        }

        // Stop at access denial; the caller retains counts for all preceding values.
        static bool ImportIndices(XmlElement setting, SettingRow row, List<SchemeItem> schemes,
            ref int written, ref int skipped, ref int failed, ref uint error)
        {
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
                if (!Guid.TryParse(index.GetAttribute("scheme"), out schemeId)
                    || !uint.TryParse(index.GetAttribute("value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    failed++;
                    continue;
                }
                SchemeItem scheme = FindScheme(schemes, schemeId);
                if (scheme == null)
                {
                    skipped++;
                    continue;
                }
                uint rc = PowerCatalog.WriteIndex(scheme, row, ac, value);
                if (rc == Native.Ok) written++;
                else
                {
                    failed++;
                    error = rc;
                    if (rc == Native.AccessDenied) return false;
                }
            }
            return true;
        }

        static SchemeItem FindScheme(List<SchemeItem> schemes, Guid id)
        {
            for (int s = 0; s < schemes.Count; s++)
                if (!schemes[s].Legacy && schemes[s].Guid == id) return schemes[s];
            return null;
        }
    }
}
