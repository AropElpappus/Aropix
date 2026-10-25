// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace PixelArt {
partial class Editor {
    static void CheckPencilPixels(Bitmap a,Bitmap b,string message){if(a.Size!=b.Size)throw new Exception(message);for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb())throw new Exception(message+" en "+x+","+y);}
    public void TestPencilTips(){
        int[] counts={0,1,4,9,12,21,32,37,52,69,80};
        for(int n=1;n<=10;n++)using(Document square=new Document(40,40))using(Document legacy=new Document(40,40))using(Document circle=new Document(40,40)){
            Point center=new Point(12,12);square.PencilLine(center,center,Color.Blue,n,PencilTips.All[0]);legacy.Stamp(12,12,Color.Blue,n);CheckPencilPixels(square.Image,legacy.Image,"Punta cuadrada original");
            circle.PencilLine(center,center,Color.Blue,n,PencilTips.All[1]);int count=0,start=12-(n-1)/2;
            for(int y=0;y<40;y++)for(int x=0;x<40;x++){
                Color p=circle.Image.GetPixel(x,y);if(p.A==0)continue;count++;
                if(p.ToArgb()!=Color.Blue.ToArgb() || x<start || y<start || x>=start+n || y>=start+n)throw new Exception("Cobertura exacta N x N");
                if(circle.Image.GetPixel(2*start+n-1-x,y).A!=255 || circle.Image.GetPixel(x,2*start+n-1-y).A!=255)throw new Exception("Simetría de huella par/impar");
            }
            if(count!=counts[n])throw new Exception("Huella circular de tamaño "+n);
            foreach(Point end in new[]{new Point(30,12),new Point(12,30),new Point(30,30),new Point(18,31)})using(Document line=new Document(40,40))using(Document path=new Document(40,40)){
                line.PencilLine(center,end,Color.Red,n,PencilTips.All[1]);path.Line(center,end,Color.Red);
                for(int y=0;y<40;y++)for(int x=0;x<40;x++)if(path.Image.GetPixel(x,y).A!=0 && line.Image.GetPixel(x,y).ToArgb()!=Color.Red.ToArgb())throw new Exception("Trazo continuo");
            }
            foreach(Point edge in new[]{Point.Empty,new Point(39,0),new Point(0,39),new Point(39,39)})circle.PencilLine(edge,edge,Color.Red,n,PencilTips.All[1]);
            for(int mode=1;mode<=3;mode++)using(Document mirrored=new Document(40,40)){
                mirrored.MirrorVertical=(mode&1)!=0;mirrored.MirrorHorizontal=(mode&2)!=0;mirrored.MirrorAxisX2=31;mirrored.MirrorAxisY2=32;
                mirrored.PencilLine(center,center,Color.Blue,n,PencilTips.All[1]);
                using(Document seed=new Document(40,40)){
                    seed.PencilLine(center,center,Color.Blue,n,PencilTips.All[1]);
                    for(int y=0;y<40;y++)for(int x=0;x<40;x++){
                        bool expected=seed.Image.GetPixel(x,y).A!=0;
                        int rx=31-x,ry=32-y;
                        if(mirrored.MirrorVertical && rx>=0 && rx<40)expected|=seed.Image.GetPixel(rx,y).A!=0;
                        if(mirrored.MirrorHorizontal && ry>=0 && ry<40)expected|=seed.Image.GetPixel(x,ry).A!=0;
                        if(mode==3 && rx>=0 && rx<40 && ry>=0 && ry<40)expected|=seed.Image.GetPixel(rx,ry).A!=0;
                        if((mirrored.Image.GetPixel(x,y).A!=0)!=expected)throw new Exception("Reflejo exacto en eje desplazado");
                    }
                }
            }
        }
        using(Editor view=new Editor(true)){
            view.Show();Application.DoEvents();view.SetDocument(new Document(40,40));view.SetTool("Lápiz");view.canvas.PencilSize=7;view.SetColor(Color.Blue);view.canvas.Zoom=4;view.canvas.ViewOrigin=PointF.Empty;
            Action<int,bool,bool> wheel=delegate(int delta,bool alt,bool control){view.canvas.HandleCanvasWheel(new HandledMouseEventArgs(MouseButtons.None,0,10,10,delta),alt,control);};
            int history=view.undo.Count;bool dirtyBefore=view.dirty;
            wheel(-60,false,true);if(view.canvas.PencilTip.Id!="square")throw new Exception("Paso parcial");wheel(-60,false,true);
            if(view.canvas.PencilTip.Id!="circle" || view.canvas.Tool!="Lápiz" || view.canvas.PencilSize!=7 || view.canvas.Ink.ToArgb()!=Color.Blue.ToArgb() || view.canvas.Zoom!=4 || view.undo.Count!=history || view.dirty!=dirtyBefore || !view.tipButtons[1].Text.Contains("✓"))throw new Exception("Variantes conservan estado");
            wheel(120,false,true);if(view.canvas.PencilTip.Id!="square")throw new Exception("Rueda inversa");wheel(-240,false,true);if(view.canvas.PencilTip.Id!="square")throw new Exception("Varios pasos");
            view.tipButtons[1].PerformClick();if(view.canvas.PencilTip.Id!="circle")throw new Exception("Selector compartido");
            wheel(-120,true,true);if(view.canvas.Tool!="Borrador" || view.canvas.PencilTip.Id!="circle" || view.canvas.Zoom!=4)throw new Exception("Alt tiene prioridad");
            wheel(-120,false,true);if(view.canvas.Tool!="Borrador" || view.canvas.Zoom!=4)throw new Exception("Sin variantes no cambia ni hace zoom");wheel(120,true,false);if(view.canvas.Tool!="Lápiz" || view.canvas.PencilTip.Id!="circle")throw new Exception("Conservar punta entre herramientas");
            wheel(120,false,false);if(view.canvas.Zoom!=5)throw new Exception("Zoom normal intacto");view.canvas.Zoom=4;view.canvas.ViewOrigin=PointF.Empty;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Action<string,MouseEventArgs> mouse=delegate(string name,MouseEventArgs e){typeof(Control).GetMethod(name,flags).Invoke(view.canvas,new object[]{e});};
            mouse("OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,20,20,0));wheel(-120,false,true);view.SetPencilTip("square");
            if(view.canvas.PencilTip.Id!="circle")throw new Exception("No cambiar durante trazo");
            mouse("OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,60,60,0));mouse("OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,60,60,0));
            if(view.undo.Count!=history+1)throw new Exception("Un trazo una acción");
            using(Bitmap painted=(Bitmap)view.doc.Image.Clone()){
                view.Undo(false);if(view.doc.Image.GetPixel(5,5).A!=0)throw new Exception("Deshacer circular");view.Undo(true);CheckPencilPixels(painted,view.doc.Image,"Rehacer exacto");
                view.SetLayerLocked(true);int lockedHistory=view.undo.Count;mouse("OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,100,100,0));mouse("OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,100,100,0));CheckPencilPixels(painted,view.doc.Image,"Capa bloqueada");if(view.undo.Count!=lockedHistory)throw new Exception("Bloqueo sin historial");view.SetLayerLocked(false);
                view.doc.Scale=2;using(Bitmap png=view.doc.Export())for(int y=0;y<40;y++)for(int x=0;x<40;x++)for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)if(png.GetPixel(x*2+dx,y*2+dy).ToArgb()!=painted.GetPixel(x,y).ToArgb())throw new Exception("PNG nítido");
            }
            string path=Path.Combine(TestArtifacts.DirectoryPath,"pencil-preferences.xml");view.SaveSettings(path);view.SetPencilTip("square");view.LoadSettings(path);if(view.canvas.PencilTip.Id!="circle")throw new Exception("Preferencia persistente");
            view.SetTool("Rectángulo");view.canvas.ShapeClick(new Point(2,2));wheel(120,false,true);if(view.canvas.PendingPoints!=1)throw new Exception("Figura pendiente conservada");view.canvas.CancelShape();
            view.SetTool("Selección");using(Bitmap paste=new Bitmap(2,2))view.canvas.PasteSelection(paste);wheel(120,false,true);if(!view.canvas.HasPendingSelection)throw new Exception("Pegado pendiente conservado");view.canvas.CancelSelection();
            view.SetTool("Lápiz");view.CaptureUI("puntas-lapiz.png");
        }
    }
}
}
