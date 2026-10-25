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
partial class Document {
    public int? MirrorAxisX2,MirrorAxisY2;
    public int AxisX2 {get{return MirrorAxisX2??(Image.Width-1);}}
    public int AxisY2 {get{return MirrorAxisY2??(Image.Height-1);}}
    public void SaveAxes(XmlElement root){if(MirrorAxisX2.HasValue)root.SetAttribute("axisX2",MirrorAxisX2.Value.ToString());if(MirrorAxisY2.HasValue)root.SetAttribute("axisY2",MirrorAxisY2.Value.ToString());}
    public void ReadAxes(XmlElement root){int value;if(int.TryParse(root.GetAttribute("axisX2"),out value))MirrorAxisX2=Math.Max(0,Math.Min((Image.Width-1)*2,value));if(int.TryParse(root.GetAttribute("axisY2"),out value))MirrorAxisY2=Math.Max(0,Math.Min((Image.Height-1)*2,value));}
    public Bitmap ExportLayer(int index,int scale,bool opacity){
        if(index<0 || index>=Layers.Count || scale<1 || scale>32 || (long)Image.Width*scale>8192 || (long)Image.Height*scale>8192)throw new ArgumentOutOfRangeException("scale");
        Bitmap output=new Bitmap(Image.Width*scale,Image.Height*scale,PixelFormat.Format32bppArgb);
        try{using(Graphics g=Graphics.FromImage(output)){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;Rectangle area=new Rectangle(Point.Empty,output.Size);if(opacity)Layers[index].Draw(g,area,null,true);else g.DrawImage(Layers[index].Pixels,area,0,0,Image.Width,Image.Height,GraphicsUnit.Pixel);}return output;}catch{output.Dispose();throw;}
    }
}
class AxesDialog : SecondaryDialog {
    public NumericUpDown AxisX,AxisY;public CheckBox CenterX,CenterY;
    public AxesDialog(Document doc){
        Text="Posición de los ejes de simetría";ClientSize=new Size(460,310);BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        Controls.Add(new Label{Text="Eje vertical — coordenada X",Location=new Point(20,20),AutoSize=true});Controls.Add(new Label{Text="Eje horizontal — coordenada Y",Location=new Point(20,105),AutoSize=true});
        AxisX=new NumericUpDown{Minimum=0,Maximum=doc.Image.Width-1,DecimalPlaces=1,Increment=0.5m,Value=doc.AxisX2/2m,Location=new Point(20,50),Width=150};AxisY=new NumericUpDown{Minimum=0,Maximum=doc.Image.Height-1,DecimalPlaces=1,Increment=0.5m,Value=doc.AxisY2/2m,Location=new Point(20,135),Width=150};Controls.Add(AxisX);Controls.Add(AxisY);
        CenterX=new CheckBox{Text="Centrado en el lienzo",Checked=!doc.MirrorAxisX2.HasValue,Location=new Point(190,50),Width=250};CenterY=new CheckBox{Text="Centrado en el lienzo",Checked=!doc.MirrorAxisY2.HasValue,Location=new Point(190,135),Width=250};Controls.Add(CenterX);Controls.Add(CenterY);
        EventHandler update=delegate{AxisX.Enabled=!CenterX.Checked;AxisY.Enabled=!CenterY.Checked;if(CenterX.Checked)AxisX.Value=(doc.Image.Width-1)/2m;if(CenterY.Checked)AxisY.Value=(doc.Image.Height-1)/2m;};CenterX.CheckedChanged+=update;CenterY.CheckedChanged+=update;update(null,EventArgs.Empty);
        Controls.Add(new Label{Text="0 es el centro del primer píxel; 0,5 está entre el 0 y el 1.\nLos valores se ajustan a pasos de medio píxel.\nActiva cada simetría desde Dibujo. Los reflejos que\nqueden fuera del lienzo se recortan.",Location=new Point(20,185),Size=new Size(425,75)});
        Button cancel=Editor.Button("Cancelar",100);cancel.Location=new Point(220,270);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);Button apply=Editor.Button("Aplicar",100);apply.Location=new Point(330,270);apply.DialogResult=DialogResult.OK;Controls.Add(apply);AcceptButton=apply;CancelButton=cancel;
    }
}
class LayerExportDialog : SecondaryDialog {
    public CheckedListBox Choices=new CheckedListBox();public NumericUpDown ExportScale;public CheckBox LayerOpacity;
    public List<int> Indices=new List<int>();
    public LayerExportDialog(Document doc){
        Text="Exportar capas como PNG";ClientSize=new Size(460,420);BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        Controls.Add(new Label{Text="Elige al menos una capa (arriba = delante)",Location=new Point(20,15),AutoSize=true});Choices.Location=new Point(20,45);Choices.Size=new Size(420,175);Choices.BackColor=Editor.PanelColor;Choices.ForeColor=Color.White;Choices.CheckOnClick=true;Controls.Add(Choices);
        for(int i=doc.Layers.Count-1;i>=0;i--){Indices.Add(i);Choices.Items.Add(doc.Layers[i].Name+(doc.Layers[i].Visible?"":" (oculta)"),doc.Layers[i].Visible);}
        Button all=Editor.Button("Todas",95);all.Location=new Point(20,225);all.Click+=delegate{for(int i=0;i<Choices.Items.Count;i++)Choices.SetItemChecked(i,true);};Controls.Add(all);
        Button none=Editor.Button("Ninguna",95);none.Location=new Point(125,225);none.Click+=delegate{for(int i=0;i<Choices.Items.Count;i++)Choices.SetItemChecked(i,false);};Controls.Add(none);
        Controls.Add(new Label{Text="Escala",Location=new Point(245,230),AutoSize=true});ExportScale=new NumericUpDown{Minimum=1,Maximum=Math.Min(32,8192/Math.Max(doc.Image.Width,doc.Image.Height)),Value=1,Location=new Point(310,228),Width=100};Controls.Add(ExportScale);
        LayerOpacity=new CheckBox{Text="Respetar la opacidad de cada capa",Checked=true,Location=new Point(20,270),Width=420};Controls.Add(LayerOpacity);
        Controls.Add(new Label{Text="Un PNG transparente por capa, del tamaño del lienzo.\nConserva la posición y permite elegir capas ocultas.\nSe crea una carpeta nueva para no sobrescribir archivos.",Location=new Point(20,310),Size=new Size(425,65)});
        Button cancel=Editor.Button("Cancelar",100);cancel.Location=new Point(220,380);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);Button export=Editor.Button("Exportar…",100);export.Location=new Point(330,380);export.Enabled=Choices.CheckedIndices.Count>0;Choices.ItemCheck+=delegate(object sender,ItemCheckEventArgs e){int count=Choices.CheckedIndices.Count+(e.NewValue==CheckState.Checked?1:0)-(e.CurrentValue==CheckState.Checked?1:0);export.Enabled=count>0;};export.Click+=delegate{if(Choices.CheckedIndices.Count>0)DialogResult=DialogResult.OK;};Controls.Add(export);AcceptButton=export;CancelButton=cancel;
    }
}
partial class Editor {
    public void TestAxesAndLayerExport(){
        foreach(int axis in new int[]{0,5,6,18})foreach(string shape in new string[]{"Línea recta","Rectángulo","Círculo","Triángulo"})using(Document original=new Document(10,10))using(Document mirror=new Document(10,10)){
            mirror.MirrorVertical=mirror.MirrorHorizontal=true;mirror.MirrorAxisX2=axis;mirror.MirrorAxisY2=5;
            original.Shape(shape,new Point(1,1),new Point(4,4),new Point(1,4),Color.Red,true,2);mirror.Shape(shape,new Point(1,1),new Point(4,4),new Point(1,4),Color.Red,true,2);
            for(int y=0;y<10;y++)for(int x=0;x<10;x++){int rx=axis-x,ry=5-y;bool expected=original.Image.GetPixel(x,y).A>0 || (rx>=0 && rx<10 && original.Image.GetPixel(rx,y).A>0) || (ry>=0 && ry<10 && original.Image.GetPixel(x,ry).A>0) || (rx>=0 && rx<10 && ry>=0 && ry<10 && original.Image.GetPixel(rx,ry).A>0);if((mirror.Image.GetPixel(x,y).A>0)!=expected)throw new Exception("Ejes desplazados con recorte y medio píxel");}
            using(Document loaded=Document.Deserialize(mirror.Serialize().DocumentElement))if(loaded.AxisX2!=axis || loaded.AxisY2!=5)throw new Exception("Guardar ejes personalizados");
        }
        SetDocument(new Document(10,10));SetAxes(5,6);ResizeCanvas(14,14,8);if(doc.MirrorAxisX2!=13 || doc.MirrorAxisY2!=14)throw new Exception("Ejes siguen al dibujo al ampliar");Undo(false);if(doc.MirrorAxisX2!=5 || doc.Image.Width!=10)throw new Exception("Restaurar ejes y lienzo");Undo(false);if(doc.MirrorAxisX2.HasValue)throw new Exception("Deshacer posición de ejes");Undo(true);SetAxes(null,null);if(doc.AxisX2!=9)throw new Exception("Centrar ejes");
        SetDocument(new Document(8,6));doc.Name="Prueba";doc.Layers[0].Name="A:B";doc.Image.SetPixel(2,3,Color.Red);AddLayer();doc.Layers[1].Name="A:B";doc.Image.SetPixel(4,1,Color.Blue);doc.Layers[1].Visible=false;doc.Layers[1].Locked=true;doc.Layers[1].Opacity=50;
        using(Bitmap image=doc.ExportLayer(1,2,true)){if(image.Size!=new Size(16,12) || image.GetPixel(8,2).A<126 || image.GetPixel(8,2).A>129 || image.GetPixel(4,6).A!=0)throw new Exception("PNG de capa oculta, escala y opacidad");}
        using(Bitmap image=doc.ExportLayer(1,1,false))if(image.GetPixel(4,1).ToArgb()!=Color.Blue.ToArgb() || doc.Layers[1].Visible || doc.Layers[1].Opacity!=50)throw new Exception("Exportar sin opacidad no modifica proyecto");
        string folder=null,second=null;
        try{folder=WriteLayerPNGs(Path.GetTempPath(),new List<int>{1,0},1,true);second=WriteLayerPNGs(Path.GetTempPath(),new List<int>{1,0},1,true);string[] files=Directory.GetFiles(folder,"*.png");if(files.Length!=2 || folder==second || !File.Exists(Path.Combine(folder,"01-A_B.png")))throw new Exception("Exportación separada con nombres seguros y orden");using(Bitmap image=new Bitmap(Path.Combine(folder,"02-A_B.png")))if(image.GetPixel(2,3).ToArgb()!=Color.Red.ToArgb())throw new Exception("Contenido exportado por capa");}
        finally{foreach(string directory in new string[]{folder,second})if(directory!=null){foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}}
        using(AxesDialog dialog=new AxesDialog(doc)){dialog.Show();Application.DoEvents();using(Bitmap image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-ejes.png"),ImageFormat.Png);}}
        using(LayerExportDialog dialog=new LayerExportDialog(doc)){dialog.Show();Application.DoEvents();using(Bitmap image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-exportar-capas.png"),ImageFormat.Png);}}
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    void ConfigureAxes(){using(AxesDialog dialog=new AxesDialog(doc))if(dialog.ShowDialog(this)==DialogResult.OK){int? x=dialog.CenterX.Checked?(int?)null:(int)Math.Round(dialog.AxisX.Value*2,MidpointRounding.AwayFromZero),y=dialog.CenterY.Checked?(int?)null:(int)Math.Round(dialog.AxisY.Value*2,MidpointRounding.AwayFromZero);SetAxes(x,y);}}
    void SetAxes(int? x,int? y){if(doc.MirrorAxisX2==x && doc.MirrorAxisY2==y)return;FinishPendingLayerEdit();PushProperties();doc.MirrorAxisX2=x;doc.MirrorAxisY2=y;LayerEdited();}
    void ExportLayers(){
        using(LayerExportDialog dialog=new LayerExportDialog(doc))if(dialog.ShowDialog(this)==DialogResult.OK)using(FolderBrowserDialog folder=new FolderBrowserDialog{Description="Carpeta donde crear la exportación de capas"})if(folder.ShowDialog(this)==DialogResult.OK){
            try{canvas.CommitSelection();List<int> indices=new List<int>();foreach(int row in dialog.Choices.CheckedIndices)indices.Add(dialog.Indices[row]);string destination=WriteLayerPNGs(folder.SelectedPath,indices,(int)dialog.ExportScale.Value,dialog.LayerOpacity.Checked);Notify("Capas exportadas en: "+destination,NoticeKind.Information);}catch(Exception ex){Error(ex);}
        }
    }
    string WriteLayerPNGs(string parent,List<int> indices,int scale,bool opacity){
        string stem=SafeName(doc.Name);if(stem.Length>60)stem=stem.Substring(0,60);string directory=Path.Combine(parent,stem+"-capas-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(directory);
        for(int order=0;order<indices.Count;order++){int index=indices[order];string name=SafeName(doc.Layers[index].Name);if(name.Length>80)name=name.Substring(0,80);string path=Path.Combine(directory,(order+1).ToString("D2")+"-"+name+".png");using(Bitmap image=doc.ExportLayer(index,scale,opacity))using(FileStream stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))image.Save(stream,ImageFormat.Png);}
        return directory;
    }
}
}


