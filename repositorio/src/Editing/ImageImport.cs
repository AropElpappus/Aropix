// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace PixelArt {
class ImportDialog : SecondaryDialog {
    public NumericUpDown ImageWidth,ImageHeight,ImportOpacity;
    public CheckBox Smooth;
    bool adjusting;
    public ImportDialog(Image source,Size canvas){
        Text="Importar imagen como capa";ClientSize=new Size(530,370);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font("Segoe UI",10);
        PictureBox preview=new PictureBox{Image=source,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(65,70,78),Location=new Point(15,20),Size=new Size(230,230)};Controls.Add(preview);
        Label details=new Label{Text=string.Format("Original: {0} × {1} px",source.Width,source.Height),Location=new Point(15,260),Size=new Size(235,45)};Controls.Add(details);
        double scale=Math.Min(1,Math.Min(canvas.Width/(double)source.Width,canvas.Height/(double)source.Height));
        ImageWidth=Number("Ancho importado (px)",275,20,Math.Max(1,(int)Math.Round(source.Width*scale)),2048);ImageHeight=Number("Alto importado (px)",275,83,Math.Max(1,(int)Math.Round(source.Height*scale)),2048);
        CheckBox ratio=new CheckBox{Text="Conservar proporción",Checked=true,Location=new Point(275,145),Width=240};Controls.Add(ratio);
        ImageWidth.ValueChanged+=delegate{if(adjusting || !ratio.Checked)return;adjusting=true;ImageHeight.Value=Math.Max(1,Math.Min(2048,Math.Round(ImageWidth.Value*source.Height/source.Width)));adjusting=false;};
        ImageHeight.ValueChanged+=delegate{if(adjusting || !ratio.Checked)return;adjusting=true;ImageWidth.Value=Math.Max(1,Math.Min(2048,Math.Round(ImageHeight.Value*source.Width/source.Height)));adjusting=false;};
        ImportOpacity=Number("Opacidad (%)",275,175,50,100,0);Smooth=new CheckBox{Text="Suavizar (fotografías)",Location=new Point(275,238),Width=240};Controls.Add(Smooth);
        Controls.Add(new Label{Text="Se centra en el lienzo. Lo que quede fuera se recorta.\nDespués puedes moverla y redimensionarla con Selección.",Location=new Point(15,295),Size=new Size(500,42),Font=new Font("Segoe UI",9)});
        Button cancel=Editor.Button("Cancelar",100);cancel.Location=new Point(290,335);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);
        Button import=Editor.Button("Importar",110);import.Location=new Point(400,335);import.BackColor=Editor.Accent;import.DialogResult=DialogResult.OK;Controls.Add(import);AcceptButton=import;CancelButton=cancel;
    }
    NumericUpDown Number(string label,int x,int y,int value,int max,int min=1){Controls.Add(new Label{Text=label,Location=new Point(x,y),AutoSize=true});NumericUpDown n=new NumericUpDown{Location=new Point(x,y+25),Width=170,Minimum=min,Maximum=max,Value=value};Controls.Add(n);return n;}
}
partial class PixelCanvas {
    public void SelectBounds(Rectangle bounds){CancelSelection();Selection=Rectangle.Intersect(bounds,new Rectangle(0,0,Doc.Image.Width,Doc.Image.Height));Invalidate();}
}
partial class Editor {
    void ImportImage(){
        if(doc==null)return;if(doc.Layers.Count>=16){MessageBox.Show(this,"El proyecto admite hasta 16 capas.");return;}
        using(OpenFileDialog open=new OpenFileDialog{Title="Importar imagen como capa",Filter="Imágenes PNG o JPG|*.png;*.jpg;*.jpeg"}){
            if(open.ShowDialog(this)!=DialogResult.OK)return;
            ImportImageFile(open.FileName);
        }
    }
    void ImportImageFile(string path){
        if(doc==null)return;if(doc.Layers.Count>=16){MessageBox.Show(this,"El proyecto admite hasta 16 capas.");return;}
            try{using(Image image=Image.FromFile(path)){
                if(image.RawFormat.Guid!=ImageFormat.Png.Guid && image.RawFormat.Guid!=ImageFormat.Jpeg.Guid)throw new Exception("Selecciona una imagen PNG o JPG.");
                if((long)image.Width*image.Height>100000000)throw new Exception("La imagen supera 100 megapíxeles. Reduce su tamaño antes de importarla.");
                using(ImportDialog dialog=new ImportDialog(image,doc.Image.Size))if(dialog.ShowDialog(this)==DialogResult.OK)ImportAsLayer(image,Path.GetFileNameWithoutExtension(path),(int)dialog.ImageWidth.Value,(int)dialog.ImageHeight.Value,(int)dialog.ImportOpacity.Value,dialog.Smooth.Checked);
            }}catch(Exception ex){Error(ex);}
    }
    void ImportAsLayer(Image source,string name,int width,int height,int opacity,bool smooth){
        if(doc.Layers.Count>=16 || width<1 || height<1 || width>2048 || height>2048 || opacity<0 || opacity>100)throw new ArgumentException("Dimensiones, opacidad o número de capas no válidos.");
        int x=(doc.Image.Width-width)/2,y=(doc.Image.Height-height)/2;
        Bitmap pixels=new Bitmap(doc.Image.Width,doc.Image.Height,PixelFormat.Format32bppArgb);
        try{
            using(Graphics g=Graphics.FromImage(pixels))
            using(ImageAttributes attrs=new ImageAttributes()){
                g.CompositingMode=CompositingMode.SourceCopy;
                g.InterpolationMode=smooth?InterpolationMode.HighQualityBicubic:InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode=PixelOffsetMode.Half;attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source,new Rectangle(x,y,width,height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attrs);
            }
            FinishPendingLayerEdit();
        }catch{pixels.Dispose();throw;}
        InsertLayerPixels(name,pixels,doc.ActiveLayer+1,opacity);
        LayerEdited();SetTool("Selección");canvas.SelectBounds(new Rectangle(x,y,width,height));
    }
    public void TestImportAndEvenCircles(){
        string png=Path.Combine(Path.GetTempPath(),"pixelart-import-"+Guid.NewGuid()+".png"),jpg=png+".jpg";
        try{
            using(Bitmap image=new Bitmap(4,2)){image.SetPixel(0,0,Color.Red);image.SetPixel(3,1,Color.Blue);image.Save(png,ImageFormat.Png);image.Save(jpg,ImageFormat.Jpeg);}
            using(Image image=Image.FromFile(png))using(ImportDialog dialog=new ImportDialog(image,new Size(100,100))){
                dialog.Show();Application.DoEvents();
                dialog.ImageWidth.Value=8;if(dialog.ImageHeight.Value!=4)throw new Exception("Proporción al importar");
                using(Bitmap shot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(shot,new Rectangle(Point.Empty,dialog.Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-importar.png"),ImageFormat.Png);}
            }
            SetDocument(new Document(20,20));using(Image image=Image.FromFile(png))ImportAsLayer(image,"Referencia PNG",8,4,35,false);
            if(doc.Layers.Count!=2 || doc.Layers[1].Opacity!=35 || doc.Image.GetPixel(6,8).ToArgb()!=Color.Red.ToArgb() || doc.Image.GetPixel(9,8).A!=0 || canvas.Selection!=new Rectangle(6,8,8,4))throw new Exception("Importar PNG centrado con transparencia");
            Undo(false);if(doc.Layers.Count!=1)throw new Exception("Deshacer importación");Undo(true);if(doc.Layers.Count!=2)throw new Exception("Rehacer importación");
            using(Image image=Image.FromFile(jpg))ImportAsLayer(image,"Referencia JPG",30,25,50,true);if(doc.Layers.Count!=3 || doc.Image.GetPixel(19,19).A!=255)throw new Exception("Importar JPG y recortar al lienzo");
            string saved=png+".pixelart";try{doc.Save(saved);using(Document reopened=Document.Load(saved))if(reopened.Layers[1].Name!="Referencia PNG" || reopened.Layers[1].Opacity!=35)throw new Exception("Guardar referencias importadas");}finally{if(File.Exists(saved))File.Delete(saved);}
        }finally{if(File.Exists(png))File.Delete(png);if(File.Exists(jpg))File.Delete(jpg);}
        for(int radius=0;radius<=8;radius++)foreach(bool filled in new bool[]{false,true})using(Document even=new Document(32,32)){
            even.Shape("Círculo",new Point(15,15),new Point(15+radius,15),Point.Empty,Color.Red,filled,1,true);
            int left=32,right=-1,top=32,bottom=-1;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(even.Image.GetPixel(x,y).A>0){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);if(even.Image.GetPixel(31-x,y).A==0 || even.Image.GetPixel(x,31-y).A==0)throw new Exception("Simetría círculo par");}
            if(right-left+1!=2*radius+2 || bottom-top+1!=2*radius+2)throw new Exception("Diámetro par exacto");
        }
        using(Document border=new Document(20,20))using(Document fill=new Document(20,20)){
            border.Shape("Círculo",new Point(9,9),new Point(17,9),Point.Empty,Color.Red,false,4,true);fill.Shape("Círculo",new Point(9,9),new Point(17,9),Point.Empty,Color.Red,true,1,true);
            for(int y=0;y<20;y++)for(int x=0;x<20;x++)if(border.Image.GetPixel(x,y).A>0 && fill.Image.GetPixel(x,y).A==0)throw new Exception("Grosor interior en círculo par");
        }
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}


