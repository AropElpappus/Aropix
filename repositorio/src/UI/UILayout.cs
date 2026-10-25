// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;
namespace PixelArt {
partial class Editor {
    int leftDockWidth=210,rightDockWidth=280;
    double colorShare=0.48;
    bool arrangingPanels;
    Panel leftHost,rightHost;
    void BuildDockHosts(){
        dockLeft.Margin=dockRight.Margin=new Padding(0);dockLeft.Padding=dockRight.Padding=new Padding(2,0,2,0);
        leftHost=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};rightHost=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};
        Panel leftGrip=new Panel{Dock=DockStyle.Right,Width=5,Cursor=Cursors.VSplit,BackColor=Color.FromArgb(47,56,66)},rightGrip=new Panel{Dock=DockStyle.Left,Width=5,Cursor=Cursors.VSplit,BackColor=Color.FromArgb(47,56,66)};
        leftHost.Controls.Add(dockLeft);leftHost.Controls.Add(leftGrip);rightHost.Controls.Add(dockRight);rightHost.Controls.Add(rightGrip);workspace.Controls.Add(leftHost,0,0);workspace.Controls.Add(rightHost,2,0);
        SetupWidthGrip(leftGrip,true);SetupWidthGrip(rightGrip,false);workspace.SizeChanged+=delegate{LayoutPanels();};dockLeft.SizeChanged+=delegate{LayoutPanels();};dockRight.SizeChanged+=delegate{LayoutPanels();};
    }
    void SetupWidthGrip(Panel grip,bool left){int start=0,width=0;grip.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;start=Cursor.Position.X;width=left?leftDockWidth:rightDockWidth;grip.Capture=true;};grip.MouseMove+=delegate{if(!grip.Capture)return;int change=Cursor.Position.X-start;if(left)leftDockWidth=Math.Max(200,Math.Min(360,width+change));else rightDockWidth=Math.Max(270,Math.Min(420,width-change));LayoutPanels();};grip.MouseUp+=delegate{grip.Capture=false;};tips.SetToolTip(grip,"Arrastra para ajustar el ancho del lateral");}
    bool DockShown(ToolPanel panel,FlowLayoutPanel dock){return panel!=null && panel.Parent==dock && !panel.IsHidden && !panel.IsFloating;}
    int OptionHeight(){return canvas.Tool=="Círculo"?420:canvas.IsShape?350:canvas.Tool=="Lápiz" || canvas.Tool=="Borrador"?150:85;}
    void ArrangePanels(){
        if(arrangingPanels || workspace==null || dockLeft==null)return;arrangingPanels=true;
        try{
            bool left=DockPanels(dockLeft,true).Count>0,right=DockPanels(dockRight,true).Count>0;
            int leftWidth=left?leftDockWidth:0,rightWidth=right?rightDockWidth:0;int excess=leftWidth+rightWidth+300-workspace.ClientSize.Width;if(excess>0){int cut=Math.Min(excess,Math.Max(0,leftWidth-200));leftWidth-=cut;excess-=cut;rightWidth-=Math.Min(excess,Math.Max(0,rightWidth-270));}
            workspace.ColumnStyles[0].Width=leftWidth;workspace.ColumnStyles[2].Width=rightWidth;
            dockLeft.SuspendLayout();dockRight.SuspendLayout();
            try{ArrangeDock(dockLeft);ArrangeDock(dockRight);}finally{dockLeft.ResumeLayout(true);dockRight.ResumeLayout(true);}
            AdaptOptionWidths();AdaptRightWidths();ResizeLayerList();
        }finally{arrangingPanels=false;}
    }
    void AdaptOptionWidths(){
        if(shapesPanel==null)return;int width=Math.Max(110,shapesPanel.Content.ClientSize.Width-24-(shapesPanel.Content.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0));
        foreach(Control control in shapesPanel.Content.Controls){if(control is Label || control is CheckBox || control is ComboBox)control.Width=Math.Min(165,width);}
        if(basicsPanel.Content.Controls.Count>0){Control tiles=basicsPanel.Content.Controls[0];tiles.Width=Math.Max(120,Math.Min(160,basicsPanel.Content.ClientSize.Width-24));tiles.Height=tiles.Width<160?124:90;}
    }
    void AdaptRightWidths(){
        if(colorPanel!=null){int width=Math.Max(180,colorPanel.Content.ClientSize.Width-24);foreach(Control control in colorPanel.Content.Controls)control.Width=width;foreach(Control control in colorPanel.Content.Controls){FlowLayoutPanel row=control as FlowLayoutPanel;if(row!=null && row.Controls.Contains(hex)){hex.Width=Math.Min(100,width/2-8);foreach(Control child in row.Controls)if(child is Button)child.Width=Math.Max(70,width-hex.Width-12);}}}
        if(palette!=null)foreach(Control child in palette.Controls)if(child is Label){child.Width=Math.Max(100,palette.ClientSize.Width-10);child.Height=42;}
        if(layersPanel!=null){int width=Math.Max(185,layersPanel.Content.ClientSize.Width-24);foreach(Control control in layersPanel.Content.Controls)control.Width=width;}
    }
    void RestoreDefaultLayout(){
        leftDockWidth=210;rightDockWidth=280;colorShare=.48;basicsPanel.DockWeight=150;shapesPanel.DockWeight=300;colorPanel.DockWeight=480;layersPanel.DockWeight=520;recentExpanded=false;basicExpanded=false;RefreshColorSections();
        basicsPanel.DockTo(dockLeft);shapesPanel.DockTo(dockLeft);colorPanel.DockTo(dockRight);layersPanel.DockTo(dockRight);dockLeft.Controls.SetChildIndex(basicsPanel,0);dockLeft.Controls.SetChildIndex(shapesPanel,1);dockRight.Controls.SetChildIndex(colorPanel,0);dockRight.Controls.SetChildIndex(layersPanel,1);
        foreach(ToolPanel panel in new ToolPanel[]{basicsPanel,shapesPanel,colorPanel,layersPanel})panel.Collapse(false);LayoutPanels();
    }
    void SaveWorkspaceSettings(XmlElement root){root.SetAttribute("pencilTip",canvas.PencilTip.Id);root.SetAttribute("animations",UiMotion.Enabled.ToString());root.SetAttribute("layoutVersion","3");root.SetAttribute("leftWidth",leftDockWidth.ToString());root.SetAttribute("rightWidth",rightDockWidth.ToString());root.SetAttribute("colorShare",colorShare.ToString(CultureInfo.InvariantCulture));root.SetAttribute("recentExpanded",recentExpanded.ToString());root.SetAttribute("basicExpanded",basicExpanded.ToString());}
    void ReadWorkspaceSettings(XmlElement root){canvas.PencilTip=PencilTips.Find(root.GetAttribute("pencilTip"));RefreshPencilTip();bool animations;UiMotion.Enabled=!bool.TryParse(root.GetAttribute("animations"),out animations) || animations;leftDockWidth=210;rightDockWidth=280;colorShare=.48;basicsPanel.DockWeight=150;shapesPanel.DockWeight=300;colorPanel.DockWeight=480;layersPanel.DockWeight=520;int value;double share;if(int.TryParse(root.GetAttribute("leftWidth"),out value) && value>=200 && value<=360)leftDockWidth=value;if(int.TryParse(root.GetAttribute("rightWidth"),out value) && value>=270 && value<=420)rightDockWidth=value;if(double.TryParse(root.GetAttribute("colorShare"),NumberStyles.Float,CultureInfo.InvariantCulture,out share) && share>=.15 && share<=.85)colorShare=share;if(root.GetAttribute("layoutVersion")=="2"){colorPanel.DockWeight=colorShare*1000;layersPanel.DockWeight=(1-colorShare)*1000;}recentExpanded=root.GetAttribute("recentExpanded")=="True";basicExpanded=root.GetAttribute("basicExpanded")=="True";RefreshColorSections();}
}
}


