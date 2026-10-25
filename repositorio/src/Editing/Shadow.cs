// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace PixelArt {
// Stored pixel alpha is independent of Layer.Opacity. Never bake layer opacity here.
static class ShadowImage {
    public static Bitmap Create(Bitmap source,Color color,int dx,int dy,out bool hasContent){
        Bitmap result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);hasContent=false;
        try{
            using(PixelBuffer input=new PixelBuffer(source,false))using(PixelBuffer output=new PixelBuffer(result,true)){
                int rgb=color.ToArgb() & 0x00ffffff;
                for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++){
                    int alpha=input.Pixels[y*source.Width+x] & unchecked((int)0xff000000);if(alpha==0)continue;hasContent=true;
                    long targetX=(long)x+dx,targetY=(long)y+dy;
                    if(targetX>=0 && targetX<source.Width && targetY>=0 && targetY<source.Height)output.Pixels[(int)targetY*source.Width+(int)targetX]=alpha|rgb;
                }
            }
            return result;
        }catch{result.Dispose();throw;}
    }
}
class ShadowDialog : SecondaryDialog {
    public Color ShadowColor=Color.Black;
    public event Action PreviewChanged;
    void NotifyPreview(){if(PreviewChanged!=null)PreviewChanged();}
    readonly NumericUpDown opacity=new NumericUpDown{Minimum=0,Maximum=100,Value=15};
    readonly NumericUpDown offsetX=new NumericUpDown{Minimum=-2048,Maximum=2048,Value=1};
    readonly NumericUpDown offsetY=new NumericUpDown{Minimum=-2048,Maximum=2048,Value=1};
    public int ShadowOpacity {get{return (int)opacity.Value;}}
    public int OffsetX {get{return (int)offsetX.Value;}}
    public int OffsetY {get{return (int)offsetY.Value;}}
    public ShadowDialog(string source){
        Font=new Font(AropixTheme.FontFamily,AropixTheme.ControlSize);Description="Resultado: vista previa en el lienzo. Cancelar descarta la sombra temporal.";Text="Crear sombra";ClientSize=new Size(420,330);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Editor.PanelColor;ForeColor=Color.White;
        Controls.Add(new Label{Text="Origen: "+source,AutoEllipsis=true,Bounds=new Rectangle(18,15,384,24)});
        Controls.Add(new Label{Text="Apariencia · Color",Bounds=new Rectangle(18,49,165,24)});
        Panel swatch=new Panel{BackColor=ShadowColor,BorderStyle=BorderStyle.FixedSingle,Bounds=new Rectangle(190,46,80,28)};Controls.Add(swatch);
        Button choose=Editor.Button("Elegir…",115);choose.Location=new Point(285,44);Controls.Add(choose);
        choose.Click+=delegate{using(ColorDialog picker=new ColorDialog{Color=ShadowColor,FullOpen=true})if(picker.ShowDialog(this)==DialogResult.OK){ShadowColor=Color.FromArgb(255,picker.Color);swatch.BackColor=ShadowColor;NotifyPreview();}};
        Controls.Add(new Label{Text="Opacidad (%)",Bounds=new Rectangle(18,88,155,24)});opacity.SetBounds(295,85,105,26);Controls.Add(opacity);
        TrackBar slider=new TrackBar{Minimum=0,Maximum=100,Value=15,TickFrequency=10,Bounds=new Rectangle(18,116,382,42)};Controls.Add(slider);slider.ValueChanged+=delegate{opacity.Value=slider.Value;};opacity.ValueChanged+=delegate{slider.Value=(int)opacity.Value;};
        Controls.Add(new Label{Text="Posición · X (px)",Bounds=new Rectangle(18,166,250,24)});offsetX.SetBounds(295,163,105,26);Controls.Add(offsetX);
        Controls.Add(new Label{Text="Desplazamiento Y (px)",Bounds=new Rectangle(18,202,250,24)});offsetY.SetBounds(295,199,105,26);Controls.Add(offsetY);
        Controls.Add(new Label{Text="+X: derecha · +Y: abajo. Se recorta al borde del lienzo.",Bounds=new Rectangle(18,238,384,44)});
        Button ok=Editor.Button("Crear sombra",145);ok.SetBounds(130,282,145,32);ok.DialogResult=DialogResult.OK;Controls.Add(ok);AcceptButton=ok;
        Button cancel=Editor.Button("Cancelar",115);cancel.SetBounds(285,282,115,32);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);CancelButton=cancel;
        opacity.ValueChanged+=delegate{NotifyPreview();};offsetX.ValueChanged+=delegate{NotifyPreview();};offsetY.ValueChanged+=delegate{NotifyPreview();};
        Text="Crear sombra — vista previa en el lienzo";
    }
}
partial class PixelCanvas {
    Layer shadowPreview;
    Layer shadowPreviewSource;
    Color shadowPreviewColor;
    int shadowPreviewX,shadowPreviewY;
    public bool HasShadowPreview {get{return shadowPreview!=null;}}
    public void UpdateShadowPreview(Layer source,Color color,int opacity,int dx,int dy){
        if(shadowPreview==null || shadowPreviewSource!=source || shadowPreviewColor!=color || shadowPreviewX!=dx || shadowPreviewY!=dy){
            bool content;Bitmap pixels=ShadowImage.Create(source.Pixels,color,dx,dy,out content);
            ClearShadowPreview();shadowPreview=new Layer("Vista previa",pixels);shadowPreviewSource=source;
            shadowPreviewColor=color;shadowPreviewX=dx;shadowPreviewY=dy;
        }
        shadowPreview.Opacity=opacity;Invalidate();
    }
    public void ClearShadowPreview(){if(shadowPreview!=null)shadowPreview.Dispose();shadowPreview=null;shadowPreviewSource=null;Invalidate();}
    void PaintShadowPreview(Graphics g,RectangleF destination,Layer source){if(shadowPreview!=null && source==shadowPreviewSource)shadowPreview.Draw(g,destination);}
    public Bitmap TestShadowComposition(){Bitmap image=new Bitmap(Doc.Image.Width,Doc.Image.Height,PixelFormat.Format32bppArgb);using(Graphics g=Graphics.FromImage(image))PaintDocument(g,new RectangleF(0,0,image.Width,image.Height));return image;}
}
partial class Editor {
    bool CanCreateShadow(){return doc!=null && doc.ActiveLayer>=0 && doc.ActiveLayer<doc.Layers.Count && doc.Layers.Count<16 && !canvas.IsEditing;}
    void ConfigureShadow(){
        if(!CanCreateShadow())return;
        using(ShadowDialog dialog=new ShadowDialog(doc.Layers[doc.ActiveLayer].Name)){
            Layer source=doc.Layers[doc.ActiveLayer];
            dialog.PreviewChanged+=delegate{canvas.UpdateShadowPreview(source,dialog.ShadowColor,dialog.ShadowOpacity,dialog.OffsetX,dialog.OffsetY);};
            DialogResult result;
            try{
                canvas.UpdateShadowPreview(source,dialog.ShadowColor,dialog.ShadowOpacity,dialog.OffsetX,dialog.OffsetY);
                // Place controls near the right edge, leaving the center of the drawing visible.
                dialog.StartPosition=FormStartPosition.Manual;Rectangle area=Screen.FromControl(this).WorkingArea;
                dialog.Location=new Point(Math.Max(area.Left,Math.Min(Right-dialog.Width-16,area.Right-dialog.Width)),Math.Max(area.Top,Math.Min(Top+90,area.Bottom-dialog.Height)));
                result=dialog.ShowDialog(this);
            }finally{canvas.ClearShadowPreview();}
            if(result!=DialogResult.OK)return;
            if(!CreateShadow(dialog.ShadowColor,dialog.ShadowOpacity,dialog.OffsetX,dialog.OffsetY))MessageBox.Show(this,"La capa de origen está vacía: no contiene píxeles con alfa visible.","Crear sombra");
        }
    }
    // One snapshot for the entire insertion. The source remains untouched, including hidden/locked layers.
    bool CreateShadow(Color color,int opacity,int dx,int dy){
        if(!CanCreateShadow())return false;if(opacity<0 || opacity>100)throw new ArgumentOutOfRangeException("opacity");
        Layer source=doc.Layers[doc.ActiveLayer];string name="Sombra de "+source.Name;bool hasContent;
        Bitmap pixels=ShadowImage.Create(source.Pixels,color,dx,dy,out hasContent);
        if(!hasContent){pixels.Dispose();return false;}
        InsertLayerPixels(name,pixels,doc.ActiveLayer,opacity);
        canvas.CancelSelection();LayerEdited();SyncCommands();return true;
    }
    public void TestShadows(){
        TestShadowPreview();
        SetDocument(new Document(8,8));SetTool("Lápiz");Layer original=doc.Layers[0];original.Name="Torre";original.Pixels.SetPixel(1,1,Color.FromArgb(128,200,100,50));original.Pixels.SetPixel(2,1,Color.White);original.Opacity=20;original.Locked=true;original.Visible=false;
        int count=undo.Count;
        if(!CreateShadow(Color.Black,15,1,1))throw new Exception("Crear sombra de capa oculta/bloqueada");
        Layer shadow=doc.Layers[0];if(doc.Layers[1]!=original || shadow.Name!="Sombra de Torre" || shadow.Opacity!=15 || !shadow.Visible || shadow.Locked || shadow.Pixels.GetPixel(2,2).A!=128 || shadow.Pixels.GetPixel(3,2).A!=255 || original.Pixels.GetPixel(1,1).R!=200 || undo.Count!=count+1)throw new Exception("Sombra independiente, alfa, orden e historial único");
        using(Bitmap output=doc.Composite()){if(Math.Abs(output.GetPixel(3,2).A-38)>1 || Math.Abs(output.GetPixel(2,2).A-19)>1)throw new Exception("No duplicar opacidad");}
        Undo(false);if(doc.Layers.Count!=1 || doc.Layers[0].Opacity!=20 || !doc.Layers[0].Locked)throw new Exception("Deshacer sombra");Undo(true);if(doc.Layers.Count!=2 || doc.Layers[0].Pixels.GetPixel(2,2).A!=128 || doc.Layers[0].Name!="Sombra de Torre")throw new Exception("Rehacer sombra exacta");
        string path=Path.Combine(Path.GetTempPath(),"aropix-shadow-"+Guid.NewGuid()+".pixelart");try{doc.Save(path);using(Document loaded=Document.Load(path)){if(loaded.Layers[0].Opacity!=15 || loaded.Layers[0].Pixels.GetPixel(2,2).A!=128 || loaded.Layers[1].Name!="Torre")throw new Exception("Guardar/reabrir sombra");using(Bitmap png=loaded.Export())png.Save(path+".png",ImageFormat.Png);}}finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".png"))File.Delete(path+".png");}
        SetTool("Lápiz");doc.Line(Point.Empty,Point.Empty,Color.Red);if(doc.Image.GetPixel(0,0).R!=255)throw new Exception("Sombra editable");
        foreach(int opacity in new[]{0,50,100}){
            SetDocument(new Document(4,4));doc.Image.SetPixel(1,1,Color.FromArgb(128,255,0,0));doc.Layers[0].Opacity=10;
            if(!CreateShadow(Color.Cyan,opacity,-1,-1) || doc.Image.GetPixel(0,0).ToArgb()!=Color.FromArgb(128,0,255,255).ToArgb() || doc.Layers[0].Opacity!=opacity)throw new Exception("Color y opacidad configurables");
        }
        SetDocument(new Document(4,4));count=undo.Count;if(CreateShadow(Color.Black,15,1,1) || doc.Layers.Count!=1 || undo.Count!=count)throw new Exception("Capa vacía sin modificaciones");
        doc.Image.SetPixel(0,0,Color.Red);CreateShadow(Color.Blue,100,0,0);if(doc.Image.GetPixel(0,0).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Desplazamiento cero");
        bool content;using(Bitmap clipped=ShadowImage.Create(doc.Image,Color.Black,int.MaxValue,int.MinValue,out content))if(!content || clipped.GetPixel(0,0).A!=0 || clipped.Width!=4)throw new Exception("Recorte seguro");
        // Context selection uses the same active-layer mapping as the actual right-click handler.
        AddLayer();doc.Image.SetPixel(2,2,Color.Yellow);doc.Layers[doc.ActiveLayer].Name="Otra";RefreshLayers();int sourceRow=doc.Layers.Count-1-doc.ActiveLayer;Rectangle sourceRowBounds=layerList.GetItemRectangle(sourceRow);Point contextPoint=new Point(40,sourceRowBounds.Top+sourceRowBounds.Height/2);SelectLayerContext(contextPoint);if(doc.Layers[doc.ActiveLayer].Name!="Otra")throw new Exception("Origen contextual");
        layerList.SelectedIndex=layerList.Items.Count-1;SelectLayerContext(contextPoint);CreateShadow(Color.Black,15,1,1);if(doc.Layers[doc.ActiveLayer].Name!="Sombra de Otra")throw new Exception("Sombra de capa contextual no activa");
        using(ShadowDialog dialog=new ShadowDialog("Torre")){if(dialog.ShadowOpacity!=15 || dialog.OffsetX!=1 || dialog.OffsetY!=1 || dialog.ShadowColor!=Color.Black)throw new Exception("Valores iniciales sombra");dialog.Show(this);Application.DoEvents();using(Bitmap image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(TestArtifacts.DirectoryPath,"aropix-crear-sombra.png"),ImageFormat.Png);}dialog.Close();}
        string beforeCancel=doc.Serialize().OuterXml;int beforeHistory=undo.Count;bool beforeDirty=dirty;
        using(Timer cancelTimer=new Timer{Interval=50}){
            cancelTimer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form is ShadowDialog){((Button)form.CancelButton).PerformClick();break;}};
            cancelTimer.Start();ExecuteCommand("layershadow");cancelTimer.Stop();
        }
        if(beforeCancel!=doc.Serialize().OuterXml || beforeHistory!=undo.Count || beforeDirty!=dirty)throw new Exception("Cancelar diálogo sin alterar proyecto ni historial");
        ProjectState first=activeProject;int firstCount=doc.Layers.Count;
        AddProject(new Document(6,6){Name="Sombra segundo proyecto"},null);doc.Image.SetPixel(2,2,Color.White);CreateShadow(Color.Black,15,1,1);ProjectState second=activeProject;
        ActivateProject(first);if(doc.Layers.Count!=firstCount)throw new Exception("Sombra aislada por proyecto");ActivateProject(second);Undo(false);if(doc.Layers.Count!=1)throw new Exception("Historial sombra por proyecto");Undo(true);if(doc.Layers.Count!=2)throw new Exception("Rehacer por proyecto");dirty=false;StoreActive();CloseProject();ActivateProject(first);
        SetDocument(new Document(4,4));doc.Image.SetPixel(1,1,Color.White);
        SetTool("Rectángulo");canvas.ShapeClick(Point.Empty);if(CanRun("layershadow"))throw new Exception("No crear sombra durante figura pendiente");canvas.CancelShape();SetTool("Lápiz");
        while(doc.Layers.Count<16)AddLayer();if(CanRun("layershadow") || CreateShadow(Color.Black,15,1,1))throw new Exception("Respetar máximo de capas");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    void TestShadowPreview(){
        SetDocument(new Document(8,8));SetTool("Lápiz");doc.Image.SetPixel(1,1,Color.FromArgb(128,255,0,0));doc.Image.SetPixel(2,1,Color.Red);
        string before=doc.Serialize().OuterXml;int historyCount=undo.Count;bool wasDirty=dirty;Layer source=doc.Layers[0];
        foreach(int opacity in new[]{0,15,50,100}){
            canvas.UpdateShadowPreview(source,Color.Blue,opacity,1,2);
            using(Bitmap actual=canvas.TestShadowComposition())using(Document expected=Document.Deserialize(doc.Serialize().DocumentElement)){
                bool content;expected.Layers.Insert(0,new Layer("Sombra",ShadowImage.Create(source.Pixels,Color.Blue,1,2,out content)){Opacity=opacity});
                using(Bitmap final=expected.Composite())for(int y=0;y<8;y++)for(int x=0;x<8;x++)if(actual.GetPixel(x,y)!=final.GetPixel(x,y))throw new Exception("Vista previa coincide con creación, opacidad y alfa");
            }
        }
        canvas.UpdateShadowPreview(source,Color.Lime,100,-1,-1);using(Bitmap image=canvas.TestShadowComposition())if(image.GetPixel(1,0).ToArgb()!=Color.Lime.ToArgb())throw new Exception("Vista previa actualiza color y desplazamiento");
        canvas.ClearShadowPreview();if(before!=doc.Serialize().OuterXml || undo.Count!=historyCount || dirty!=wasDirty || canvas.HasShadowPreview)throw new Exception("Vista previa no modifica documento ni historial");
        bool updated=false;
        using(Timer timer=new Timer{Interval=70}){
            timer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form is ShadowDialog){
                if(!updated){if(!canvas.HasShadowPreview)throw new Exception("Vista previa inicial del diálogo");foreach(Control c in SecondaryDialog.Descendants(form)){NumericUpDown n=c as NumericUpDown;if(n!=null)n.Value=n.Maximum==100?50:-1;}updated=true;
                    using(Bitmap image=canvas.TestShadowComposition())if(Math.Abs(image.GetPixel(1,0).A-127)>1)throw new Exception("Controles actualizan vista previa");
                }
                ((Button)form.CancelButton).PerformClick();break;
            }};
            timer.Start();ConfigureShadow();timer.Stop();
        }
        if(!updated || canvas.HasShadowPreview || before!=doc.Serialize().OuterXml || undo.Count!=historyCount || dirty!=wasDirty)throw new Exception("Cancelar limpia vista previa sin cambios");
        using(Timer timer=new Timer{Interval=70}){
            timer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form is ShadowDialog){((Button)form.AcceptButton).PerformClick();break;}};
            timer.Start();ConfigureShadow();timer.Stop();
        }
        if(canvas.HasShadowPreview || doc.Layers.Count!=2 || undo.Count!=historyCount+1)throw new Exception("Confirmar vista previa crea exactamente una capa y acción");Undo(false);if(doc.Layers.Count!=1)throw new Exception("Deshacer tras vista previa");
    }
}
}
