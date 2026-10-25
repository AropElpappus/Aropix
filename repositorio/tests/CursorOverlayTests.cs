// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace PixelArt
{
    partial class PixelCanvas
    {
        internal void TestPointerGeometry()
        {
            foreach(int n in new int[]{1,2,3,4,7,10})foreach(PencilTip tip in PencilTips.All)
            {
                Tool="Lápiz";PencilSize=n;PencilTip=tip;
                foreach(Point anchor in new Point[]{Point.Empty,new Point(9,9),new Point(19,19)})
                    using(Document expected=new Document(20,20))
                    {
                        tip.Stamp(expected,anchor.X,anchor.Y,Color.White,n);
                        var footprint=PointerFootprint(anchor);
                        for(int y=0;y<20;y++)for(int x=0;x<20;x++)
                            if(footprint.Contains(new Point(x,y))!=(expected.Image.GetPixel(x,y).A!=0))throw new Exception("Huella distinta del motor: "+tip.Id+" "+n);
                    }
            }
            Tool="Borrador";EraserSize=4;if(PointerFootprint(new Point(9,9)).Count!=16)throw new Exception("Huella del borrador");
            foreach(float zoom in new float[]{.125f,.5f,1,4,8,32})
            {
                Zoom=zoom;origin=new PointF(-3.25f,5.5f);
                Point mouse=new Point(19,21);TrackPointer(mouse);
                Point expected=Pixel(mouse);
                if(Inside(expected))
                {
                    if(MousePixel!=expected || !PointerCell(expected).Contains(mouse))throw new Exception("Desfase entre hotspot y celda");
                }
                else if(MousePixel.HasValue)throw new Exception("Resaltado fuera del documento");
            }
            Zoom=8;origin=PointF.Empty;Tool="Lápiz";PencilSize=7;TrackPointer(new Point(73,73));
            int invalidations=0;bool whole=false;
            InvalidateEventHandler invalidated=delegate(object sender,InvalidateEventArgs e){invalidations++;whole|=e.InvalidRect==ClientRectangle;};
            Invalidated+=invalidated;
            TrackPointer(new Point(74,74));if(invalidations!=0)throw new Exception("Mover dentro de la misma celda repinta");
            TrackPointer(new Point(82,74));if(invalidations==0 || whole)throw new Exception("Debe invalidar solamente huellas anterior y nueva");
            Invalidated-=invalidated;ClearPointer();if(MousePixel.HasValue)throw new Exception("Residuos al salir");
        }
    }
    partial class Editor
    {
        public void TestCursorOverlay()
        {
            using(Document document=new Document(20,20))using(PixelCanvas surface=new PixelCanvas())
            {
                surface.Doc=document;surface.Size=new Size(400,400);surface.CreateControl();surface.TestPointerGeometry();
                if(PixelCanvas.ContrastInk(Color.Black)!=Color.White || PixelCanvas.ContrastInk(Color.White)!=Color.Black)throw new Exception("Contraste adaptativo");
                foreach(string tool in new string[]{"Lápiz","Borrador","Selección","Varita mágica","Cubeta","Cuentagotas","Círculo","Línea recta"})foreach(bool dark in new bool[]{false,true})
                {
                    Cursor cursor=ToolCursors.For(tool,dark);
                    if(cursor.HotSpot!=new Point(4,4))throw new Exception("Hotspot incorrecto: "+tool);
                    if(cursor!=ToolCursors.For(tool,dark))throw new Exception("Cursor nativo no reutilizado");
                    foreach(Color background in new Color[]{Color.Black,Color.White,Color.Gray,Color.Red,Color.Blue})
                    using(Bitmap image=new Bitmap(32,32))
                    {
                        using(Graphics g=Graphics.FromImage(image)) {
                            g.Clear(background);
                            // Exercise the Windows cursor handle, not its source bitmap.
                            cursor.Draw(g,new Rectangle(0,0,32,32));
                        }
                        int light=0,darkPixels=0,changed=0;
                        for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
                            Color pixel=image.GetPixel(x,y);
                            if(pixel.ToArgb()==background.ToArgb())continue;
                            changed++;
                            if(pixel.R>220 && pixel.G>220 && pixel.B>220)light++;
                            if(pixel.R<35 && pixel.G<35 && pixel.B<35)darkPixels++;
                        }
                        if(changed<15 || (background!=Color.White && light<5) || (background!=Color.Black && darkPixels<5))
                            throw new Exception("Cursor sin contraste nativo: "+tool+" / "+dark+" / "+background);
                        if(image.GetPixel(31,0).ToArgb()!=background.ToArgb())throw new Exception("Fondo del cursor no transparente");
                    }
                }
            }
            using(Editor view=new Editor(true))
            {
                view.Show();view.SetDocument(new Document(32,24));view.canvas.Zoom=16;view.canvas.ViewOrigin=new PointF(30,30);view.canvas.Grid=false;
                Color[] colors={Color.Black,Color.White,Color.Gray,Color.Red,Color.Blue,Color.Transparent};
                using(Graphics g=Graphics.FromImage(view.doc.Image))for(int i=0;i<colors.Length;i++)using(Brush brush=new SolidBrush(colors[i]))g.FillRectangle(brush,i*5,0,5,24);
                string before=view.doc.Serialize().OuterXml;int undo=view.undo.Count;
                view.SetTool("Lápiz");view.pencilSizes.SelectedIndex=6;view.SetPencilTip("circle");view.canvas.TrackPointer(new Point(110,174));
                view.CaptureUI("cursor-circular.png");
                view.SetTool("Borrador");view.eraserSizes.SelectedIndex=3;view.canvas.TrackPointer(new Point(270,174));view.CaptureUI("cursor-borrador.png");
                view.SetTool("Círculo");view.canvas.ShapeClick(new Point(12,12));view.canvas.OnTestHover(new Point(24,12));
                using(Bitmap shot=new Bitmap(view.canvas.Width,view.canvas.Height))
                {
                    view.canvas.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));int builds=view.canvas.ShapeOutlineBuilds;
                    view.canvas.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));if(view.canvas.ShapeOutlineBuilds!=builds)throw new Exception("Reconstrucción innecesaria del contorno");
                }
                view.CaptureUI("cursor-figura.png");view.canvas.CancelShape();
                if(view.doc.Serialize().OuterXml!=before || view.undo.Count!=undo || view.dirty)throw new Exception("La superposición modifica el proyecto");
                using(Bitmap exported=view.doc.Export())if(exported.GetPixel(1,1).ToArgb()!=Color.Black.ToArgb())throw new Exception("La superposición se exporta");
                view.canvas.Zoom=.5f;view.canvas.ViewOrigin=new PointF(30,30);view.canvas.TrackPointer(new Point(33,33));
                using(Bitmap shot=new Bitmap(view.canvas.Width,view.canvas.Height))view.canvas.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));
                view.CloseProject();if(view.canvas.MousePixel.HasValue || view.canvas.Doc!=null)throw new Exception("Indicador residual al cerrar");
                // Largest supported canvas: bounded allocation and reuse on later paints.
                view.SetDocument(new Document(2048,2048));view.SetTool("Rectángulo");view.canvas.ShapeClick(new Point(0,0));view.canvas.OnTestHover(new Point(2047,2047));
                Stopwatch watch=Stopwatch.StartNew();using(Bitmap shot=new Bitmap(view.canvas.Width,view.canvas.Height))view.canvas.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));
                File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath,"cursor-rendimiento.txt"),"Primera representación con figura de 2048 × 2048: "+watch.ElapsedMilliseconds+" ms. Incluye renderizado del lienzo y vista previa existente; no es una medición aislada del contorno.");
            }
        }
    }
    partial class PixelCanvas
    {
        internal void OnTestHover(Point pixel){hover=pixel;TrackPointer(new Point((int)(origin.X+pixel.X*Zoom),(int)(origin.Y+pixel.Y*Zoom)));}
    }
}
