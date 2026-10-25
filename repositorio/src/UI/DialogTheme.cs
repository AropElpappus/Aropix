// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PixelArt
{
    // Native window chrome remains responsible for focus, movement, resizing and modality.
    class ThemedWindow : Form
    {
        protected virtual bool AnimateOpening { get { return true; } }
        protected virtual bool ConstrainOpeningPosition { get { return true; } }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!SystemInformation.HighContrast) NativeWindowTheme.Apply(Handle);
        }
        protected override void OnLoad(EventArgs e)
        {
            BackColor = AropixTheme.Surface; ForeColor = AropixTheme.Text;
            AropixTheme.StyleInputs(this);
            base.OnLoad(e);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            if(ConstrainOpeningPosition)Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
            if (AnimateOpening) UiMotion.Reveal(this);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Do not defer DialogResult, save decisions, owned-window shutdown or disposal.
            UiMotion.Cancel(this, true);
            base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Editor editor=Owner as Editor;
            base.OnFormClosed(e);
            if(editor!=null)editor.QueueCanvasFocus();
        }
    }
    static class NativeWindowTheme
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        public static void Apply(IntPtr handle)
        {
            try
            {
                // Unsupported attributes return a failure HRESULT; native chrome remains intact.
                int dark = 1, round = 2;
                DwmSetWindowAttribute(handle, 20, ref dark, 4);
                DwmSetWindowAttribute(handle, 33, ref round, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
    // Adapts existing dialog bodies without duplicating their controls or event handlers.
    class SecondaryDialog : ThemedWindow
    {
        public string Description {get;set;}
        public Control InitialField;
        readonly ToolTip inputHints=new ToolTip { AutoPopDelay=12000 };
        bool prepared; public static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(Control nested in Descendants(child))yield return nested;}}
        protected override void OnLoad(EventArgs e)
        {
            Prepare(); DescribeInputs(); base.OnLoad(e);
        }
        void DescribeInputs()
        {
            foreach(Control control in Descendants(this))
            {
                NumericUpDown number=control as NumericUpDown;if(number==null)continue;
                Label nearest=null;int distance=int.MaxValue;
                foreach(Control sibling in number.Parent.Controls)
                {
                    Label label=sibling as Label;if(label==null || label.Top>number.Bottom || label.Left>number.Right)continue;
                    int score=Math.Abs(label.Left-number.Left)*2+Math.Abs(label.Top-number.Top);
                    if(score<distance){distance=score;nearest=label;}
                }
                if(string.IsNullOrEmpty(number.AccessibleName))number.AccessibleName=nearest==null?"Valor numérico":nearest.Text;
                string limits="Valor permitido: "+number.Minimum+" a "+number.Maximum+".";
                number.AccessibleDescription=limits;inputHints.SetToolTip(number,number.AccessibleName+"\n"+limits);
            }
        }
        protected override void Dispose(bool disposing){if(disposing)inputHints.Dispose();base.Dispose(disposing);}
        void Prepare()
        {
            if (prepared) return; prepared = true;
            SuspendLayout();
            Button primary = AcceptButton as Button, cancel = CancelButton as Button;
            if (cancel == null)
            {
                cancel = Editor.Button("Cancelar", 105); cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel;
            }
            List<Control> children = new List<Control>();
            foreach (Control child in Controls) if (child != primary && child != cancel) children.Add(child);
            int contentHeight = 60;
            foreach (Control child in children) contentHeight = Math.Max(contentHeight, child.Bottom + 16);
            int width = Math.Max(ClientSize.Width, 390);
            Panel body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AropixTheme.Surface, AutoScrollMinSize = new Size(width - 24, contentHeight) };
            children.Sort(delegate(Control a, Control b) { int order = a.Top.CompareTo(b.Top); return order != 0 ? order : a.Left.CompareTo(b.Left); });
            int tab = 0;
            foreach (Control child in children)
            {
                body.Controls.Add(child); child.TabIndex = tab++;
                if (InitialField == null && child.CanSelect && (child is TextBoxBase || child is NumericUpDown || child is ComboBox)) InitialField = child;
            }
            Panel header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = AropixTheme.Raised, Padding = new Padding(20, 14, 20, 10) };
            Label heading = new Label { Text = Text, Dock = DockStyle.Top, Height = 30, Font = new Font(AropixTheme.FontFamily, 13, FontStyle.Bold), AutoEllipsis = true };
            Label description = new Label { Text = Description ?? DialogDescription(Text), Dock = DockStyle.Fill, Font = new Font(AropixTheme.FontFamily, 9), ForeColor = AropixTheme.Secondary };
            header.Controls.Add(description); header.Controls.Add(heading);
            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(12, 10, 12, 10), BackColor = AropixTheme.Raised };
            if (primary != null)
            {
                primary.BackColor = AropixTheme.Accent; primary.AutoSize = true; primary.MinimumSize = new Size(110, 34); primary.TabIndex = 1; footer.Controls.Add(primary);
            }
            if(cancel!=primary){cancel.AutoSize = true; cancel.MinimumSize = new Size(100, 34); cancel.TabIndex = 0; footer.Controls.Add(cancel);}
            Controls.Add(body); Controls.Add(footer); Controls.Add(header); body.BringToFront();
            AcceptButton = primary; CancelButton = cancel;
            body.TabIndex = 0; footer.TabIndex = 1; header.TabStop = false;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            ClientSize = new Size(Math.Min(width, Math.Max(300, area.Width - 40)), Math.Min(contentHeight + 148, Math.Max(240, area.Height - 80)));
            ResumeLayout(true);
        }
        internal static string DialogDescription(string title)
        {
            if (title.Contains("sombra")) return "Configura la copia. La vista previa aparece en el lienzo; Cancelar la descarta.";
            if (title.Contains("Importar")) return "Referencia original a la izquierda; tamaño y apariencia de la nueva capa a la derecha.";
            if (title.Contains("Nuevo")) return "Define el proyecto, su lienzo y la escala inicial de exportación.";
            if (title.Contains("lienzo")) return "Cambia el espacio disponible sin escalar los píxeles del dibujo.";
            if (title.Contains("selección")) return "Ajusta las dimensiones del contenido seleccionado en píxeles.";
            if (title.Contains("ejes")) return "Elige dónde se refleja el dibujo. Coordenadas en píxeles.";
            if (title.Contains("automático")) return "Configura la frecuencia de guardado y recuperación de tus proyectos.";
            if (title.Contains("Nombre")) return "Usa un nombre que te ayude a identificar esta capa.";
            if (title.Contains("Exportar")) return "Configura la salida PNG conservando tu proyecto editable.";
            return "Revisa las opciones antes de confirmar.";
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (InitialField != null && InitialField.CanSelect)
            {
                InitialField.Focus(); TextBoxBase text = InitialField as TextBoxBase;
                if (text != null) text.SelectAll();
            }
        }
    }
}

namespace PixelArt {
    enum NoticeKind { Information, Warning, Error }
    class NoticeDialog : SecondaryDialog {
        public NoticeDialog(string title,string message,NoticeKind kind){
            Text=title;ClientSize=new Size(500,230);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
            Font=new Font(AropixTheme.FontFamily,AropixTheme.ControlSize);
            Description=kind==NoticeKind.Error?"Error · No se pudo completar la operación.":kind==NoticeKind.Warning?"Advertencia · Revisa la información antes de continuar.":"Información";
            TextBox details=new TextBox{Text=message,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,Bounds=new Rectangle(20,20,460,150)};Controls.Add(details);
            Button close=Editor.Button("Cerrar",110);close.DialogResult=DialogResult.OK;Controls.Add(close);AcceptButton=close;CancelButton=close;
        }
        public static void ShowNotice(IWin32Window owner,string title,string text,NoticeKind kind){using(NoticeDialog dialog=new NoticeDialog(title,text,kind))dialog.ShowDialog(owner);}
    }
}
