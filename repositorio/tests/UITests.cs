// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace PixelArt {
partial class Editor {
    public void TestReorganizedUI(){if(copiedSelection!=null){copiedSelection.Dispose();copiedSelection=null;}
        RestoreDefaultLayout();SetDocument(new Document(100,100));SetTool("Lápiz");SyncCommands();Application.DoEvents();
        string[] menus={"Archivo","Editar","Selección","Lienzo","Capas","Dibujo","Ver","Ayuda"};for(int i=0;i<menus.Length;i++)if(mainMenus.Items[i].Text!=menus[i])throw new Exception("Orden de menús");
        if(CanRun("copy") || CanRun("paste") || CanRun("rotateR") || CanRun("layerdown") || CanRun("layerup") || CanRun("layermerge"))throw new Exception("Comandos deshabilitados sin selección o capa aplicable");
        if(figureButton.Bottom>figureButton.Parent.ClientSize.Height || !figureButton.Visible || layerMore.Bottom>layerActions.ClientSize.Height)throw new Exception("Acceso a figuras y menú de capas dentro del panel");
        pencilSizes.SelectedIndex=6;SetTool("Borrador");eraserSizes.SelectedIndex=3;SetTool("Círculo");if(optionGroups["pencil"][0].Visible || optionGroups["eraser"][0].Visible || !circleCenter.Visible || !shapeFill.Visible)throw new Exception("Solo propiedades de círculo");shapeFill.Checked=true;SetTool("Línea recta");if(shapeFill.Visible || circleCenter.Visible || !shapeStroke.Enabled)throw new Exception("Propiedades de línea");
        SetTool("Lápiz");if(canvas.PencilSize!=7 || canvas.EraserSize!=4 || pencilSizes.SelectedIndex!=6)throw new Exception("Conservar tamaños al cambiar herramienta");SetTool("Cubeta");if(pencilSizes.Visible || shapeStroke.Visible || shapeFill.Visible || circleCenter.Visible)throw new Exception("Herramienta sin propiedades");
        SetTool("Selección");canvas.SelectBounds(new Rectangle(1,1,3,3));doc.Image.SetPixel(1,1,Color.Red);SyncCommands();if(!CanRun("copy") || !CanRun("rotateR") || !selectionContext.Visible || confirmContext.Visible)throw new Exception("Barra contextual para selección");ExecuteCommand("copy");ExecuteCommand("paste");SyncCommands();if(!confirmContext.Visible || !cancelContext.Visible)throw new Exception("Controles temporales para pegado");ExecuteCommand("cancel");if(canvas.HasPendingSelection || selectionContext.Visible)throw new Exception("Cancelar pegado desde comando");
        ExecuteCommand("paste");ExecuteCommand("confirm");if(canvas.HasPendingSelection)throw new Exception("Confirmar desde comando");ExecuteCommand("deselect");SetLayerLocked(true);if(CanRun("paste") || CanRun("layerdelete"))throw new Exception("Restricciones de capa bloqueada en menús");SetLayerLocked(false);
        ExecuteCommand("grid");if(canvas.Grid)throw new Exception("Cuadrícula compartida entre menú y acceso rápido");ExecuteCommand("grid");ExecuteCommand("coords2");if(canvas.CoordinateMode!=2)throw new Exception("Coordenadas excluyentes");ExecuteCommand("coords1");
        string path=Path.Combine(Path.GetTempPath(),"aropix-ui-"+Guid.NewGuid()+".xml");
        try{
            leftDockWidth=240;rightDockWidth=310;colorShare=.6;recentExpanded=true;basicExpanded=false;RefreshColorSections();basicsPanel.HidePanel();LayoutPanels();SaveSettings(path);
            using(Editor restored=new Editor(true)){restored.Show();restored.LoadSettings(path);if(restored.leftDockWidth!=240 || restored.rightDockWidth!=310 || Math.Abs(restored.colorShare-.6)>.001 || !restored.recentExpanded || !restored.basicsPanel.IsHidden)throw new Exception("Restaurar anchos, reparto, secciones y visibilidad");}
            File.WriteAllText(path,"<preferences corrupt");LoadSettings(path);if(basicsPanel.IsHidden || leftDockWidth!=210)throw new Exception("Preferencias corruptas usan distribución predeterminada");
        }finally{if(File.Exists(path))File.Delete(path);}
        RestoreDefaultLayout();SetDocument(new Document(100,100));SetTool("Lápiz");canvas.PencilSize=canvas.EraserSize=canvas.ShapeThickness=1;canvas.FilledShapes=false;RefreshToolOptions();SyncCommands();UpdateStatus();UpdateCoordinates();Application.DoEvents();
        Size normal=Size;CaptureUI("aropix-general.png");Size=new Size(980,640);Application.DoEvents();LayoutPanels();if(canvas.Width<350 || !DockDividersFit() || layerMore.Bottom>layerActions.Height)throw new Exception("Distribución funcional en ventana mínima");CaptureUI("aropix-compacto.png");
        SetTool("Círculo");CaptureUI("aropix-circulo.png");Size=normal;RestoreDefaultLayout();SetTool("Selección");canvas.SelectBounds(new Rectangle(10,10,20,20));CopyPixels();PastePixels();SyncCommands();Application.DoEvents();CaptureUI("aropix-pegado.png");canvas.CancelSelection();SetTool("Lápiz");SyncCommands();
        File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath,"ui-geometria.txt"),"Estado: "+status.Bounds+" texto="+status.Text+"\nCoordenadas: "+coordinates.Bounds+"\nAuto: "+autoSaveLabel.Bounds+" texto="+autoSaveLabel.Text+"\nLienzo: "+canvas.Bounds);
    }
    void CaptureUI(string name){BringToFront();Activate();Refresh();Application.DoEvents();using(Bitmap image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(TestArtifacts.DirectoryPath,name),ImageFormat.Png);}}
}
}




