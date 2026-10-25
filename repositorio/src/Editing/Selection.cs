// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PixelArt {
partial class PixelCanvas {
    public void TestBlockedStroke(){Tool="Lápiz";DrawTo(new Point(1,1));Tool="Borrador";DrawTo(new Point(1,1));}
    public Rectangle Selection {get;private set;}
    Bitmap selectionImage;
    Rectangle sourceSelection;
    bool cuttingSource,selecting,movingSelection;
    bool transformed;
    bool PrepareTransform(){
        if(Selection.IsEmpty || !Doc.Editable)return false;
        if(selectionImage==null){selectionImage=CopySelection();sourceSelection=Selection;sourceMask=CopySelectionMask();cuttingSource=true;}
        return selectionImage!=null;
    }
    public void RotateSelection(RotateFlipType operation){
        if(!PrepareTransform())return;selectionImage.RotateFlip(operation);if(selectionMask!=null)SetSelectionMask(selectionMask.Transform(operation,0,0));transformed=true;
        Selection=new Rectangle(Selection.Location,selectionImage.Size);MoveSelectionTo(Selection.Location);
    }
    public void ResizeSelection(int width,int height){
        if(width<1 || height<1 || width>2048 || height>2048)throw new ArgumentOutOfRangeException("size");
        if(!PrepareTransform())return;
        Bitmap resized=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(resized)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(selectionImage,new Rectangle(0,0,width,height),0,0,selectionImage.Width,selectionImage.Height,GraphicsUnit.Pixel);}
        if(selectionMask!=null)SetSelectionMask(selectionMask.Transform(null,width,height));selectionImage.Dispose();selectionImage=resized;transformed=true;Selection=new Rectangle(Selection.Location,resized.Size);MoveSelectionTo(Selection.Location);
    }
    Point selectionAnchor,grabOffset;
    public bool HasPendingSelection {get{return selectionImage!=null;}}
    public bool HasSelectionGesture {get{return selecting || movingSelection;}}
    public void CancelSelection(){
        selecting=false;movingSelection=false;cuttingSource=false;transformed=false;
        if(selectionImage!=null){selectionImage.Dispose();selectionImage=null;}
        SetSelectionMask(null);sourceMask=null;Selection=Rectangle.Empty;Capture=false;Invalidate();
    }
    public Bitmap CopySelection(){
        if(selectionImage!=null)return (Bitmap)selectionImage.Clone();
        Rectangle bounds=Rectangle.Intersect(Selection,new Rectangle(0,0,Doc.Image.Width,Doc.Image.Height));
        if(bounds.IsEmpty)return null;Bitmap copy=Doc.Image.Clone(bounds,System.Drawing.Imaging.PixelFormat.Format32bppArgb);MaskPixels(copy,new Rectangle(Point.Empty,copy.Size),selectionMask,false);return copy;
    }
    public void PasteSelection(Bitmap image,SelectionMask mask=null){
        if(!Doc.Editable)return;
        CommitSelection();CancelShape();Tool="Selección";
        Point start=Selection.IsEmpty?Point.Empty:Selection.Location;
        CancelSelection();SetSelectionMask(mask==null?null:mask.Clone());selectionImage=(Bitmap)image.Clone();Selection=new Rectangle(start,image.Size);MoveSelectionTo(start);Invalidate();
    }
    public void MoveSelectionTo(Point location){if(!Doc.Editable)return;
        location.X=Math.Max(0,Math.Min(location.X,Math.Max(0,Doc.Image.Width-Selection.Width)));
        location.Y=Math.Max(0,Math.Min(location.Y,Math.Max(0,Doc.Image.Height-Selection.Height)));
        Selection=new Rectangle(location,Selection.Size);Invalidate();
    }
    public void BeginSelection(Point p){if(!Doc.Editable)return;
        if(SelectedPixel(p)){
            if(selectionImage==null){selectionImage=CopySelection();sourceSelection=Selection;sourceMask=CopySelectionMask();cuttingSource=true;}
            movingSelection=true;grabOffset=new Point(p.X-Selection.X,p.Y-Selection.Y);
        }else{
            CommitSelection();CancelSelection();selecting=true;selectionAnchor=p;Selection=new Rectangle(p,new Size(1,1));
        }
        Capture=true;Invalidate();
    }
    public void UpdateSelection(Point p){
        if(selecting){p.X=Math.Max(0,Math.Min(Doc.Image.Width-1,p.X));p.Y=Math.Max(0,Math.Min(Doc.Image.Height-1,p.Y));Selection=Rectangle.FromLTRB(Math.Min(p.X,selectionAnchor.X),Math.Min(p.Y,selectionAnchor.Y),Math.Max(p.X,selectionAnchor.X)+1,Math.Max(p.Y,selectionAnchor.Y)+1);Invalidate();}
        else if(movingSelection)MoveSelectionTo(new Point(p.X-grabOffset.X,p.Y-grabOffset.Y));
    }
    public void EndSelectionGesture(){bool commit=movingSelection;selecting=false;movingSelection=false;Capture=false;if(commit)CommitSelection();Invalidate();}
    static void ClearRegion(Bitmap image,Rectangle rect){using(Graphics g=Graphics.FromImage(image)){g.CompositingMode=CompositingMode.SourceCopy;using(Brush clear=new SolidBrush(Color.Transparent))g.FillRectangle(clear,rect);}}
    void ComposeSelection(Bitmap image){
        if(cuttingSource)MaskPixels(image,sourceSelection,sourceMask,true);
        using(Graphics g=Graphics.FromImage(image)){g.CompositingMode=CompositingMode.SourceOver;g.DrawImageUnscaled(selectionImage,Selection.Location);}
    }
    public void CommitSelection(){if(Doc==null || !Doc.Editable){CancelSelection();return;}
        if(selectionImage==null)return;
        bool changed=transformed || !cuttingSource || Selection.Location!=sourceSelection.Location;
        if(changed){if(StrokeStarted!=null)StrokeStarted();ComposeSelection(Doc.Image);}
        selectionImage.Dispose();selectionImage=null;cuttingSource=false;movingSelection=false;transformed=false;
        Rectangle clipped=Rectangle.Intersect(Selection,new Rectangle(0,0,Doc.Image.Width,Doc.Image.Height));if(selectionMask!=null && clipped!=Selection)SetSelectionMask(selectionMask.Crop(clipped.X-Selection.X,clipped.Y-Selection.Y,clipped.Width,clipped.Height));Selection=clipped;sourceMask=null;
        if(changed && StrokeFinished!=null)StrokeFinished();Invalidate();
    }
    public void DeleteSelection(){
        if(!Doc.Editable)return;
        if(selectionImage!=null){CancelSelection();return;}
        if(Selection.IsEmpty)return;
        if(StrokeStarted!=null)StrokeStarted();MaskPixels(Doc.Image,Selection,selectionMask,true);if(StrokeFinished!=null)StrokeFinished();Invalidate();
    }
    void PaintDocument(Graphics g,RectangleF destination){
        Bitmap preview=null;
        try{
            if(selectionImage!=null || (IsShape && vertices.Count>0)){
                preview=(Bitmap)Doc.Image.Clone();if(selectionImage!=null)ComposeSelection(preview);
                if(IsShape && vertices.Count>0)using(Document shape=new Document(1,1)){
                    shape.Image.Dispose();shape.Image=preview;preview=null;shape.MirrorVertical=Doc.MirrorVertical;shape.MirrorHorizontal=Doc.MirrorHorizontal;shape.MirrorAxisX2=Doc.MirrorAxisX2;shape.MirrorAxisY2=Doc.MirrorAxisY2;
                    if(Tool=="Triángulo" && vertices.Count==1)shape.Line(vertices[0],hover,Ink,FilledShapes?1:ShapeThickness);
                    else shape.Shape(Tool,vertices[0],vertices.Count==2?vertices[1]:hover,hover,Ink,FilledShapes,ShapeThickness,EvenCircleCenter);
                    preview=shape.Image;shape.Image=new Bitmap(1,1);
                }
            }
            for(int i=0;i<Doc.Layers.Count;i++){PaintShadowPreview(g,destination,Doc.Layers[i]);Doc.Layers[i].Draw(g,destination,i==Doc.ActiveLayer?preview:null);}
        }finally{if(preview!=null)preview.Dispose();}
    }
    void PaintSelection(Graphics g){
        if(Selection.IsEmpty || !IsSelectionTool)return;if(selectionMask!=null){PaintMaskOutline(g);return;}
        RectangleF r=new RectangleF(origin.X+Selection.X*Zoom,origin.Y+Selection.Y*Zoom,Selection.Width*Zoom,Selection.Height*Zoom);
        g.PixelOffsetMode=PixelOffsetMode.Default;
        using(Pen black=new Pen(Color.Black,3))using(Pen white=new Pen(Color.White,1)){white.DashPattern=new float[]{4,4};g.DrawRectangle(black,r.X,r.Y,r.Width,r.Height);g.DrawRectangle(white,r.X,r.Y,r.Width,r.Height);}
    }
    protected override void Dispose(bool disposing){if(disposing){ClearShapeOutline();SetSelectionMask(null);ClearShadowPreview();if(selectionImage!=null){selectionImage.Dispose();selectionImage=null;}if(gridLayer!=null){gridLayer.Dispose();gridLayer=null;}}base.Dispose(disposing);}
}
}






