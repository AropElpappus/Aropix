// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PixelArt
{
    // A single replaceable notice: no queue, no focus theft, no document state.
    class InlineNotice : Panel
    {
        readonly Label message = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
        readonly Button dismiss = Editor.Button("×", 30);
        readonly Timer expiry = new Timer { Interval = 250 };
        readonly ToolTip fullText = new ToolTip { AutoPopDelay = 15000 };
        int remaining;
        public NoticeKind Kind { get; private set; }
        public string MessageText { get { return message.Text; } }
        internal bool ExpiryRunning { get { return expiry.Enabled; } }
        public event Action Dismissed;
        public InlineNotice()
        {
            BackColor = AropixTheme.Raised; ForeColor = AropixTheme.Text; Padding = new Padding(12, 8, 8, 8); Height = 76;
            AccessibleRole = AccessibleRole.Alert; Visible = false;
            dismiss.Dock = DockStyle.Right; dismiss.AccessibleName = "Cerrar notificación";
            dismiss.Click += delegate { HideNotice(); };
            Controls.Add(message); Controls.Add(dismiss);
            expiry.Tick += delegate { if (!ContainsFocus && !RectangleToScreen(ClientRectangle).Contains(Cursor.Position)) AdvanceExpiry(250); };
        }
        public void Present(string text, NoticeKind kind)
        {
            expiry.Stop(); Kind = kind; remaining = 8000;
            string title = kind == NoticeKind.Error ? "Error" : kind == NoticeKind.Warning ? "Advertencia" : "Información";
            message.Text = title + ": " + text; AccessibleName = message.Text;
            fullText.SetToolTip(message,message.Text);
            message.ForeColor = SystemInformation.HighContrast ? SystemColors.ControlText : kind == NoticeKind.Error ? AropixTheme.Error : kind == NoticeKind.Warning ? AropixTheme.Warning : AropixTheme.Text;
            Show(); BringToFront();
            if (kind == NoticeKind.Information && !UiMotion.TestMode) expiry.Start();
        }
        internal void AdvanceExpiry(int milliseconds)
        {
            if (Kind != NoticeKind.Information || !Visible) return;
            remaining -= milliseconds; if (remaining <= 0) HideNotice();
        }
        public void HideNotice()
        {
            bool focused = ContainsFocus; expiry.Stop(); Hide();
            if (focused && Dismissed != null) Dismissed();
        }
        protected override void OnVisibleChanged(EventArgs e) { if (!Visible) expiry.Stop(); base.OnVisibleChanged(e); }
        protected override void Dispose(bool disposing) { if (disposing) { expiry.Dispose(); fullText.Dispose(); } base.Dispose(disposing); }
    }
    partial class Editor
    {
        internal void QueueCanvasFocus()
        {
            if(IsDisposed || Disposing || !IsHandleCreated)return;
            BeginInvoke((Action)delegate{if(!IsDisposed && !Disposing && Enabled && Visible && Form.ActiveForm==this)FocusWorkspace();});
        }
        InlineNotice notice;
        Label colorValidation;
        readonly Dictionary<Control, string> propertyHelp = new Dictionary<Control, string>();
        void Notify(string text, NoticeKind kind)
        {
            if (notice == null)
            {
                notice = new InlineNotice(); Controls.Add(notice);
                notice.Dismissed += delegate { if (!IsDisposed) canvas.Focus(); };
                SizeChanged += delegate { PlaceNotice(); };
                canvas.SizeChanged += delegate { PlaceNotice(); };
                canvas.LocationChanged += delegate { PlaceNotice(); };
            }
            PlaceNotice(); notice.Present(text, kind);
        }
        void PlaceNotice()
        {
            if (notice == null || canvas.IsDisposed || !IsHandleCreated) return;
            Rectangle area = RectangleToClient(canvas.RectangleToScreen(canvas.ClientRectangle));
            notice.Width = Math.Max(180, Math.Min(520, area.Width - 16));
            notice.Location = new Point(Math.Max(0, area.Right - notice.Width - 8), Math.Max(0, area.Bottom - notice.Height - 8));
        }
        void ContextHelp(Control control, string topic)
        {
            propertyHelp[control] = topic;
            HelpEntry entry = HelpCatalog.Find(topic);
            control.AccessibleName = entry.Name;
            control.AccessibleDescription = entry.Description + " F1: ayuda.";
            tips.SetToolTip(control, HelpCatalog.Tooltip(topic) + "\nF1: ayuda de esta propiedad.");
            // Floating panels forward HelpRequested through the same catalog.
            control.HelpRequested += delegate(object sender, HelpEventArgs e) { OpenHelp(topic); e.Handled = true; };
        }
        void InitializeUX()
        {
            tips.InitialDelay = 450; tips.ReshowDelay = 100; tips.AutoPopDelay = 12000; tips.ShowAlways = true;
            ContextHelp(circleCenter, "circlecenter"); ContextHelp(opacityInput, "opacity"); ContextHelp(lockLayer, "lock");
            ContextHelp(pencilSizes, "pencil"); ContextHelp(eraserSizes, "eraser");
            autoSaveLabel.AccessibleName = "Estado del guardado automático";
            tips.SetToolTip(autoSaveLabel, HelpCatalog.Tooltip("autosave"));
            layerList.AccessibleName = "Capas del proyecto; selecciona la capa activa";
            projectTabs.AccessibleName = "Proyectos abiertos"; canvas.AccessibleName = "Lienzo de dibujo";
            layerMore.AccessibleName = "Más acciones de capa";
            foreach(KeyValuePair<Button,string> action in actionButtons) action.Key.AccessibleName = commands[action.Value].Text;
        }
        void OpenContextHelp()
        {
            foreach(KeyValuePair<Control,string> entry in propertyHelp) if(entry.Key.ContainsFocus){OpenHelp(entry.Value);return;}
            OpenToolHelp();
        }
        string UnavailableReason(string id)
        {
            if (CanRun(id)) return "";
            if (doc == null) return "No hay un proyecto activo.";
            if (id == "paste" && copiedSelection == null) return "Copia primero una selección dentro de Aropix.";
            if (id == "layermerge" && doc.ActiveLayer == 0) return "No existe una capa inferior.";
            if ((id == "layernew" || id == "layerduplicate" || id == "import" || id == "layershadow") && doc.Layers.Count >= 16) return "Se ha alcanzado el límite de 16 capas.";
            if (id == "layerdelete" && doc.Layers[doc.ActiveLayer].Locked) return "Desbloquea la capa para eliminarla.";
            if (id == "paste" || id == "deletepixels" || id == "layermerge" || id == "rotateR" || id == "rotateL" || id == "flipH" || id == "flipV" || id == "resizeSelection")
            {
                if (!doc.Editable) return "La capa activa debe estar visible y desbloqueada.";
                if (id == "layermerge") return "La capa inferior debe estar visible y desbloqueada.";
                if (id != "paste" && canvas.Selection.IsEmpty) return "Selecciona primero una región del dibujo.";
            }
            return "La acción no está disponible en el estado actual.";
        }
    }
}
