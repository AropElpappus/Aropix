// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace PixelArt
{
    partial class Editor
    {
        static void WelcomeCheck(bool condition, string message)
        {
            if(!condition)throw new Exception("Bienvenida: " + message);
        }
        void CheckEmptyWorkspace()
        {
            WelcomeCheck(projects.Count == 0 && projectTabs.TabCount == 0 && activeProject == null && doc == null && canvas.Doc == null, "cero documentos reales");
            WelcomeCheck(welcome.Visible && !canvas.Visible && !projectTabs.Visible && !canvas.Enabled, "bienvenida sin lienzo ni pestaña fantasma");
            WelcomeCheck(!dirty && undo.Count == 0 && redo.Count == 0 && projectPath == null, "sin historial ni ruta residual");
            WelcomeCheck(layerList.Items.Count == 0 && palette.Controls.Count == 0 && !layersPanel.Content.Enabled, "paneles vacíos y deshabilitados");
            WelcomeCheck(status.Text.Contains("Sin proyectos") && !status.Text.Contains("100") && coordinates.Text == "", "estado sin dimensiones ni coordenadas");
            SyncCommands();
            foreach(var pair in commands)
                if(pair.Value.RequiresDocument)
                {
                    WelcomeCheck(!CanRun(pair.Key), "comando deshabilitado " + pair.Key);
                    foreach(ToolStripItem item in pair.Value.Items)WelcomeCheck(!item.Enabled, "indicador deshabilitado " + pair.Key);
                    ExecuteCommand(pair.Key); // A disabled command must be safe even if invoked directly.
                }
            foreach(string id in new string[] { "new", "open", "help", "panel0", "autoconfig", "coords1", "animations" })WelcomeCheck(CanRun(id), "acción global disponible " + id);
        }
        void TestNewProjectChoice(DialogResult choice)
        {
            bool visited = false;
            using(Timer timer = new Timer { Interval = 30 })
            {
                timer.Tick += delegate {
                    foreach(Form form in Application.OpenForms)
                    {
                        NewProject dialog = form as NewProject;
                        if(dialog == null)continue;
                        visited = true; timer.Stop();
                        dialog.ProjectName.Text = "Proyecto de bienvenida";
                        dialog.W.Value = 24; dialog.H.Value = 18;
                        dialog.DialogResult = choice; dialog.Close(); break;
                    }
                };
                timer.Start();welcomeNew.PerformClick();timer.Stop();
            }
            WelcomeCheck(visited, "botón Nuevo abre el diálogo existente");
        }
        public void TestWelcome()
        {
            string root = Path.Combine(TestArtifacts.DirectoryPath, "welcome"); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "dibujo.pixelart");
            InternalStore store = new InternalStore(Path.Combine(root, "local"), Path.Combine(root, "legacy"));
            using(Editor view = new Editor(true))
            {
                view.internalStore = store;view.Show();view.RestoreDefaultLayout();Application.DoEvents();
                WelcomeCheck(!view.RestoreLatestSession(), "primera ejecución sin sesión");view.CheckEmptyWorkspace();
                view.CaptureUI("bienvenida.png");Size original = view.Size;view.Size = new Size(980,640);Application.DoEvents();
                view.CaptureUI("bienvenida-compacta.png");
                WelcomeCheck(view.welcomeNew.Width > 100 && view.welcomeOpen.Width > 100 && view.welcomeNew.Height >= 40, "acciones accesibles en tamaño mínimo");view.Size = original;
                // Verify keyboard routing independently of native file-picker automation.
                Action create = view.commands["new"].Run, open = view.commands["open"].Run;int newCalls = 0, openCalls = 0;
                try
                {
                    view.commands["new"].Run = delegate { newCalls++; };
                    view.commands["open"].Run = delegate { openCalls++; };
                    Message message = new Message();
                    view.ProcessCmdKey(ref message, Keys.Control | Keys.N);view.ProcessCmdKey(ref message, Keys.Control | Keys.O);
                    view.welcomeOpen.PerformClick();
                    WelcomeCheck(newCalls == 1 && openCalls == 2, "atajos y botón Abrir usan los comandos centrales");
                }
                finally { view.commands["new"].Run = create;view.commands["open"].Run = open; }
                foreach(ToolStripMenuItem menu in view.mainMenus.Items)CheckVisualMenu(menu);
                view.OpenProject(delegate { return null; });view.CheckEmptyWorkspace();
                bool errorShown=false;
                using(Timer timer=new Timer{Interval=30})
                {
                    timer.Tick+=delegate{
                        foreach(Form form in Application.OpenForms)if(form is NoticeDialog){errorShown=true;timer.Stop();form.Close();break;}
                    };
                    timer.Start();view.OpenProjectFile(Path.Combine(root,"inexistente.pixelart"));timer.Stop();
                }
                WelcomeCheck(errorShown,"error de apertura informado");view.CheckEmptyWorkspace();
                view.TestNewProjectChoice(DialogResult.Cancel);view.CheckEmptyWorkspace();
                view.TestNewProjectChoice(DialogResult.OK);
                WelcomeCheck(view.projects.Count == 1 && view.doc.Image.Size == new Size(24,18) && !view.welcome.Visible && view.canvas.Visible, "crear sin documentos duplicados");
                view.doc.Image.SetPixel(1,1,Color.Red);view.MarkDirty();ProjectState first = view.activeProject;
                view.CloseProject(delegate { return DialogResult.Cancel; });
                WelcomeCheck(view.activeProject == first && view.dirty && !view.welcome.Visible, "cancelar el cierre conserva el documento");
                view.CloseProject(delegate { return DialogResult.Yes; }, delegate { return false; });
                WelcomeCheck(view.activeProject == first && view.dirty, "guardar fallido no cierra");
                view.projectPath = path;view.CloseProject(delegate { return DialogResult.Yes; });view.CheckEmptyWorkspace();
                using(Document saved = Document.Load(path))WelcomeCheck(saved.Image.GetPixel(1,1).ToArgb() == Color.Red.ToArgb(), "Guardar y cerrar conserva los píxeles");
                view.OpenProject(delegate { return path; });
                WelcomeCheck(view.projects.Count == 1 && view.canvas.Visible && !view.welcome.Visible, "abrir desde bienvenida");
                view.OpenProjectFile(path);WelcomeCheck(view.projects.Count == 1, "abrir otra vez activa sin duplicar");
                ProjectState savedState = view.activeProject;
                view.AddProject(new Document(12,12) { Name = "Sin título" }, null);
                view.AddProject(new Document(15,15) { Name = "Otro" }, null);
                WelcomeCheck(view.projects.Count == 3, "un proyecto creado Sin título no se reemplaza automáticamente");
                view.CloseProject();view.CloseProject();WelcomeCheck(view.activeProject == savedState && !view.welcome.Visible, "cerrar pestaña conserva la vecina");
                view.CloseProject();view.CheckEmptyWorkspace();
                DataObject drop = new DataObject(DataFormats.FileDrop, new string[] { path });
                WelcomeCheck(DroppedProjects(drop).Length == 1, "aceptar proyecto arrastrado");
                DragEventArgs args = new DragEventArgs(drop,0,0,0,DragDropEffects.Copy,DragDropEffects.None);
                typeof(Control).GetMethod("OnDragDrop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view.welcome,new object[] { args });Application.DoEvents();
                WelcomeCheck(view.projects.Count == 1, "arrastrar abre el proyecto desde bienvenida");
                view.CloseProject();view.CheckEmptyWorkspace();
                view.basicsPanel.HidePanel();view.leftDockWidth = 250;view.canvas.CoordinateMode = 2;
                WelcomeCheck(view.ConfirmAllProjects(delegate { throw new Exception("No debe solicitar guardar sin proyectos"); }), "cierre vacío sin confirmaciones");
                WelcomeCheck(view.PersistClose(delegate(string warning) { throw new Exception(warning); }, delegate { return false; }), "guardar sesión vacía");
                XmlDocument xml = InternalStore.ReadXml(view.SessionPath);WelcomeCheck(xml.DocumentElement.SelectNodes("tab").Count == 0, "no serializa documento ficticio");
                using(Editor restored = new Editor(true))
                {
                    restored.internalStore = store;restored.Show();restored.LoadInternalSettings();
                    WelcomeCheck(restored.RestoreLatestSession(), "sesión vacía es una sesión válida");restored.CheckEmptyWorkspace();
                    WelcomeCheck(restored.basicsPanel.IsHidden && restored.leftDockWidth == 250 && restored.canvas.CoordinateMode == 2, "preferencias independientes de proyectos");
                }
                // Newer recoverable work must win over an older valid empty session.
                view.OpenProjectFile(path);view.doc.Image.SetPixel(2,2,Color.Blue);view.MarkDirty();view.SaveSession(view.RecoveryPath);
                File.SetLastWriteTimeUtc(view.SessionPath, DateTime.UtcNow.AddMinutes(-2));
                using(Editor recovered = new Editor(true))
                {
                    recovered.internalStore = store;recovered.Show();WelcomeCheck(recovered.RestoreLatestSession() && recovered.dirty && recovered.doc.Image.GetPixel(2,2).ToArgb() == Color.Blue.ToArgb() && !recovered.welcome.Visible, "recuperar trabajo posterior a sesión vacía");
                }
                view.CloseProject(delegate { return DialogResult.No; });view.CheckEmptyWorkspace();
                WelcomeCheck(view.PersistClose(delegate(string warning) { throw new Exception(warning); }, delegate { return false; }), "cierre limpio retira recuperaciones anteriores");
                using(Editor restored = new Editor(true))
                {
                    restored.internalStore = store;restored.Show();WelcomeCheck(restored.RestoreLatestSession(), "restaurar cierre vacío posterior");restored.CheckEmptyWorkspace();
                }
                WelcomeCheck(File.Exists(view.RecoveryPath), "recuperación anterior se conserva físicamente");
                view.Close();WelcomeCheck(view.IsDisposed, "cerrar Aropix desde bienvenida");
            }
            // Invalid snapshots are reported and retained, never replaced with a blank document.
            InternalStore damaged=new InternalStore(Path.Combine(root,"damaged"),Path.Combine(root,"no-legacy"));
            damaged.EnsureDirectory();string broken=damaged.PathFor("sesion.xml");File.WriteAllText(broken,"<session incompleta");
            using(Editor view=new Editor(true))
            {
                view.internalStore=damaged;view.Show();WelcomeCheck(!view.RestoreLatestSession() && damaged.Warnings.Count>0,"restauración fallida informa sin crear documentos");
                view.CheckEmptyWorkspace();WelcomeCheck(File.ReadAllText(broken)=="<session incompleta","archivo de recuperación dañado conservado");
            }
        }
    }
}
