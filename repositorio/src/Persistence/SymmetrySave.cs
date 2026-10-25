// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;
namespace PixelArt {
partial class Document {
    public bool MirrorVertical,MirrorHorizontal;
    public Point[] MirrorPoints(Point p){int x=AxisX2-p.X,y=AxisY2-p.Y;return MirrorVertical && MirrorHorizontal?new Point[]{p,new Point(x,p.Y),new Point(p.X,y),new Point(x,y)}:MirrorVertical?new Point[]{p,new Point(x,p.Y)}:MirrorHorizontal?new Point[]{p,new Point(p.X,y)}:new Point[]{p};}
    public bool CanSymmetryFill(Point p,Color color){foreach(Point seed in MirrorPoints(p))if(CanFill(seed,color))return true;return false;}
    void SymmetryPixel(int x,int y,Color color){int rx=AxisX2-x,ry=AxisY2-y;bool validX=rx>=0 && rx<Image.Width,validY=ry>=0 && ry<Image.Height;Image.SetPixel(x,y,color);if(MirrorVertical && validX)Image.SetPixel(rx,y,color);if(MirrorHorizontal && validY)Image.SetPixel(x,ry,color);if(MirrorVertical && MirrorHorizontal && validX && validY)Image.SetPixel(rx,ry,color);}
    void SymmetryBufferPixel(int[] pixels,int width,int height,int x,int y,int color){int rx=(MirrorAxisX2??(width-1))-x,ry=(MirrorAxisY2??(height-1))-y;bool validX=rx>=0 && rx<width,validY=ry>=0 && ry<height;pixels[y*width+x]=color;if(MirrorVertical && validX)pixels[y*width+rx]=color;if(MirrorHorizontal && validY)pixels[ry*width+x]=color;if(MirrorVertical && MirrorHorizontal && validX && validY)pixels[ry*width+rx]=color;}
}
partial class PixelCanvas {
    public bool IsEditing {get{return drawing || rightHeld || HasSelectionGesture || HasPendingSelection || PendingPoints>0;}}
    void PaintSymmetry(Graphics g,RectangleF rect){using(Pen pen=new Pen(Color.FromArgb(210,255,190,65),1)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;if(Doc.MirrorVertical)g.DrawLine(pen,rect.Left+(Doc.AxisX2+1)*Zoom/2,rect.Top,rect.Left+(Doc.AxisX2+1)*Zoom/2,rect.Bottom);if(Doc.MirrorHorizontal)g.DrawLine(pen,rect.Left,rect.Top+(Doc.AxisY2+1)*Zoom/2,rect.Right,rect.Top+(Doc.AxisY2+1)*Zoom/2);}}
}
class AutoSaveDialog : SecondaryDialog {
    public CheckBox EnabledOption;
    public ComboBox IntervalText;
    public int Seconds;
    public AutoSaveDialog(bool enabled,int seconds){
        Text="Guardado automático";ClientSize=new Size(460,285);BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        EnabledOption=new CheckBox{Text="Activar guardado automático",Checked=enabled,Location=new Point(20,20),Width=400};Controls.Add(EnabledOption);
        Controls.Add(new Label{Text="Intervalo (elige o escribe, por ejemplo: 45 s o 2 min)",Location=new Point(20,60),Size=new Size(425,30)});
        IntervalText=new ComboBox{Location=new Point(20,95),Width=410,DropDownStyle=ComboBoxStyle.DropDown};IntervalText.Items.AddRange(new object[]{"15 s","30 s","1 min","2 min","5 min","10 min"});IntervalText.Text=seconds%60==0?(seconds/60)+" min":seconds+" s";Controls.Add(IntervalText);
        Controls.Add(new Label{Text="Actualiza los proyectos que ya tienen archivo.\nLos nuevos se conservan en una copia de recuperación.\nEspera a que termines el trazo o confirmes la selección.\nIntervalo permitido: de 1 segundo a 24 horas.",Location=new Point(20,140),Size=new Size(425,85)});
        Button cancel=Editor.Button("Cancelar",100);cancel.Location=new Point(220,240);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);Button save=Editor.Button("Aplicar",100);save.Location=new Point(330,240);Controls.Add(save);CancelButton=cancel;AcceptButton=save;
        Label validation=new Label{Text="",ForeColor=AropixTheme.Error,Bounds=new Rectangle(20,123,425,20)};Controls.Add(validation);IntervalText.TextChanged+=delegate{validation.Text="";};InitialField=IntervalText;save.Click+=delegate{int parsed;if(!TryInterval(IntervalText.Text,out parsed)){validation.Text="Introduce un intervalo de 1 s a 24 h (ejemplo: 30 s).";IntervalText.Focus();return;}validation.Text="";Seconds=parsed;DialogResult=DialogResult.OK;};
    }
    public static bool TryInterval(string text,out int seconds){seconds=0;string value=text.Trim().ToLowerInvariant();double multiplier=60;string[] units={"segundos","segundo","seconds","second","minutos","minuto","minutes","minute","seg","min","s","m"};foreach(string unit in units)if(value.EndsWith(unit,StringComparison.Ordinal)){multiplier=unit.StartsWith("s")?1:60;value=value.Substring(0,value.Length-unit.Length).Trim();break;}double number;if(!double.TryParse(value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out number) || double.IsNaN(number) || number*multiplier<1 || number*multiplier>86400)return false;seconds=(int)Math.Ceiling(number*multiplier);return true;}
}
partial class Editor {
    readonly Timer autoSaveTimer=new Timer();
    bool autoSaveEnabled,previewEditor,autoSaveBusy;
    int autoSaveSeconds=120;
    string autoSaveStatus="Desactivado";
    string RecoveryPath {get{return internalStore.PathFor("recuperacion-automatica.xml");}}
    void InitializeAutoSave(bool preview){previewEditor=preview;
        autoSaveTimer.Tick+=delegate{if(closingPersistence || autoSaveBusy || doc==null || canvas.IsEditing || (Form.ActiveForm!=null && Form.ActiveForm.Modal))return;autoSaveBusy=true;try{RunAutoSave(RecoveryPath);autoSaveStatus="Último: "+DateTime.Now.ToString("HH:mm:ss");}catch(Exception ex){autoSaveStatus="Error: "+ex.Message;Text="Error de autoguardado — "+doc.Name;}finally{autoSaveBusy=false;}};
    }
    void PersistAutoSave(){try{if(!previewEditor)SaveSettings(SettingsPath);}catch(Exception ex){Error(ex);}} void ConfigureAutoSave(){using(AutoSaveDialog dialog=new AutoSaveDialog(autoSaveEnabled,autoSaveSeconds))if(dialog.ShowDialog(this)==DialogResult.OK){autoSaveEnabled=dialog.EnabledOption.Checked;autoSaveSeconds=dialog.Seconds;ApplyAutoSave();PersistAutoSave();}}
    void ApplyAutoSave(){autoSaveTimer.Stop();autoSaveTimer.Interval=autoSaveSeconds*1000;autoSaveStatus=autoSaveEnabled?"Cada "+autoSaveSeconds+" s":"Desactivado";if(autoSaveEnabled && !previewEditor)autoSaveTimer.Start();}
    void RunAutoSave(string recovery){
        if(doc==null || closingPersistence || canvas.IsEditing)return;StoreActive();discardedOnExit.Clear();
        // Recovery is written first, so it remains available even if an original path fails.
        SaveSession(recovery);
        foreach(ProjectState state in projects)if(state.Dirty && !string.IsNullOrEmpty(state.Path)){state.Doc.Save(state.Path);state.Dirty=false;if(state==activeProject)dirty=false;state.Tab.Text=state.Doc.Name;}
        Text=doc.Name+(dirty?" *":"")+" — Aropix";StoreActive();
    }
    void SaveAutoSaveSettings(XmlElement root){root.SetAttribute("autoSave",autoSaveEnabled.ToString());root.SetAttribute("autoSaveSeconds",autoSaveSeconds.ToString());}
    void ReadAutoSaveSettings(XmlElement root){if(root.HasAttribute("autoSave"))autoSaveEnabled=root.GetAttribute("autoSave")=="True";int seconds;if(int.TryParse(root.GetAttribute("autoSaveSeconds"),out seconds) && seconds>=1 && seconds<=86400)autoSaveSeconds=seconds;ApplyAutoSave();}
    public void TestSymmetryAndAutoSave(){
        SetDocument(new Document(10,10));SetColor(Color.Red);doc.MirrorVertical=true;canvas.SelectTool("Línea recta");canvas.ShapeClick(new Point(1,1));canvas.ShapeClick(new Point(1,3));if(undo.Count!=1 || doc.Image.GetPixel(8,2).A==0)throw new Exception("Simetría como una acción");Undo(false);if(doc.Image.GetPixel(8,2).A!=0)throw new Exception("Deshacer ambos reflejos");Undo(true);SetLayerLocked(true);canvas.ShapeClick(new Point(2,2));canvas.ShapeClick(new Point(2,4));if(doc.Image.GetPixel(7,3).A!=0)throw new Exception("Simetría respeta bloqueo");SetDocument(new Document(100,100));
        foreach(int width in new int[]{15,16})foreach(string shape in new string[]{"Línea recta","Rectángulo","Círculo","Triángulo"})foreach(bool filled in new bool[]{false,true})using(Document original=new Document(width,15))using(Document mirror=new Document(width,15)){
            original.Shape(shape,new Point(2,3),new Point(5,5),new Point(1,6),Color.Red,filled,2);mirror.MirrorVertical=true;mirror.MirrorHorizontal=true;mirror.Shape(shape,new Point(2,3),new Point(5,5),new Point(1,6),Color.Red,filled,2);
            for(int y=0;y<15;y++)for(int x=0;x<width;x++){bool expected=original.Image.GetPixel(x,y).A>0 || original.Image.GetPixel(width-1-x,y).A>0 || original.Image.GetPixel(x,14-y).A>0 || original.Image.GetPixel(width-1-x,14-y).A>0;if((mirror.Image.GetPixel(x,y).A>0)!=expected)throw new Exception("Simetría exacta de figuras y grosores pares");}
        }
        using(Document mirror=new Document(10,10)){
            mirror.MirrorVertical=true;mirror.Stamp(1,2,Color.Red,2);if(mirror.Image.GetPixel(8,2).A==0 || mirror.Image.GetPixel(7,3).A==0 || mirror.Image.GetPixel(1,7).A!=0)throw new Exception("Simetría vertical de lápiz");mirror.MirrorHorizontal=true;mirror.Stamp(1,2,Color.Blue,1);mirror.Stamp(1,2,Color.Transparent,1);if(mirror.Image.GetPixel(8,7).A!=0)throw new Exception("Borrador simétrico");
            using(Graphics g=Graphics.FromImage(mirror.Image))g.Clear(Color.Transparent);mirror.Line(new Point(4,0),new Point(4,9),Color.Black);mirror.FloodFill(Point.Empty,Color.Blue);if(mirror.Image.GetPixel(9,9).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Cubeta simétrica");
            using(Document loaded=Document.Deserialize(mirror.Serialize().DocumentElement))if(!loaded.MirrorVertical || !loaded.MirrorHorizontal)throw new Exception("Guardar simetría del proyecto");
        }
        int seconds;if(!AutoSaveDialog.TryInterval("30 segundos",out seconds) || seconds!=30 || !AutoSaveDialog.TryInterval("1,5 minutos",out seconds) || seconds!=90 || !AutoSaveDialog.TryInterval("2",out seconds) || seconds!=120 || AutoSaveDialog.TryInterval("0 s",out seconds) || AutoSaveDialog.TryInterval("abc",out seconds) || AutoSaveDialog.TryInterval("99999 min",out seconds))throw new Exception("Intervalos personalizados");
        string prefix=Path.Combine(Path.GetTempPath(),"pixelart-auto-"+Guid.NewGuid()),named=prefix+".pixelart",recovery=prefix+".xml",settings=prefix+".settings";
        try{
            SetDocument(new Document(10,10));doc.Name="Guardado";projectPath=named;doc.Save(named);SetColor(Color.Red);canvas.SelectTool("Lápiz");doc.Stamp(1,1,Color.Red,1);MarkDirty();RunAutoSave(recovery);
            using(Document saved=Document.Load(named))if(saved.Image.GetPixel(1,1).A==0 || dirty)throw new Exception("Autoguardar archivo existente");
            AddProject(new Document(12,12){Name="Sin archivo"},null);doc.Stamp(3,3,Color.Blue,1);doc.MirrorHorizontal=true;MarkDirty();RunAutoSave(recovery);
            using(Editor restored=new Editor(true)){restored.Show();if(!restored.LoadSession(recovery) || restored.projects.Count!=2 || restored.doc.Name!="Sin archivo" || restored.doc.Image.GetPixel(3,3).A==0 || !restored.doc.MirrorHorizontal)throw new Exception("Recuperar borrador y pestañas de autoguardado");}
            string before=File.ReadAllText(recovery);canvas.SelectTool("Línea recta");canvas.ShapeClick(Point.Empty);RunAutoSave(recovery);if(File.ReadAllText(recovery)!=before)throw new Exception("Autoguardado espera gesto pendiente");canvas.CancelShape();
            autoSaveEnabled=true;autoSaveSeconds=45;SaveSettings(settings);autoSaveEnabled=false;autoSaveSeconds=120;LoadSettings(settings);if(!autoSaveEnabled || autoSaveSeconds!=45 || autoSaveTimer.Enabled)throw new Exception("Preferencias autoguardado y aislamiento de pruebas");
            using(AutoSaveDialog dialog=new AutoSaveDialog(true,45)){dialog.Show();Application.DoEvents();using(Bitmap shot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(shot,new Rectangle(Point.Empty,dialog.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-autoguardado.png"));}}
        }finally{foreach(string path in new string[]{named,recovery,settings})if(File.Exists(path))File.Delete(path);autoSaveEnabled=false;autoSaveSeconds=120;ApplyAutoSave();}
        dirty=false;StoreActive();CloseProject();SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}




