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
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PowerOptionEditor
{
    sealed partial class MainForm : Form
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
        readonly SelectableTextBox _description = new SelectableTextBox();
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
        readonly Dictionary<string, List<SettingRow>> _dailyCache = new Dictionary<string, List<SettingRow>>();
        readonly Dictionary<string, int[]> _columnWidths = new Dictionary<string, int[]>();
        readonly Dictionary<string, GridPosition> _gridPositions = new Dictionary<string, GridPosition>();
        string _boundViewKey;
        bool _profilesLoaded;
        bool _catalogLoading;
        bool _layoutQueued;

        sealed class GridPosition
        {
            public Guid Setting, Scheme;
            public int TopIndex;
        }
        readonly Panel _bar = new Panel();
        readonly Panel _editorRow = new Panel();
        readonly Panel _actionRow = new Panel();
        Label _acCaption;
        Label _dcCaption;
        bool _filling;
        bool _arrangingBar;
        bool _arrangingEditors;
        bool _arrangingActions;

        public MainForm()
        {
            InitializeWindow();
            InitializePageLayout();
            InitializeToolbarHost();
            InitializeTabs();
            InitializeSelectors();
            InitializeToolbarActions();
            InitializeSystemActions();
            ArrangeBar();
            InitializeGrid();
            TableLayoutPanel detail = InitializeDetails();
            InitializeEditors(detail);
            InitializePendingChanges(detail);
            InitializeActions(detail);
            InitializeStatus();
            InitializeWindowEvents();
        }

        void SaveGridPosition()
        {
            if (_boundViewKey == null || _grid.Items.Count == 0) return;
            SettingRow row = SelectedRow();
            if (row == null) return;
            _gridPositions[_boundViewKey] = new GridPosition { Setting = row.Setting, Scheme = row.ProfileScheme,
                TopIndex = _grid.TopItem == null ? 0 : _grid.TopItem.Index };
        }

        static string HideUnavailableText(PowerCatalog.UnavailableHideResult hidden)
        {
            string text = "";
            if (hidden.Hidden > 0)
                text += Ui.T("hid.unavailable.prefix") + hidden.Hidden.ToString(CultureInfo.InvariantCulture) + Ui.T("hid.unavailable.suffix");
            if (hidden.AccessDenied)
                text += Ui.T("could.not.hide.unavailable");
            return text;
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
                ListViewItem[] items = new ListViewItem[view.Count];
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
                    items[i] = item;
                }
                _grid.Items.AddRange(items);
                FitColumns(items);
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
                _apply.Enabled = has && (row.HasAc || row.HasDc);
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
            bool acChanged = row != null && row.HasAc
                && (!TryReadEditor(_acList, _acBox, true, row.Ac, out ac) || ac != row.Ac);
            bool dcChanged = row != null && row.HasDc
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
            Text = (PowerCatalog.Modern ? Ui.Title : Ui.Title + Ui.T("legacy.power.schemes.windows.server.2003"))
                + (changed ? Ui.T("unsaved.changes") : "");
            ArrangeActions();
        }

        static void BindEditor(ComboBox list, TextBox box, SettingRow row, bool has, uint value)
        {
            list.Items.Clear();
            if (!has)
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
            if (SceneMode || !PowerCatalog.Modern) return;
            SchemeItem scheme = _schemes.SelectedItem as SchemeItem;
            if (scheme == null) return;
            int changed = 0, failed = 0;
            uint firstError = Native.Ok;
            UseWaitCursor = true;
            try
            {
                // Reload the complete daily catalog: search must not limit this operation.
                foreach (SettingRow row in PowerCatalog.LoadSettings(scheme))
                {
                    if (row.Legacy || !row.AttributesKnown || !row.Hidden) continue;
                    uint rc = PowerCatalog.WriteHidden(row, false);
                    if (rc == Native.Ok) changed++;
                    else { failed++; if (firstError == Native.Ok) firstError = rc; }
                }
                InvalidateCatalog();
                ReloadSettings(Ui.T("unhidden") + changed.ToString(CultureInfo.InvariantCulture)
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
            InvalidateCatalog();
            uint rc = Native.Ok;
            if (row.HasAc) rc = PowerCatalog.WriteIndex(scheme, row, true, ac);
            if (rc == Native.Ok && row.HasDc) rc = PowerCatalog.WriteIndex(scheme, row, false, dc);
            if (rc != Native.Ok)
            {
                Report(Explain(rc));
                return;
            }
            if (_hidden.Enabled && _hidden.Checked != row.Hidden)
            {
                rc = PowerCatalog.WriteHidden(row, _hidden.Checked);
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
            uint rc = PowerCatalog.Activate(scheme);
            if (rc != Native.Ok) { Report(Explain(rc)); return; }
            Report(Ui.T("the.active.power.plan.was.changed"));
            ReloadSchemes(true);
        }

        void OnExport()
        {
            if (!PowerCatalog.Modern)
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
            if (!PowerCatalog.Modern)
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
                InvalidateCatalog();
                if (error != Native.Ok)
                    ReloadSettings(Ui.T("import.stopped") + Explain(error));
                else
                    ReloadSettings(Ui.T("imported.wrote")
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
            SettingRow row = SelectedRow();
            SchemeItem scheme = _schemes.SelectedItem as SchemeItem;
            if (row == null || row.Legacy || (row.Profile == Guid.Empty && (scheme == null || scheme.Legacy))) return;
            try
            {
                string path;
                bool definition = false;
                if (row.Profile != Guid.Empty)
                {
                    path = ProfileCatalog.Path(row, row.Profile, row.ProfileScheme);
                }
                else
                {
                    string settingPath = (row.Subgroup == Guid.Empty ? "" : row.Subgroup.ToString("D") + "\\")
                        + row.Setting.ToString("D");
                    path = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\"
                        + scheme.Guid.ToString("D") + "\\" + settingPath;
                    using (RegistryKey values = Registry.LocalMachine.OpenSubKey(path))
                        definition = values == null;
                    if (definition)
                        path = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\" + settingPath;
                }
                using (RegistryKey target = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (target == null)
                    {
                        Report(Ui.T("registry.key.missing"));
                        return;
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                {
                    string previous = key.GetValue("LastKey") as string;
                    string prefix = Ui.T("computer");
                    if (!string.IsNullOrEmpty(previous))
                    {
                        string root = previous.Split('\\')[0];
                        if (!string.IsNullOrEmpty(root) && root != "." && !root.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase))
                            prefix = root;
                    }
                    key.SetValue("LastKey", prefix + @"\HKEY_LOCAL_MACHINE\" + path);
                }
                // An existing Regedit window does not reread LastKey.
                Process.Start(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "regedit.exe"), "/m");
                Report(definition ? Ui.T("registry.definition.opened") : "");
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
}
