// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace PixelArt {
    class DockDivider : Panel {
        public readonly ToolPanel Above, Below;
        int startY, startHeight, total;
        readonly Action<ToolPanel,ToolPanel,int,int> resize;
        void HoverTone(Color color){Color from=BackColor;UiMotion.Start(this,90,delegate(double p){BackColor=UiMotion.Blend(from,color,p);});}
        public DockDivider(ToolPanel above,ToolPanel below,Action<ToolPanel,ToolPanel,int,int> change){
            Above=above;Below=below;resize=change;Height=8;Margin=new Padding(0);Cursor=Cursors.HSplit;
            BackColor=AropixTheme.Border;AccessibleName="Altura entre "+above.PanelTitle+" y "+below.PanelTitle;
            MouseEnter+=delegate{if(!Capture)HoverTone(AropixTheme.Focus);};MouseLeave+=delegate{if(!Capture)HoverTone(AropixTheme.Border);};
            MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;UiMotion.Cancel(this,false);UiMotion.Cancel(Parent,true);startY=Cursor.Position.Y;startHeight=Above.Height;total=Above.Height+Below.Height;Capture=true;BackColor=AropixTheme.Accent;};
            MouseMove+=delegate{if(Capture)resize(Above,Below,startHeight+Cursor.Position.Y-startY,total);};
            MouseUp+=delegate{Capture=false;BackColor=AropixTheme.Border;};MouseCaptureChanged+=delegate{if(!Capture)BackColor=AropixTheme.Border;};
            DoubleClick+=delegate{resize(Above,Below,(Above.Height+Below.Height)/2,Above.Height+Below.Height);};
        }
    }
    partial class Editor {
        readonly List<DockDivider> dockDividers=new List<DockDivider>();
        static List<ToolPanel> DockPanels(FlowLayoutPanel dock,bool visibleOnly){
            List<ToolPanel> result=new List<ToolPanel>();
            foreach(Control child in dock.Controls){ToolPanel panel=child as ToolPanel;if(panel!=null && !panel.IsFloating && (!visibleOnly || !panel.IsHidden))result.Add(panel);}
            return result;
        }
        void ResizeDockPair(ToolPanel above,ToolPanel below,int height,int total){
            FlowLayoutPanel dock=above.Parent as FlowLayoutPanel;
            if(dock==null || below.Parent!=dock || above.IsCollapsed || below.IsCollapsed || above.IsHidden || below.IsHidden)return;
            height=Math.Max(above.MinimumDockHeight,Math.Min(total-below.MinimumDockHeight,height));
            foreach(ToolPanel panel in DockPanels(dock,true))if(!panel.IsCollapsed)panel.DockWeight=panel.Height;
            above.DockWeight=height;below.DockWeight=Math.Max(below.MinimumDockHeight,total-height);
            LayoutPanels();
        }
        void ArrangeDock(FlowLayoutPanel dock){
            UiMotion.Cancel(dock,true);
            List<ToolPanel> panels=DockPanels(dock,true);
            List<DockDivider> needed=new List<DockDivider>();
            for(int i=0;i+1<panels.Count;i++){
                ToolPanel a=panels[i],b=panels[i+1];if(a.IsCollapsed || b.IsCollapsed)continue;
                DockDivider grip=dockDividers.Find(delegate(DockDivider d){return d.Above==a && d.Below==b && d.Parent==dock;});
                if(grip==null){grip=new DockDivider(a,b,ResizeDockPair);dockDividers.Add(grip);dock.Controls.Add(grip);tips.SetToolTip(grip,"Arrastra para cambiar alturas. Doble clic: equilibrar.");}
                needed.Add(grip);
            }
            foreach(DockDivider old in dockDividers.ToArray())if(old.Parent==dock && !needed.Contains(old)){dockDividers.Remove(old);old.Dispose();}
            int available=dock.ClientSize.Height-dock.Padding.Vertical-needed.Count*8;
            List<ToolPanel> expanded=new List<ToolPanel>();
            foreach(ToolPanel panel in panels){available-=panel.Margin.Vertical;if(panel.IsCollapsed)available-=35;else expanded.Add(panel);}
            Dictionary<ToolPanel,int> heights=new Dictionary<ToolPanel,int>();
            int minimum=0;foreach(ToolPanel panel in expanded)minimum+=panel.MinimumDockHeight;
            int remaining=Math.Max(minimum,available);
            List<ToolPanel> pending=new List<ToolPanel>(expanded);
            while(pending.Count>0){
                double weights=0;foreach(ToolPanel panel in pending)weights+=panel.DockWeight;
                bool clamped=false;int budget=remaining;
                foreach(ToolPanel panel in pending.ToArray())if(budget*panel.DockWeight/weights<panel.MinimumDockHeight){heights[panel]=panel.MinimumDockHeight;remaining-=panel.MinimumDockHeight;pending.Remove(panel);clamped=true;}
                if(clamped)continue;
                int assigned=0;for(int i=0;i<pending.Count;i++){ToolPanel panel=pending[i];int h=i==pending.Count-1?remaining-assigned:(int)Math.Floor(remaining*panel.DockWeight/weights);h=Math.Max(panel.MinimumDockHeight,h);heights[panel]=h;assigned+=h;}break;
            }
            int width=Math.Max(170,dock.ClientSize.Width-dock.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth);
            int index=0;
            foreach(ToolPanel panel in DockPanels(dock,false)){dock.Controls.SetChildIndex(panel,index++);if(panel.IsHidden)continue;panel.SetDockSize(width,panel.IsCollapsed?panel.MinimumDockHeight:heights[panel]);DockDivider next=needed.Find(delegate(DockDivider d){return d.Above==panel;});if(next!=null){next.Width=width;dock.Controls.SetChildIndex(next,index++);}}
        }
        bool DockDividersFit(){foreach(DockDivider divider in dockDividers){FlowLayoutPanel dock=divider.Parent as FlowLayoutPanel;if(dock!=null && !dock.VerticalScroll.Visible && divider.Bottom>dock.ClientSize.Height)return false;}return true;}
    }
}