namespace PixelArt {
partial class Editor {
    public void TestVisualTheme(){
        using(Editor view=new Editor(true)){
            view.Show();view.RestoreDefaultLayout();view.SetDocument(new Document(128,96){Name="Bosque — escena principal"});
            using(Graphics g=Graphics.FromImage(view.doc.Image)){
                g.Clear(Color.FromArgb(27,42,62));
                using(Brush moon=new SolidBrush(Color.FromArgb(239,224,176)))g.FillRectangle(moon,92,12,12,12);
                using(Brush hills=new SolidBrush(Color.FromArgb(43,85,92))){g.FillRectangle(hills,0,51,128,45);g.FillRectangle(hills,20,44,38,10);g.FillRectangle(hills,75,38,35,20);}
                using(Brush ground=new SolidBrush(Color.FromArgb(24,58,60)))g.FillRectangle(ground,0,75,128,21);
                for(int x=9;x<120;x+=22){using(Brush trunk=new SolidBrush(Color.FromArgb(92,72,67)))g.FillRectangle(trunk,x+5,56,3,23);using(Brush leaves=new SolidBrush(Color.FromArgb(56,129,113))){g.FillRectangle(leaves,x,51,14,14);g.FillRectangle(leaves,x+3,44,8,8);}}
            }
            view.MarkDirty();ProjectState first=view.activeProject;
            view.AddProject(new Document(32,32){Name="Objeto — espada"},null);view.ActivateProject(first);view.canvas.Fit();view.SyncCommands();Application.DoEvents();
            if(!(view.projectTabs is ProjectTabs) || !view.projectTabs.TabPages[0].Text.EndsWith(" *"))throw new Exception("Pestañas modernas conservan indicador accesible");
            if(!view.projectTabs.GetTabRect(0).Contains(view.projectTabs.CloseBounds(0)))throw new Exception("Cierre dentro de la pestaña");
            if(view.commands["save"].Items[0] is ToolStripMenuItem && ((ToolStripMenuItem)view.commands["save"].Items[0]).ShortcutKeyDisplayString!="Ctrl+S")throw new Exception("Atajo de menú");
            view.CaptureUI("visual-principal.png");
            foreach(ToolStripMenuItem menu in view.mainMenus.Items)CheckVisualMenu(menu);
            ToolStripMenuItem file=(ToolStripMenuItem)view.mainMenus.Items[0];file.ShowDropDown();Application.DoEvents();
            using(Bitmap shot=new Bitmap(file.DropDown.Width,file.DropDown.Height)){file.DropDown.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"visual-menu-archivo.png"));}file.HideDropDown();
            view.projectTabs.SelectedIndex=1;Application.DoEvents();if(view.doc.Name!="Objeto — espada")throw new Exception("Activación nativa de pestaña");
            Rectangle close=view.projectTabs.CloseBounds(1);Point click=new Point(close.Left+close.Width/2,close.Top+close.Height/2);
            System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ProjectTabs).GetMethod("OnMouseDown",flags).Invoke(view.projectTabs,new object[]{new MouseEventArgs(MouseButtons.Left,1,click.X,click.Y,0)});
            typeof(ProjectTabs).GetMethod("OnMouseUp",flags).Invoke(view.projectTabs,new object[]{new MouseEventArgs(MouseButtons.Left,1,click.X,click.Y,0)});
            if(view.projects.Count!=1 || view.activeProject!=first || !view.dirty)throw new Exception("Cierre limpio conserva pestaña vecina y cambios");
            if(view.ConfirmAllProjects(delegate{return DialogResult.Cancel;}) || view.projects.Count!=1 || !view.dirty)throw new Exception("Confirmación de cierre cancelada conserva cambios");
            view.Size=new Size(980,640);view.LayoutPanels();Application.DoEvents();if(view.canvas.Width<350)throw new Exception("Lienzo en tamaño mínimo");view.CaptureUI("visual-compacto.png");
            view.colorPanel.FloatAt(new Point(view.Left+120,view.Top+140));Application.DoEvents();Form floating=view.colorPanel.FindForm();
            using(Bitmap shot=new Bitmap(floating.Width,floating.Height)){floating.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"visual-panel-flotante.png"));}
            view.colorPanel.DockTo(view.dockRight);
            // Rendering at additional resolutions checks GDI geometry, not actual per-monitor Windows DPI.
            using(AropixButton button=new AropixButton{Text="Confirmar",Size=new Size(140,34)})
            foreach(int dpi in new[]{96,144,192})using(Bitmap bitmap=new Bitmap(280,68)){bitmap.SetResolution(dpi,dpi);using(Graphics graphics=Graphics.FromImage(bitmap)){
                typeof(AropixButton).GetMethod("OnPaint",flags).Invoke(button,new object[]{new PaintEventArgs(graphics,new Rectangle(0,0,280,68))});
            }}
        }
    }
    static void CheckVisualMenu(ToolStripMenuItem menu){
        menu.ShowDropDown();Application.DoEvents();if(!(menu.DropDown.Renderer is AropixRenderer))throw new Exception("Renderer de submenú");
        foreach(ToolStripItem item in menu.DropDownItems){ToolStripMenuItem child=item as ToolStripMenuItem;if(child!=null && child.HasDropDownItems)CheckVisualMenu(child);}
        menu.HideDropDown();
    }
}
}

