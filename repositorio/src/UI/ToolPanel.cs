// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;

namespace PixelArt {
class FloatingPanelForm : ThemedWindow {
    public Editor Editor;
    public bool OpeningDuringDrag;
    public int ResizeGrip { get { return Padding.Left; } }
    protected override bool AnimateOpening { get { return !OpeningDuringDrag; } }
    protected override bool ConstrainOpeningPosition { get { return !OpeningDuringDrag; } }
    public FloatingPanelForm(){
        FormBorderStyle=FormBorderStyle.None;
        Padding=new Padding(7);
        DoubleBuffered=true;
    }
    protected override CreateParams CreateParams {
        get {
            CreateParams cp=base.CreateParams;
            // Keep the native sizing loop and system close command, without a caption.
            cp.Style|=0x40000|0x80000; // WS_THICKFRAME | WS_SYSMENU
            cp.ExStyle|=0x80; // WS_EX_TOOLWINDOW
            return cp;
        }
    }
    protected override void OnHandleCreated(EventArgs e){
        base.OnHandleCreated(e);
        using(Graphics g=CreateGraphics())Padding=new Padding(Math.Max(5,(int)Math.Round(7*g.DpiX/96)));
    }
    internal static int BorderHit(Point p,Size size,int grip){
        if(p.X<0 || p.Y<0 || p.X>=size.Width || p.Y>=size.Height)return 1;
        bool left=p.X<grip,right=p.X>=size.Width-grip,top=p.Y<grip,bottom=p.Y>=size.Height-grip;
        if(top)return left?13:right?14:12;
        if(bottom)return left?16:right?17:15;
        return left?10:right?11:1;
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0x83){m.Result=IntPtr.Zero;return;} // Full client area, no native caption/frame.
        if(m.Msg==0x84){
            long packed=m.LParam.ToInt64();
            Point screen=new Point(unchecked((short)(packed&65535)),unchecked((short)((packed>>16)&65535)));
            int hit=BorderHit(PointToClient(screen),ClientSize,ResizeGrip);
            if(hit!=1){m.Result=new IntPtr(hit);return;}
        }
        if(m.Msg==0x231)UiMotion.Cancel(this,true);
        base.WndProc(ref m);
        if(m.Msg==0x2E0 && !IsDisposed){
            int dpi=(int)(m.WParam.ToInt64()&65535);
            if(dpi>0)Padding=new Padding(Math.Max(5,(int)Math.Round(7*dpi/96.0)));
        }
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);
        using(Pen pen=new Pen(AropixTheme.Border))e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,ClientSize.Width-1),Math.Max(0,ClientSize.Height-1));
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys key){
        Control focused=ActiveControl;while(focused is ContainerControl && ((ContainerControl)focused).ActiveControl!=null)focused=((ContainerControl)focused).ActiveControl;
        if(!(focused is TextBoxBase) && !(focused is NumericUpDown) && !(focused is ComboBox) && Editor!=null && Editor.SelectionShortcut(key))return true;
        return base.ProcessCmdKey(ref msg,key);
    }
}
// One content instance moves between its dock and an owned floating window.
class ToolPanel : Panel {
    public readonly FlowLayoutPanel Content;
    public readonly string PanelTitle;
    readonly Form owner;
    readonly Action layoutChanged;
    readonly FlowLayoutPanel left,right;
    int expandedHeight; public int MinimumDockHeight=100; public double DockWeight=300;
    public void SetDockSize(int width,int height){expandedHeight=Math.Max(65,height);if(floating==null){Width=Math.Max(170,width);Height=collapsed?35:expandedHeight;}}
    Form floating;
    bool collapsed,hidden,closing,dragging,moved;
    Point dragStart,windowStart;
    readonly Label title;
    readonly Button hideButton;
    bool adjustingFloat;
    public bool IsHidden {get{return hidden;}}
    public bool IsFloating {get{return floating!=null;}}
    public bool IsCollapsed {get{return collapsed;}}
    public void SaveState(XmlElement node){
        if(floating!=null)node.SetAttribute("floatingFrame","2");
        node.SetAttribute("weight",DockWeight.ToString(System.Globalization.CultureInfo.InvariantCulture));node.SetAttribute("side",floating!=null?"floating":Parent==right?"right":"left");
        int order=0;if(floating==null && Parent!=null)foreach(Control sibling in Parent.Controls){if(sibling==this)break;if(sibling is ToolPanel)order++;}node.SetAttribute("order",order.ToString());
        node.SetAttribute("hidden",hidden.ToString());node.SetAttribute("collapsed",collapsed.ToString());
        if(floating!=null){node.SetAttribute("expandedHeight",expandedHeight.ToString());Rectangle b=floating.Bounds;node.SetAttribute("x",b.X.ToString());node.SetAttribute("y",b.Y.ToString());node.SetAttribute("width",b.Width.ToString());node.SetAttribute("height",b.Height.ToString());}
    }
    static int Number(XmlElement node,string key,int fallback){int n;return int.TryParse(node.GetAttribute(key),out n)?n:fallback;}
    public void RestoreState(XmlElement node){
        double weight;if(double.TryParse(node.GetAttribute("weight"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out weight) && !double.IsNaN(weight) && !double.IsInfinity(weight) && weight>=1 && weight<=100000)DockWeight=weight;string side=node.GetAttribute("side");if(side!="left" && side!="right" && side!="floating")return;DockTo(side=="right"?right:left);
        Collapse(string.Equals(node.GetAttribute("collapsed"),"true",StringComparison.OrdinalIgnoreCase));
        if(side=="floating"){expandedHeight=Math.Max(75,Math.Min(1600,Number(node,"expandedHeight",expandedHeight)));
            Rectangle requested=new Rectangle(Number(node,"x",owner.Left+70),Number(node,"y",owner.Top+110),Math.Max(285,Math.Min(1600,Number(node,"width",290))),Math.Max(75,Math.Min(1600,Number(node,"height",expandedHeight))));
            Rectangle area=Screen.FromRectangle(requested).WorkingArea;
            requested.Width=Math.Min(requested.Width,area.Width);requested.Height=Math.Min(requested.Height,area.Height);
            requested.X=Math.Max(area.Left,Math.Min(requested.X,area.Right-requested.Width));requested.Y=Math.Max(area.Top,Math.Min(requested.Y,area.Bottom-requested.Height));
            // Older preferences stored native outer bounds and the expanded client height.
            if(node.GetAttribute("floatingFrame")!="2")requested.Height=expandedHeight+14;
            if(collapsed)requested.Height=49;
            requested.Height=Math.Min(requested.Height,area.Height);
            requested.Y=Math.Max(area.Top,Math.Min(requested.Y,area.Bottom-requested.Height));
            FloatAt(requested.Location);
            if(collapsed)requested.Height=35+floating.Padding.Vertical;
            else if(node.GetAttribute("floatingFrame")!="2")requested.Height=expandedHeight+floating.Padding.Vertical;
            requested.Height=Math.Min(requested.Height,area.Height);
            requested.Y=Math.Max(area.Top,Math.Min(requested.Y,area.Bottom-requested.Height));
            floating.Bounds=requested;
        }
        if(string.Equals(node.GetAttribute("hidden"),"true",StringComparison.OrdinalIgnoreCase))HidePanel();
    }
    public void RestoreOrder(int index){if(floating==null && Parent!=null)Parent.Controls.SetChildIndex(this,Math.Max(0,Math.Min(index,Parent.Controls.Count-1)));}
    public ToolPanel(Form owner,string name,int height,FlowLayoutPanel left,FlowLayoutPanel right,Action changed){
        this.owner=owner;PanelTitle=name;expandedHeight=height;DockWeight=height;this.left=left;this.right=right;layoutChanged=changed;
        Width=270;Height=height;Margin=new Padding(0,0,0,6);BackColor=Editor.PanelColor;
        Content=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8)};
        Panel header=new Panel{Dock=DockStyle.Top,Height=35,BackColor=AropixTheme.Raised};
        title=new Label{Text="▾ "+name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(6,0,0,0),Cursor=Cursors.SizeAll};
        Button menu=Editor.Button("⋯",32);menu.AccessibleName="Opciones del panel "+name;menu.Dock=DockStyle.Right;menu.Height=30;
        hideButton=Editor.Button("×",32);hideButton.Dock=DockStyle.Right;hideButton.Visible=false;
        hideButton.AccessibleName="Ocultar panel";hideButton.AccessibleDescription="Oculta este panel; se puede recuperar desde Ver > Paneles.";
        ToolTip headerHints=new ToolTip();headerHints.SetToolTip(hideButton,"Ocultar panel");
        hideButton.Click+=delegate{HidePanel();};
        header.Controls.Add(title);header.Controls.Add(menu);header.Controls.Add(hideButton);Controls.Add(Content);Controls.Add(header);
        ContextMenuStrip choices=new ContextMenuStrip();header.Paint+=delegate(object s,PaintEventArgs e){using(Pen pen=new Pen(AropixTheme.Border))e.Graphics.DrawLine(pen,0,header.Height-1,header.Width,header.Height-1);};title.MouseEnter+=delegate{header.BackColor=AropixTheme.Hover;};title.MouseLeave+=delegate{header.BackColor=AropixTheme.Raised;};
        choices.Items.Add("Plegar / desplegar",null,delegate{Collapse(!collapsed);});
        choices.Items.Add("Ventana flotante",null,delegate{FloatAt(owner.PointToScreen(new Point(70,110)));});
        choices.Items.Add("Acoplar a la izquierda",null,delegate{DockTo(left);});
        choices.Items.Add("Acoplar a la derecha",null,delegate{DockTo(right);});
        choices.Items.Add("Ocultar panel",null,delegate{HidePanel();});
        AropixTheme.StyleMenu(choices);menu.Click+=delegate{choices.Show(menu,new Point(0,menu.Height));};title.ContextMenuStrip=choices;
        title.MouseDown+=delegate(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;dragging=true;moved=false;dragStart=Cursor.Position;windowStart=floating==null?PointToScreen(Point.Empty):floating.Location;title.Capture=true;};
        title.MouseMove+=delegate{
            if(!dragging)return;Point p=Cursor.Position;int dx=p.X-dragStart.X,dy=p.Y-dragStart.Y;
            if(!moved && Math.Abs(dx)+Math.Abs(dy)<8)return;
            moved=true;
            if(floating==null){FloatAt(windowStart);windowStart=floating.Location;title.Capture=true;}
            UiMotion.Cancel(floating,true);
            floating.Location=new Point(windowStart.X+dx,windowStart.Y+dy);
        };
        title.MouseUp+=delegate{if(!dragging)return;dragging=false;title.Capture=false;if(!moved){Collapse(!collapsed);return;}if(floating==null)return;Point p=owner.PointToClient(Cursor.Position);if(p.Y>75 && p.Y<owner.ClientSize.Height-40){Point screen=Cursor.Position;FlowLayoutPanel target=left.RectangleToScreen(left.ClientRectangle).Contains(screen)?left:right.RectangleToScreen(right.ClientRectangle).Contains(screen)?right:p.X>=0 && p.X<65?left:p.X>owner.ClientSize.Width-65 && p.X<owner.ClientSize.Width?right:null;if(target!=null){Control before=null;foreach(Control item in target.Controls){ToolPanel sibling=item as ToolPanel;if(sibling!=null && !sibling.IsHidden && screen.Y<sibling.PointToScreen(new Point(0,sibling.Height/2)).Y){before=sibling;break;}}DockTo(target);if(before!=null)target.Controls.SetChildIndex(this,target.Controls.GetChildIndex(before));else target.Controls.SetChildIndex(this,target.Controls.Count-1);layoutChanged();}}};
        Disposed+=delegate{headerHints.Dispose();choices.Dispose();if(floating!=null){closing=true;floating.Dispose();floating=null;}};
    }
    public void Collapse(bool value){
        FlowLayoutPanel dock=Parent as FlowLayoutPanel;
        bool animate=collapsed!=value && floating==null && dock!=null && Visible && UiMotion.Allowed && !dock.VerticalScroll.Visible;
        Dictionary<ToolPanel,int> starts=new Dictionary<ToolPanel,int>();
        if(dock!=null)UiMotion.Cancel(dock,false);
        if(animate)foreach(Control child in dock.Controls){ToolPanel panel=child as ToolPanel;if(panel!=null && panel.Visible)starts[panel]=panel.Height;}
        collapsed=value;Content.Visible=!value;title.Text=(value?"▸ ":"▾ ")+PanelTitle;
        if(floating==null)Height=value?35:expandedHeight;else {
            int desired=expandedHeight;
            adjustingFloat=true;
            try{
                floating.MaximumSize=Size.Empty;
                floating.MinimumSize=new Size(285,(value?35:75)+floating.Padding.Vertical);
                floating.Size=new Size(floating.Width,(value?35:desired)+floating.Padding.Vertical);
                if(value)floating.MaximumSize=new Size(32767,floating.Height);
            }finally{adjustingFloat=false;}
        }
        layoutChanged();
        if(!animate || dock.VerticalScroll.Visible)return;
        Dictionary<ToolPanel,int> targets=new Dictionary<ToolPanel,int>();
        foreach(ToolPanel panel in starts.Keys)targets[panel]=panel.Height;
        // Only presentation heights change. SetDockSize already stored the final desired size.
        UiMotion.Start(dock,140,delegate(double progress){
            dock.SuspendLayout();
            try{
                foreach(ToolPanel panel in starts.Keys)if(!panel.IsDisposed && panel.Parent==dock){
                    panel.Height=(int)Math.Round(starts[panel]+(targets[panel]-starts[panel])*progress);
                    panel.Content.Visible=(panel==this && progress<1) || !panel.IsCollapsed;
                }
            }finally{dock.ResumeLayout(true);}
        });
    }
    public void DockTo(FlowLayoutPanel target){
        if(floating!=null){Form old=floating;floating=null;old.Controls.Remove(this);closing=true;old.Close();old.Dispose();closing=false;}
        hideButton.Visible=false;Dock=DockStyle.None;Width=270;Height=collapsed?35:expandedHeight;target.Controls.Add(this);hidden=false;Show();layoutChanged();
    }
    public void FloatAt(Point location){
        if(floating!=null){hidden=false;floating.Show();floating.Activate();return;}
        Parent.Controls.Remove(this);hidden=false;
        floating=new FloatingPanelForm{Editor=owner as Editor,OpeningDuringDrag=dragging,Text=PanelTitle,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=location,Size=new Size(294,(collapsed?35:expandedHeight)+14),MinimumSize=new Size(285,(collapsed?35:75)+14),BackColor=Editor.PanelColor,ForeColor=Color.White,Font=owner.Font};
        hideButton.Visible=true;
        // Resolve the actual monitor DPI before sizing the content/header.
        IntPtr handle=floating.Handle;
        floating.MinimumSize=new Size(285,(collapsed?35:75)+floating.Padding.Vertical);
        floating.Size=new Size(floating.Width,(collapsed?35:expandedHeight)+floating.Padding.Vertical);
        if(dragging)floating.Location=new Point(location.X-floating.Padding.Left,location.Y-floating.Padding.Top);
        if(collapsed)floating.MaximumSize=new Size(32767,floating.Height);
        Dock=DockStyle.Fill;floating.Controls.Add(this);Show();
        floating.FormClosing+=delegate(object s,FormClosingEventArgs e){if(!closing && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HidePanel();}};
        floating.Resize+=delegate{if(!collapsed && !adjustingFloat && floating!=null)expandedHeight=Math.Max(75,floating.ClientSize.Height-floating.Padding.Vertical);};floating.Show(owner);layoutChanged();
    }
    public void HidePanel(){hidden=true;if(floating!=null)floating.Hide();else Hide();layoutChanged();}
    public void Reveal(){hidden=false;Show();if(floating!=null){floating.Show();floating.Activate();}else Collapse(false);layoutChanged();}
}
}

