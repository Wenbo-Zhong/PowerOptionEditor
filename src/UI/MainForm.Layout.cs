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
using System.Windows.Forms;

namespace PowerOptionEditor
{
    sealed partial class MainForm
    {
        void InitializeWindow()
        {
            Text = Ui.Title;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Dpi.S(1100), Dpi.S(720));
            MinimumSize = new Size(Dpi.S(860), Dpi.S(560));
        }

        void InitializePageLayout()
        {
            TableLayoutPanel root = _pageContent;
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(Dpi.S(8));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(198)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(24)));
        }

        void InitializeToolbarHost()
        {
            _bar.Dock = DockStyle.Top;
            _bar.AutoSize = false;
            _bar.SizeChanged += delegate { ArrangeBar(); };
            _bar.Margin = new Padding(0, 0, 0, Dpi.S(6));
            _pageContent.Controls.Add(_bar, 0, 0);
        }

        void InitializeTabs()
        {
            _mode.TabPages.Add(Ui.T("daily"));
            _mode.TabPages.Add(Ui.T("scenarios"));
            _mode.Dock = DockStyle.Fill;
            Controls.Add(_mode);
            _mode.TabPages[0].Controls.Add(_pageContent);
            // Only block the scenario page on legacy systems; disabling the
            // container would also disable every daily setting inside it.
            _mode.Selecting += delegate(object sender, TabControlCancelEventArgs e)
            {
                if (_catalogLoading || (e.TabPageIndex == 1 && !PowerCatalog.Modern)) e.Cancel = true;
                else SaveGridPosition();
            };
            _mode.SelectedIndexChanged += delegate { ReloadProfiles(); };
        }

        void InitializeSelectors()
        {
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
        }

        void InitializeToolbarActions()
        {
            Button activate = MakeButton(Ui.T("set.active"));
            activate.Click += delegate { OnActivate(); };
            AddBar(activate);

            Button refresh = MakeButton(Ui.T("refresh"));
            refresh.Click += delegate { ReloadSchemes(true); };
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
        }

        void InitializeSystemActions()
        {
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
        }

        void InitializeGrid()
        {
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
            _pageContent.Controls.Add(_grid, 0, 1);
        }

        TableLayoutPanel InitializeDetails()
        {
            TableLayoutPanel detail = new TableLayoutPanel();
            detail.Dock = DockStyle.Fill;
            detail.ColumnCount = 1;
            detail.RowCount = 5;
            detail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pageContent.Controls.Add(detail, 0, 2);

            _description.Multiline = true;
            _description.ReadOnly = true;
            _description.ShortcutsEnabled = true;
            _description.HideSelection = false;
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
            return detail;
        }

        void InitializeEditors(TableLayoutPanel detail)
        {
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
        }

        void InitializePendingChanges(TableLayoutPanel detail)
        {
            _pending.Dock = DockStyle.Fill;
            _pending.AutoSize = true;
            _pending.MinimumSize = new Size(0, Dpi.S(30));
            _pending.Font = new Font(Font, FontStyle.Bold);
            _pending.TextAlign = ContentAlignment.MiddleLeft;
            _pending.BorderStyle = BorderStyle.FixedSingle;
            _pending.Padding = new Padding(Dpi.S(4));
            _pending.Margin = new Padding(0, 0, 0, Dpi.S(4));
            detail.Controls.Add(_pending, 0, 3);
        }

        void InitializeActions(TableLayoutPanel detail)
        {
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
        }

        void InitializeStatus()
        {
            _status.Dock = DockStyle.Fill;
            _status.AutoEllipsis = true;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _pageContent.Controls.Add(_status, 0, 3);
        }

        void InitializeWindowEvents()
        {
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

            Load += delegate
            {
                RefreshTheme();
                ArrangeBar();
                ArrangeEditors();
                ArrangeActions();
                Report(Ui.T("loading.power.plans"));
            };
            Shown += delegate { ReloadSchemes(false); };
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

        void AddColumn(string text)
        {
            ColumnHeader header = new ColumnHeader();
            header.Text = text;
            header.Width = Dpi.S(80);
            _grid.Columns.Add(header);
        }

        void FitColumns(ListViewItem[] items)
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
                HashSet<string> measured = new HashSet<string>(StringComparer.Ordinal);
                measured.Add(_grid.Columns[c].Text);
                foreach (ListViewItem item in items)
                {
                    string text = item.SubItems[c].Text;
                    if (!measured.Add(text)) continue;
                    int itemWidth = TextRenderer.MeasureText(text, _grid.Font).Width + pad;
                    if (itemWidth > width) width = itemWidth;
                }
                _grid.Columns[c].Width = width;
                widths[c] = width;
            }
            _columnWidths[key] = widths;
        }

    }
}