namespace PixelArt {
partial class Editor {
    public void TestSecondaryDialogs(){
        using(Document fixture=new Document(128,96))using(Bitmap image=new Bitmap(64,32)){
            using(Graphics g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(28,50,65));using(Brush b=new SolidBrush(Color.Turquoise))g.FillRectangle(b,16,6,30,20);}
            SecondaryDialog[] dialogs={new NewProject(),new ImportDialog(image,fixture.Image.Size),new ShadowDialog("Personaje — referencia"),new CanvasSizeDialog(fixture.Image.Size),new AxesDialog(fixture),new AutoSaveDialog(true,120),new LayerExportDialog(fixture),new NoticeDialog("Exportación", "La imagen PNG se exportó correctamente.",NoticeKind.Information)};
            string[] names={"nuevo","importar","sombra","lienzo","ejes","autoguardado","exportar-capas","mensaje"};
            try{for(int i=0;i<dialogs.Length;i++){
                SecondaryDialog dialog=dialogs[i];dialog.Show(this);Application.DoEvents();
                Button primary=dialog.AcceptButton as Button,cancel=dialog.CancelButton as Button;
                if(primary==null || cancel==null || !primary.Visible || !cancel.Visible || primary.Parent!=cancel.Parent)throw new Exception("Acciones de diálogo: "+names[i]);
                Rectangle area=Screen.FromControl(dialog).WorkingArea;if(!area.Contains(dialog.Bounds))throw new Exception("Diálogo fuera de pantalla: "+names[i]);
                NewProject create=dialog as NewProject;if(create!=null){create.W.Value=2048;create.H.Value=2048;if(create.ExportScale.Maximum!=4)throw new Exception("Límite PNG conservado");create.W.Value=100;create.H.Value=100;}
                AutoSaveDialog auto=dialog as AutoSaveDialog;if(auto!=null){auto.IntervalText.Text="inválido";primary.PerformClick();if(auto.DialogResult!=DialogResult.None || auto.IntervalText.Text!="inválido")throw new Exception("Validación conserva entrada");auto.IntervalText.Text="45 s";}
                using(Bitmap shot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(shot,new Rectangle(Point.Empty,dialog.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"dialogo-"+names[i]+".png"));}
                dialog.Hide();
            }}finally{foreach(SecondaryDialog dialog in dialogs)dialog.Dispose();}
        }
        using(NewProject dialog=new NewProject())CheckDialogKey(dialog,Keys.Enter,DialogResult.OK);
        using(NewProject dialog=new NewProject())CheckDialogKey(dialog,Keys.Escape,DialogResult.Cancel);
        using(AutoSaveDialog dialog=new AutoSaveDialog(true,120)){dialog.Shown+=delegate{dialog.IntervalText.Text="45 s";};CheckDialogKey(dialog,Keys.Enter,DialogResult.OK);if(dialog.Seconds!=45)throw new Exception("Aplicar intervalo con Enter: "+dialog.Seconds+" texto="+dialog.IntervalText.Text);}
    }
    void CheckDialogKey(SecondaryDialog dialog,Keys key,DialogResult expected){
        using(Timer timer=new Timer{Interval=150}){
            timer.Tick+=delegate{timer.Stop();typeof(Form).GetMethod("ProcessDialogKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(dialog,new object[]{key});};
            timer.Start();DialogResult actual=dialog.ShowDialog(this);timer.Stop();if(actual!=expected)throw new Exception("Tecla de diálogo: "+key);
        }
    }
}
}

namespace PixelArt {
partial class Editor {
    public void TestDockSizing(){
        TestFloatingChrome();
        string path=Path.Combine(TestArtifacts.DirectoryPath,"dock-layout.xml");
        using(Editor view=new Editor(true)){
            view.SetDocument(new Document(100,100));view.Show();view.RestoreDefaultLayout();Application.DoEvents();view.LayoutPanels();
            if(view.dockDividers.Count!=2)throw new Exception("Un divisor por lateral");
            int total=view.basicsPanel.Height+view.shapesPanel.Height;
            view.ResizeDockPair(view.basicsPanel,view.shapesPanel,200,total);
            if(Math.Abs(view.basicsPanel.Height-200)>1 || view.basicsPanel.Height+view.shapesPanel.Height!=total)throw new Exception("Reparto izquierdo conserva suma");
            int first=view.basicsPanel.Height;view.leftDockWidth=300;view.rightDockWidth=340;view.LayoutPanels();
            if(Math.Abs(view.basicsPanel.Height-first)>1)throw new Exception("Ancho independiente de alturas");
            view.ResizeDockPair(view.colorPanel,view.layersPanel,240,view.colorPanel.Height+view.layersPanel.Height);
            double remembered=view.colorPanel.DockWeight;view.colorPanel.HidePanel();
            if(view.dockDividers.Count!=1 || view.colorPanel.DockWeight!=remembered)throw new Exception("Ocultar conserva peso y retira divisor");
            view.colorPanel.Reveal();if(view.dockDividers.Count!=2 || Math.Abs(view.colorPanel.Height-240)>2)throw new Exception("Recuperar proporción");
            view.basicsPanel.Collapse(true);if(view.basicsPanel.Height!=35 || view.dockDividers.Count!=1)throw new Exception("Plegado ocupa encabezado");view.basicsPanel.Collapse(false);
            DockDivider left=view.dockDividers.Find(delegate(DockDivider d){return d.Above==view.basicsPanel;});
            typeof(Control).GetMethod("OnDoubleClick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(left,new object[]{EventArgs.Empty});
            if(Math.Abs(view.basicsPanel.Height-view.shapesPanel.Height)>1)throw new Exception("Doble clic equilibra");
            view.CaptureUI("paneles-dos-laterales.png");
            view.colorPanel.DockTo(view.dockLeft);view.layersPanel.DockTo(view.dockLeft);view.LayoutPanels();
            if(view.dockDividers.Count!=3)throw new Exception("Tres divisores para cuatro paneles");
            view.dockLeft.Controls.SetChildIndex(view.layersPanel,0);view.LayoutPanels();
            if(DockPanels(view.dockLeft,true)[0]!=view.layersPanel || view.dockDividers.Find(delegate(DockDivider d){return d.Above==view.layersPanel;})==null)throw new Exception("Reordenar recalcula vecinos");
            view.shapesPanel.HidePanel();view.SaveSettings(path);
            using(Editor restored=new Editor(true)){
                restored.Show();restored.LoadSettings(path);if(restored.leftDockWidth!=300 || restored.rightDockWidth!=340 || !restored.shapesPanel.IsHidden || DockPanels(restored.dockLeft,false)[0]!=restored.layersPanel)throw new Exception("Restaurar lado orden ancho visibilidad");
                if(Math.Abs(restored.colorPanel.DockWeight-view.colorPanel.DockWeight)>.01)throw new Exception("Restaurar pesos individuales");
            }
            view.shapesPanel.Reveal();view.Size=new Size(980,640);Application.DoEvents();view.LayoutPanels();
            foreach(ToolPanel panel in DockPanels(view.dockLeft,true))if(panel.Height<panel.MinimumDockHeight)throw new Exception("Alturas mínimas");
            if(!view.dockLeft.VerticalScroll.Visible || view.canvas.Width<350)throw new Exception("Desplazamiento con espacio insuficiente");
            view.CaptureUI("paneles-cuatro-izquierda.png");
            view.dockLeft.ScrollControlIntoView(view.colorPanel);view.CaptureUI("paneles-cuatro-desplazados.png");
            view.colorPanel.FloatAt(new Point(view.Left+80,view.Top+90));view.colorPanel.FindForm().Size=new Size(350,430);view.SaveSettings(path);
            if(view.dockDividers.Count!=2 || view.colorPanel.Controls.Count!=2)throw new Exception("Flotante sin divisores internos");
            using(Editor restored=new Editor(true)){restored.Show();restored.LoadSettings(path);if(!restored.colorPanel.IsFloating || restored.colorPanel.FindForm().Size!=new Size(350,430))throw new Exception("Tamaño flotante persistente");}
            view.colorPanel.DockTo(view.dockRight);view.LayoutPanels();if(view.dockDividers.Count!=2)throw new Exception("Reacoplar recalcula divisores");
            System.Xml.XmlDocument xml=new System.Xml.XmlDocument();xml.Load(path);xml.DocumentElement.SetAttribute("layoutVersion","2");xml.DocumentElement.SetAttribute("colorShare","0.6");foreach(System.Xml.XmlElement node in xml.DocumentElement.ChildNodes)node.RemoveAttribute("weight");xml.Save(path);
            view.LoadSettings(path);if(view.colorPanel.DockWeight!=600 || view.layersPanel.DockWeight!=400)throw new Exception("Migrar colorShare antiguo");
            xml.DocumentElement.SetAttribute("layoutVersion","3");xml.DocumentElement["basic"].SetAttribute("weight","NaN");xml.DocumentElement["shapes"].SetAttribute("weight","-1");xml.Save(path);view.LoadSettings(path);
            if(view.basicsPanel.DockWeight!=150 || view.shapesPanel.DockWeight!=300)throw new Exception("Pesos inválidos usan valores predeterminados");
            view.RestoreDefaultLayout();if(view.dockDividers.Count!=2 || view.basicsPanel.DockWeight!=150 || view.colorPanel.DockWeight!=480)throw new Exception("Restablecer distribución completa");
            view.SetTool("Lápiz");view.doc.Stamp(0,0,Color.Red,1);view.canvas.Invalidate();Application.DoEvents();if(view.doc.Image.GetPixel(0,0).ToArgb()!=Color.Red.ToArgb())throw new Exception("Lienzo tras redistribución");
        }
    }
    void TestFloatingChrome(){
        using(Editor view=new Editor(true)){
            view.Show();view.RestoreDefaultLayout();
            ToolPanel[] panels={view.basicsPanel,view.shapesPanel,view.colorPanel,view.layersPanel};
            foreach(ToolPanel panel in panels){
                Control content=panel.Content;
                panel.FloatAt(new Point(view.Left+100,view.Top+100));
                FloatingPanelForm form=panel.FindForm() as FloatingPanelForm;
                if(form==null || form.FormBorderStyle!=FormBorderStyle.None || form.Owner!=view || form.ShowInTaskbar)throw new Exception("Marco y propietario del panel flotante");
                Button close=null;
                foreach(Control header in panel.Controls)foreach(Control child in header.Controls)if(child.AccessibleName=="Ocultar panel")close=child as Button;
                if(close==null || !close.Visible)throw new Exception("X integrada visible");
                form.Size=new Size(350,430);
                int g=form.ResizeGrip;
                Point[] points={new Point(0,200),new Point(349,200),new Point(170,0),new Point(170,429),new Point(0,0),new Point(349,0),new Point(0,429),new Point(349,429)};
                int[] hits={10,11,12,15,13,14,16,17};
                for(int i=0;i<points.Length;i++){
                    Point screen=form.PointToScreen(points[i]);
                    long packed=((long)(ushort)screen.Y<<16)|(ushort)screen.X;
                    Message message=Message.Create(form.Handle,0x84,IntPtr.Zero,new IntPtr(packed));
                    object[] args={message};
                    typeof(FloatingPanelForm).GetMethod("WndProc",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(form,args);
                    if(((Message)args[0]).Result.ToInt32()!=hits[i])throw new Exception("Hit test del borde "+i);
                }
                if(FloatingPanelForm.BorderHit(new Point(100,100),form.ClientSize,g)!=1)throw new Exception("Interior no redimensiona");
                foreach(int dpi in new int[]{96,144,192})if(FloatingPanelForm.BorderHit(new Point(0,0),new Size(350,430),(int)Math.Round(7*dpi/96.0))!=13)throw new Exception("Borde escalado");
                panel.Collapse(true);panel.HidePanel();panel.Reveal();
                if(!panel.IsCollapsed || panel.Height!=35)throw new Exception("Recuperar panel plegado: "+panel.IsCollapsed+" panel="+panel.Size+" form="+form.Size+" client="+form.ClientSize+" padding="+form.Padding);
                panel.Collapse(false);if(form.Height!=430)throw new Exception("Recuperar altura expandida");
                close.PerformClick();if(!panel.IsHidden || form.IsDisposed || !object.ReferenceEquals(content,panel.Content))throw new Exception("X oculta sin destruir");
                panel.Reveal();form.Close();if(!panel.IsHidden || form.IsDisposed)throw new Exception("Cierre del sistema oculta");panel.Reveal();
                if(panel==view.colorPanel || panel==view.layersPanel){
                    using(Bitmap shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,form.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"flotante-"+panel.PanelTitle+".png"),ImageFormat.Png);}
                }
                System.Xml.XmlDocument xml=new System.Xml.XmlDocument();xml.LoadXml("<panel/>");panel.SaveState(xml.DocumentElement);
                panel.DockTo(view.dockLeft);if(close.Visible || !form.IsDisposed)throw new Exception("Acoplar retira controles flotantes");
                panel.RestoreState(xml.DocumentElement);if(panel.FindForm().Size!=new Size(350,430))throw new Exception("Persistencia del marco nuevo");
                xml.DocumentElement.RemoveAttribute("floatingFrame");xml.DocumentElement.SetAttribute("expandedHeight","320");xml.DocumentElement.SetAttribute("x","-30000");xml.DocumentElement.SetAttribute("y","-30000");
                panel.RestoreState(xml.DocumentElement);
                if(panel.Height!=320 || !Screen.FromControl(panel.FindForm()).WorkingArea.Contains(panel.FindForm().Bounds))throw new Exception("Migración y posición visible");
                panel.DockTo(view.dockRight);
            }
            using(NewProject dialog=new NewProject())if(dialog.FormBorderStyle==FormBorderStyle.None)throw new Exception("Los diálogos conservan su barra nativa");
            view.colorPanel.FloatAt(new Point(view.Left+80,view.Top+80));Form owned=view.colorPanel.FindForm();view.Dispose();if(!owned.IsDisposed)throw new Exception("Cerrar propietario libera flotante");
        }
    }
}
}
