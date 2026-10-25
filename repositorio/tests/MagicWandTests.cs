// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
namespace PixelArt {
partial class Editor {
    static int MaskCount(SelectionMask mask){int count=0;foreach(bool bit in mask.Bits)if(bit)count++;return count;}
    static void RequireWand(bool value,string message){if(!value)throw new Exception("Varita: "+message);}
    public void TestMagicWand(){
        TestWandCombine();
        SetDocument(new Document(8,8));
        for(int pass=0;pass<4;pass++){
            ShowSelectionTools(toolButtons["Selección"]);Application.DoEvents();
            int index=pass%2;((ToolStripMenuItem)selectionToolsMenu.Items[index]).PerformClick();
            selectionToolsMenu.Close(ToolStripDropDownCloseReason.ItemClicked);Application.DoEvents();
            RequireWand(!selectionToolsMenu.IsDisposed && canvas.Tool==(index==0?"Selección":"Varita mágica"),"menú reutilizable tras elegir herramienta");
        }
        ShowSelectionTools(toolButtons["Selección"]);selectionToolsMenu.Close(ToolStripDropDownCloseReason.Keyboard);Application.DoEvents();
        ShowSelectionTools(toolButtons["Selección"]);RequireWand(((ToolStripMenuItem)selectionToolsMenu.Items[1]).Checked,"menú reabierto sincronizado");selectionToolsMenu.Close();
        using(Bitmap pixels=new Bitmap(7,7,PixelFormat.Format32bppArgb)){
            foreach(Point p in new[]{new Point(3,3),new Point(3,2),new Point(3,4),new Point(2,3),new Point(4,3)})pixels.SetPixel(p.X,p.Y,Color.Red);
            Rectangle bounds;RequireWand(MaskCount(SelectionMask.Region(pixels,new Point(3,3),out bounds))==5,"cruz conecta sus cuatro lados");
            using(Graphics g=Graphics.FromImage(pixels))g.Clear(Color.White);
            for(int y=0;y<7;y++)for(int x=0;x<7;x++)if((x+y)%2==0)pixels.SetPixel(x,y,Color.Black);
            RequireWand(MaskCount(SelectionMask.Region(pixels,new Point(2,2),out bounds))==1,"tablero y diagonales aislados");
            using(Graphics g=Graphics.FromImage(pixels))g.Clear(Color.Transparent);
            for(int x=1;x<=5;x++){pixels.SetPixel(x,1,Color.Red);pixels.SetPixel(x,5,Color.Red);}for(int y=1;y<=5;y++){pixels.SetPixel(1,y,Color.Red);pixels.SetPixel(5,y,Color.Red);}
            SelectionMask ring=SelectionMask.Region(pixels,new Point(1,1),out bounds);RequireWand(MaskCount(ring)==16 && !ring.At(2,2),"anillo conserva hueco");
            RequireWand(MaskCount(SelectionMask.Region(pixels,new Point(3,3),out bounds))==9,"transparencia interior no atraviesa barrera");
            using(PixelBuffer raw=new PixelBuffer(pixels,true)){raw.Pixels[3*7+3]=0x00010203;raw.Pixels[3*7+4]=0x00ffffff;}
            RequireWand(MaskCount(SelectionMask.Region(pixels,new Point(3,3),out bounds))==9,"ignora RGB invisible");
            pixels.SetPixel(3,3,Color.FromArgb(128,255,0,0));pixels.SetPixel(4,3,Color.FromArgb(127,255,0,0));pixels.SetPixel(3,4,Color.FromArgb(128,254,0,0));
            RequireWand(MaskCount(SelectionMask.Region(pixels,new Point(3,3),out bounds))==1,"RGBA exacto sin tolerancia");
        }
        SetDocument(new Document(8,8));SetTool("Varita mágica");doc.Image.SetPixel(1,1,Color.Red);doc.Image.SetPixel(1,2,Color.Red);doc.Image.SetPixel(2,2,Color.Red);doc.Image.SetPixel(2,1,Color.Blue);
        int historyCount=undo.Count;string initial=doc.Serialize().OuterXml;canvas.WandSelect(new Point(1,1));RequireWand(MaskCount(canvas.CopySelectionMask())==3 && undo.Count==historyCount && doc.Serialize().OuterXml==initial,"L selecciona sin historial ni cambios");
        using(Bitmap copy=canvas.CopySelection())RequireWand(copy.GetPixel(1,0).A==0 && copy.GetPixel(0,0).R==255,"copia solo L");CopyPixels();
        canvas.DeleteSelection();RequireWand(doc.Image.GetPixel(2,1).B==255 && doc.Image.GetPixel(1,1).A==0,"borrado respeta hueco");Undo(false);RequireWand(doc.Image.GetPixel(1,1).R==255,"deshacer borrado");Undo(true);RequireWand(doc.Image.GetPixel(1,1).A==0,"rehacer borrado");Undo(false);
        SetTool("Varita mágica");canvas.WandSelect(new Point(1,1));canvas.BeginSelection(new Point(1,1));canvas.MoveSelectionTo(new Point(4,4));canvas.EndSelectionGesture();RequireWand(doc.Image.GetPixel(2,1).B==255 && doc.Image.GetPixel(1,1).A==0 && doc.Image.GetPixel(4,4).R==255,"mover L no corta hueco");Undo(false);
        foreach(RotateFlipType op in new[]{RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.RotateNoneFlipY}){
            SetTool("Varita mágica");canvas.WandSelect(new Point(1,1));canvas.RotateSelection(op);SelectionMask actual=canvas.CopySelectionMask();bool bottomHole=op==RotateFlipType.Rotate90FlipNone || op==RotateFlipType.RotateNoneFlipY;int hole=bottomHole?3:0;RequireWand(MaskCount(actual)==3 && !actual.Bits[hole],"giro/volteo máscara con resultado esperado");canvas.CancelSelection();RequireWand(doc.Image.GetPixel(2,1).B==255,"cancelar transformación");
            canvas.WandSelect(new Point(1,1));canvas.RotateSelection(op);canvas.MoveSelectionTo(new Point(4,4));canvas.CommitSelection();RequireWand(doc.Image.GetPixel(2,1).B==255 && doc.Image.GetPixel(4+hole%2,4+hole/2).A==0,"confirmar giro/volteo respeta hueco");Undo(false);
        }
        canvas.WandSelect(new Point(1,1));canvas.ResizeSelection(4,4);RequireWand(MaskCount(canvas.CopySelectionMask())==12,"escalar máscara sin rellenar hueco");canvas.MoveSelectionTo(new Point(4,4));canvas.CommitSelection();RequireWand(doc.Image.GetPixel(2,1).B==255 && MaskCount(canvas.CopySelectionMask())==12,"confirmar escala conserva máscara");Undo(false);Undo(true);RequireWand(doc.Image.GetPixel(4,4).R==255,"rehacer escala irregular");Undo(false);
        SetTool("Varita mágica");doc.Layers[0].Locked=true;doc.Layers[0].Visible=false;canvas.WandSelect(new Point(1,1));historyCount=undo.Count;canvas.DeleteSelection();canvas.RotateSelection(RotateFlipType.Rotate90FlipNone);RequireWand(MaskCount(canvas.CopySelectionMask())==3 && undo.Count==historyCount,"seleccionar oculto/bloqueado sin editar");doc.Layers[0].Locked=false;doc.Layers[0].Visible=true;
        AddLayer();doc.Image.SetPixel(1,1,Color.Lime);canvas.WandSelect(new Point(1,1));RequireWand(MaskCount(canvas.CopySelectionMask())==1,"solo capa activa");
        SetDocument(new Document(8,8));PastePixels();RequireWand(MaskCount(canvas.CopySelectionMask())==3,"pegar máscara interna");canvas.CommitSelection();doc.Image.SetPixel(1,0,Color.Blue);canvas.DeleteSelection();RequireWand(doc.Image.GetPixel(1,0).B==255,"borrar pegado respeta máscara");
        doc.Image.SetPixel(0,0,Color.Red);SetTool("Varita mágica");canvas.WandSelect(Point.Empty);float zoom=canvas.Zoom;CycleTool(1);RequireWand(canvas.Tool=="Selección" && canvas.Zoom==zoom,"Alt rueda grupo selección");CycleTool(1);RequireWand(canvas.Tool=="Varita mágica" && optionsTitle.Text=="Varita mágica" && toolButtons["Selección"].BackColor==Accent,"indicadores sincronizados");
        canvas.WandSelect(Point.Empty);using(Bitmap raw=doc.Export()){canvas.Zoom=16;using(Bitmap view=new Bitmap(canvas.Width,canvas.Height))canvas.DrawToBitmap(view,new Rectangle(Point.Empty,canvas.Size));using(Bitmap after=doc.Export())RequireWand(after.GetPixel(0,0)==raw.GetPixel(0,0),"contorno no exportado");}
        SyncCommands();RequireWand(selectionContext.Visible,"barra contextual para varita");OpenToolHelp();RequireWand(helpCenter.CurrentId=="wand","F1 documentación de varita");helpCenter.Close();
        ProjectState first=activeProject;CopyPixels();AddProject(new Document(8,8){Name="Destino varita"},null);PastePixels();RequireWand(canvas.CopySelectionMask()!=null,"máscara entre pestañas");canvas.CommitSelection();ActivateProject(first);RequireWand(!canvas.HasPendingSelection && canvas.Selection.IsEmpty,"cambio de proyecto limpia selección temporal");
        SetDocument(new Document(24,24));SetTool("Varita mágica");for(int x=4;x<=18;x++){doc.Image.SetPixel(x,4,Color.Red);doc.Image.SetPixel(x,18,Color.Red);}for(int y=4;y<=18;y++){doc.Image.SetPixel(4,y,Color.Red);doc.Image.SetPixel(18,y,Color.Red);}canvas.WandSelect(new Point(4,4));canvas.Grid=false;
        foreach(float level in new[]{.5f,4f,16f}){canvas.Zoom=level;canvas.ViewOrigin=new PointF(60,60);using(Bitmap view=new Bitmap(canvas.Width,canvas.Height)){canvas.DrawToBitmap(view,new Rectangle(Point.Empty,canvas.Size));view.Save(Path.Combine(TestArtifacts.DirectoryPath,"varita-contorno-"+((int)(level*100))+".png"),ImageFormat.Png);}}
        canvas.Grid=true;
        SetTool("Selección");using(Bitmap paste=new Bitmap(2,2))canvas.PasteSelection(paste);CycleTool(1);RequireWand(canvas.Tool=="Selección" && canvas.HasPendingSelection,"rueda respeta pegado pendiente");canvas.CancelSelection();
        using(Bitmap large=new Bitmap(2048,2048)){Stopwatch watch=Stopwatch.StartNew();Rectangle bounds;SelectionMask mask=SelectionMask.Region(large,Point.Empty,out bounds);watch.Stop();RequireWand(MaskCount(mask)==2048*2048,"región grande sin recursión");File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath,"rendimiento-varita.txt"),"Región transparente 2048 × 2048: "+watch.ElapsedMilliseconds+" ms. Conectividad 4, alfa exacto, sin modificar imagen.");}
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    void TestWandCombine(){
        SetDocument(new Document(8,8));SetTool("Varita mágica");
        doc.Image.SetPixel(1,1,Color.Red);doc.Image.SetPixel(2,1,Color.Red);doc.Image.SetPixel(3,2,Color.Red);doc.Image.SetPixel(5,5,Color.Blue);
        string before=doc.Serialize().OuterXml;int actions=undo.Count;bool wasDirty=dirty;
        canvas.WandClick(new Point(1,1),Keys.Control);RequireWand(MaskCount(canvas.CopySelectionMask())==2,"Ctrl sin selección inicia región");
        canvas.WandClick(new Point(3,2),Keys.Control);RequireWand(MaskCount(canvas.CopySelectionMask())==3,"Ctrl añade región diagonal separada solo explícitamente");
        canvas.WandClick(new Point(5,5),Keys.Control);RequireWand(MaskCount(canvas.CopySelectionMask())==4,"Ctrl añade otro color");
        canvas.WandClick(new Point(1,1),Keys.Control);RequireWand(!canvas.HasPendingSelection && MaskCount(canvas.CopySelectionMask())==4,"Ctrl dentro no arrastra ni duplica");
        canvas.WandClick(new Point(3,2),Keys.Alt);RequireWand(MaskCount(canvas.CopySelectionMask())==3,"Alt resta región");
        canvas.WandClick(new Point(3,2),Keys.Alt);RequireWand(MaskCount(canvas.CopySelectionMask())==3,"restar región exterior no cambia máscara");
        canvas.WandClick(new Point(1,1),Keys.Control|Keys.Alt);RequireWand(MaskCount(canvas.CopySelectionMask())==3,"modificadores simultáneos no cambian selección");
        RequireWand(before==doc.Serialize().OuterXml && undo.Count==actions && dirty==wasDirty,"combinar no modifica documento ni historial");
        CopyPixels();canvas.DeleteSelection();RequireWand(doc.Image.GetPixel(3,2).R==255 && doc.Image.GetPixel(5,5).A==0,"borrar respeta regiones restadas");Undo(false);
        PastePixels();SelectionMask pending=canvas.CopySelectionMask();canvas.WandClick(new Point(3,2),Keys.Control);RequireWand(canvas.HasPendingSelection && MaskCount(canvas.CopySelectionMask())==MaskCount(pending),"Ctrl preserva pegado pendiente");canvas.WandClick(new Point(1,1),Keys.Alt);RequireWand(canvas.HasPendingSelection && MaskCount(canvas.CopySelectionMask())==3,"Alt preserva pegado pendiente");canvas.CancelSelection();
        SetTool("Varita mágica");canvas.WandClick(Point.Empty,Keys.Alt);RequireWand(canvas.Selection.IsEmpty,"Alt sin selección no inicia región");
        canvas.WandSelect(new Point(1,1));canvas.WandClick(new Point(1,1),Keys.Alt);RequireWand(canvas.Selection.IsEmpty && canvas.CopySelectionMask()==null,"restar todo deselecciona");
        canvas.SelectBounds(new Rectangle(0,0,4,4));canvas.WandClick(new Point(1,1),Keys.Alt);RequireWand(MaskCount(canvas.CopySelectionMask())==14,"restar a selección rectangular");
        canvas.WandClick(new Point(1,1),Keys.Control);RequireWand(MaskCount(canvas.CopySelectionMask())==16,"sumar rellena hueco de máscara");
        doc.Layers[0].Locked=true;doc.Layers[0].Visible=false;canvas.WandSelect(new Point(1,1));canvas.WandClick(new Point(5,5),Keys.Control);RequireWand(MaskCount(canvas.CopySelectionMask())==3,"combinar oculto bloqueado");canvas.WandClick(new Point(1,1),Keys.Alt);RequireWand(canvas.Selection==new Rectangle(5,5,1,1),"restar ajusta límites");
        SetDocument(new Document(8,8));SetTool("Varita mágica");
        for(int x=2;x<=5;x++){doc.Image.SetPixel(x,2,Color.Red);doc.Image.SetPixel(x,5,Color.Red);}for(int y=2;y<=5;y++){doc.Image.SetPixel(2,y,Color.Red);doc.Image.SetPixel(5,y,Color.Red);}
        canvas.WandSelect(Point.Empty);canvas.WandClick(new Point(3,3),Keys.Control);RequireWand(MaskCount(canvas.CopySelectionMask())==52,"añadir transparencia interior separada");canvas.WandClick(new Point(3,3),Keys.Alt);RequireWand(MaskCount(canvas.CopySelectionMask())==48,"restar transparencia interior");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}
