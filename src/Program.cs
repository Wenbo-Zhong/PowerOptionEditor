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
using System.Windows.Forms;

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
            List<SchemeItem> schemes = PowerCatalog.LoadSchemes();
            sb.AppendLine(PowerCatalog.Modern ? "modern" : "legacy");
            sb.AppendLine("schemes=" + schemes.Count.ToString(CultureInfo.InvariantCulture));
            SchemeItem current = null;
            for (int i = 0; i < schemes.Count; i++)
            {
                if (schemes[i].Active) current = schemes[i];
                sb.AppendLine((schemes[i].Active ? "* " : "  ") + schemes[i].Name);
            }
            if (current == null && schemes.Count > 0) current = schemes[0];
            if (current == null) return sb.ToString();
            List<SettingRow> rows = PowerCatalog.LoadSettings(current);
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
}
