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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;

namespace PowerOptionEditor
{
    sealed partial class MainForm
    {
        void RunCatalogRead<T>(string loadingMessage, Func<T> read, Action<T> show)
        {
            _catalogLoading = true;
            _bar.Enabled = _editorRow.Enabled = _actionRow.Enabled = false;
            Report(loadingMessage);

            // The worker reads the catalog. Completion runs on the WinForms thread.
            BackgroundWorker worker = new BackgroundWorker();
            CultureInfo culture = CultureInfo.CurrentCulture;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                System.Threading.Thread thread = System.Threading.Thread.CurrentThread;
                CultureInfo previous = thread.CurrentCulture;
                try
                {
                    thread.CurrentCulture = culture;
                    e.Result = read();
                }
                finally { thread.CurrentCulture = previous; }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                worker.Dispose();
                if (IsDisposed || Disposing) return;
                _catalogLoading = false;
                _bar.Enabled = _editorRow.Enabled = _actionRow.Enabled = true;
                if (e.Error != null) { Report(e.Error.Message); return; }
                try { show((T)e.Result); }
                catch (Exception ex) { Report(ex.Message); }
            };
            worker.RunWorkerAsync();
        }

        void InvalidateCatalog()
        {
            _dailyCache.Clear();
            _columnWidths.Clear();
        }

        void ReloadSchemes(bool keep)
        {
            InvalidateCatalog();
            _profilesLoaded = false;
            SchemeItem previous = keep ? _schemes.SelectedItem as SchemeItem : null;
            RunCatalogRead(Ui.T("loading.power.plans"), PowerCatalog.LoadSchemes, schemes =>
            {
                int index = 0;
                for (int i = 0; i < schemes.Count; i++)
                    if (schemes[i].Active) index = i;
                if (previous != null)
                {
                    int match = schemes.FindIndex(item => item.Legacy == previous.Legacy
                        && (item.Legacy ? item.LegacyIndex == previous.LegacyIndex : item.Guid == previous.Guid));
                    if (match >= 0) index = match;
                }

                _filling = true;
                try
                {
                    _schemes.Items.Clear();
                    _schemes.Items.AddRange(schemes.ToArray());
                    if (schemes.Count > 0) _schemes.SelectedIndex = index;
                }
                finally { _filling = false; }

                if (SceneMode) ReloadProfiles();
                else ReloadSettings(schemes.Count == 0 ? Ui.T("no.power.plans.were.found") : null);
            });
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
            ClearSettings();
            if (_profilesLoaded) { ReloadProfileSchemes(); return; }

            ProfileItem previous = _profiles.SelectedItem as ProfileItem;
            RunCatalogRead(Ui.T("loading.settings"), () =>
            {
                List<ProfileItem> profiles = ProfileCatalog.LoadProfiles();
                return profiles.FindAll(ProfileCatalog.HasAdjustableTargets);
            }, profiles =>
            {
                _filling = true;
                try
                {
                    _profiles.Items.Clear();
                    _profiles.Items.AddRange(profiles.ToArray());
                    int selected = previous == null ? -1 : profiles.FindIndex(item => item.Guid == previous.Guid);
                    if (profiles.Count > 0) _profiles.SelectedIndex = Math.Max(0, selected);
                    _profilesLoaded = true;
                }
                finally { _filling = false; }
                ReloadProfileSchemes();
            });
        }

        void ReloadProfileSchemes()
        {
            if (_filling || _catalogLoading || !SceneMode) return;
            SaveGridPosition();
            ClearSettings();
            ProfileItem profile = _profiles.SelectedItem as ProfileItem;
            SchemeItem daily = _schemes.SelectedItem as SchemeItem;
            SchemeItem previous = _profileSchemes.SelectedItem as SchemeItem ?? daily;
            List<SchemeItem> known = new List<SchemeItem>();
            foreach (SchemeItem item in _schemes.Items) known.Add(item);

            RunCatalogRead(Ui.T("loading.settings"), () => profile == null
                ? new List<SchemeItem>() : ProfileCatalog.LoadSchemes(profile, daily, known), schemes =>
            {
                _filling = true;
                try
                {
                    _profileSchemes.Items.Clear();
                    _profileSchemes.Items.AddRange(schemes.ToArray());
                    int selected = previous == null ? -1 : schemes.FindIndex(item => item.Guid == previous.Guid);
                    if (schemes.Count > 0) _profileSchemes.SelectedIndex = Math.Max(0, selected);
                }
                finally { _filling = false; }
                ReloadSettings();
            });
        }

