// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Windows.Forms;
namespace PixelArt
{
    partial class Editor
    {
        public void TestUXPolish()
        {
            using(Editor view=new Editor(true))
            {
                view.SetDocument(new Document(100,100));view.Show();Application.DoEvents();view.RestoreDefaultLayout();view.SetTool("Lápiz");
                int history=view.undo.Count;bool dirtyBefore=view.dirty;Control focus=view.ActiveControl;
                view.Notify("Imagen exportada correctamente · Prueba",NoticeKind.Information);
                if(!view.notice.Visible || view.notice.ExpiryRunning || view.ActiveControl!=focus)throw new Exception("Aviso no modal sin robo de foco");
                view.notice.AdvanceExpiry(4000);if(!view.notice.Visible)throw new Exception("Aviso prematuro");
                if(view.Controls.GetChildIndex(view.notice)!=0)throw new Exception("Aviso encima del contenido");
                Rectangle canvasArea=view.RectangleToClient(view.canvas.RectangleToScreen(view.canvas.ClientRectangle));
                if(!canvasArea.Contains(view.notice.Bounds))throw new Exception("Aviso dentro del área del lienzo");
                // DrawToBitmap reverses overlapping sibling order. Capture the actual control separately.
                using(Bitmap shot=new Bitmap(view.notice.Width,view.notice.Height)){
                    view.notice.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));
                    shot.Save(System.IO.Path.Combine(TestArtifacts.DirectoryPath,"ux-aviso.png"));
                }
                view.notice.AdvanceExpiry(4000);
                if(view.notice.Visible)throw new Exception("Caducidad informativa");
                view.Notify("Revisa el destino antes de continuar.",NoticeKind.Warning);view.notice.AdvanceExpiry(60000);
                if(!view.notice.Visible || !view.notice.MessageText.StartsWith("Advertencia:"))throw new Exception("Advertencia persistente y etiquetada");
                view.Notify("No se pudo escribir el archivo.",NoticeKind.Error);view.notice.AdvanceExpiry(60000);
                if(!view.notice.Visible || view.notice.ExpiryRunning || !view.notice.MessageText.StartsWith("Error:"))throw new Exception("Error persistente");
                view.notice.HideNotice();if(view.undo.Count!=history || view.dirty!=dirtyBefore)throw new Exception("Notificaciones ajenas al documento");
                Color ink=view.canvas.Ink;view.hex.Text="#ZZZZZZ";view.ApplyHex();
                if(view.hex.Text!="#ZZZZZZ" || view.canvas.Ink!=ink || !view.colorValidation.Visible)throw new Exception("Color inválido conserva entrada e imagen");
                view.CaptureUI("ux-validacion-color.png");
                view.hex.Text="#123456";view.ApplyHex();
                if(view.canvas.Ink.ToArgb()!=Color.FromArgb(0x12,0x34,0x56).ToArgb() || view.colorValidation.Visible)throw new Exception("Corregir color sin perder valores");
                view.SetTool("Círculo");view.circleCenter.Focus();view.OpenContextHelp();Application.DoEvents();
                if(view.helpCenter.CurrentId!="circlecenter")throw new Exception("Ayuda contextual del centro");view.helpCenter.Close();Application.DoEvents();
                view.opacityInput.Focus();view.OpenContextHelp();Application.DoEvents();
                if(view.helpCenter.CurrentId!="opacity")throw new Exception("Ayuda contextual de opacidad");view.helpCenter.Close();Application.DoEvents();
                view.SetLayerLocked(true);view.SyncCommands();
                if(view.CanRun("layerdelete") || !view.UnavailableReason("layerdelete").Contains("Desbloquea"))throw new Exception("Motivo de eliminación deshabilitada");
                view.SetLayerLocked(false);if(view.CanRun("layermerge") || !view.UnavailableReason("layermerge").Contains("inferior"))throw new Exception("Motivo sin capa inferior");
                using(NewProject dialog=new NewProject())
                {
                    dialog.Show(view);Application.DoEvents();
                    if(string.IsNullOrWhiteSpace(dialog.W.AccessibleName) || !dialog.W.AccessibleDescription.Contains("2048"))throw new Exception("Dimensiones accesibles y límites");dialog.Close();
                }
                using(LayerExportDialog dialog=new LayerExportDialog(view.doc))
                {
                    for(int i=0;i<dialog.Choices.Items.Count;i++)dialog.Choices.SetItemChecked(i,false);
                    if(((Button)dialog.AcceptButton).Enabled)throw new Exception("Exportar requiere una capa marcada");
                    dialog.Choices.SetItemChecked(0,true);if(!((Button)dialog.AcceptButton).Enabled)throw new Exception("Exportar recupera disponibilidad");
                }
                view.dirty=false;view.doc.Name="Conservar A";view.StoreActive();Document first=view.doc;
                Document target=new Document(20,20);target.Name="Cerrar B";view.AddProject(target,null);
                Document last=new Document(20,20);last.Name="Conservar C";view.AddProject(last,null);view.StoreActive();
                Rectangle tab=view.projectTabs.GetTabRect(1);
                typeof(Control).GetMethod("OnMouseUp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view.projectTabs,new object[]{new MouseEventArgs(MouseButtons.Middle,1,tab.Left+8,tab.Top+8,0)});
                if(view.projects.Count!=2 || view.projects[0].Doc!=first || view.projects[1].Doc!=last || view.doc!=last)throw new Exception("Cierre central conserva asociación de proyectos");
                for(int i=0;i<10;i++)view.AddProject(new Document(8,8){Name="Proyecto adicional "+i},null);
                Message key=new Message();view.ProcessCmdKey(ref key,Keys.Control|Keys.Tab);
                if(view.doc!=first || view.projectTabs.SelectedTab.Tag!=view.activeProject)throw new Exception("Muchas pestañas y navegación de teclado");
                view.CaptureUI("ux-pestanas.png");
            }
        }
    }
}
