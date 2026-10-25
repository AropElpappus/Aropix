// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace PixelArt {
static class Program {
    [STAThread] static void Main(string[] args) { ToolStripManager.Renderer=new AropixRenderer();
        UiMotion.TestMode = args.Length > 0 && (args[0] == "--test" || args[0].EndsWith("-diagnostic"));
        Application.ApplicationExit += delegate { UiMotion.FinishAll(); };
        if (args.Length > 0 && args[0] == "--autosave-diagnostic")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Environment.ExitCode = AutoSaveDiagnostics.Run(args);
            return;
        }
        if (args.Length > 0 && args[0] == "--history-diagnostic")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Environment.ExitCode = HistoryDiagnostics.Run(args);
            return;
        }
        if (args.Length > 0 && args[0] == "--test")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Environment.ExitCode = TestArtifacts.RunAll(args);
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Editor());
    }
}
partial class Document : IDisposable {
    public string Name = "Sin título";
    public int Scale = 4;
    public List<Color> Palette = new List<Color>();
    public Document(int w, int h) { Layers.Add(new Layer("Fondo",new Bitmap(w,h,PixelFormat.Format32bppArgb))); }
    public void Stamp(int x,int y,Color color,int size) {if(spanLeft!=null){if(y>=0 && y<spanLeft.Length){spanLeft[y]=Math.Min(spanLeft[y],x);spanRight[y]=Math.Max(spanRight[y],x);}return;}
        int offset=(size-1)/2;
        for(int yy=Math.Max(0,y-offset);yy<Math.Min(Image.Height,y-offset+size);yy++)
        for(int xx=Math.Max(0,x-offset);xx<Math.Min(Image.Width,x-offset+size);xx++)SymmetryPixel(xx,yy,color);
    }
    public void Line(Point a, Point b, Color color,int size=1) {RasterLine(a,b,color,size,Stamp); }
    void RasterLine(Point a,Point b,Color color,int size,Action<int,int,Color,int> stamp) {
        int x=a.X,y=a.Y,dx=Math.Abs(b.X-x),sx=x<b.X?1:-1,dy=-Math.Abs(b.Y-y),sy=y<b.Y?1:-1,err=dx+dy;
        while(true) {
            stamp(x,y,color,size);
            if(x==b.X && y==b.Y) break;
            int e=2*err; if(e>=dy){err+=dy;x+=sx;} if(e<=dx){err+=dx;y+=sy;}
        }
    }
    public static bool SameColor(Color a,Color b){return (a.A==0 && b.A==0) || a.ToArgb()==b.ToArgb();}
    public bool CanFill(Point p,Color color){return p.X>=0 && p.Y>=0 && p.X<Image.Width && p.Y<Image.Height && !SameColor(Image.GetPixel(p.X,p.Y),color);}
    public void FloodFill(Point start,Color color){foreach(Point seed in MirrorPoints(start))FastFill(seed,color);}
    public void Shape(string tool,Point a,Point b,Point c,Color color,bool filled=false,int thickness=1,bool evenCenter=false) {
        if(tool=="Línea recta"){Line(a,b,color,thickness);return;}
        if(filled || thickness>1){FillShapeSpans(tool,a,b,c,color,filled,thickness,evenCenter);return;}
        if(tool=="Rectángulo") {
            Point tr=new Point(b.X,a.Y),bl=new Point(a.X,b.Y);
            Line(a,tr,color);Line(tr,b,color);Line(b,bl,color);Line(bl,a,color);
        } else if(tool=="Triángulo") {Line(a,b,color);Line(b,c,color);Line(c,a,color);}
        else if(tool=="Círculo") {
            int dx=b.X-a.X,dy=b.Y-a.Y,r=(int)Math.Round(Math.Sqrt(dx*dx+dy*dy));
            int x=r,y=0,err=1-r;
            while(x>=y){
                CirclePixel(a,x,y,color,evenCenter);CirclePixel(a,y,x,color,evenCenter);
                CirclePixel(a,-y,x,color,evenCenter);CirclePixel(a,-x,y,color,evenCenter);
                CirclePixel(a,-x,-y,color,evenCenter);CirclePixel(a,-y,-x,color,evenCenter);
                CirclePixel(a,y,-x,color,evenCenter);CirclePixel(a,x,-y,color,evenCenter);
                y++;if(err<0)err+=2*y+1;else{x--;err+=2*(y-x)+1;}
            }
        }
    }
    void CirclePixel(Point center,int x,int y,Color color,bool even){
        int px=center.X+x+(even && x>0?1:0),py=center.Y+y+(even && y>0?1:0);Stamp(px,py,color,1);
        if(even && x==0)Stamp(px+1,py,color,1);if(even && y==0)Stamp(px,py+1,color,1);if(even && x==0 && y==0)Stamp(px+1,py+1,color,1);
    }
    public Bitmap Export() {
        if(Scale<1 || Scale>32 || (long)Image.Width*Scale>8192 || (long)Image.Height*Scale>8192)throw new Exception("El PNG admite escalas de 1 a 32 y hasta 8192 píxeles por lado.");
        Bitmap output = new Bitmap(Image.Width*Scale,Image.Height*Scale,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(output)) {
            g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode=PixelOffsetMode.Half;
            using(Bitmap composite=Composite())g.DrawImage(composite,new Rectangle(0,0,output.Width,output.Height),0,0,Image.Width,Image.Height,GraphicsUnit.Pixel);
        }
        return output;
    }
    public XmlDocument Serialize() {
        XmlDocument xml=new XmlDocument(); XmlElement root=xml.CreateElement("pixelart"); xml.AppendChild(root);
        root.SetAttribute("version","1"); root.SetAttribute("name",Name); root.SetAttribute("scale",Scale.ToString());root.SetAttribute("mirrorVertical",MirrorVertical.ToString());root.SetAttribute("mirrorHorizontal",MirrorHorizontal.ToString());SaveAxes(root);
        XmlElement image=xml.CreateElement("image");
        using(Bitmap composite=Composite())using(MemoryStream stream=new MemoryStream()){ composite.Save(stream,ImageFormat.Png); image.InnerText=Convert.ToBase64String(stream.ToArray()); }
        root.AppendChild(image);
        foreach(Color c in Palette){XmlElement swatch=xml.CreateElement("color");swatch.InnerText=c.ToArgb().ToString();root.AppendChild(swatch);}
        SerializeLayers(xml);return xml;
    }
    public void Save(string path) {
        XmlDocument xml=Serialize();
        string temp=path+".tmp";
        try { xml.Save(temp); if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static Document Load(string path) {
        XmlDocument xml=new XmlDocument(); xml.XmlResolver=null;
        XmlReaderSettings settings=new XmlReaderSettings();settings.DtdProcessing=DtdProcessing.Prohibit;settings.MaxCharactersInDocument=536870912;
        using(XmlReader reader=XmlReader.Create(path,settings))xml.Load(reader);
        return Deserialize(xml.DocumentElement);
    }
    public static Document Deserialize(XmlElement root) {
        if(root==null || root.Name!="pixelart" || (root.GetAttribute("version")!="1" && root.GetAttribute("version")!="2"))throw new Exception("El archivo no es un proyecto compatible.");
        int scale=int.Parse(root.GetAttribute("scale"));if(scale<1 || scale>32)throw new Exception("Escala no válida.");
        Bitmap bitmap;
        using(MemoryStream stream=new MemoryStream(Convert.FromBase64String(root["image"].InnerText)))
        using(Bitmap source=new Bitmap(stream)) {
            if(source.Width<1 || source.Height<1 || source.Width>2048 || source.Height>2048)throw new Exception("Dimensiones no compatibles (máximo 2048 × 2048).");
            // Detach decoded pixels from the PNG stream before it is disposed.
            bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
            try{using(Graphics g=Graphics.FromImage(bitmap)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(source,0,0);}}
            catch{bitmap.Dispose();throw;}
        }
        Document doc=new Document(1,1);doc.Image.Dispose();doc.Image=bitmap;doc.Name=root.GetAttribute("name");doc.Scale=scale;doc.MirrorVertical=root.GetAttribute("mirrorVertical")=="True";doc.MirrorHorizontal=root.GetAttribute("mirrorHorizontal")=="True";doc.ReadAxes(root);
        foreach(XmlNode node in root.SelectNodes("color")){int argb;if(int.TryParse(node.InnerText,out argb) && doc.Palette.Count<64)doc.Palette.Add(Color.FromArgb(argb));}
        try{doc.ReadLayers(root);return doc;}catch{doc.Dispose();throw;}
    }
    public void Dispose(){foreach(Layer layer in Layers)layer.Dispose();Layers.Clear();}
}
partial class PixelCanvas : Control {
    public Document Doc;
    public Color Ink { get; set; }
    public string Tool="Lápiz";
    public int PencilSize=1,EraserSize=1;
    public bool FilledShapes=false;
    public int ShapeThickness=1;
    public bool EvenCircleCenter;
    public int CoordinateMode {get;set;} // 0 hidden, 1 status bar, 2 next to pointer.
    public Point? MousePixel {get;private set;}
    Point pointer;
    public event Action PointerChanged;
    public void TrackPointer(Point position){if(precisionActive){RefreshPrecisionPointer();return;}Point previous=pointer;Point? old=MousePixel;pointer=position;Point p=Pixel(position);MousePixel=Doc!=null && Inside(p)?(Point?)p:null;if(old!=MousePixel){InvalidatePointer(old,MousePixel);UpdatePointerCursor();if(PointerChanged!=null)PointerChanged();}if(CoordinateMode==2 && (old.HasValue || MousePixel.HasValue)){Invalidate(new Rectangle(previous.X-180,previous.Y-60,360,140));Invalidate(new Rectangle(pointer.X-180,pointer.Y-60,360,140));}}
    public void ClearPointer(){precisionActive=false;keyboardVisible=false;bool had=MousePixel.HasValue;InvalidatePointer(MousePixel,null);MousePixel=null;UpdatePointerCursor();if(had && PointerChanged!=null)PointerChanged();if(had && CoordinateMode==2)Invalidate(new Rectangle(pointer.X-180,pointer.Y-60,360,140));}
    protected override void OnMouseLeave(EventArgs e){toolWheelRemainder=variantWheelRemainder=0;base.OnMouseLeave(e);if(!precisionActive)ClearPointer();}
    readonly List<Point> vertices=new List<Point>();
    Point hover;
    public bool IsShape {get{return Tool=="Rectángulo" || Tool=="Círculo" || Tool=="Triángulo" || Tool=="Línea recta";}}
    public int PendingPoints {get{return vertices.Count;}}
    public void CancelShape(){ClearShapeOutline();precisionActive=false;keyboardVisible=false;vertices.Clear();Invalidate();if(ViewChanged!=null)ViewChanged();}
    public void SelectTool(string tool){variantWheelRemainder=0;precisionActive=false;keyboardVisible=false;Finish();CommitSelection();if(tool!="Selección" && tool!="Varita mágica")CancelSelection();CancelShape();Tool=tool;Invalidate();}
    public void ShapeClick(Point p){if(!Doc.Editable)return;
        if(!Inside(p))return;
        vertices.Add(p);hover=p;keyboardPixel=p;
        if(vertices.Count==(Tool=="Triángulo"?3:2)){
            if(StrokeStarted!=null)StrokeStarted();
            Doc.Shape(Tool,vertices[0],vertices[1],vertices.Count==3?vertices[2]:vertices[1],Ink,FilledShapes,ShapeThickness,EvenCircleCenter);
            vertices.Clear();if(StrokeFinished!=null)StrokeFinished();
        }
        Invalidate();if(ViewChanged!=null)ViewChanged();
    }
    public bool Grid=true;
    public float Zoom=4;
    public event Action StrokeStarted, StrokeFinished;
    public event Action<Color> ColorPicked;
    public bool UsePointTool(Point p){
        if(Tool!="Cubeta" && Tool!="Cuentagotas")return false;
        if(Doc==null || !Inside(p))return true;
        if(Tool=="Cuentagotas"){using(Bitmap composite=Doc.Composite()){Color c=composite.GetPixel(p.X,p.Y);if(c.A>0 && ColorPicked!=null)ColorPicked(c);}return true;}
        if(!Doc.Editable)return true;
        if(Doc.CanSymmetryFill(p,Ink)){if(StrokeStarted!=null)StrokeStarted();Doc.FloodFill(p,Ink);Invalidate();if(StrokeFinished!=null)StrokeFinished();}
        return true;
    }
    public event Action ViewChanged;
    PointF origin=new PointF(32,32);
    Point last,panStart;
    bool drawing,panning;
    public PixelCanvas(){CoordinateMode=1;Ink=Color.FromArgb(101,184,121);DoubleBuffered=true;BackColor=Color.FromArgb(24,28,33);Dock=DockStyle.Fill;TabStop=true;}
    public void Fit(){if(Doc==null)return;float fit=Math.Min(Math.Max(1,Width-48)/(float)Doc.Image.Width,Math.Max(1,Height-48)/(float)Doc.Image.Height);Zoom=fit>=1?Math.Min(32,(float)Math.Floor(fit)):fit>=0.5f?0.5f:fit>=0.25f?0.25f:0.125f;origin=new PointF((Width-Doc.Image.Width*Zoom)/2f,(Height-Doc.Image.Height*Zoom)/2f);ReprojectPointer();Invalidate();if(ViewChanged!=null)ViewChanged();}
    public void ChangeZoom(int direction){float value=direction>0?(Zoom<1?Zoom*2:Math.Min(32,Zoom+1)):(Zoom<=1?Math.Max(0.125f,Zoom/2):Zoom-1);float cx=Width/2f,cy=Height/2f;origin=new PointF(cx-(cx-origin.X)*value/Zoom,cy-(cy-origin.Y)*value/Zoom);Zoom=value;ReprojectPointer();Invalidate();if(ViewChanged!=null)ViewChanged();}
    public Point Pixel(Point p){return new Point((int)Math.Floor((p.X-origin.X)/Zoom),(int)Math.Floor((p.Y-origin.Y)/Zoom));}
    bool Inside(Point p){return p.X>=0 && p.Y>=0 && p.X<Doc.Image.Width && p.Y<Doc.Image.Height;}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(Doc==null)return;Graphics g=e.Graphics;
        RectangleF rect=new RectangleF(origin.X,origin.Y,Doc.Image.Width*Zoom,Doc.Image.Height*Zoom);
        GraphicsState state=g.Save();g.SetClip(rect,CombineMode.Intersect);
        using(Brush a=new SolidBrush(Color.FromArgb(62,67,75)))g.FillRectangle(a,rect);
        int tile=Math.Max(8,(int)(Zoom*2));
        using(Brush b=new SolidBrush(Color.FromArgb(78,84,92)))
        for(int y=Math.Max(0,(int)(-origin.Y/tile));y*tile<rect.Height && origin.Y+y*tile<Height;y++)
        for(int x=Math.Max(0,(int)(-origin.X/tile));x*tile<rect.Width && origin.X+x*tile<Width;x++)
        if((x+y)%2==0)g.FillRectangle(b,origin.X+x*tile,origin.Y+y*tile,tile,tile);
        g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
        PaintDocument(g,rect);
        PaintGrid(g,rect);
        PaintSelection(g);PaintSymmetry(g,rect);PaintShapeOutline(g);PaintPointerOverlay(g);
        g.Restore(state);using(Pen pen=new Pen(Color.FromArgb(115,125,139)))g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);
        if(CoordinateMode==2 && MousePixel.HasValue){
            string text=string.Format("X: {0}   Y: {1}",MousePixel.Value.X,MousePixel.Value.Y);
            Size size=TextRenderer.MeasureText(text,Font);int x=pointer.X+18,y=pointer.Y+22;
            if(x+size.Width+10>Width)x=pointer.X-size.Width-18;if(y+size.Height+8>Height)y=pointer.Y-size.Height-14;
            Rectangle box=new Rectangle(Math.Max(0,x),Math.Max(0,y),size.Width+10,size.Height+8);
            using(Brush brush=new SolidBrush(Color.FromArgb(40,48,58)))g.FillRectangle(brush,box);
            TextRenderer.DrawText(g,text,Font,new Point(box.X+5,box.Y+4),Color.White);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(Doc==null)return;physicalPointer=e.Location;
        if(e.Button==MouseButtons.Middle || (e.Button==MouseButtons.Left && Tool=="Mover vista")){panning=true;panStart=e.Location;Capture=true;UpdatePointerCursor();InvalidatePointer(MousePixel,null);return;}
        TrackPointer(e.Location);Point p=precisionActive?keyboardPixel:Pixel(e.Location);
        if(e.Button==MouseButtons.Right){StartRightPrecision(p);return;}
        if(Tool=="Varita mágica" && e.Button==MouseButtons.Left && Inside(p)){WandClick(p,ModifierKeys);return;}
        if(!Doc.Editable && Tool!="Cuentagotas")return;
        if(e.Button!=MouseButtons.Left || !Inside(p))return;
        if(Tool=="Selección"){BeginSelection(p);return;}
        if(UsePointTool(p))return;
        if(IsShape){ShapeClick(p);if(PendingPoints==0){precisionActive=false;keyboardVisible=false;}return;}
        if(StrokeStarted!=null)StrokeStarted();drawing=true;Capture=true;last=p;DrawTo(p);
    }
    void DrawTo(Point p){if(!Doc.Editable)return;if(Tool=="Borrador")Doc.Line(last,p,Color.Transparent,EraserSize);else Doc.PencilLine(last,p,Ink,PencilSize,PencilTip);last=p;Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);bool moved=!physicalPointer.HasValue || physicalPointer.Value!=e.Location;physicalPointer=e.Location;
        if(panning){origin.X+=e.X-panStart.X;origin.Y+=e.Y-panStart.Y;panStart=e.Location;TrackPointer(e.Location);UpdatePointerCursor();Invalidate();return;}
        if(rightHeld || (precisionActive && !moved))return;
        if(precisionActive || keyboardVisible){precisionActive=false;keyboardVisible=false;Invalidate();}
        if(HasSelectionGesture)UpdateSelection(Pixel(e.Location));else if(drawing)DrawTo(Pixel(e.Location));else if(IsShape && vertices.Count>0){Point p=Pixel(e.Location);hover=new Point(Math.Max(0,Math.Min(Doc.Image.Width-1,p.X)),Math.Max(0,Math.Min(Doc.Image.Height-1,p.Y)));Invalidate();}TrackPointer(e.Location);
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);physicalPointer=e.Location;if(e.Button==MouseButtons.Middle && panning){panning=false;Capture=selecting || movingSelection || drawing || rightHeld;TrackPointer(e.Location);UpdatePointerCursor();Invalidate();return;}rightHeld=false;Finish();}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)Finish();}
    void Finish(){if(HasSelectionGesture)EndSelectionGesture();bool was=drawing;rightHeld=false;drawing=false;panning=false;Capture=false;UpdatePointerCursor();InvalidatePointer(MousePixel,MousePixel);if(was && StrokeFinished!=null)StrokeFinished();}
    protected override void OnMouseWheel(MouseEventArgs e){HandleCanvasWheel(e,(ModifierKeys & Keys.Alt)!=0,(ModifierKeys & Keys.Control)!=0);}
}
class NewProject : SecondaryDialog {
    public NumericUpDown W,H,ExportScale;
    public TextBox ProjectName;
    public ComboBox BackgroundChoice;
    public NewProject(){
        Text="Nuevo proyecto";ClientSize=new Size(410,440);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
        BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font(AropixTheme.FontFamily,AropixTheme.ControlSize);
        LabelAt("Información del proyecto · Nombre",24,20);ProjectName=new TextBox{Text="Mi dibujo",Location=new Point(24,48),Width=360,MaxLength=80};Controls.Add(ProjectName);
        LabelAt("Lienzo · de 1 a 2048 píxeles",24,91);
        LabelAt("Ancho",24,125);LabelAt("Alto",215,125);W=Number(24,153,100,2048);H=Number(215,153,100,2048);
        LabelAt("Fondo",24,198);BackgroundChoice=new ComboBox{Location=new Point(24,225),Width=360,DropDownStyle=ComboBoxStyle.DropDownList};BackgroundChoice.Items.AddRange(new object[]{"Transparente","Blanco","Negro"});BackgroundChoice.SelectedIndex=0;Controls.Add(BackgroundChoice);
        LabelAt("Exportación · Escala PNG (hasta ×32)",24,267);ExportScale=Number(24,295,4,32);
        Label result=LabelAt("",195,299);result.Width=190;
        EventHandler update=delegate{ExportScale.Maximum=Math.Min(32,8192/Math.Max(W.Value,H.Value));result.Text=string.Format("Resultado: {0} × {1} px",W.Value*ExportScale.Value,H.Value*ExportScale.Value);};W.ValueChanged+=update;H.ValueChanged+=update;ExportScale.ValueChanged+=update;update(null,EventArgs.Empty);
        Label hint=LabelAt("La escala se puede cambiar al exportar.",24,337);hint.Width=365;hint.Font=new Font("Segoe UI",9);
        Button cancel=Editor.Button("Cancelar",100);cancel.Location=new Point(124,385);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);
        Button create=Editor.Button("Crear proyecto",150);create.Location=new Point(234,385);create.BackColor=Editor.Accent;create.DialogResult=DialogResult.OK;Controls.Add(create);AcceptButton=create;CancelButton=cancel;
    }
    Label LabelAt(string text,int x,int y){Label l=new Label{Text=text,Location=new Point(x,y),Size=new Size(Math.Min(360,ClientSize.Width-x-20),25)};if(text.Contains(" · ")){l.ForeColor=AropixTheme.Focus;l.Font=new Font(Font,FontStyle.Bold);}Controls.Add(l);return l;}
    NumericUpDown Number(int x,int y,int value,int max){NumericUpDown n=new NumericUpDown{Location=new Point(x,y),Width=160,Minimum=1,Maximum=max,Value=value};Controls.Add(n);return n;}
}
partial class Editor : Form {
    public static readonly Color PanelColor=AropixTheme.Surface,Accent=AropixTheme.Accent;
    Document doc;PixelCanvas canvas=new PixelCanvas();Label status=new Label();Label current=new Label();TextBox hex=new TextBox();
    FlowLayoutPanel palette,recent;List<Color> history=new List<Color>();List<HistoryEntry> undo=new List<HistoryEntry>(),redo=new List<HistoryEntry>();
    Dictionary<string,Button> toolButtons=new Dictionary<string,Button>();bool dirty;string projectPath;
    ToolTip tips=new ToolTip();
    Label toolHelp=new Label(); Action refreshShapeOptions;
    Bitmap copiedSelection;SelectionMask copiedSelectionMask;
    void CopyPixels(){Bitmap copy=canvas.CopySelection();if(copy==null)return;if(copiedSelection!=null)copiedSelection.Dispose();copiedSelection=copy;copiedSelectionMask=canvas.CopySelectionMask();}
    void PastePixels(){if(copiedSelection==null)return;SetTool("Selección");canvas.PasteSelection(copiedSelection,copiedSelectionMask);canvas.Focus();}
    public bool SelectionShortcut(Keys key){if(doc==null)return false;
        if(key==(Keys.Control|Keys.C)){ExecuteCommand("copy");return true;}
        if(key==(Keys.Control|Keys.V)){ExecuteCommand("paste");return true;}
        if(key==(Keys.Control|Keys.Z)){ExecuteCommand("undo");return true;}if(key==(Keys.Control|Keys.Y)){ExecuteCommand("redo");return true;}
        if(key==Keys.Escape && !canvas.Selection.IsEmpty){canvas.CancelSelection();return true;}
        if(key==Keys.Enter && canvas.HasPendingSelection){ExecuteCommand("confirm");return true;}
        if(key==Keys.Delete && canvas.IsSelectionTool){ExecuteCommand("deletepixels");return true;}
        if(key==Keys.M){SetTool("Selección");return true;}
        return false;
    }
    TableLayoutPanel workspace;
    FlowLayoutPanel dockLeft,dockRight;
    ToolPanel basicsPanel,shapesPanel,colorPanel;
    Label coordinates=new Label{Width=160,Height=30,TextAlign=ContentAlignment.MiddleLeft};
    string SettingsPath {get{return internalStore.PathFor("preferencias.xml");}}
    public void SaveSettings(string path){
        XmlDocument xml=new XmlDocument();XmlElement root=xml.CreateElement("preferences");xml.AppendChild(root);root.SetAttribute("version","1");root.SetAttribute("coordinates",canvas.CoordinateMode.ToString());SaveAutoSaveSettings(root);SaveWorkspaceSettings(root);
        ToolPanel[] panels={basicsPanel,shapesPanel,colorPanel,layersPanel};string[] ids={"basic","shapes","color","layers"};
        for(int i=0;i<panels.Length;i++){XmlElement item=xml.CreateElement(ids[i]);panels[i].SaveState(item);root.AppendChild(item);}
        internalStore.Write(path,xml);
    }
    public void LoadSettings(string path){
        if(!File.Exists(path))return;
        XmlDocument xml=new XmlDocument();xml.XmlResolver=null;
        XmlReaderSettings settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=20000};try{using(XmlReader reader=XmlReader.Create(path,settings))xml.Load(reader);}catch(XmlException){RestoreDefaultLayout();return;}catch(IOException){RestoreDefaultLayout();return;}catch(UnauthorizedAccessException){RestoreDefaultLayout();return;}
        XmlElement root=xml.DocumentElement;if(root==null || root.Name!="preferences" || root.GetAttribute("version")!="1")return;
        ReadAutoSaveSettings(root);ReadWorkspaceSettings(root);int mode;if(int.TryParse(root.GetAttribute("coordinates"),out mode) && mode>=0 && mode<=2)canvas.CoordinateMode=mode;
        if(root.GetAttribute("layoutVersion")!="2" && root.GetAttribute("layoutVersion")!="3"){RestoreDefaultLayout();UpdateCoordinates();return;}
        ToolPanel[] panels={basicsPanel,shapesPanel,colorPanel,layersPanel};string[] ids={"basic","shapes","color","layers"};
        for(int i=0;i<panels.Length;i++)if(root[ids[i]]!=null)panels[i].RestoreState(root[ids[i]]);
        // Restore siblings by ascending saved position, independently of panel IDs.
        List<int> order=new List<int>{0,1,2,3};order.Sort(delegate(int a,int b){return SavedOrder(root[ids[a]]).CompareTo(SavedOrder(root[ids[b]]));});
        foreach(FlowLayoutPanel dock in new[]{dockLeft,dockRight}){int position=0;foreach(int i in order)if(panels[i].Parent==dock)dock.Controls.SetChildIndex(panels[i],position++);}
        LayoutPanels();UpdateCoordinates();canvas.Invalidate();
    }
    static int SavedOrder(XmlElement node){int n;return node!=null && int.TryParse(node.GetAttribute("order"),out n)?Math.Max(0,n):0;}
    public void TestPreferences(){
        string file=Path.Combine(Path.GetTempPath(),"pixelart-layout-"+Guid.NewGuid()+".xml");
        try{
            canvas.CoordinateMode=2;basicsPanel.Collapse(true);shapesPanel.DockTo(dockRight);shapesPanel.HidePanel();colorPanel.FloatAt(PointToScreen(new Point(140,120)));SaveSettings(file);
            using(Editor restored=new Editor(true)){
                restored.Show();restored.LoadSettings(file);
                if(restored.canvas.CoordinateMode!=2 || !restored.basicsPanel.IsCollapsed || !restored.shapesPanel.IsHidden || restored.shapesPanel.Parent!=restored.dockRight || !restored.colorPanel.IsFloating)throw new Exception("Restauración de preferencias entre editores");
                restored.SaveSettings(file+".roundtrip");XmlDocument first=new XmlDocument(),second=new XmlDocument();first.Load(file);second.Load(file+".roundtrip");
                if(first.OuterXml!=second.OuterXml)throw new Exception("Posición, tamaño u orden no conservados");
                File.WriteAllText(file,"<preferences version='1' coordinates='99'><color side='floating' x='-99999' y='99999'/></preferences>");restored.LoadSettings(file);if(restored.canvas.CoordinateMode!=2)throw new Exception("Modo inválido");
            }
        }finally{if(File.Exists(file))File.Delete(file);if(File.Exists(file+".roundtrip"))File.Delete(file+".roundtrip");}
        basicsPanel.DockTo(dockLeft);shapesPanel.DockTo(dockLeft);colorPanel.DockTo(dockRight);basicsPanel.Collapse(false);shapesPanel.Collapse(true);canvas.CoordinateMode=1;UpdateCoordinates();
    }
    public void TestPanels(){
        basicsPanel.Collapse(true);if(!basicsPanel.IsCollapsed || basicsPanel.Height!=35)throw new Exception("Plegar panel");basicsPanel.Reveal();
        layersPanel.HidePanel();colorPanel.HidePanel();if(workspace.ColumnStyles[2].Width!=0)throw new Exception("Ocultar libera espacio");colorPanel.Reveal();layersPanel.Reveal();
        colorPanel.FloatAt(PointToScreen(new Point(200,150)));if(!colorPanel.IsFloating || current.IsDisposed)throw new Exception("Separar panel");
        colorPanel.HidePanel();colorPanel.Reveal();colorPanel.DockTo(dockLeft);if(colorPanel.IsFloating || colorPanel.Parent!=dockLeft)throw new Exception("Reacoplar panel");colorPanel.DockTo(dockRight);
        SetTool("Triángulo");if(!optionGroups["fill"][0].Visible || optionGroups["pencil"][0].Visible)throw new Exception("Opciones contextuales de herramientas");SetTool("Lápiz");
    }
    public void TestBucket(){
        SetDocument(new Document(20,20));doc.Shape("Rectángulo",new Point(2,2),new Point(17,17),Point.Empty,Color.Black);
        SetColor(Color.Red);SetTool("Cubeta");canvas.UsePointTool(new Point(5,5));
        if(undo.Count!=1 || !dirty || history.Count!=1 || doc.Image.GetPixel(5,5).ToArgb()!=Color.Red.ToArgb())throw new Exception("Cubeta: historial y color reciente");
        canvas.UsePointTool(new Point(5,5));if(undo.Count!=1)throw new Exception("Relleno idéntico sin historial extra");
        Undo(false);if(doc.Image.GetPixel(5,5).A!=0)throw new Exception("Deshacer cubeta");Undo(true);
        SetColor(Color.Blue);SetTool("Cuentagotas");canvas.UsePointTool(new Point(5,5));
        if(canvas.Ink.ToArgb()!=Color.Red.ToArgb() || undo.Count!=1)throw new Exception("Cuentagotas sin editar ni añadir historial");
        canvas.UsePointTool(Point.Empty);if(canvas.Ink.ToArgb()!=Color.Red.ToArgb())throw new Exception("Transparencia conserva color");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    public void TestSelection(){
        SetDocument(new Document(20,20));doc.Shape("Rectángulo",new Point(2,2),new Point(5,5),Point.Empty,Color.Red,true);
        SetTool("Selección");canvas.BeginSelection(new Point(5,5));canvas.UpdateSelection(new Point(2,2));canvas.EndSelectionGesture();
        if(canvas.Selection!=new Rectangle(2,2,4,4) || dirty || undo.Count!=0)throw new Exception("Seleccionar no modifica dibujo");
        CopyPixels();canvas.BeginSelection(new Point(3,3));canvas.UpdateSelection(new Point(5,3));canvas.EndSelectionGesture();
        if(doc.Image.GetPixel(2,2).A!=0 || doc.Image.GetPixel(4,2).ToArgb()!=Color.Red.ToArgb() || undo.Count!=1)throw new Exception("Mover selección superpuesta en una acción");
        Undo(false);if(doc.Image.GetPixel(2,2).A!=255 || doc.Image.GetPixel(7,2).A!=0)throw new Exception("Deshacer movimiento");Undo(true);
        PastePixels();canvas.MoveSelectionTo(new Point(12,12));canvas.CommitSelection();if(doc.Image.GetPixel(12,12).ToArgb()!=Color.Red.ToArgb() || undo.Count!=2)throw new Exception("Copiar y pegar");
        Undo(false);if(doc.Image.GetPixel(12,12).A!=0)throw new Exception("Deshacer pegado");Undo(true);
        PastePixels();canvas.MoveSelectionTo(new Point(0,12));canvas.CancelSelection();if(doc.Image.GetPixel(0,12).A!=0 || undo.Count!=2)throw new Exception("Cancelar pegado sin historial");
        canvas.BeginSelection(new Point(4,2));canvas.UpdateSelection(new Point(7,5));canvas.EndSelectionGesture();canvas.BeginSelection(new Point(5,3));canvas.UpdateSelection(new Point(10,8));canvas.CancelSelection();if(doc.Image.GetPixel(4,2).A!=255 || undo.Count!=2)throw new Exception("Cancelar movimiento conserva original");
        SetDocument(new Document(10,10));PastePixels();canvas.MoveSelectionTo(new Point(9,9));canvas.CommitSelection();if(doc.Image.GetPixel(9,9).A!=255)throw new Exception("Copiar entre proyectos y limitar bordes");
        using(Bitmap transparent=new Bitmap(3,3)){transparent.SetPixel(0,0,Color.Green);doc.Image.SetPixel(1,1,Color.Blue);canvas.CancelSelection();canvas.PasteSelection(transparent);canvas.CommitSelection();if(doc.Image.GetPixel(1,1).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Pegado respeta transparencia");}
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    public void TestExistingProject(string path){
        SetDocument(Document.Load(path));Show();Application.DoEvents();
        string folder=Path.Combine(TestArtifacts.DirectoryPath,"comprobacion-proyecto");Directory.CreateDirectory(folder);
        using(Bitmap screenshot=new Bitmap(Width,Height)){DrawToBitmap(screenshot,new Rectangle(Point.Empty,Size));screenshot.Save(Path.Combine(folder,"proyecto-abierto.png"),ImageFormat.Png);}
        using(Bitmap png=doc.Export())png.Save(Path.Combine(folder,"original-exportado.png"),ImageFormat.Png);
        Color before=doc.Image.GetPixel(0,0);Color marker=before.ToArgb()==Color.Magenta.ToArgb()?Color.Cyan:Color.Magenta;
        PushUndo();doc.Line(Point.Empty,Point.Empty,marker);Undo(false);
        if(doc.Image.GetPixel(0,0).ToArgb()!=before.ToArgb())throw new Exception("Deshacer en proyecto del usuario");Undo(true);
        if(doc.Image.GetPixel(0,0).ToArgb()!=marker.ToArgb())throw new Exception("Rehacer en proyecto del usuario");
        string copy=Path.Combine(folder,"copia-editada.pixelart");doc.Save(copy);
        using(Document reopened=Document.Load(copy))if(reopened.Image.GetPixel(0,0).ToArgb()!=marker.ToArgb())throw new Exception("Reabrir copia editada");
        dirty=false;File.AppendAllText(Path.Combine(TestArtifacts.DirectoryPath,"pruebas.txt"),"\nOK: proyecto del usuario: "+doc.Name+", "+doc.Image.Width+" × "+doc.Image.Height+"; visualizar, exportar, editar, deshacer, rehacer, guardar copia y reabrir.");
    }
    void UpdateCoordinates(){coordinates.Text=doc!=null && canvas.CoordinateMode==1?(canvas.MousePixel.HasValue?string.Format("X: {0}   Y: {1}",canvas.MousePixel.Value.X,canvas.MousePixel.Value.Y):"X: —   Y: —"):"";}
    void LayoutPanels(){ArrangePanels();}
    public Editor() : this(false) {}
    public Editor(bool preview){
        Icon applicationIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Icon=applicationIcon;Disposed+=delegate{if(applicationIcon!=null)applicationIcon.Dispose();};
        Text="Aropix";Width=1250;Height=830;MinimumSize=new Size(980,640);StartPosition=FormStartPosition.CenterScreen;BackColor=PanelColor;ForeColor=Color.White;Font=new Font(AropixTheme.FontFamily,AropixTheme.ControlSize);KeyPreview=true;
        TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));Controls.Add(root);root.Controls.Add(projectTabs,0,1);InitializeTabs();
        Panel top=new Panel{Dock=DockStyle.Fill};root.Controls.Add(top,0,0);
        workspace=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,294));workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,294));root.Controls.Add(workspace,0,2);
        dockLeft=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};dockRight=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};BuildDockHosts();BuildCanvasHost();
        BuildToolPanels();
        BuildCompactColor();
        basicsPanel.DockTo(dockLeft);shapesPanel.DockTo(dockLeft);colorPanel.DockTo(dockRight);shapesPanel.Collapse(false);BuildLayersPanel();dockRight.Controls.SetChildIndex(colorPanel,0);BuildMainMenus(top,preview);
        BuildSelectionContext();BuildStatusBar(root);
        canvas.ToolWheel+=CycleTool;canvas.ColorPicked+=SetColor;canvas.StrokeStarted+=delegate{PushUndo();};canvas.StrokeFinished+=delegate{MarkDirty();if(canvas.Tool!="Borrador" && !canvas.IsSelectionTool){history.RemoveAll(delegate(Color c){return c.ToArgb()==canvas.Ink.ToArgb();});history.Insert(0,canvas.Ink);if(history.Count>12)history.RemoveAt(12);RefreshColors();}};canvas.ViewChanged+=UpdateStatus;
        AropixTheme.StyleInputs(this);AropixTheme.StyleMenu(mainMenus);AropixTheme.StyleMenu(quickBar);BuildWelcome();SetupImageDrop(this);InitializeUX();SetColor(canvas.Ink);SetTool("Lápiz");EnterEmptyWorkspace();Shown+=delegate{LayoutPanels();canvas.Fit();if(!preview){try{LoadInternalSettings();}catch(Exception ex){MessageBox.Show(this,"No se pudieron cargar las preferencias. Se usará la distribución inicial.\n\n"+ex.Message,"Preferencias");}try{RestoreLatestSession();}catch(Exception ex){MessageBox.Show(this,"No se pudo recuperar la sesión. Tus archivos de proyecto siguen disponibles.\n\n"+ex.Message,"Sesión");}ShowStorageWarnings();}};FormClosing+=delegate(object sender,FormClosingEventArgs e){HandleSafeClose(e);};
    }
    public static Button Button(string text,int width){return new AropixButton{Text=text,Width=width,Height=32,FlatStyle=FlatStyle.Flat,BackColor=AropixTheme.Raised,ForeColor=AropixTheme.Text,Cursor=Cursors.Hand,Margin=new Padding(3),TabStop=true};}
    ComboBox SizePicker(){ComboBox box=new ComboBox{Width=125,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Color.White,ForeColor=Color.Black};for(int i=1;i<=10;i++)box.Items.Add(string.Format("{0} × {0} px",i));box.SelectedIndex=0;return box;}
    Button AddButton(Control parent,string text,int width,Action action){Button b=Button(text,width);b.Click+=delegate{action();};parent.Controls.Add(b);return b;}
    void Heading(Control parent,string text){parent.Controls.Add(new Label{Text=text,Width=235,Height=28,Margin=new Padding(3,12,3,0),Font=new Font("Segoe UI",10,FontStyle.Bold)});}
    FlowLayoutPanel SwatchPanel(int height){return new FlowLayoutPanel{Width=240,Height=height,AutoScroll=true,Margin=new Padding(0)};}
    static bool Contains(List<Color> list,Color c){return list.Exists(delegate(Color other){return other.ToArgb()==c.ToArgb();});}
    void AddSwatch(FlowLayoutPanel target,Color c,bool removable){Button b=Button("",32);b.Height=30;b.BackColor=c;b.FlatAppearance.BorderColor=Color.FromArgb(135,145,158);b.AccessibleName="Color "+ColorTranslator.ToHtml(c);b.AccessibleDescription=removable?"Seleccionar color; clic derecho para quitarlo de la paleta.":"Seleccionar color.";tips.SetToolTip(b,b.AccessibleName);b.Click+=delegate{SetColor(c);};if(removable)b.MouseUp+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right){doc.Palette.Remove(c);MarkDirty();RefreshColors();}};target.Controls.Add(b);}
    void ClearSwatches(FlowLayoutPanel panel){while(panel.Controls.Count>0)panel.Controls[0].Dispose();}
    void RefreshColors(){RefreshLayers();ClearSwatches(palette);ClearSwatches(recent);if(doc==null)return;foreach(Color c in doc.Palette)AddSwatch(palette,c,true);foreach(Color c in history)AddSwatch(recent,c,false);if(doc.Palette.Count==0)palette.Controls.Add(new Label{Text="Añade colores aquí.\nClic derecho para quitarlos.",Width=Math.Max(100,palette.Width-24),Height=42,ForeColor=Color.Silver});}
    void SetColor(Color c){canvas.Ink=Color.FromArgb(255,c.R,c.G,c.B);current.BackColor=canvas.Ink;hex.Text=string.Format("#{0:X2}{1:X2}{2:X2}",c.R,c.G,c.B);canvas.Invalidate();}
    void ApplyHex(){string value=hex.Text.Trim();int rgb;if(value.StartsWith("#"))value=value.Substring(1);if(value.Length==6 && int.TryParse(value,System.Globalization.NumberStyles.HexNumber,null,out rgb))SetColor(Color.FromArgb(255,(rgb>>16)&255,(rgb>>8)&255,rgb&255));else {colorValidation.Visible=true;hex.AccessibleDescription=colorValidation.Text;}}
    void PickColor(){using(ColorDialog dialog=new ColorDialog{Color=canvas.Ink,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK)SetColor(dialog.Color);}
    void SetTool(string tool){canvas.SelectTool(tool);canvas.Focus();canvas.Cursor=ToolCursors.For(tool);if(refreshShapeOptions!=null)refreshShapeOptions();foreach(KeyValuePair<string,Button> pair in toolButtons)pair.Value.BackColor=pair.Key==tool?Accent:Color.FromArgb(47,56,66);RefreshSelectionTool();UpdateStatus();}
    void UpdateStatus(){if(doc==null){status.Text="  Sin proyectos abiertos";toolHelp.Text="Crea o abre un proyecto para comenzar.";return;}
        if(doc!=null){string detail=canvas.Tool;if(canvas.Tool=="Lápiz" || canvas.Tool=="Borrador"){int size=canvas.Tool=="Lápiz"?canvas.PencilSize:canvas.EraserSize;detail+=string.Format(" {0} × {0}",size);if(canvas.Tool=="Lápiz")detail+=" · "+canvas.PencilTip.Name;}status.Text=string.Format("  {0} × {1} px   |   Zoom: {2}%   |   {3}",doc.Image.Width,doc.Image.Height,canvas.Zoom*100,detail);}
        if(canvas.Tool=="Línea recta")toolHelp.Text=canvas.PendingPoints==0?"Haz clic en el punto inicial.":"Ajusta el extremo y confirma con Enter o un clic."; else if(canvas.Tool=="Rectángulo")toolHelp.Text=canvas.PendingPoints==0?"Haz clic en la primera esquina.":"Haz clic en la esquina opuesta para terminar.";
        else if(canvas.Tool=="Círculo")toolHelp.Text=canvas.PendingPoints==0?(canvas.EvenCircleCenter?"Haz clic en el píxel superior izquierdo del centro de 2 × 2.":"Haz clic en el centro del círculo."):"Haz clic para definir el radio y terminar.";
        else if(canvas.Tool=="Triángulo")toolHelp.Text=string.Format("Haz clic en el vértice {0} de 3.",canvas.PendingPoints+1);
        else toolHelp.Text=canvas.Tool=="Mover vista"?"Arrastra para desplazar el lienzo.":"Arrastra para dibujar o borrar. Cada herramienta conserva su tamaño.";
        if(canvas.IsShape)toolHelp.Text+="\nFlechas: ajustar · Enter: confirmar"; if(canvas.Tool=="Línea recta")toolHelp.Text+=string.Format("\nGrosor: {0} px. Esc: cancelar.",canvas.ShapeThickness);else if(canvas.IsShape)toolHelp.Text+=canvas.FilledShapes?"\n\nCon relleno.\nEsc: cancelar.":string.Format("\n\nContorno de {0} px.\nEsc: cancelar.",canvas.ShapeThickness);
    }
    void MarkDirty(){if(doc==null)return;dirty=true;UpdateTab();Text=doc.Name+" * — Aropix";}
    void ClearHistory(List<HistoryEntry> list){foreach(HistoryEntry b in list)b.Dispose();list.Clear();}
    const int HistoryEntryLimit=30;
    const long HistoryByteBudget=64L*1024*1024;
    // Instance-scoped injection: null during normal use, never persisted.
    Func<Document,LayerSnapshot> historySnapshotFactory;
    Action<HistoryEntry> beforeHistoryCommit;
    LayerSnapshot CaptureHistory(){return historySnapshotFactory==null?new LayerSnapshot(doc):historySnapshotFactory(doc);}
    long HistoryEstimatedBytes {
        get{long bytes=0;foreach(HistoryEntry entry in undo)bytes+=entry.Bytes;foreach(HistoryEntry entry in redo)bytes+=entry.Bytes;return bytes;}
    }
    void PushUndo(){PushHistory(CaptureHistory());}
    void PushProperties(){PushHistory(new PropertySnapshot(doc));}
    void PushHistory(HistoryEntry next){
        try{
            long bytes=next.Bytes;
            foreach(HistoryEntry entry in undo)bytes+=entry.Bytes;
            int remove=0;
            while(remove<undo.Count && (undo.Count-remove>=HistoryEntryLimit || bytes>HistoryByteBudget)){
                bytes-=undo[remove].Bytes;remove++;
            }
            // Reserve before invalidating redo or evicting anything. A failed allocation
            // or capture leaves the original lists and their order untouched.
            if(undo.Capacity<undo.Count+1)undo.Capacity=undo.Count+1;
            if(beforeHistoryCommit!=null)beforeHistoryCommit(next);
            ClearHistory(redo);
            for(int i=0;i<remove;i++)undo[i].Dispose();
            if(remove>0)undo.RemoveRange(0,remove);
            undo.Add(next);next=null;
        }finally{if(next!=null)next.Dispose();}
    }
    void Undo(bool forward){
        if(canvas.HasPendingSelection){canvas.CancelSelection();return;}
        canvas.CancelSelection();
        if(canvas.PendingPoints>0){canvas.CancelShape();return;}
        List<HistoryEntry> source=forward?redo:undo,target=forward?undo:redo;
        if(source.Count==0)return;
        HistoryEntry snapshot=source[source.Count-1];
        HistoryEntry current=snapshot.CaptureInverse(doc,historySnapshotFactory);
        try{
            if(target.Capacity<target.Count+1)target.Capacity=target.Count+1;
            if(beforeHistoryCommit!=null)beforeHistoryCommit(current);
            snapshot.Restore(doc);
            source.RemoveAt(source.Count-1);snapshot.Dispose();
            target.Add(current);current=null;
        }finally{if(current!=null)current.Dispose();}
        MarkDirty();RefreshLayers();UpdateStatus();canvas.ClearPointer();canvas.Invalidate();
    }
    void SetDocument(Document next){canvas.CancelSelection();canvas.CancelShape();if(doc!=null)doc.Dispose();doc=next;canvas.Doc=doc;projectPath=null;dirty=false;history.Clear();ClearHistory(undo);ClearHistory(redo);RefreshColors();Text=doc.Name+" — Aropix";canvas.Fit();RegisterInitial();RefreshDocumentUI();}
    bool ConfirmDiscard(Func<DialogResult> decide=null,Func<bool> save=null){canvas.CommitSelection();if(!dirty)return true;DialogResult result=decide!=null?decide():MessageBox.Show(this,"¿Quieres guardar los cambios del proyecto?","Cambios sin guardar",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);return result==DialogResult.No || (result==DialogResult.Yes && (save!=null?save():SaveProject(false)));}
    void CreateProject(){using(NewProject dialog=new NewProject()){if(dialog.ShowDialog(this)!=DialogResult.OK)return;Document next=new Document((int)dialog.W.Value,(int)dialog.H.Value);next.Name=string.IsNullOrWhiteSpace(dialog.ProjectName.Text)?"Mi dibujo":dialog.ProjectName.Text.Trim();next.Scale=(int)dialog.ExportScale.Value;if(dialog.BackgroundChoice.SelectedIndex>0)using(Graphics g=Graphics.FromImage(next.Image))g.Clear(dialog.BackgroundChoice.SelectedIndex==1?Color.White:Color.Black);AddProject(next,null);}}
    bool SaveProject(bool saveAs){if(doc==null)return false;canvas.CommitSelection();using(SaveFileDialog dialog=new SaveFileDialog{Filter="Proyecto de pixel art (*.pixelart)|*.pixelart",DefaultExt="pixelart",AddExtension=true,FileName=SafeName(doc.Name)+".pixelart"}){string path=projectPath;if(saveAs || path==null){if(dialog.ShowDialog(this)!=DialogResult.OK)return false;path=dialog.FileName;}try{doc.Save(path);projectPath=path;dirty=false;StoreActive();Text=doc.Name+" — Aropix";return true;}catch(Exception ex){Error(ex);return false;}}}
    string SafeName(string name){foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');return name;}
    void OpenProject(){OpenProject(ChooseProjectPath);}
    void OpenProject(Func<string> choosePath){string path=choosePath();if(!string.IsNullOrEmpty(path))OpenProjectFile(path);}
    string ChooseProjectPath(){using(OpenFileDialog dialog=new OpenFileDialog{Filter="Proyecto de pixel art (*.pixelart)|*.pixelart"})return dialog.ShowDialog(this)==DialogResult.OK?dialog.FileName:null;}
    void Export(){canvas.CommitSelection();using(Form settings=new SecondaryDialog{Text="Exportar PNG",ClientSize=new Size(410,210),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=PanelColor,ForeColor=Color.White,Font=Font}){
        Label label=new Label{Text="Escala",Location=new Point(20,20),AutoSize=true};settings.Controls.Add(label);NumericUpDown scale=new NumericUpDown{Minimum=1,Maximum=Math.Min(32,8192/Math.Max(doc.Image.Width,doc.Image.Height)),Value=Math.Min(doc.Scale,Math.Min(32,8192/Math.Max(doc.Image.Width,doc.Image.Height))),Location=new Point(100,18),Width=80};settings.Controls.Add(scale);Label result=new Label{Location=new Point(20,60),Width=330};settings.Controls.Add(result);EventHandler update=delegate{result.Text=string.Format("Resultado: {0} × {1} píxeles",doc.Image.Width*scale.Value,doc.Image.Height*scale.Value);};scale.ValueChanged+=update;update(null,EventArgs.Empty);Button ok=Button("Exportar…",120);settings.Controls.Add(new Label{Text="Escala hasta ×32 (máximo 8192 px por lado).\nAmplía el PNG, sin cambiar el detalle del dibujo.\nEl zoom solo cambia la vista del editor.",Location=new Point(20,90),Size=new Size(370,70)});ok.Location=new Point(265,165);ok.DialogResult=DialogResult.OK;settings.Controls.Add(ok);settings.AcceptButton=ok;if(settings.ShowDialog(this)!=DialogResult.OK)return;
        using(SaveFileDialog save=new SaveFileDialog{Filter="Imagen PNG (*.png)|*.png",DefaultExt="png",AddExtension=true,FileName=SafeName(doc.Name)+".png"})if(save.ShowDialog(this)==DialogResult.OK){try{int oldScale=doc.Scale;doc.Scale=(int)scale.Value;using(Bitmap output=doc.Export())output.Save(save.FileName,ImageFormat.Png);if(oldScale!=doc.Scale)MarkDirty();Notify("Imagen exportada correctamente · "+doc.Name,NoticeKind.Information);}catch(Exception ex){Error(ex);}}
    }}
    void Error(Exception ex){NoticeDialog.ShowNotice(this,"Aropix · Error",ex.Message,NoticeKind.Error);}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(keyData==Keys.F1){OpenContextHelp();return true;}
        if(keyData==Keys.Escape && canvas.PendingPoints>0){canvas.CancelShape();return true;}
        if(keyData==(Keys.Control|Keys.W)){CloseProject();return true;}if(keyData==(Keys.Control|Keys.Tab)){if(projects.Count>1)ActivateProject(projects[(projects.IndexOf(activeProject)+1)%projects.Count]);return true;}if(keyData==(Keys.Control|Keys.S)){ExecuteCommand("save");return true;}if(keyData==(Keys.Control|Keys.Shift|Keys.S)){ExecuteCommand("saveas");return true;}
        if(keyData==(Keys.Control|Keys.O)){ExecuteCommand("open");return true;}if(keyData==(Keys.Control|Keys.N)){ExecuteCommand("new");return true;}
        if(TextInputFocused() || doc==null)return base.ProcessCmdKey(ref msg,keyData);
        if(canvas.ContainsFocus && canvas.PrecisionKey(keyData))return true;if(SelectionShortcut(keyData))return true;
        if(keyData==(Keys.Control|Keys.Z)){Undo(false);return true;}if(keyData==(Keys.Control|Keys.Y)){Undo(true);return true;}
        if(keyData==Keys.G){SetTool("Cubeta");return true;}if(keyData==Keys.I){SetTool("Cuentagotas");return true;}if(keyData==Keys.B){SetTool("Lápiz");return true;}if(keyData==Keys.E){SetTool("Borrador");return true;}if(keyData==Keys.H){SetTool("Mover vista");return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void Dispose(bool disposing){if(disposing){if(helpCenter!=null)helpCenter.Dispose();autoSaveTimer.Dispose();if(layersPanel!=null)layersPanel.Dispose();if(copiedSelection!=null)copiedSelection.Dispose();if(basicsPanel!=null)basicsPanel.Dispose();if(shapesPanel!=null)shapesPanel.Dispose();if(colorPanel!=null)colorPanel.Dispose();StoreActive();changingTab=true;foreach(ProjectState state in projects)ReleaseProject(state);projects.Clear();activeProject=null;tips.Dispose();}base.Dispose(disposing);}
}
static class Tests {
    static void Check(bool ok,string description){if(!ok)throw new Exception(description);}
    static int Painted(Document d){int total=0;for(int y=0;y<d.Image.Height;y++)for(int x=0;x<d.Image.Width;x++)if(d.Image.GetPixel(x,y).A>0)total++;return total;}
    static void Geometry(){
        for(int size=1;size<=10;size++)using(Document d=new Document(32,32)){
            d.Line(new Point(15,15),new Point(15,15),Color.Red,size);Check(Painted(d)==size*size,"Área exacta del lápiz "+size);
            d.Line(new Point(15,15),new Point(15,15),Color.Transparent,size);Check(Painted(d)==0,"Área exacta del borrador "+size);
            d.Line(new Point(5,15),new Point(20,15),Color.Blue,size);Check(Painted(d)==(16+size-1)*size,"Trazo continuo "+size);
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);
            d.Line(new Point(0,0),new Point(0,0),Color.Red,size);int edge=size-(size-1)/2;Check(Painted(d)==edge*edge,"Pincel recortado en borde");
        }
        using(Document d=new Document(32,32)){
            d.Shape("Rectángulo",new Point(9,8),new Point(2,3),Point.Empty,Color.Red);
            Check(Painted(d)==24 && d.Image.GetPixel(5,5).A==0,"Rectángulo invertido y sin relleno");
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);
            d.Shape("Círculo",new Point(16,16),new Point(21,16),Point.Empty,Color.Blue);
            Check(d.Image.GetPixel(16,16).A==0 && d.Image.GetPixel(21,16).A==255 && d.Image.GetPixel(11,16).A==255 && d.Image.GetPixel(16,21).A==255 && d.Image.GetPixel(16,11).A==255,"Centro y radio de círculo");
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(d.Image.GetPixel(x,y).A>0)Check(d.Image.GetPixel(32-x,y).A>0 && d.Image.GetPixel(x,32-y).A>0,"Simetría de círculo");
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);
            d.Shape("Círculo",new Point(0,0),new Point(0,0),Point.Empty,Color.Blue);Check(Painted(d)==1,"Radio cero");
            d.Shape("Círculo",new Point(0,0),new Point(12,12),Point.Empty,Color.Blue);
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);
            d.Shape("Triángulo",new Point(2,2),new Point(12,2),new Point(7,12),Color.Red);
            Check(d.Image.GetPixel(2,2).A==255 && d.Image.GetPixel(12,2).A==255 && d.Image.GetPixel(7,12).A==255 && d.Image.GetPixel(7,5).A==0,"Tres vértices sin relleno");
        }
        using(Document d=new Document(32,32))using(PixelCanvas canvas=new PixelCanvas()){
            canvas.Doc=d;int starts=0,ends=0;canvas.StrokeStarted+=delegate{starts++;};canvas.StrokeFinished+=delegate{ends++;};
            canvas.SelectTool("Triángulo");canvas.ShapeClick(new Point(2,2));canvas.ShapeClick(new Point(12,2));
            using(Bitmap preview=new Bitmap(200,200)){canvas.Size=preview.Size;canvas.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));}
            Check(Painted(d)==0 && starts==0,"Vista previa no modifica dibujo ni historial");canvas.CancelShape();Check(canvas.PendingPoints==0 && Painted(d)==0,"Cancelar figura");
            canvas.ShapeClick(new Point(2,2));canvas.SelectTool("Rectángulo");Check(canvas.PendingPoints==0,"Cambiar herramienta cancela figura");
            canvas.ShapeClick(new Point(2,2));canvas.ShapeClick(new Point(12,12));Check(starts==1 && ends==1 && canvas.PendingPoints==0,"Figura completa es una acción");
            canvas.PencilSize=3;canvas.EraserSize=8;canvas.SelectTool("Borrador");canvas.SelectTool("Lápiz");Check(canvas.PencilSize==3 && canvas.EraserSize==8,"Tamaños independientes");
        }
    }
    public static void Run(){
        Geometry();
        foreach(Color background in new Color[]{Color.Black,Color.White})using(Document d=new Document(8,8))using(PixelCanvas c=new PixelCanvas())using(Bitmap screenshot=new Bitmap(200,200)){
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(background);c.Doc=d;c.Size=screenshot.Size;c.Fit();c.Grid=true;c.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));
            bool visible=false;for(int x=42;x<=44;x++)for(int y=29;y<38;y++)if(screenshot.GetPixel(x,y).ToArgb()!=background.ToArgb())visible=true;
            Check(visible,"Cuadrícula visible sobre "+background.Name);
            int built=c.GridBuilds;c.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));Check(c.GridBuilds==built,"Caché de cuadrícula reutilizada");
            int invalidations=0;bool fullRepaint=false;c.Invalidated+=delegate(object sender,InvalidateEventArgs e){invalidations++;fullRepaint|=e.InvalidRect==c.ClientRectangle;};c.CoordinateMode=1;for(int i=30;i<80;i++)c.TrackPointer(new Point(i,50));Check(invalidations>0 && !fullRepaint,"Mouse actualiza solamente la región del indicador");
            c.ViewOrigin=new PointF(25,24);c.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));Check(c.GridBuilds==built+1,"Caché invalidada al desplazar");c.Fit();
            c.Grid=false;c.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));Check(screenshot.GetPixel(43,30).ToArgb()==background.ToArgb(),"Ocultar cuadrícula no altera imagen");
        }
        using(Document d=new Document(12,12)){
            d.Shape("Rectángulo",new Point(2,2),new Point(9,9),Point.Empty,Color.Black);
            d.FloodFill(new Point(5,5),Color.Red);
            Check(d.Image.GetPixel(5,5).ToArgb()==Color.Red.ToArgb() && d.Image.GetPixel(2,2).ToArgb()==Color.Black.ToArgb() && d.Image.GetPixel(0,0).A==0,"Cubeta respeta contorno y exterior");
            d.FloodFill(Point.Empty,Color.Blue);Check(d.Image.GetPixel(11,11).ToArgb()==Color.Blue.ToArgb() && d.Image.GetPixel(5,5).ToArgb()==Color.Red.ToArgb(),"Relleno exterior y bordes");
            d.FloodFill(new Point(-1,-1),Color.Green);Check(!d.CanFill(Point.Empty,Color.Blue),"Fuera del lienzo y relleno idéntico");
        }
        using(Document d=new Document(3,3)){
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Black);
            d.Image.SetPixel(0,0,Color.White);d.Image.SetPixel(1,1,Color.White);d.FloodFill(Point.Empty,Color.Red);
            Check(d.Image.GetPixel(1,1).ToArgb()==Color.White.ToArgb(),"Cubeta no cruza diagonales");
        }
        using(Document d=new Document(2048,2048)){d.Image.SetPixel(20,20,Color.FromArgb(0,10,30,40));d.FloodFill(Point.Empty,Color.Green);Check(Painted(d)==2048*2048,"Relleno completo sin recursión y transparencia equivalente");}
        string reopenPath=Path.Combine(Path.GetTempPath(),"pixelart-reopen-"+Guid.NewGuid()+".pixelart");
        try{
            foreach(int size in new int[]{100,2048}){
                using(Document original=new Document(size,size)){original.Shape("Rectángulo",Point.Empty,new Point(size-1,size-1),Point.Empty,Color.Green,false,10);original.Save(reopenPath);}
                using(Document loaded=Document.Load(reopenPath)){
                    GC.Collect();GC.WaitForPendingFinalizers();
                    using(Bitmap display=new Bitmap(size*2,size*2))using(Graphics g=Graphics.FromImage(display))g.DrawImage(loaded.Image,new Rectangle(0,0,display.Width,display.Height));
                    using(Bitmap snapshot=(Bitmap)loaded.Image.Clone()){loaded.Line(new Point(20,20),new Point(30,30),Color.Blue,4);Check(snapshot.GetPixel(20,20).A==0,"Deshacer tras reabrir independiente");}
                    loaded.Save(reopenPath);using(Bitmap output=loaded.Export())Check(output.Width==size*4,"Exportar tras reabrir");
                }
                using(Document reopened=Document.Load(reopenPath))Check(reopened.Image.GetPixel(20,20).ToArgb()==Color.Blue.ToArgb(),"Editar, guardar y reabrir proyecto antiguo");
            }
        }finally{if(File.Exists(reopenPath))File.Delete(reopenPath);}
        for(int thickness=1;thickness<=10;thickness++)using(Document d=new Document(100,100)){
            d.Shape("Rectángulo",new Point(99,99),Point.Empty,Point.Empty,Color.Red,false,thickness);
            for(int y=0;y<100;y++)for(int x=0;x<100;x++)Check((d.Image.GetPixel(x,y).A>0)==(x<thickness || y<thickness || x>=100-thickness || y>=100-thickness),"Rectángulo 0–99 con grosor interior exacto");
        }
        foreach(string shape in new string[]{"Rectángulo","Círculo","Triángulo"})using(Document outline=new Document(40,40))using(Document fill=new Document(40,40)){
            Point a=shape=="Círculo"?new Point(20,20):new Point(5,5),b=shape=="Círculo"?new Point(34,20):new Point(34,34),c=new Point(5,34);
            fill.Shape(shape,a,b,c,Color.Blue,true);
            for(int thickness=1;thickness<=10;thickness++){
                using(Graphics g=Graphics.FromImage(outline.Image))g.Clear(Color.Transparent);
                outline.Shape(shape,a,b,c,Color.Red,false,thickness);
                for(int y=0;y<40;y++)for(int x=0;x<40;x++)if(outline.Image.GetPixel(x,y).A>0)Check(fill.Image.GetPixel(x,y).A>0,"Contorno nunca fuera de los límites elegidos");
            }
        }
        using(Document d=new Document(10,10))using(PixelCanvas c=new PixelCanvas()){
            c.Doc=d;c.Size=new Size(248,248);c.Fit();c.TrackPointer(new Point(24,24));Check(c.MousePixel==new Point(0,0),"Primer píxel bajo el mouse");
            c.TrackPointer(new Point(223,223));Check(c.MousePixel==new Point(9,9),"Último píxel bajo el mouse");
            c.TrackPointer(new Point(23,24));Check(!c.MousePixel.HasValue,"Fuera del lienzo");
            c.ChangeZoom(1);c.TrackPointer(new Point(124,124));Check(c.MousePixel==new Point(5,5),"Coordenadas con zoom");
            c.ClearPointer();Check(!c.MousePixel.HasValue,"Limpiar al salir");
        }
        foreach(string shape in new string[]{"Rectángulo","Círculo","Triángulo"})using(Document d=new Document(32,32)){
            Point a=shape=="Círculo"?new Point(16,16):new Point(4,4),b=shape=="Círculo"?new Point(26,16):new Point(26,4),c=new Point(16,26);
            if(shape=="Rectángulo")b=new Point(26,26);
            d.Shape(shape,a,b,c,Color.Red,true,10);
            Check(d.Image.GetPixel(16,12).ToArgb()==Color.Red.ToArgb(),"Interior relleno "+shape);
            Check(d.Image.GetPixel(0,0).A==0,"Exterior intacto "+shape);
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);
            d.Shape(shape,a,b,c,Color.Red,false,1);int thin=Painted(d);
            using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);
            d.Shape(shape,a,b,c,Color.Red,false,3);Check(Painted(d)>thin && d.Image.GetPixel(16,12).A==0,"Grosor sin relleno "+shape);
            for(int thickness=1;thickness<=10;thickness++){using(Graphics g=Graphics.FromImage(d.Image))g.Clear(Color.Transparent);d.Shape(shape,a,b,c,Color.Blue,false,thickness);Check(Painted(d)>0,"Contorno válido");}
        }
        using(Document d=new Document(20,20)){
            d.Shape("Círculo",new Point(0,0),new Point(8,0),Point.Empty,Color.Red,true);Check(d.Image.GetPixel(0,0).A==255 && d.Image.GetPixel(5,3).A==255 && d.Image.GetPixel(9,9).A==0,"Círculo relleno recortado");
            d.Scale=32;using(Bitmap output=d.Export()){Check(output.Width==640 && output.Height==640,"Exportación ×32");Check(output.GetPixel(160,96).ToArgb()==Color.Red.ToArgb() && output.GetPixel(288,288).A==0,"Píxeles y transparencia ×32");}
        }
        string path=Path.Combine(Path.GetTempPath(),"pixelart-test-"+Guid.NewGuid()+".pixelart");
        try{using(Document d=new Document(10,7)){
            Check(d.Image.GetPixel(0,0).A==0,"Fondo transparente");
            d.Line(new Point(0,0),new Point(9,6),Color.Red);Check(d.Image.GetPixel(9,6).ToArgb()==Color.Red.ToArgb(),"Trazo completo");
            d.Line(new Point(-5,2),new Point(15,2),Color.Blue);for(int x=0;x<10;x++)Check(d.Image.GetPixel(x,2).ToArgb()==Color.Blue.ToArgb(),"Trazo recortado");
            d.Line(new Point(3,2),new Point(3,2),Color.Transparent);Check(d.Image.GetPixel(3,2).A==0,"Borrador");
            d.Scale=4;using(Bitmap exported=d.Export()){Check(exported.Width==40 && exported.Height==28,"Dimensiones exportadas");for(int y=0;y<28;y++)for(int x=0;x<40;x++){Color expected=d.Image.GetPixel(x/4,y/4),actual=exported.GetPixel(x,y);Check(actual.A==expected.A && (actual.A==0 || actual.ToArgb()==expected.ToArgb()),"Exportación sin suavizado");}}
            d.Name="Paisaje de prueba ñ";d.Palette.Add(Color.Lime);d.Save(path);d.Save(path);
            using(Document loaded=Document.Load(path)){Check(loaded.Name==d.Name && loaded.Scale==4 && loaded.Palette.Count==1,"Metadatos y paleta");for(int y=0;y<7;y++)for(int x=0;x<10;x++)Check(loaded.Image.GetPixel(x,y).A==d.Image.GetPixel(x,y).A && (d.Image.GetPixel(x,y).A==0 || loaded.Image.GetPixel(x,y).ToArgb()==d.Image.GetPixel(x,y).ToArgb()),"Guardado sin pérdida");}
            d.Scale=32;d.Save(path);using(Document loaded=Document.Load(path))Check(loaded.Scale==32,"Reabrir proyecto con escala ×32");
        }
        using (Editor editor = new Editor(true))
        using (Bitmap preview = new Bitmap(editor.Width, editor.Height))
        {
            editor.Show();
            Application.DoEvents();
            TestArtifacts.Run("TestBucket", editor.TestBucket);
            TestArtifacts.Run("TestSelection", editor.TestSelection);
            TestArtifacts.Run("TestTransformsAndTabs", editor.TestTransformsAndTabs);
            TestArtifacts.Run("TestSession", editor.TestSession);
            TestArtifacts.Run("TestLayers", editor.TestLayers);
            TestArtifacts.Run("TestAdvancedLayers", editor.TestAdvancedLayers);
            TestArtifacts.Run("TestLockedLayers", editor.TestLockedLayers);
            TestArtifacts.Run("TestCanvasSizeAndDrop", editor.TestCanvasSizeAndDrop);
            TestArtifacts.Run("TestAxesAndLayerExport", editor.TestAxesAndLayerExport);
            TestArtifacts.Run("TestSymmetryAndAutoSave", editor.TestSymmetryAndAutoSave);
            TestArtifacts.Run("TestStraightLine", editor.TestStraightLine);
            TestArtifacts.Run("TestPrecisionAndLargeCanvas", editor.TestPrecisionAndLargeCanvas);
            TestArtifacts.Run("TestImportAndEvenCircles", editor.TestImportAndEvenCircles);
            TestArtifacts.Run("TestPanels", editor.TestPanels);
            TestArtifacts.Run("TestPreferences", editor.TestPreferences);
            TestArtifacts.Run("TestReorganizedUI", editor.TestReorganizedUI);
            TestArtifacts.Run("TestQuickTools", editor.TestQuickTools);
            TestArtifacts.Run("TestShadows", editor.TestShadows);
            TestArtifacts.Run("TestHelpCenter", editor.TestHelpCenter);
            TestArtifacts.Run("TestMagicWand", editor.TestMagicWand);
            TestArtifacts.Run("TestPersistence", editor.TestPersistence);
            TestArtifacts.Run("TestHistoryIntegrity", editor.TestHistoryIntegrity);
            TestArtifacts.Run("TestHistorySafety", editor.TestHistorySafety);
            TestArtifacts.Run("TestPropertyHistory", editor.TestPropertyHistory);TestArtifacts.Run("TestVisualTheme", editor.TestVisualTheme);TestArtifacts.Run("TestSecondaryDialogs", editor.TestSecondaryDialogs);TestArtifacts.Run("TestDockSizing", editor.TestDockSizing);TestArtifacts.Run("TestMotion", editor.TestMotion);TestArtifacts.Run("TestMotionPanelsAndControls", editor.TestMotionPanelsAndControls);TestArtifacts.Run("TestUXPolish", editor.TestUXPolish);TestArtifacts.Run("TestPencilTips", editor.TestPencilTips);TestArtifacts.Run("TestWelcome", editor.TestWelcome);TestArtifacts.Run("TestCursorOverlay", editor.TestCursorOverlay);
            Application.DoEvents();
            editor.DrawToBitmap(preview, new Rectangle(Point.Empty, editor.Size));
            preview.Save(Path.Combine(TestArtifacts.DirectoryPath, "vista-editor.png"), ImageFormat.Png);
        }
        using(NewProject dialog=new NewProject())using(Bitmap preview=new Bitmap(dialog.Width,dialog.Height)){dialog.Show();Application.DoEvents();dialog.DrawToBitmap(preview,new Rectangle(Point.Empty,dialog.Size));preview.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-nuevo-proyecto.png"),ImageFormat.Png);}
        File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath,"pruebas.txt"),"OK: persistencia LocalAppData, migración, recuperación validada, escrituras atómicas, fallos y cierre seguro; Varita mágica de cuatro vecinos, RGBA exacto, máscaras irregulares con huecos, transparencia, capas ocultas/bloqueadas, copiar/pegar/mover/borrar/transformar, historial y región 2048; Centro de Ayuda local, categorías, navegación, búsqueda sin acentos, enlaces, atrás, ayuda contextual, ventana no modal, preferencias, contenido incompleto y tamaños de ventana; Alt + rueda por grupos, pasos parciales y múltiples, sincronización y zoom sin cambios, protección de operaciones pendientes; sombras con alfa individual y opacidad independiente, origen oculto/bloqueado, color y desplazamientos, historial único, recorte, archivos y origen contextual; reorganización Aropix, ocho menús, comandos compartidos y deshabilitados, opciones contextuales, Color y Capas compactos, barras temporales, ventana 980x640, preferencias de anchos y reparto, recuperación de configuración inválida, ejes desplazados y persistentes, exportar capas separadas, simetría por píxel y cubeta, intervalos y recuperación de autoguardado, línea recta, grosor 1–10, flechas y Enter, vista previa, deshacer y bloqueo, lienzos 2048, edición con flechas y Enter, botón derecho, bloqueo y deshacer con teclado, tamaño de lienzo con nueve anclajes, recorte, deshacer/rehacer, archivos arrastrados, bloqueo de capas, persistencia y protección de contenido, importar PNG/JPG, transparencia, escala y recorte, deshacer importación, diámetros pares y grosor interior, duplicar y eliminar capas, combinación conserva apariencia, opacidad y archivo, reordenar, cursores con punto exacto, capas independientes, composición y exportación, reordenar, ocultar, deshacer capas, archivos antiguos y sesión con capas, recuperar sesión, orden de pestañas, proyecto activo, zoom y desplazamiento, respetar descartes, giros y volteos, escalado nítido, deshacer transformación, estado independiente por pestaña, pegar entre pestañas, cierre de pestaña, caché de cuadrícula y movimiento sin repintado, seleccionar, mover con superposición, copiar y pegar entre proyectos, transparencia, cancelar, deshacer/rehacer selección, cuadrícula sobre negro y blanco, cubeta en áreas cerradas, bordes y transparencia, conectividad sin diagonales, relleno de 2048×2048, deshacer/rehacer relleno, cuentagotas sin modificar historial, cargar, mostrar, editar, guardar y exportar proyectos de 100 y 2048 px, preferencias entre editores, posición y tamaño flotante, paneles ocultos y plegados, contornos interiores, paneles plegables, ocultar y recuperar, ventanas flotantes y reacoplamiento, coordenadas con zoom, relleno de las tres figuras, contornos 1–10, círculo relleno recortado, exportación y reapertura ×32, tamaños 1–10 de lápiz y borrador, trazos continuos, bordes, rectángulo, círculo simétrico, radio cero, triángulo, vista previa sin cambios, cancelación, figuras como una acción, tamaños independientes, transparencia, exportación exacta, guardado, reapertura, paleta y renderizado de ventanas.");}
        finally{if(File.Exists(path))File.Delete(path);}
    }
}
}


