        sealed class LoadedSettings
        {
            public List<SettingRow> Catalog;
            public List<SettingRow> Rows;
            public List<string> Unavailable;
        }

        void ClearSettings()
        {
            _boundViewKey = null;
            _rows.Clear();
            _filling = true;
            try { _grid.Items.Clear(); }
            finally { _filling = false; }
            ShowSelected();
        }

        void ReloadSettings(string completionMessage = null)
        {
            if (_filling || _catalogLoading) return;
            SaveGridPosition();
            ClearSettings();

            SchemeItem scheme = _schemes.SelectedItem as SchemeItem;
            bool scene = SceneMode;
            ProfileItem profile = scene ? _profiles.SelectedItem as ProfileItem : null;
            if (scheme == null || (scene && profile == null))
            {
                ShowLoadedSettings(null, new List<SettingRow>(), completionMessage, "");
                return;
            }

            string dailyKey = "daily:" + (scheme.Legacy
                ? scheme.LegacyIndex.ToString(CultureInfo.InvariantCulture) : scheme.Guid.ToString());
            string viewKey = scene ? "profile:" + profile.Guid.ToString() : dailyKey;
            List<SettingRow> cached;
            _dailyCache.TryGetValue(dailyKey, out cached);
            if (!scene && cached != null)
            {
                ShowLoadedSettings(viewKey, cached, completionMessage, "");
                return;
            }

            List<SchemeItem> targets = new List<SchemeItem>();
            if (scene)
                foreach (SchemeItem target in _profileSchemes.Items) targets.Add(target);

            RunCatalogRead(Ui.T("loading.settings"), () => ReadSettings(scheme, cached, scene, profile, targets), result =>
            {
                string hideStatus = result.Unavailable == null ? ""
                    : HideUnavailableText(PowerCatalog.HideUnavailable(result.Unavailable));
                _dailyCache[dailyKey] = result.Catalog;
                if (scene)
                {
                    // Overrides are read live and may have changed outside the editor.
                    string prefix = viewKey + "|";
                    foreach (string key in new List<string>(_columnWidths.Keys))
                        if (key.StartsWith(prefix, StringComparison.Ordinal)) _columnWidths.Remove(key);
                }
                ShowLoadedSettings(viewKey, result.Rows, completionMessage, hideStatus);
            });
        }

        static LoadedSettings ReadSettings(SchemeItem scheme, List<SettingRow> cached, bool scene,
            ProfileItem profile, List<SchemeItem> targets)
        {
            LoadedSettings result = new LoadedSettings();
            result.Catalog = cached ?? PowerCatalog.LoadSettings(scheme);
            if (cached == null && !scheme.Legacy)
                result.Unavailable = PowerCatalog.FindUnavailable(scheme.Guid, result.Catalog);
            result.Rows = result.Catalog;
            if (scene)
                result.Rows = LoadProfileSettings(profile, targets, result.Catalog);
            return result;
        }

        static List<SettingRow> LoadProfileSettings(ProfileItem profile, List<SchemeItem> targets, List<SettingRow> catalog)
        {
            List<SettingRow> rows = new List<SettingRow>();
            List<SettingRow> templates = catalog.FindAll(row => row.Subgroup == ProfileCatalog.Processor);
            foreach (SchemeItem target in targets)
            {
                foreach (SettingRow row in ProfileCatalog.LoadSettings(profile, target, templates))
                {
                    if (!row.HasAc && !row.HasDc) continue;
                    row.ProfileScheme = target.Guid;
                    if (targets.Count > 1) row.GroupName += " — " + target.Name;
                    rows.Add(row);
                }
            }
            return rows;
        }

        void ShowLoadedSettings(string viewKey, List<SettingRow> rows, string completionMessage, string hideStatus)
        {
            _boundViewKey = viewKey;
            _rows.AddRange(rows);
            BindGrid();
            Report(completionMessage ?? (Ui.T("loaded") + _rows.Count.ToString(CultureInfo.InvariantCulture) + Ui.T("settings")
                + (PowerCatalog.Modern ? "" : Ui.T("this.system.has.no.power.interface.from.vista.onward.so.legacy.schemes"))
                + hideStatus));
        }
    }
}
