using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PowerOptionEditor
{
    sealed class AppTheme
    {
        public bool Dark { get; private set; }
        public bool HighContrast { get; private set; }
        bool _initialized;

        public Color Surface { get { return Dark ? Color.FromArgb(30, 32, 36) : SystemColors.Control; } }
        public Color Input { get { return Dark ? Color.FromArgb(42, 45, 50) : SystemColors.Window; } }
        public Color Text { get { return Dark ? Color.FromArgb(245, 246, 248) : SystemColors.ControlText; } }
        public Color InputText { get { return Dark ? Text : SystemColors.WindowText; } }
        public Color Pending { get { return HighContrast ? SystemColors.Highlight : Dark ? Color.FromArgb(83, 65, 30) : Color.FromArgb(255, 244, 214); } }
        public Color PendingText { get { return HighContrast ? SystemColors.HighlightText : Dark ? Color.FromArgb(255, 222, 156) : Color.FromArgb(112, 76, 20); } }

        public static bool SystemUsesDarkMode()
        {
            if (SystemInformation.HighContrast) return false;
            try
            {
                using (RegistryKey version = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                using (RegistryKey preferences = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    int build;
                    if (version == null || !int.TryParse(version.GetValue("CurrentBuildNumber") as string, out build) || build < 17763) return false;
                    return preferences != null && preferences.GetValue("AppsUseLightTheme") is int
                        && (int)preferences.GetValue("AppsUseLightTheme") == 0;
                }
            }
            catch (System.Security.SecurityException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.IO.IOException) { return false; }
        }

        public bool Apply(Form form)
        {
            return ApplyPalette(form, SystemUsesDarkMode(), SystemInformation.HighContrast);
        }

        bool ApplyPalette(Form form, bool dark, bool contrast)
        {
            dark = dark && !contrast;
            if (_initialized && dark == Dark && contrast == HighContrast) return false;
            Dark = dark;
            HighContrast = contrast;
            _initialized = true;
            ApplyControls(form);
            ApplyTitleBar(form.Handle);
            form.Invalidate(true);
            return true;
        }

        void ApplyControls(Control control)
        {
            bool input = control is TextBoxBase || control is ComboBox || control is ListView;
            control.BackColor = input ? Input : Surface;
            control.ForeColor = input ? InputText : Text;
            Button button = control as Button;
            if (button != null)
            {
                ThemeButton themed = button as ThemeButton;
                if (themed != null) themed.Dark = Dark;
                button.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                button.UseVisualStyleBackColor = !Dark;
                button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 58);
            }
            CheckBox check = control as CheckBox;
            if (check != null) check.UseVisualStyleBackColor = !Dark;
            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                combo.DrawItem -= DrawComboItem;
                combo.DrawMode = Dark ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
                if (Dark) combo.DrawItem += DrawComboItem;
            }
            ThemeTabs tabs = control as ThemeTabs;
            if (tabs != null) tabs.SetDark(Dark);
            ListView list = control as ListView;
            if (list != null)
            {
                list.DrawColumnHeader -= DrawHeader;
                list.DrawItem -= DrawItem;
                list.DrawSubItem -= DrawSubItem;
                list.OwnerDraw = Dark;
                list.GridLines = !Dark;
                if (Dark)
                {
                    list.DrawColumnHeader += DrawHeader;
                    list.DrawItem += DrawItem;
                    list.DrawSubItem += DrawSubItem;
                }
            }
            if (control is ListView || control is TextBoxBase)
            {
                try { SetWindowTheme(control.Handle, Dark ? "DarkMode_Explorer" : null, null); }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }
            foreach (Control child in control.Controls) ApplyControls(child);
        }

        void DrawComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Color.FromArgb(38, 88, 135) : combo.BackColor;
            using (SolidBrush fill = new SolidBrush(background)) e.Graphics.FillRectangle(fill, e.Bounds);
            string text = e.Index >= 0 && e.Index < combo.Items.Count ? combo.Items[e.Index].ToString() : combo.Text;
            Rectangle bounds = e.Bounds;
            bounds.Inflate(-3, 0);
            TextRenderer.DrawText(e.Graphics, text, combo.Font, bounds,
                combo.Enabled ? combo.ForeColor : Color.FromArgb(170, 175, 183),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }

        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(52, 57, 64))) e.Graphics.FillRectangle(brush, e.Bounds);
            using (Pen pen = new Pen(Color.FromArgb(90, 97, 107)))
                e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 1), Math.Max(0, e.Bounds.Height - 1));
            Rectangle text = e.Bounds;
            text.Inflate(-4, 0);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, ((ListView)sender).Font, text, Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        void DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (((ListView)sender).View != View.Details) e.DrawDefault = true;
        }

        void DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            ListView list = (ListView)sender;
            if (e.ColumnIndex >= list.Columns.Count || list.Columns[e.ColumnIndex].Width == 0) return;
            // WinForms may report the entire row as subitem zero's bounds.
            // Clip to the actual column, including after scrolling/restoring.
            Rectangle bounds = e.Bounds;
            if (e.ColumnIndex == 0) bounds.Width = list.Columns[0].Width;
            System.Drawing.Drawing2D.GraphicsState state = e.Graphics.Save();
            e.Graphics.SetClip(bounds, System.Drawing.Drawing2D.CombineMode.Intersect);
            Color background = e.Item.Selected ? Color.FromArgb(38, 88, 135)
                : e.ItemIndex % 2 == 0 ? Input : Color.FromArgb(36, 39, 44);
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, bounds);
            using (Pen grid = new Pen(Color.FromArgb(67, 73, 82)))
            {
                e.Graphics.DrawLine(grid, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
                e.Graphics.DrawLine(grid, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }
            Rectangle text = bounds;
            text.Inflate(-4, 0);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, text, Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (e.Item.Focused && list.Focused && e.ColumnIndex == 0)
                ControlPaint.DrawFocusRectangle(e.Graphics, bounds, Text, background);
            e.Graphics.Restore(state);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr window, string app, string ids);

        public void ApplyTitleBar(IntPtr handle)
        {
            try
            {
                int enabled = Dark ? 1 : 0;
                if (DwmSetWindowAttribute(handle, 20, ref enabled, 4) != 0)
                    DwmSetWindowAttribute(handle, 19, ref enabled, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }

    sealed class ThemeTabs : TabControl
    {
        bool _dark;

        public void SetDark(bool dark)
        {
            _dark = dark;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, dark);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_dark) { base.OnPaint(e); return; }
            Color background = Color.FromArgb(30, 32, 36);
            e.Graphics.Clear(background);
            using (Pen border = new Pen(Color.FromArgb(70, 70, 70)))
            {
                Rectangle pane = DisplayRectangle;
                pane.Inflate(1, 1);
                e.Graphics.DrawRectangle(border, pane);
                for (int i = 0; i < TabCount; i++)
                {
                    Rectangle tab = GetTabRect(i);
                    using (SolidBrush fill = new SolidBrush(i == SelectedIndex ? Color.FromArgb(50, 50, 50) : background))
                        e.Graphics.FillRectangle(fill, tab);
                    TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, tab, ForeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    if (i == SelectedIndex)
                    {
                        using (Pen accent = new Pen(Color.FromArgb(111, 175, 230), 2))
                            e.Graphics.DrawLine(accent, tab.Left + 3, tab.Bottom - 2, tab.Right - 3, tab.Bottom - 2);
                        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(tab, -3, -3), ForeColor, BackColor);
                    }
                }
            }
        }
    }

    sealed class ThemeButton : Button
    {
        public bool Dark;
        bool _keyboardPressed;

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { _keyboardPressed = true; Invalidate(); }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { _keyboardPressed = false; Invalidate(); }
            base.OnKeyUp(e);
        }
        protected override void OnLostFocus(EventArgs e)
        {
            _keyboardPressed = false;
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Dark) { base.OnPaint(e); return; }
            bool hover = Enabled && ClientRectangle.Contains(PointToClient(MousePosition));
            bool pressed = Enabled && (_keyboardPressed || (hover && (MouseButtons & MouseButtons.Left) != 0));
            bool focus = Enabled && Focused && ShowFocusCues;
            // A visible surface and a DPI-scaled inner border distinguish buttons
            // from surrounding panels without relying on the text alone.
            Color fill = pressed ? Color.FromArgb(35, 65, 92)
                : hover ? Color.FromArgb(67, 74, 84)
                : BackColor == Color.FromArgb(30, 32, 36) ? Color.FromArgb(46, 50, 57) : BackColor;
            using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, ClientRectangle);
            Color edge = !Enabled ? Color.FromArgb(100, 107, 117)
                : focus || hover || pressed ? Color.FromArgb(139, 202, 255) : Color.FromArgb(155, 166, 181);
            int thickness = Math.Max(2, (int)Math.Round(2 * e.Graphics.DpiX / 96.0));
            using (SolidBrush border = new SolidBrush(edge))
            {
                e.Graphics.FillRectangle(border, 0, 0, Width, thickness);
                e.Graphics.FillRectangle(border, 0, Height - thickness, Width, thickness);
                e.Graphics.FillRectangle(border, 0, 0, thickness, Height);
                e.Graphics.FillRectangle(border, Width - thickness, 0, thickness, Height);
            }
            Rectangle caption = Rectangle.Inflate(ClientRectangle, -thickness - 1, -thickness);
            if (pressed) caption.Offset(1, 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, caption,
                Enabled ? ForeColor : Color.FromArgb(170, 175, 183),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (focus)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -thickness - 2, -thickness - 2), ForeColor, fill);
        }
    }

    sealed class BufferedListView : ListView
    {
        public BufferedListView() { DoubleBuffered = true; }
    }
}
