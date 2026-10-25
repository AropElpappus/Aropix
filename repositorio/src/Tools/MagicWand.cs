// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
namespace PixelArt {
enum SelectionCombine { Replace, Add, Subtract }
// A local binary mask, independent of pixel alpha (transparent pixels may be selected).
class SelectionMask {
    public readonly int Width,Height;public readonly bool[] Bits;
    public SelectionMask(int w,int h){Width=w;Height=h;Bits=new bool[w*h];}
    public bool At(int x,int y){return x>=0 && y>=0 && x<Width && y<Height && Bits[y*Width+x];}
    public SelectionMask Clone(){SelectionMask copy=new SelectionMask(Width,Height);Array.Copy(Bits,copy.Bits,Bits.Length);return copy;}
    public SelectionMask Crop(int x,int y,int w,int h){SelectionMask result=new SelectionMask(w,h);for(int j=0;j<h;j++)for(int i=0;i<w;i++)result.Bits[j*w+i]=At(i+x,j+y);return result;}
    public SelectionMask Transform(RotateFlipType? rotation,int width,int height){
        using(Bitmap image=new Bitmap(Width,Height,PixelFormat.Format32bppArgb)){
            using(PixelBuffer buffer=new PixelBuffer(image,true))for(int i=0;i<Bits.Length;i++)if(Bits[i])buffer.Pixels[i]=-1;
            if(rotation.HasValue){image.RotateFlip(rotation.Value);return From(image);}
            using(Bitmap scaled=new Bitmap(width,height,PixelFormat.Format32bppArgb)){using(Graphics g=Graphics.FromImage(scaled)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(image,new Rectangle(0,0,width,height),0,0,Width,Height,GraphicsUnit.Pixel);}return From(scaled);}
        }
    }
    internal static SelectionMask From(Bitmap image){SelectionMask mask=new SelectionMask(image.Width,image.Height);using(PixelBuffer b=new PixelBuffer(image,false))for(int i=0;i<b.Pixels.Length;i++)mask.Bits[i]=(b.Pixels[i] & unchecked((int)0xff000000))!=0;return mask;}
    internal GraphicsPath Outline(){
        GraphicsPath path=new GraphicsPath();
        for(int y=0;y<=Height;y++){int start=-1;for(int x=0;x<=Width;x++){bool edge=x<Width && At(x,y-1)!=At(x,y);if(edge && start<0)start=x;if(!edge && start>=0){path.StartFigure();path.AddLine(start,y,x,y);start=-1;}}}
        for(int x=0;x<=Width;x++){int start=-1;for(int y=0;y<=Height;y++){bool edge=y<Height && At(x-1,y)!=At(x,y);if(edge && start<0)start=y;if(!edge && start>=0){path.StartFigure();path.AddLine(x,start,x,y);start=-1;}}}
        return path;
    }
    public static bool SameColor(int a,int b){return a==b || ((a | b) & unchecked((int)0xff000000))==0;}
    public static SelectionMask Region(Bitmap image,Point seed,out Rectangle bounds){
        bounds=Rectangle.Empty;if(seed.X<0 || seed.Y<0 || seed.X>=image.Width || seed.Y>=image.Height)return null;
        int w=image.Width,h=image.Height;bool[] found=new bool[w*h];int[] queue=new int[w*h];int head=0,tail=1;queue[0]=seed.Y*w+seed.X;found[queue[0]]=true;int minX=seed.X,maxX=seed.X,minY=seed.Y,maxY=seed.Y;
        using(PixelBuffer buffer=new PixelBuffer(image,false)){int color=buffer.Pixels[queue[0]];
            while(head<tail){int index=queue[head++],x=index%w,y=index/w;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                if(x>0)Visit(index-1,color,buffer.Pixels,found,queue,ref tail);
                if(x+1<w)Visit(index+1,color,buffer.Pixels,found,queue,ref tail);
                if(y>0)Visit(index-w,color,buffer.Pixels,found,queue,ref tail);
                if(y+1<h)Visit(index+w,color,buffer.Pixels,found,queue,ref tail);
            }
        }
        bounds=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);SelectionMask mask=new SelectionMask(bounds.Width,bounds.Height);for(int y=0;y<mask.Height;y++)Array.Copy(found,(y+minY)*w+minX,mask.Bits,y*mask.Width,mask.Width);return mask;
    }
    static void Visit(int index,int color,int[] pixels,bool[] found,int[] queue,ref int tail){if(!found[index] && SameColor(pixels[index],color)){found[index]=true;queue[tail++]=index;}}
}
partial class PixelCanvas {
    SelectionMask selectionMask,sourceMask;
    GraphicsPath maskOutline;
    public bool IsSelectionTool {get{return Tool=="Selección" || Tool=="Varita mágica";}}
    public SelectionMask CopySelectionMask(){return selectionMask==null?null:selectionMask.Clone();}
    bool SelectedPixel(Point p){return Selection.Contains(p) && (selectionMask==null || selectionMask.At(p.X-Selection.X,p.Y-Selection.Y));}
    void SetSelectionMask(SelectionMask mask){selectionMask=mask;if(maskOutline!=null)maskOutline.Dispose();maskOutline=null;}
    public void WandClick(Point p,Keys modifiers){
        bool add=(modifiers & Keys.Control)!=0,subtract=(modifiers & Keys.Alt)!=0;
        if(add && subtract)return;
        if(add || subtract){WandSelect(p,add?SelectionCombine.Add:SelectionCombine.Subtract);return;}
        if(SelectedPixel(p) && Doc.Editable)BeginSelection(p);else WandSelect(p);
    }
    public void WandSelect(Point p,SelectionCombine mode=SelectionCombine.Replace){
        if(Doc==null || !Inside(p) || HasPendingSelection)return;
        if(mode==SelectionCombine.Subtract && Selection.IsEmpty)return;
        Rectangle bounds;SelectionMask mask=SelectionMask.Region(Doc.Image,p,out bounds);
        if(mode!=SelectionCombine.Replace && !Selection.IsEmpty){
            Rectangle area=mode==SelectionCombine.Add?Rectangle.Union(Selection,bounds):Selection;
            SelectionMask combined=new SelectionMask(area.Width,area.Height);int left=area.Width,top=area.Height,right=-1,bottom=-1;
            for(int y=0;y<area.Height;y++)for(int x=0;x<area.Width;x++){
                Point absolute=new Point(area.X+x,area.Y+y);bool old=SelectedPixel(absolute),incoming=mask.At(absolute.X-bounds.X,absolute.Y-bounds.Y);
                bool selected=mode==SelectionCombine.Add?old || incoming:old && !incoming;
                if(!selected)continue;combined.Bits[y*area.Width+x]=true;left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
            }
            if(right<0){mask=null;bounds=Rectangle.Empty;}
            else{mask=combined.Crop(left,top,right-left+1,bottom-top+1);bounds=new Rectangle(area.X+left,area.Y+top,mask.Width,mask.Height);}
        }
        CancelSelection();Selection=bounds;SetSelectionMask(mask);Invalidate();if(ViewChanged!=null)ViewChanged();
    }
    void MaskPixels(Bitmap image,Rectangle region,SelectionMask mask,bool clearSelected){
        if(mask==null){if(clearSelected)ClearRegion(image,region);return;}
        using(PixelBuffer buffer=new PixelBuffer(image,true))for(int y=Math.Max(0,region.Y);y<Math.Min(image.Height,region.Bottom);y++)for(int x=Math.Max(0,region.X);x<Math.Min(image.Width,region.Right);x++)if(mask.At(x-region.X,y-region.Y)==clearSelected)buffer.Pixels[y*image.Width+x]=0;
    }
    void PaintMaskOutline(Graphics g){
        if(maskOutline==null)maskOutline=selectionMask.Outline();
        using(GraphicsPath path=(GraphicsPath)maskOutline.Clone())using(Matrix matrix=new Matrix(Zoom,0,0,Zoom,origin.X+Selection.X*Zoom,origin.Y+Selection.Y*Zoom))using(Pen black=new Pen(Color.Black,3))using(Pen white=new Pen(Color.White,1)){path.Transform(matrix);white.DashPattern=new float[]{4,4};g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.Default;g.DrawPath(black,path);g.DrawPath(white,path);}
    }
}
partial class Editor {
    ContextMenuStrip selectionToolsMenu;
    void ShowSelectionTools(Control button){
        // WinForms still accesses a dropdown after raising Closed. Keep it alive
        // for the editor's lifetime instead of disposing during that event.
        if(selectionToolsMenu==null){
            selectionToolsMenu=new ContextMenuStrip{Renderer=new AropixRenderer()};
            foreach(string name in new[]{"Selección","Varita mágica"}){
                string tool=name;ToolStripMenuItem item=new ToolStripMenuItem(name=="Selección"?"Selección rectangular":name){Tag=tool,ForeColor=Color.White};
                item.Click+=delegate{SetTool(tool);};selectionToolsMenu.Items.Add(item);
            }
            Disposed+=delegate{selectionToolsMenu.Dispose();};
        }
        foreach(ToolStripMenuItem item in selectionToolsMenu.Items)item.Checked=canvas.Tool==(string)item.Tag;
        selectionToolsMenu.Show(button,new Point(0,button.Height));
    }
    void RefreshSelectionTool(){Button button;if(!toolButtons.TryGetValue("Selección",out button))return;button.Text=canvas.Tool=="Varita mágica"?"Varita mágica":"Selección";button.BackColor=canvas.IsSelectionTool?Accent:Color.FromArgb(47,56,66);button.AccessibleName=button.Text+" · herramientas de selección";tips.SetToolTip(button,HelpCatalog.Tooltip(canvas.Tool=="Varita mágica"?"wand":"selection")+"\nClic: elegir herramienta de selección.");}
}
}
