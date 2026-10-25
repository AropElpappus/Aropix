// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PixelArt {
class ProjectState {
    public Document Doc;
    public string Path;
    public bool Dirty;
    public List<HistoryEntry> Undo=new List<HistoryEntry>(),Redo=new List<HistoryEntry>();
    public List<Color> Recent=new List<Color>();
    public float Zoom=4;
    public PointF Origin;
    public TabPage Tab;
}
partial class Editor {
    ProjectTabs projectTabs=new ProjectTabs{Dock=DockStyle.Fill};
    List<ProjectState> projects=new List<ProjectState>();
    ProjectState activeProject;
    bool changingTab;
    void StoreActive(){if(activeProject==null)return;activeProject.Doc=doc;activeProject.Path=projectPath;activeProject.Dirty=dirty;activeProject.Undo=undo;activeProject.Redo=redo;activeProject.Recent=history;activeProject.Zoom=canvas.Zoom;activeProject.Origin=canvas.ViewOrigin;UpdateTab();}
    void UpdateTab(){if(activeProject!=null){activeProject.Tab.Text=doc.Name+(dirty?" *":"");activeProject.Tab.ToolTipText=doc.Name+(dirty?" — Cambios sin guardar":"")+" · Cerrar: Ctrl+W o clic central";projectTabs.Invalidate();}}
    void InitializeTabs(){
        projectTabs.CloseRequested+=delegate(int index){projectTabs.SelectedIndex=index;CloseProject();};
        projectTabs.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right)for(int i=0;i<projectTabs.TabCount;i++)if(projectTabs.GetTabRect(i).Contains(e.Location)){projectTabs.SelectedIndex=i;break;}};
        projectTabs.SelectedIndexChanged+=delegate{if(!changingTab && projectTabs.SelectedTab!=null)ActivateProject((ProjectState)projectTabs.SelectedTab.Tag);};
        ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("Cerrar proyecto (Ctrl+W)",null,delegate{CloseProject();});projectTabs.ContextMenuStrip=menu;Disposed+=delegate{menu.Dispose();};
    }
    void RegisterInitial(){if(activeProject!=null){StoreActive();return;}ProjectState state=new ProjectState{Doc=doc};projects.Add(state);state.Tab=new TabPage(doc.Name){Tag=state};activeProject=state;changingTab=true;projectTabs.TabPages.Add(state.Tab);changingTab=false;StoreActive();}
    void AddProject(Document next,string path){
        canvas.CommitSelection();
        
        canvas.CommitSelection();StoreActive();ProjectState state=new ProjectState{Doc=next,Path=path};state.Tab=new TabPage(next.Name){Tag=state};projects.Add(state);
        changingTab=true;projectTabs.TabPages.Add(state.Tab);changingTab=false;ActivateProject(state);canvas.Fit();StoreActive();
    }
    void ActivateProject(ProjectState state){
        if(state==null || state==activeProject)return;
        canvas.CommitSelection();StoreActive();canvas.CancelSelection();canvas.CancelShape();activeProject=state;
        doc=state.Doc;projectPath=state.Path;dirty=state.Dirty;undo=state.Undo;redo=state.Redo;history=state.Recent;canvas.Doc=doc;canvas.Zoom=state.Zoom;canvas.ViewOrigin=state.Origin;
        changingTab=true;projectTabs.SelectedTab=state.Tab;changingTab=false;RefreshColors();UpdateStatus();UpdateTab();Text=doc.Name+(dirty?" *":"")+" — Aropix";canvas.ClearPointer();canvas.Invalidate();RefreshDocumentUI();
    }
    void ReleaseProject(ProjectState state){state.Doc.Dispose();ClearHistory(state.Undo);ClearHistory(state.Redo);state.Tab.Dispose();}
    void CloseProject(Func<DialogResult> decide=null,Func<bool> save=null){
        if(activeProject==null || !ConfirmDiscard(decide,save))return;StoreActive();ProjectState old=activeProject;int index=projects.IndexOf(old);projects.Remove(old);
        changingTab=true;projectTabs.TabPages.Remove(old.Tab);changingTab=false;activeProject=null;canvas.CancelSelection();canvas.Doc=null;doc=null;ReleaseProject(old);
        if(projects.Count==0)EnterEmptyWorkspace();else ActivateProject(projects[Math.Min(index,projects.Count-1)]);
    }
    bool ConfirmAllProjects(Func<ProjectState,DialogResult> decide=null,Func<bool> save=null){
        discardedOnExit.Clear();ProjectState original=activeProject;
        foreach(ProjectState state in projects.ToArray()){
            ActivateProject(state);canvas.CommitSelection();if(!dirty)continue;
            DialogResult answer=decide!=null?decide(state):MessageBox.Show(this,"¿Quieres guardar los cambios de «"+doc.Name+"»?\nSi eliges No, esos cambios no se recuperarán en la próxima sesión.","Cerrar proyecto",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(answer==DialogResult.Cancel || (answer==DialogResult.Yes && !(save!=null?save():SaveProject(false)))){discardedOnExit.Clear();ActivateProject(original);return false;}
            if(answer==DialogResult.No)discardedOnExit.Add(state);
        }
        ActivateProject(original);return true;
    }
    void ResizeSelected(){
        if(canvas.Selection.IsEmpty)return;
        using(Form dialog=new SecondaryDialog{Text="Tamaño de la selección",ClientSize=new Size(340,200),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,BackColor=PanelColor,ForeColor=Color.White,Font=Font}){
            dialog.Controls.Add(new Label{Text="Ancho (px)",Location=new Point(20,20),AutoSize=true});dialog.Controls.Add(new Label{Text="Alto (px)",Location=new Point(180,20),AutoSize=true});
            NumericUpDown w=new NumericUpDown{Minimum=1,Maximum=2048,Value=canvas.Selection.Width,Location=new Point(20,50),Width=130},h=new NumericUpDown{Minimum=1,Maximum=2048,Value=canvas.Selection.Height,Location=new Point(180,50),Width=130};dialog.Controls.Add(w);dialog.Controls.Add(h);
            dialog.Controls.Add(new Label{Text="Píxeles nítidos, sin suavizado.\nEnter confirma la vista previa; Esc la cancela.",Location=new Point(20,90),Size=new Size(300,50)});
            Button ok=Button("Aplicar",110);ok.Location=new Point(200,150);ok.DialogResult=DialogResult.OK;dialog.Controls.Add(ok);dialog.AcceptButton=ok;
            if(dialog.ShowDialog(this)==DialogResult.OK)canvas.ResizeSelection((int)w.Value,(int)h.Value);
        }
    }
    public void TestTransformsAndTabs(){
        SetDocument(new Document(20,20));doc.Name="Proyecto A";doc.Image.SetPixel(2,2,Color.Red);doc.Image.SetPixel(3,4,Color.Blue);SetTool("Selección");
        canvas.BeginSelection(new Point(2,2));canvas.UpdateSelection(new Point(3,4));canvas.EndSelectionGesture();
        canvas.RotateSelection(RotateFlipType.Rotate90FlipNone);
        using(Bitmap view=canvas.CopySelection()){if(view.Width!=3 || view.Height!=2 || view.GetPixel(2,0).ToArgb()!=Color.Red.ToArgb() || view.GetPixel(0,1).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Giro 90 grados");}
        canvas.RotateSelection(RotateFlipType.RotateNoneFlipX);canvas.RotateSelection(RotateFlipType.RotateNoneFlipY);
        using(Bitmap view=canvas.CopySelection()){if(view.GetPixel(0,1).ToArgb()!=Color.Red.ToArgb() || view.GetPixel(2,0).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Voltear H y V");}
        canvas.ResizeSelection(6,4);
        using(Bitmap view=canvas.CopySelection()){if(view.GetPixel(0,2).ToArgb()!=Color.Red.ToArgb() || view.GetPixel(1,3).ToArgb()!=Color.Red.ToArgb() || view.GetPixel(5,0).ToArgb()!=Color.Blue.ToArgb() || view.GetPixel(3,2).A!=0)throw new Exception("Escala exacta sin suavizado");}
        canvas.CommitSelection();if(undo.Count!=1)throw new Exception("Transformación como una acción");Undo(false);if(doc.Image.GetPixel(3,4).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Deshacer transformación");Undo(true);
        canvas.BeginSelection(new Point(2,2));canvas.UpdateSelection(new Point(7,5));canvas.EndSelectionGesture();CopyPixels();canvas.RotateSelection(RotateFlipType.Rotate90FlipNone);canvas.CancelSelection();if(undo.Count!=1)throw new Exception("Cancelar transformación");
        doc.Palette.Add(Color.Red);history.Add(Color.Blue);projectPath="test-a.pixelart";canvas.Zoom=12;canvas.ViewOrigin=new PointF(-30,10);ProjectState first=activeProject;
        AddProject(new Document(30,30){Name="Proyecto B"},"test-b.pixelart");ProjectState second=activeProject;
        if(projects.Count!=2 || undo.Count!=0 || dirty || doc.Palette.Count!=0)throw new Exception("Proyecto nuevo independiente");
        PastePixels();canvas.CommitSelection();if(undo.Count!=1 || !dirty)throw new Exception("Pegar entre pestañas");
        ActivateProject(first);if(doc.Name!="Proyecto A" || projectPath!="test-a.pixelart" || undo.Count!=1 || !dirty || canvas.Zoom!=12 || canvas.ViewOrigin!=new PointF(-30,10) || doc.Palette.Count!=1 || history.Count!=1)throw new Exception("Estado por pestaña");
        ActivateProject(second);dirty=false;StoreActive();CloseProject();if(activeProject!=first || projects.Count!=1)throw new Exception("Cerrar pestaña conserva vecina");
        dirty=false;StoreActive();CloseProject();if(projects.Count!=0 || doc!=null || canvas.Doc!=null)throw new Exception("Cerrar última pestaña deja el editor sin documentos");SetTool("Lápiz");
    }
}
partial class PixelCanvas {
    public PointF ViewOrigin {get{return origin;}set{origin=value;ReprojectPointer();Invalidate();}}
    Bitmap gridLayer;
    PointF gridOrigin;
    Size gridDocSize;
    float gridZoom;
    public int GridBuilds {get;private set;}
    void PaintGrid(Graphics g,RectangleF rect){
        if(!Grid || Zoom<8 || Width<1 || Height<1)return;
        if(gridLayer==null || gridLayer.Width!=Width || gridLayer.Height!=Height || gridOrigin!=origin || gridZoom!=Zoom || gridDocSize!=Doc.Image.Size){
            if(gridLayer!=null)gridLayer.Dispose();gridLayer=new Bitmap(Width,Height);gridOrigin=origin;gridZoom=Zoom;gridDocSize=Doc.Image.Size;GridBuilds++;
            using(Graphics layer=Graphics.FromImage(gridLayer))using(Pen light=new Pen(Color.FromArgb(170,255,255,255)))using(Pen dark=new Pen(Color.FromArgb(210,0,0,0))){
                layer.SetClip(rect);layer.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;dark.DashPattern=new float[]{2,2};
                int x0=Math.Max(0,(int)Math.Floor(-origin.X/Zoom)),x1=Math.Min(Doc.Image.Width,(int)Math.Ceiling((Width-origin.X)/Zoom));
                int y0=Math.Max(0,(int)Math.Floor(-origin.Y/Zoom)),y1=Math.Min(Doc.Image.Height,(int)Math.Ceiling((Height-origin.Y)/Zoom));
                for(int x=x0;x<=x1;x++){float xx=origin.X+x*Zoom;layer.DrawLine(light,xx,Math.Max(0,rect.Top),xx,Math.Min(Height,rect.Bottom));layer.DrawLine(dark,xx,Math.Max(0,rect.Top),xx,Math.Min(Height,rect.Bottom));}
                for(int y=y0;y<=y1;y++){float yy=origin.Y+y*Zoom;layer.DrawLine(light,Math.Max(0,rect.Left),yy,Math.Min(Width,rect.Right),yy);layer.DrawLine(dark,Math.Max(0,rect.Left),yy,Math.Min(Width,rect.Right),yy);}
            }
        }
        g.DrawImageUnscaled(gridLayer,Point.Empty);
    }
}
}



