// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
namespace PixelArt {
class CanvasSizeDialog : SecondaryDialog {
    public NumericUpDown CanvasWidth,CanvasHeight;
    public int AnchorIndex=4;
    public CanvasSizeDialog(Size size){
        Text="Tamaño del lienzo";ClientSize=new Size(460,405);BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        Controls.Add(new Label{Text="Ancho (px)",Location=new Point(20,15),AutoSize=true});Controls.Add(new Label{Text="Alto (px)",Location=new Point(240,15),AutoSize=true});
        CanvasWidth=new NumericUpDown{Minimum=1,Maximum=2048,Value=size.Width,Location=new Point(20,42),Width=190};CanvasHeight=new NumericUpDown{Minimum=1,Maximum=2048,Value=size.Height,Location=new Point(240,42),Width=190};Controls.Add(CanvasWidth);Controls.Add(CanvasHeight);
        Controls.Add(new Label{Text="Conservar el dibujo en:",Location=new Point(20,85),AutoSize=true});
        string[] names={"Arriba izq.","Arriba","Arriba der.","Izquierda","Centro","Derecha","Abajo izq.","Abajo","Abajo der."};Button[] anchors=new Button[9];
        for(int i=0;i<9;i++){int index=i;Button b=Editor.Button(names[i],130);b.Location=new Point(20+(i%3)*140,115+(i/3)*40);b.BackColor=i==4?Editor.Accent:Color.FromArgb(47,56,66);anchors[i]=b;Controls.Add(b);b.Click+=delegate{AnchorIndex=index;for(int j=0;j<9;j++)anchors[j].BackColor=j==index?Editor.Accent:Color.FromArgb(47,56,66);};}
        Controls.Add(new Label{Text="Ejemplo: Izquierda añade espacio a la derecha.\nCentro reparte el espacio entre ambos lados.\nSe aplica a todas las capas, incluidas las bloqueadas.\nEl espacio nuevo es transparente; el dibujo no se escala.",Location=new Point(20,245),Size=new Size(425,85)});
        Label crop=new Label{Location=new Point(20,330),Size=new Size(420,25),ForeColor=Color.Gold};Controls.Add(crop);EventHandler update=delegate{crop.Text=CanvasWidth.Value<size.Width || CanvasHeight.Value<size.Height?"Al reducir se recortará el contenido que quede fuera.":"";};CanvasWidth.ValueChanged+=update;CanvasHeight.ValueChanged+=update;
        Button cancel=Editor.Button("Cancelar",100);cancel.Location=new Point(220,365);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);Button apply=Editor.Button("Aplicar",100);apply.Location=new Point(330,365);apply.DialogResult=DialogResult.OK;Controls.Add(apply);AcceptButton=apply;CancelButton=cancel;
    }
}
partial class Editor {
    void ChangeCanvasSize(){using(CanvasSizeDialog dialog=new CanvasSizeDialog(doc.Image.Size))if(dialog.ShowDialog(this)==DialogResult.OK)try{ResizeCanvas((int)dialog.CanvasWidth.Value,(int)dialog.CanvasHeight.Value,dialog.AnchorIndex);}catch(Exception ex){Error(ex);}}
    void ResizeCanvas(int width,int height,int anchor){
        if(width<1 || width>2048 || height<1 || height>2048 || anchor<0 || anchor>8)throw new ArgumentOutOfRangeException("size");
        if(doc.Image.Size==new Size(width,height))return;
        canvas.CommitSelection();canvas.CancelSelection();canvas.CancelShape();
        int dx=(width-doc.Image.Width)*(anchor%3)/2,dy=(height-doc.Image.Height)*(anchor/3)/2;
        List<Bitmap> images=new List<Bitmap>(doc.Layers.Count);
        try{
            foreach(Layer layer in doc.Layers){Bitmap image=new Bitmap(width,height,PixelFormat.Format32bppArgb);images.Add(image);if(preparedImageObserver!=null)preparedImageObserver(image);using(Graphics g=Graphics.FromImage(image)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(layer.Pixels,dx,dy);}}
            PushUndo();for(int i=0;i<images.Count;i++){doc.Layers[i].Pixels.Dispose();doc.Layers[i].Pixels=images[i];}images.Clear();if(doc.MirrorAxisX2.HasValue)doc.MirrorAxisX2=Math.Max(0,Math.Min((width-1)*2,doc.MirrorAxisX2.Value+2*dx));if(doc.MirrorAxisY2.HasValue)doc.MirrorAxisY2=Math.Max(0,Math.Min((height-1)*2,doc.MirrorAxisY2.Value+2*dy));
        }finally{foreach(Bitmap image in images)image.Dispose();}
        LayerEdited();canvas.ClearPointer();canvas.Fit();UpdateStatus();StoreActive();
    }
    static string[] DroppedImages(IDataObject data){
        if(data==null || !data.GetDataPresent(DataFormats.FileDrop))return new string[0];
        string[] paths=data.GetData(DataFormats.FileDrop) as string[];if(paths==null || paths.Length==0)return new string[0];
        foreach(string path in paths){string ext=Path.GetExtension(path).ToLowerInvariant();if((ext!=".png" && ext!=".jpg" && ext!=".jpeg") || !File.Exists(path))return new string[0];}return paths;
    }
    void SetupImageDrop(Control control){
        control.AllowDrop=true;
        DragEventHandler enter=delegate(object sender,DragEventArgs e){e.Effect=(DroppedProjects(e.Data).Length>0 || (doc!=null && DroppedImages(e.Data).Length>0)) && (e.AllowedEffect & DragDropEffects.Copy)!=0?DragDropEffects.Copy:DragDropEffects.None;};
        control.DragEnter+=enter;control.DragOver+=enter;
        control.DragDrop+=delegate(object sender,DragEventArgs e){string[] documents=DroppedProjects(e.Data);string[] images=DroppedImages(e.Data);if(documents.Length==0 && images.Length==0)return;BeginInvoke(new Action(delegate{if(IsDisposed || Disposing)return;foreach(string path in documents)OpenProjectFile(path);foreach(string path in images){if(doc==null)break;if(doc.Layers.Count>=16){MessageBox.Show(this,"El proyecto admite hasta 16 capas.");break;}ImportImageFile(path);}}));};
        foreach(Control child in control.Controls)SetupImageDrop(child);
    }
    static string[] DroppedProjects(IDataObject data){
        if(data==null || !data.GetDataPresent(DataFormats.FileDrop))return new string[0];
        string[] paths=data.GetData(DataFormats.FileDrop) as string[];
        if(paths==null)return new string[0];
        foreach(string path in paths)if(!string.Equals(Path.GetExtension(path),".pixelart",StringComparison.OrdinalIgnoreCase) || !File.Exists(path))return new string[0];
        return paths;
    }
    public void TestCanvasSizeAndDrop(){
        for(int anchor=0;anchor<9;anchor++){
            SetDocument(new Document(4,4));doc.Image.SetPixel(1,1,Color.Red);AddLayer();doc.Image.SetPixel(2,2,Color.FromArgb(128,0,0,255));doc.Layers[1].Locked=true;doc.Layers[1].Visible=false;doc.Layers[1].Opacity=35;
            ResizeCanvas(8,10,anchor);int dx=2*(anchor%3),dy=3*(anchor/3);
            if(doc.Image.Size!=new Size(8,10) || doc.Layers[0].Pixels.GetPixel(1+dx,1+dy).ToArgb()!=Color.Red.ToArgb() || doc.Image.GetPixel(2+dx,2+dy).A!=128 || !doc.Layers[1].Locked || doc.Layers[1].Visible || doc.Layers[1].Opacity!=35)throw new Exception("Ampliar lienzo con anclaje y capas intactas");
            Undo(false);if(doc.Image.Size!=new Size(4,4))throw new Exception("Deshacer tamaño de lienzo");Undo(true);if(doc.Image.Size!=new Size(8,10))throw new Exception("Rehacer tamaño de lienzo");
            ResizeCanvas(4,4,anchor);if(doc.Layers[0].Pixels.GetPixel(1,1).ToArgb()!=Color.Red.ToArgb() || doc.Image.GetPixel(2,2).A!=128)throw new Exception("Recortar con anclaje");
            using(Document loaded=Document.Deserialize(doc.Serialize().DocumentElement))if(loaded.Image.Size!=new Size(4,4) || !loaded.Layers[1].Locked)throw new Exception("Guardar lienzo y bloqueo");
        }
        string png=Path.Combine(Path.GetTempPath(),"pixelart-drop-"+Guid.NewGuid()+".png");try{using(Bitmap image=new Bitmap(2,2))image.Save(png,ImageFormat.Png);DataObject data=new DataObject(DataFormats.FileDrop,new string[]{png});if(DroppedImages(data).Length!=1)throw new Exception("Aceptar PNG arrastrado");if(DroppedImages(new DataObject(DataFormats.Text,png)).Length!=0 || DroppedImages(new DataObject(DataFormats.FileDrop,new string[]{png+".exe"})).Length!=0)throw new Exception("Rechazar arrastre incompatible");}finally{if(File.Exists(png))File.Delete(png);}
        using(CanvasSizeDialog dialog=new CanvasSizeDialog(new Size(100,100))){dialog.Show();Application.DoEvents();using(Bitmap shot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(shot,new Rectangle(Point.Empty,dialog.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-lienzo.png"),ImageFormat.Png);}}
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}


