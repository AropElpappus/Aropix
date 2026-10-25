// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PixelArt
{
    partial class PixelCanvas
    {
        GraphicsPath shapeOutline;
        string shapeOutlineKey;
        internal int ShapeOutlineBuilds;
        void ClearShapeOutline(){if(shapeOutline!=null)shapeOutline.Dispose();shapeOutline=null;shapeOutlineKey=null;}
        void PaintShapeOutline(Graphics g)
        {
            if(!IsShape || vertices.Count==0){ClearShapeOutline();return;}
            Point a=vertices[0],b=vertices.Count==2?vertices[1]:hover;
            string key=Tool+"|"+a+"|"+b+"|"+hover+"|"+vertices.Count+"|"+FilledShapes+"|"+ShapeThickness+"|"+EvenCircleCenter+"|"+Doc.Image.Size+"|"+Doc.MirrorVertical+"|"+Doc.MirrorHorizontal+"|"+Doc.AxisX2+"|"+Doc.AxisY2;
            if(shapeOutlineKey!=key)
            {
                ClearShapeOutline();
                // Same rasterizer as the real operation; cached independently of zoom and ink.
                using(Document mask=new Document(Doc.Image.Width,Doc.Image.Height))
                {
                    mask.MirrorVertical=Doc.MirrorVertical;mask.MirrorHorizontal=Doc.MirrorHorizontal;
                    mask.MirrorAxisX2=Doc.AxisX2;mask.MirrorAxisY2=Doc.AxisY2;
                    if(Tool=="Triángulo" && vertices.Count==1)mask.Line(a,hover,Color.White,FilledShapes?1:ShapeThickness);
                    else mask.Shape(Tool,a,b,hover,Color.White,FilledShapes,ShapeThickness,EvenCircleCenter);
                    shapeOutline=SelectionMask.From(mask.Image).Outline();
                }
                shapeOutlineKey=key;ShapeOutlineBuilds++;
            }
            using(GraphicsPath screen=(GraphicsPath)shapeOutline.Clone())
            using(Matrix transform=new Matrix(Zoom,0,0,Zoom,origin.X,origin.Y))
            {
                screen.Transform(transform);GraphicsState saved=g.Save();
                g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.Default;
                DrawContrastPath(g,screen);g.Restore(saved);
            }
        }
        // All geometry here is presentation-only and uses the drawing engine's pixel coordinates.
        internal RectangleF PointerCell(Point p)
        {
            return new RectangleF(origin.X+p.X*Zoom,origin.Y+p.Y*Zoom,Zoom,Zoom);
        }
        internal static Color ContrastInk(Color background)
        {
            return background.R*299+background.G*587+background.B*114 >= 140000 ? Color.Black : Color.White;
        }
        Color PointerBackground(Point p)
        {
            RectangleF cell=PointerCell(p);int tile=Math.Max(8,(int)(Zoom*2));
            int checkerX=(int)((cell.X+Zoom/2-origin.X)/tile),checkerY=(int)((cell.Y+Zoom/2-origin.Y)/tile);
            Color result=(checkerX+checkerY)%2==0?Color.FromArgb(78,84,92):Color.FromArgb(62,67,75);
            foreach(Layer layer in Doc.Layers)
            {
                if(!layer.Visible || layer.Opacity==0)continue;
                Color source=layer.Pixels.GetPixel(p.X,p.Y);
                double alpha=source.A/255.0*layer.Opacity/100.0;
                result=Color.FromArgb((int)Math.Round(source.R*alpha+result.R*(1-alpha)),(int)Math.Round(source.G*alpha+result.G*(1-alpha)),(int)Math.Round(source.B*alpha+result.B*(1-alpha)));
            }
            return result;
        }
        void UpdatePointerCursor()
        {
            bool dark=Doc!=null && MousePixel.HasValue && Inside(MousePixel.Value) && ContrastInk(PointerBackground(MousePixel.Value))==Color.Black;
            Cursor=panning?System.Windows.Forms.Cursors.SizeAll:ToolCursors.For(Tool,dark);
        }
        Rectangle PointerDamage(Point? point)
        {
            if(!point.HasValue)return Rectangle.Empty;
            int size=Tool=="Lápiz"?PencilSize:Tool=="Borrador"?EraserSize:1;
            int offset=(size-1)/2;
            RectangleF bounds=new RectangleF(origin.X+(point.Value.X-offset)*Zoom,origin.Y+(point.Value.Y-offset)*Zoom,size*Zoom,size*Zoom);
            Rectangle damage=Rectangle.Ceiling(bounds);damage.Inflate(8,8);return Rectangle.Intersect(ClientRectangle,damage);
        }
        void InvalidatePointer(Point? old,Point? next)
        {
            Rectangle before=PointerDamage(old),after=PointerDamage(next);
            if(!before.IsEmpty)Invalidate(before);if(!after.IsEmpty && after!=before)Invalidate(after);
        }
        void ReprojectPointer()
        {
            if(precisionActive){RefreshPrecisionPointer();return;}
            if(MousePixel.HasValue)TrackPointer(pointer);
        }
        internal HashSet<Point> PointerFootprint(Point anchor)
        {
            HashSet<Point> pixels=new HashSet<Point>();
            int size=Tool=="Lápiz"?PencilSize:EraserSize;
            foreach(Point offset in (Tool=="Lápiz"?PencilTip:PencilTips.All[0]).Offsets(size))
            {
                Point p=new Point(anchor.X+offset.X,anchor.Y+offset.Y);
                if(Inside(p))pixels.Add(p);
            }
            return pixels;
        }
        static void Edge(GraphicsPath path,float x,float y,float x2,float y2)
        {
            path.StartFigure();path.AddLine(x,y,x2,y2);
        }
        internal static GraphicsPath PixelOutline(HashSet<Point> pixels)
        {
            GraphicsPath path=new GraphicsPath();
            foreach(Point p in pixels)
            {
                if(!pixels.Contains(new Point(p.X-1,p.Y)))Edge(path,p.X,p.Y,p.X,p.Y+1);
                if(!pixels.Contains(new Point(p.X+1,p.Y)))Edge(path,p.X+1,p.Y,p.X+1,p.Y+1);
                if(!pixels.Contains(new Point(p.X,p.Y-1)))Edge(path,p.X,p.Y,p.X+1,p.Y);
                if(!pixels.Contains(new Point(p.X,p.Y+1)))Edge(path,p.X,p.Y+1,p.X+1,p.Y+1);
            }
            return path;
        }
        // Two nested contrasting strokes remain visible even across black/white boundaries.
        internal static void DrawContrastPath(Graphics g,GraphicsPath path)
        {
            if(path.PointCount==0)return;
            using(Pen outer=new Pen(Color.Black,3))using(Pen inner=new Pen(Color.White,1))
            { outer.LineJoin=inner.LineJoin=LineJoin.Miter;g.DrawPath(outer,path);g.DrawPath(inner,path); }
        }
        void PaintPointerOverlay(Graphics g)
        {
            if(Doc==null || !MousePixel.HasValue || !Inside(MousePixel.Value) || panning || Tool=="Mover vista")return;
            UpdatePointerCursor();Point p=MousePixel.Value;
            GraphicsState saved=g.Save();g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.Default;
            try
            {
                if(Zoom>=2 && (Tool=="Lápiz" || Tool=="Borrador"))
                    using(GraphicsPath path=PixelOutline(PointerFootprint(p)))
                    using(Matrix transform=new Matrix(Zoom,0,0,Zoom,origin.X,origin.Y))
                    { path.Transform(transform);DrawContrastPath(g,path); }
                RectangleF cell=PointerCell(p);
                if(Zoom>=4)
                {
                    // Both strokes stay inside the mathematical cell, never filling its centre.
                    int left=(int)Math.Ceiling(cell.Left),top=(int)Math.Ceiling(cell.Top);
                    int right=(int)Math.Ceiling(cell.Right)-1,bottom=(int)Math.Ceiling(cell.Bottom)-1;
                    Color ink=ContrastInk(PointerBackground(p));
                    using(Pen primary=new Pen(ink))using(Pen edge=new Pen(ink==Color.White?Color.Black:Color.White))
                    { g.DrawRectangle(edge,left,top,right-left,bottom-top);g.DrawRectangle(primary,left+1,top+1,right-left-2,bottom-top-2); }
                }
                else
                {
                    float x=(float)Math.Floor(cell.X+Zoom/2),y=(float)Math.Floor(cell.Y+Zoom/2);
                    using(GraphicsPath cross=new GraphicsPath()){Edge(cross,x-2,y,x+2,y);Edge(cross,x,y-2,x,y+2);DrawContrastPath(g,cross);}
                }
            }
            finally { g.Restore(saved); }
        }
    }
}
