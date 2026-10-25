// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace PixelArt
{
    // Stable preference ids; masks are constructed once, never edited by drawing operations.
    sealed class PencilTip
    {
        public readonly string Id, Name;
        readonly Point[][] masks;
        public PencilTip(string id,string name,Func<int,int,int,bool> contains)
        {
            Id=id;Name=name;if(contains==null)return;
            masks=new Point[11][];
            for(int n=1;n<=10;n++){
                List<Point> points=new List<Point>();
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)if(contains(n,x,y))points.Add(new Point(x-(n-1)/2,y-(n-1)/2));
                masks[n]=points.ToArray();
            }
        }
        public void Stamp(Document doc,int x,int y,Color color,int size)
        {
            if(masks==null){doc.Stamp(x,y,color,size);return;}
            foreach(Point offset in masks[Math.Max(1,Math.Min(10,size))]){
                int px=x+offset.X,py=y+offset.Y;
                if(px>=0 && py>=0 && px<doc.Image.Width && py<doc.Image.Height)doc.Stamp(px,py,color,1);
            }
        }
        public IEnumerable<Point> Offsets(int size)
        {
            size=Math.Max(1,Math.Min(10,size));
            if(masks!=null){foreach(Point point in masks[size])yield return point;}
            else for(int y=0;y<size;y++)for(int x=0;x<size;x++)yield return new Point(x-(size-1)/2,y-(size-1)/2);
        }
    }
    static class PencilTips
    {
        public static readonly PencilTip[] All={new PencilTip("square","Cuadrada",null),new PencilTip("circle","Circular",delegate(int n,int x,int y){int dx=2*x-(n-1),dy=2*y-(n-1);return dx*dx+dy*dy<=n*n;})};
        public static PencilTip Find(string id){foreach(PencilTip tip in All)if(tip.Id==id)return tip;return All[0];}
    }
    partial class Document
    {
        public void PencilLine(Point a,Point b,Color color,int size,PencilTip tip)
        {
            RasterLine(a,b,color,size,delegate(int x,int y,Color ink,int n){tip.Stamp(this,x,y,ink,n);});
        }
    }
    partial class PixelCanvas
    {
        public PencilTip PencilTip=PencilTips.All[0];
        public event Action<int> VariantWheel;
        int variantWheelRemainder;
        public void HandleCanvasWheel(MouseEventArgs e,bool alt,bool control)
        {
            if(alt){variantWheelRemainder=0;HandleCanvasWheel(e,true);return;}
            if(!control){variantWheelRemainder=0;HandleCanvasWheel(e,false);return;}
            toolWheelRemainder=0;
            if(!ClientRectangle.Contains(e.Location)){variantWheelRemainder=0;return;}
            HandledMouseEventArgs handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
            if(IsEditing || panning){variantWheelRemainder=0;return;}
            if(Math.Sign(e.Delta)!=Math.Sign(variantWheelRemainder))variantWheelRemainder=0;
            long delta=(long)variantWheelRemainder+e.Delta;
            int steps=(int)(delta/120);variantWheelRemainder=(int)(delta%120);
            if(steps!=0 && VariantWheel!=null)VariantWheel(-steps);
        }
    }
    partial class Editor
    {
        sealed class VariantGroup {public string[] Ids;public Func<string> Current;public Action<string> Apply;}
        readonly Dictionary<string,VariantGroup> variants=new Dictionary<string,VariantGroup>();
        readonly List<Button> tipButtons=new List<Button>();
        void BuildPencilTips()
        {
            FlowLayoutPanel row=new FlowLayoutPanel{Width=160,Height=32,Margin=new Padding(0)};
            List<string> ids=new List<string>();
            foreach(PencilTip tip in PencilTips.All){PencilTip choice=tip;ids.Add(tip.Id);Button button=Button(tip.Id=="square"?"■":"●",65);button.Height=27;button.AccessibleName="Punta "+tip.Name;button.Click+=delegate{SetPencilTip(choice.Id);};tips.SetToolTip(button,"Punta "+tip.Name+"\nCtrl + rueda: cambiar punta; conserva tamaño y color.");row.Controls.Add(button);tipButtons.Add(button);}
            AddOptionGroup("pencilTip",OptionLabel("Forma de punta"),row);
            variants["Lápiz"]=new VariantGroup{Ids=ids.ToArray(),Current=delegate{return canvas.PencilTip.Id;},Apply=SetPencilTip};
            canvas.VariantWheel+=CycleVariant;RefreshPencilTip();
        }
        void SetPencilTip(string id)
        {
            if(canvas.IsEditing)return;
            canvas.PencilTip=PencilTips.Find(id);RefreshPencilTip();UpdateStatus();canvas.Invalidate();canvas.Focus();
        }
        void RefreshPencilTip()
        {
            for(int i=0;i<tipButtons.Count;i++){
                bool selected=canvas.PencilTip==PencilTips.All[i];
                tipButtons[i].BackColor=selected?Accent:AropixTheme.Raised;
                tipButtons[i].Text=(selected?"✓ ":"")+(PencilTips.All[i].Id=="square"?"■":"●");
                tipButtons[i].AccessibleDescription=selected?"Punta activa":"Seleccionar esta punta";
            }
        }
        void CycleVariant(int steps)
        {
            if(canvas.IsEditing)return;VariantGroup group;if(!variants.TryGetValue(canvas.Tool,out group) || group.Ids.Length<2)return;
            int index=Array.IndexOf(group.Ids,group.Current());int target=((index+steps%group.Ids.Length)%group.Ids.Length+group.Ids.Length)%group.Ids.Length;
            if(target!=index)group.Apply(group.Ids[target]);
        }
    }
}
