// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
namespace PixelArt {
// Row copies avoid millions of GDI+ GetPixel/SetPixel calls on large images.
class PixelBuffer : IDisposable {
    readonly Bitmap image;readonly BitmapData data;readonly bool write;
    public readonly int[] Pixels;
    public PixelBuffer(Bitmap bitmap,bool writable){image=bitmap;write=writable;data=image.LockBits(new Rectangle(Point.Empty,image.Size),writable?ImageLockMode.ReadWrite:ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);Pixels=new int[image.Width*image.Height];for(int y=0;y<image.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),Pixels,y*image.Width,image.Width);}
    public void Dispose(){try{if(write)for(int y=0;y<image.Height;y++)Marshal.Copy(Pixels,y*image.Width,IntPtr.Add(data.Scan0,y*data.Stride),image.Width);}finally{image.UnlockBits(data);}}
}
partial class Document {
    int[] spanLeft,spanRight;
    void FastFill(Point start,Color color){
        if(!CanFill(start,color))return;int w=Image.Width,h=Image.Height;
        using(PixelBuffer buffer=new PixelBuffer(Image,true)){
            int[] pixels=buffer.Pixels;int target=pixels[start.Y*w+start.X],replacement=color.ToArgb();bool transparent=(target & unchecked((int)0xff000000))==0;
            int[] queue=new int[w*h];int head=0,tail=0;int first=start.Y*w+start.X;queue[tail++]=first;pixels[first]=replacement;
            while(head<tail){int index=queue[head++],x=index%w,y=index/w;
                for(int direction=0;direction<4;direction++){int next=direction==0?index-1:direction==1?index+1:direction==2?index-w:index+w;
                    if((direction==0 && x==0)||(direction==1 && x==w-1)||(direction==2 && y==0)||(direction==3 && y==h-1))continue;
                    int value=pixels[next];if(transparent?(value & unchecked((int)0xff000000))==0:value==target){pixels[next]=replacement;queue[tail++]=next;}
                }
            }
        }
    }
    void FillShapeSpans(string tool,Point a,Point b,Point c,Color color,bool filled,int thickness,bool evenCenter){
        int left=Math.Min(a.X,b.X),top=Math.Min(a.Y,b.Y),right=Math.Max(a.X,b.X),bottom=Math.Max(a.Y,b.Y);
        if(tool=="Triángulo"){left=Math.Min(left,c.X);top=Math.Min(top,c.Y);right=Math.Max(right,c.X);bottom=Math.Max(bottom,c.Y);}
        if(tool=="Círculo"){int dx=b.X-a.X,dy=b.Y-a.Y,r=(int)Math.Round(Math.Sqrt(dx*dx+dy*dy));left=a.X-r;right=a.X+r+(evenCenter?1:0);top=a.Y-r;bottom=a.Y+r+(evenCenter?1:0);}
        int height=bottom-top+1,imageWidth=Image.Width,imageHeight=Image.Height,argb=color.ToArgb();
        using(Document outline=new Document(1,1)){
            outline.spanLeft=new int[height];outline.spanRight=new int[height];for(int y=0;y<height;y++){outline.spanLeft[y]=int.MaxValue;outline.spanRight[y]=int.MinValue;}
            outline.Shape(tool,new Point(a.X-left,a.Y-top),new Point(b.X-left,b.Y-top),new Point(c.X-left,c.Y-top),color,false,1,evenCenter);
            using(PixelBuffer buffer=new PixelBuffer(Image,true))for(int y=Math.Max(0,-top);y<Math.Min(height,imageHeight-top);y++){
                int lo=outline.spanLeft[y],hi=outline.spanRight[y];if(lo>hi)continue;
                int innerLo=lo,innerHi=hi;
                if(!filled){if(y<thickness || y+thickness>=height)innerLo=int.MaxValue;else{for(int yy=y-thickness;yy<=y+thickness;yy++){innerLo=Math.Max(innerLo,outline.spanLeft[yy]);innerHi=Math.Min(innerHi,outline.spanRight[yy]);}if(innerLo!=int.MaxValue)innerLo+=thickness;if(innerHi!=int.MinValue)innerHi-=thickness;}}
                for(int x=Math.Max(lo,-left);x<=Math.Min(hi,imageWidth-left-1);x++)if(filled || x<innerLo || x>innerHi)SymmetryBufferPixel(buffer.Pixels,imageWidth,imageHeight,x+left,y+top,argb);
            }
        }
    }
}
partial class PixelCanvas {
    Point keyboardPixel;bool keyboardVisible,rightHeld,precisionActive;
    Point? physicalPointer;
    void RefreshPrecisionPointer(){if(!precisionActive)return;hover=keyboardPixel;MousePixel=keyboardPixel;pointer=new Point((int)(origin.X+keyboardPixel.X*Zoom),(int)(origin.Y+keyboardPixel.Y*Zoom));if(PointerChanged!=null)PointerChanged();}
    void SetKeyboardPixel(Point p){keyboardPixel=new Point(Math.Max(0,Math.Min(Doc.Image.Width-1,p.X)),Math.Max(0,Math.Min(Doc.Image.Height-1,p.Y)));hover=keyboardPixel;MousePixel=keyboardPixel;if(PointerChanged!=null)PointerChanged();}
    public bool PrecisionKey(Keys keys){
        if(Doc==null)return false;Keys key=keys & Keys.KeyCode;
        bool arrow=key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down;
        if((keys & (Keys.Control|Keys.Alt))!=0 || (!arrow && key!=Keys.Enter))return false;
        if(!precisionActive){SetKeyboardPixel(PendingPoints>0?hover:MousePixel??new Point(Doc.Image.Width/2,Doc.Image.Height/2));precisionActive=true;keyboardVisible=true;}
        if(arrow){int step=(keys & Keys.Shift)!=0?10:1,dx=key==Keys.Left?-step:key==Keys.Right?step:0,dy=key==Keys.Up?-step:key==Keys.Down?step:0;
            if(Tool=="Mover vista"){origin.X-=dx*Zoom;origin.Y-=dy*Zoom;Invalidate();return true;}
            if(IsSelectionTool && !Selection.IsEmpty && !selecting){if(PrepareTransform()){MoveSelectionTo(new Point(Selection.X+dx,Selection.Y+dy));SetKeyboardPixel(Selection.Location);}}
            else{SetKeyboardPixel(new Point(keyboardPixel.X+dx,keyboardPixel.Y+dy));if(selecting)UpdateSelection(keyboardPixel);if(rightHeld && (Tool=="Lápiz" || Tool=="Borrador"))DrawTo(keyboardPixel);}
        }else{
            if(IsShape){ShapeClick(keyboardPixel);if(PendingPoints==0){precisionActive=false;keyboardVisible=false;}}
            else if(IsSelectionTool){if(selecting)EndSelectionGesture();else if(HasPendingSelection)CommitSelection();else if(Tool=="Varita mágica")WandSelect(keyboardPixel);else if(Selection.IsEmpty)BeginSelection(keyboardPixel);}
            else if(Tool=="Cubeta" || Tool=="Cuentagotas")UsePointTool(keyboardPixel);
            else if((Tool=="Lápiz" || Tool=="Borrador") && Doc.Editable && !rightHeld){if(StrokeStarted!=null)StrokeStarted();last=keyboardPixel;DrawTo(keyboardPixel);if(StrokeFinished!=null)StrokeFinished();}
        }
        float sx=origin.X+keyboardPixel.X*Zoom,sy=origin.Y+keyboardPixel.Y*Zoom;
        if(sx<0)origin.X-=sx;else if(sx+Zoom>Width)origin.X-=sx+Zoom-Width;
        if(sy<0)origin.Y-=sy;else if(sy+Zoom>Height)origin.Y-=sy+Zoom-Height;
        pointer=new Point((int)(origin.X+keyboardPixel.X*Zoom),(int)(origin.Y+keyboardPixel.Y*Zoom));Invalidate();if(ViewChanged!=null)ViewChanged();return true;
    }
    void StartRightPrecision(Point p){SetKeyboardPixel(p);precisionActive=true;keyboardVisible=true;rightHeld=true;Capture=true;
        if(!Doc.Editable)return;
        if(IsShape && PendingPoints==0)ShapeClick(keyboardPixel);
        if(Tool=="Lápiz" || Tool=="Borrador"){if(StrokeStarted!=null)StrokeStarted();drawing=true;last=keyboardPixel;DrawTo(keyboardPixel);}
    }
    public void TestKeyboardDrawing(){
        SelectTool("Círculo");ShapeClick(new Point(10,10));PrecisionKey(Keys.Right);PrecisionKey(Keys.Right);PrecisionKey(Keys.Enter);if(PendingPoints!=0 || Doc.Image.GetPixel(12,10).A==0 || Doc.Image.GetPixel(8,10).A==0)throw new Exception("Radio de círculo con flechas y Enter");
        SelectTool("Lápiz");StartRightPrecision(new Point(2,2));PrecisionKey(Keys.Right|Keys.Shift);Finish();for(int x=2;x<=12;x++)if(Doc.Image.GetPixel(x,2).A==0)throw new Exception("Trazo continuo con botón derecho y flechas");
        SelectTool("Borrador");StartRightPrecision(new Point(2,2));PrecisionKey(Keys.Right);Finish();if(Doc.Image.GetPixel(2,2).A!=0 || Doc.Image.GetPixel(3,2).A!=0)throw new Exception("Borrar con flechas");
        SelectTool("Rectángulo");StartRightPrecision(new Point(20,20));PrecisionKey(Keys.Right);PrecisionKey(Keys.Down);Finish();PrecisionKey(Keys.Enter);if(Doc.Image.GetPixel(21,21).A==0)throw new Exception("Figura iniciada con botón derecho");
        SelectTool("Selección");SelectBounds(new Rectangle(20,20,2,2));PrecisionKey(Keys.Right);PrecisionKey(Keys.Down);PrecisionKey(Keys.Enter);if(Doc.Image.GetPixel(20,20).A!=0 || Doc.Image.GetPixel(22,22).A==0)throw new Exception("Mover selección con flechas");
        Doc.Layers[Doc.ActiveLayer].Locked=true;SelectTool("Lápiz");StartRightPrecision(Point.Empty);PrecisionKey(Keys.Right);Finish();PrecisionKey(Keys.Enter);if(Doc.Image.GetPixel(0,0).A!=0 || Doc.Image.GetPixel(1,0).A!=0)throw new Exception("Flechas respetan bloqueo");Doc.Layers[Doc.ActiveLayer].Locked=false;
    }
    public void TestPrecisionViewAnchor(){
        SelectTool("Círculo");ShapeClick(new Point(10,10));PrecisionKey(Keys.Right);PrecisionKey(Keys.Right);
        OnMouseDown(new MouseEventArgs(MouseButtons.Middle,1,100,100,0));OnMouseMove(new MouseEventArgs(MouseButtons.Middle,0,130,140,0));OnMouseUp(new MouseEventArgs(MouseButtons.Middle,1,130,140,0));
        OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,130,140,120));OnMouseMove(new MouseEventArgs(MouseButtons.None,0,130,140,0));
        if(keyboardPixel!=new Point(12,10) || hover!=keyboardPixel || vertices[0]!=new Point(10,10))throw new Exception("Ancla temporal conserva coordenadas al desplazar y ampliar");
        PrecisionKey(Keys.Right);OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,130,140,0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,130,140,0));
        if(Doc.Image.GetPixel(13,10).A==0 || Doc.Image.GetPixel(7,10).A==0 || PendingPoints!=0 || precisionActive)throw new Exception("Confirmar figura en punto de teclado y liberar referencia");
        SelectTool("Lápiz");StartRightPrecision(new Point(2,2));PrecisionKey(Keys.Right);OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,150,150,-120));OnMouseLeave(EventArgs.Empty);PrecisionKey(Keys.Right);Finish();
        if(Doc.Image.GetPixel(4,2).A==0 || keyboardPixel!=new Point(4,2))throw new Exception("Trazo conserva referencia al usar rueda y salir del lienzo");
        SelectTool("Rectángulo");ShapeClick(new Point(20,20));PrecisionKey(Keys.Down);CancelShape();if(precisionActive)throw new Exception("Cancelar libera referencia");
        SelectTool("Selección");SetKeyboardPixel(new Point(20,20));precisionActive=true;keyboardVisible=true;PrecisionKey(Keys.Enter);PrecisionKey(Keys.Right);
        OnMouseDown(new MouseEventArgs(MouseButtons.Middle,1,100,100,0));OnMouseMove(new MouseEventArgs(MouseButtons.Middle,0,110,115,0));OnMouseUp(new MouseEventArgs(MouseButtons.Middle,1,110,115,0));PrecisionKey(Keys.Down);PrecisionKey(Keys.Enter);
        if(Selection!=new Rectangle(20,20,2,2))throw new Exception("Seleccionar con flechas conserva inicio durante desplazamiento");CancelSelection();SelectTool("Lápiz");
    }
}
partial class Editor {
    public void TestStraightLine(){
        for(int thickness=1;thickness<=10;thickness++)using(Document line=new Document(40,40)){
            line.Shape("Línea recta",new Point(8,20),new Point(30,20),Point.Empty,Color.Red,true,thickness);
            int count=0;for(int y=0;y<40;y++)if(line.Image.GetPixel(19,y).A>0)count++;
            if(count!=thickness || line.Image.GetPixel(8,20).A==0 || line.Image.GetPixel(30,20).A==0)throw new Exception("Grosor de línea 1–10 y extremos");
        }
        SetDocument(new Document(32,32));SetColor(Color.Red);SetTool("Línea recta");canvas.ShapeThickness=3;canvas.FilledShapes=true;
        canvas.ShapeClick(new Point(5,5));canvas.PrecisionKey(Keys.Right|Keys.Shift);canvas.PrecisionKey(Keys.Down|Keys.Shift);
        using(Bitmap preview=new Bitmap(canvas.Width,canvas.Height))canvas.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));
        if(undo.Count!=0 || doc.Image.GetPixel(5,5).A!=0)throw new Exception("Vista previa de línea sin modificar dibujo");
        canvas.ChangeZoom(1);canvas.TrackPointer(new Point(100,100));canvas.PrecisionKey(Keys.Enter);
        if(undo.Count!=1 || doc.Image.GetPixel(5,5).A==0 || doc.Image.GetPixel(15,15).A==0 || canvas.PendingPoints!=0)throw new Exception("Línea con flechas y Enter conserva ancla");
        Undo(false);if(doc.Image.GetPixel(5,5).A!=0)throw new Exception("Deshacer línea completa");Undo(true);if(doc.Image.GetPixel(15,15).A==0)throw new Exception("Rehacer línea");
        canvas.ShapeClick(Point.Empty);canvas.CancelShape();if(undo.Count!=1)throw new Exception("Cancelar línea sin acción");
        SetLayerLocked(true);canvas.ShapeClick(Point.Empty);canvas.ShapeClick(new Point(20,0));if(doc.Image.GetPixel(0,0).A!=0 || canvas.PendingPoints!=0)throw new Exception("Bloqueo impide línea");
        foreach(Control control in shapesPanel.Content.Controls)if(control is CheckBox)((CheckBox)control).Checked=true;
        SetTool("Línea recta");foreach(Control control in shapesPanel.Content.Controls)if(control is NumericUpDown && !control.Enabled)throw new Exception("Grosor disponible aunque figura anterior tenga relleno");
        using(Bitmap shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(TestArtifacts.DirectoryPath,"vista-linea.png"),ImageFormat.Png);}
        foreach(Control control in shapesPanel.Content.Controls)if(control is CheckBox)((CheckBox)control).Checked=false;
        canvas.ShapeThickness=1;canvas.FilledShapes=false;SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    public void TestPrecisionAndLargeCanvas(){
        SetDocument(new Document(32,32));SetColor(Color.Red);canvas.TestPrecisionViewAnchor();
        SetDocument(new Document(32,32));SetColor(Color.Red);canvas.TestKeyboardDrawing();Undo(false);if(doc.Image.GetPixel(20,20).A==0 || doc.Image.GetPixel(22,22).A!=0)throw new Exception("Deshacer movimiento con flechas");
        SetDocument(new Document(2048,2048));canvas.Fit();if(canvas.Zoom>=1 || doc.Image.Width*canvas.Zoom>canvas.Width)throw new Exception("Ajustar paisaje completo");Stopwatch timer=Stopwatch.StartNew();doc.FloodFill(Point.Empty,Color.Blue);long fill=timer.ElapsedMilliseconds;
        if(doc.Image.GetPixel(2047,2047).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Cubeta grande");
        timer.Restart();doc.Shape("Círculo",new Point(1024,1024),new Point(2047,1024),Point.Empty,Color.Red,true);long shape=timer.ElapsedMilliseconds;
        if(doc.Image.GetPixel(1024,1024).ToArgb()!=Color.Red.ToArgb() || doc.Image.GetPixel(0,0).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Figura grande");
        using(Document loaded=Document.Deserialize(doc.Serialize().DocumentElement))if(loaded.Image.Width!=2048)throw new Exception("Reabrir lienzo grande");
        using(NewProject dialog=new NewProject())if(dialog.W.Maximum!=2048 || dialog.H.Maximum!=2048)throw new Exception("Límite de creación");
        File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath,"rendimiento.txt"),"Lienzo 2048 × 2048, medición local sin historial:\r\nCubeta: "+fill+" ms\r\nCírculo relleno: "+shape+" ms\r\n");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}


