// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PixelArt
{
    // Shared visual tokens. No document or command state belongs to the theme.
    static class AropixTheme
    {
        public static readonly Color Background = SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(23, 28, 33);
        public static readonly Color Surface = SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(30, 36, 43);
        public static readonly Color Raised = SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(39, 47, 55);
        public static readonly Color Hover = SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(48, 64, 70);
        public static readonly Color Accent = SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(0, 128, 128);
        public static readonly Color Focus = SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(74, 211, 199);
        public static readonly Color Text = SystemInformation.HighContrast ? SystemColors.ControlText : Color.FromArgb(234, 240, 244);
        public static readonly Color Secondary = SystemInformation.HighContrast ? SystemColors.ControlText : Color.FromArgb(168, 182, 193);
        public static readonly Color Disabled = SystemInformation.HighContrast ? SystemColors.GrayText : Color.FromArgb(119, 131, 141);
        public static readonly Color Border = SystemInformation.HighContrast ? SystemColors.ControlText : Color.FromArgb(63, 75, 85);
        public static readonly Color Error = SystemInformation.HighContrast ? SystemColors.ControlText : Color.FromArgb(233, 143, 148);
        public static readonly Color Warning = SystemInformation.HighContrast ? SystemColors.ControlText : Color.FromArgb(226, 187, 112);
        public static readonly Color Success = SystemInformation.HighContrast ? SystemColors.ControlText : Color.FromArgb(116, 202, 161);
        public const int Gap = 6, Radius = 6;
        public const float TitleSize = 12, HeadingSize = 10, ControlSize = 10, CaptionSize = 9;
        public const string FontFamily = "Segoe UI";

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static void StyleMenu(ToolStrip strip)
        {
            strip.Renderer = new AropixRenderer(); strip.BackColor = Raised; strip.ForeColor = Text;
            foreach (ToolStripItem item in strip.Items)
            {
                item.ForeColor = Text;
                if (!(item is ToolStripSeparator)) item.Padding = new Padding(4, 3, 4, 3);
                ToolStripDropDownItem child = item as ToolStripDropDownItem;
                if (child != null && child.HasDropDownItems) StyleMenu(child.DropDown);
            }
        }
        public static void StyleInputs(Control root)
        {
            // Keep native keyboard, accessibility and editing behavior.
            if (root is TextBoxBase || root is NumericUpDown || root is ComboBox || root is ListBox)
            { root.BackColor = Raised; root.ForeColor = Text; }
            ComboBox combo = root as ComboBox;
            if (combo != null && combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.DrawMode == DrawMode.Normal)
            {
                combo.FlatStyle = FlatStyle.Flat; combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
                {
                    bool selected = (e.State & DrawItemState.Selected) != 0;
                    using (Brush b = new SolidBrush(selected ? Hover : Raised)) e.Graphics.FillRectangle(b, e.Bounds);
                    string label = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
                    TextRenderer.DrawText(e.Graphics, label, combo.Font, e.Bounds, combo.Enabled ? (SystemInformation.HighContrast && selected?SystemColors.HighlightText:Text) : Disabled, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
                };
            }
            CheckBox check = root as CheckBox;
            if (check != null) { check.FlatStyle = FlatStyle.Flat; check.FlatAppearance.BorderColor = Border; check.ForeColor = Text; }
            if (root.ContextMenuStrip != null) StyleMenu(root.ContextMenuStrip);
            foreach (Control child in root.Controls) StyleInputs(child);
        }
    }
    class AropixRenderer : ToolStripProfessionalRenderer
    {
        public AropixRenderer() : base(new AropixColors()) { RoundedEdges = false; }
        void Background(ToolStripItemRenderEventArgs e, bool selected)
        {
            Rectangle r = new Rectangle(1, 1, e.Item.Width - 3, e.Item.Height - 3);
            if (r.Width < 1 || r.Height < 1) return;
            if (e.Item.Selected || e.Item.Pressed || selected || e.Item.BackColor == AropixTheme.Accent)
                using (GraphicsPath p = AropixTheme.Rounded(r, AropixTheme.Radius))
                using (Brush b = new SolidBrush(e.Item.Pressed || selected || e.Item.BackColor == AropixTheme.Accent ? AropixTheme.Accent : AropixTheme.Hover))
                    e.Graphics.FillPath(b, p);
            if (selected) using (Pen pen = new Pen(AropixTheme.Focus, 2)) e.Graphics.DrawLine(pen, 6, r.Bottom - 1, r.Right - 5, r.Bottom - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) { Background(e, false); }
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) { ToolStripButton b = e.Item as ToolStripButton; Background(e, b != null && b.Checked); }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        { e.TextColor = e.Item.Enabled ? (SystemInformation.HighContrast && (e.Item.Selected || e.Item.Pressed)?SystemColors.HighlightText:AropixTheme.Text) : AropixTheme.Disabled; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        { e.ArrowColor = e.Item.Enabled ? AropixTheme.Secondary : AropixTheme.Disabled; base.OnRenderArrow(e); }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(AropixTheme.Border))
                if (e.Vertical) e.Graphics.DrawLine(pen, e.Item.Width / 2, 6, e.Item.Width / 2, e.Item.Height - 6);
                else e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            using (Pen p = new Pen(e.Item.Enabled ? AropixTheme.Focus : AropixTheme.Disabled, 2))
                e.Graphics.DrawLines(p, new Point[] { new Point(r.Left + 3, r.Top + r.Height / 2), new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4), new Point(r.Right - 2, r.Top + 3) });
        }
    }
    class AropixButton : Button
    {
        bool pressed;
        double hoverAmount;
        void TransitionHover(bool value)
        {
            double start=hoverAmount, end=value?1:0;
            UiMotion.Start(this,100,delegate(double p){hoverAmount=start+(end-start)*p;Invalidate();});
        }
        public bool Destructive;
        public AropixButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = AropixTheme.Raised; ForeColor = AropixTheme.Text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { TransitionHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { pressed = false; TransitionHover(false); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? AropixTheme.Surface : Parent.BackColor);
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (r.Width < 1 || r.Height < 1) return;
            Color fill = SystemInformation.HighContrast && Text.Length>0 ? SystemColors.Control : BackColor;
            if (Enabled && Text.Length > 0 && !SystemInformation.HighContrast) fill = pressed ? ControlPaint.Dark(fill, .12f) : UiMotion.Blend(fill,ControlPaint.Light(fill,.12f),hoverAmount);
            using (GraphicsPath p = AropixTheme.Rounded(r, (int)(AropixTheme.Radius * g.DpiX / 96f)))
            using (Brush b = new SolidBrush(fill))
            using (Pen pen = new Pen(Focused ? AropixTheme.Focus : AropixTheme.Border)) { g.FillPath(b, p); g.DrawPath(pen, p); }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(5, pressed ? 2 : 0, Width - 10, Height),
                !Enabled ? AropixTheme.Disabled : SystemInformation.HighContrast ? SystemColors.ControlText : Destructive ? AropixTheme.Error : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(5, 5, Width - 10, Height - 10), AropixTheme.Text, fill);
        }
    }
    // Preserve native tab navigation/overflow/accessibility; customize only drawing and close hit testing.
    class ProjectTabs : TabControl
    {
        int hovered = -1, closePressed = -1;
        double tabHover;
        void HoverTab(int next)
        {
            hovered=next;tabHover=0;
            UiMotion.Start(this,100,delegate(double progress){tabHover=progress;Invalidate(new Rectangle(0,0,Width,ItemSize.Height+8));});
        }
        public event Action<int> CloseRequested;
        public ProjectTabs()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed; Padding = new Point(22, 5); ItemSize = new Size(190, 26);
            SizeMode = TabSizeMode.Fixed;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ShowToolTips = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(AropixTheme.Background);
            for (int i = 0; i < TabCount; i++)
                if (GetTabRect(i).IntersectsWith(e.ClipRectangle))
                    OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, i == SelectedIndex ? DrawItemState.Selected : DrawItemState.None));
        }
        internal Rectangle CloseBounds(int index)
        {
            Rectangle r = GetTabRect(index); int side = Math.Min(20, r.Height - 4);
            return new Rectangle(r.Right - side - 5, r.Top + (r.Height - side) / 2, side, side);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int next = -1; for (int i = 0; i < TabCount; i++) if (GetTabRect(i).Contains(e.Location)) { next = i; break; }
            if (next != hovered) HoverTab(next); base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { UiMotion.Cancel(this,false);hovered = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            closePressed = -1;
            if (e.Button == MouseButtons.Left) for (int i = 0; i < TabCount; i++) if (CloseBounds(i).Contains(e.Location)) { closePressed = i; break; }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            int index = closePressed; closePressed = -1; base.OnMouseUp(e);
            if (e.Button == MouseButtons.Middle && CloseRequested != null)
                for(int i=0;i<TabCount;i++) if(GetTabRect(i).Contains(e.Location)){CloseRequested(i);return;}
            if (e.Button == MouseButtons.Left && index >= 0 && index < TabCount && CloseBounds(index).Contains(e.Location) && CloseRequested != null) CloseRequested(index);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= TabCount) return;
            Rectangle r = GetTabRect(e.Index); bool active = e.Index == SelectedIndex;
            using (Brush b = new SolidBrush(active ? AropixTheme.Raised : e.Index == hovered ? UiMotion.Blend(AropixTheme.Surface,AropixTheme.Hover,tabHover) : AropixTheme.Surface)) e.Graphics.FillRectangle(b, r);
            string text = TabPages[e.Index].Text; bool dirty = text.EndsWith(" *", StringComparison.Ordinal);
            Rectangle close = CloseBounds(e.Index);
            TextRenderer.DrawText(e.Graphics, dirty ? text.Substring(0, text.Length - 2) : text, Font,
                new Rectangle(r.Left + 10, r.Top, Math.Max(0, close.Left - r.Left - 25), r.Height), active ? AropixTheme.Text : AropixTheme.Secondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (dirty) using (Brush b = new SolidBrush(AropixTheme.Focus)) e.Graphics.FillEllipse(b, close.Left - 10, r.Top + r.Height / 2 - 3, 6, 6);
            using (Pen p = new Pen(AropixTheme.Secondary, 1.3f))
            { e.Graphics.DrawLine(p, close.Left + 6, close.Top + 6, close.Right - 6, close.Bottom - 6); e.Graphics.DrawLine(p, close.Right - 6, close.Top + 6, close.Left + 6, close.Bottom - 6); }
            if (active) using (Pen p = new Pen(AropixTheme.Focus, 3)) e.Graphics.DrawLine(p, r.Left + 6, r.Bottom - 2, r.Right - 6, r.Bottom - 2);
            if (Focused && active && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(r, -4, -4));
        }
    }
}
