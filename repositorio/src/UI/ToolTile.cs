// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace PixelArt {
class ToolTile : Button {
    bool pressed; public bool IconOnly,HasMenu;
    double hoverAmount;
    void TransitionHover(bool value){double start=hoverAmount,end=value?1:0;UiMotion.Start(this,100,delegate(double p){hoverAmount=start+(end-start)*p;Invalidate();});}
    public ToolTile(string name){Text=name;Width=114;Height=82;Margin=new Padding(3);FlatStyle=FlatStyle.Flat;ForeColor=Color.White;BackColor=Color.FromArgb(47,56,66);Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    protected override void OnMouseEnter(EventArgs e){TransitionHover(true);base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){TransitionHover(false);base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=e.Button==MouseButtons.Left;Invalidate();base.OnMouseDown(e);} protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);} protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);} protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);} protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);} protected override void OnPaint(PaintEventArgs e){
        Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Editor.PanelColor:Parent.BackColor);
        Color tileColor=SystemInformation.HighContrast?(BackColor==Editor.Accent?SystemColors.Highlight:SystemColors.Control):BackColor;
        Rectangle r=new Rectangle(1,1,Width-3,Height-3);using(GraphicsPath path=Rounded(r,8))using(Brush fill=new SolidBrush(SystemInformation.HighContrast?tileColor:Enabled && pressed?ControlPaint.Dark(tileColor,.12f):Enabled?UiMotion.Blend(tileColor,ControlPaint.Light(tileColor,.12f),hoverAmount):tileColor))using(Pen border=new Pen(BackColor==Editor.Accent?Color.FromArgb(30,205,197):Color.FromArgb(76,91,105))){g.FillPath(fill,path);g.DrawPath(border,path);}
        GraphicsState state=g.Save();float iconSize=IconOnly?Math.Min(24*g.DpiX/96f,Math.Min(Width,Height)-10):32;g.TranslateTransform((Width-iconSize)/2f,IconOnly?(Height-iconSize)/2f:12);g.ScaleTransform(iconSize/32f,iconSize/32f);using(Pen p=new Pen(Enabled?(SystemInformation.HighContrast && BackColor==Editor.Accent?SystemColors.HighlightText:AropixTheme.Text):AropixTheme.Disabled,1.7f)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;DrawIcon(g,p,Text);}g.Restore(state);
        if(!IconOnly)TextRenderer.DrawText(g,Text,Font,new Rectangle(3,52,Width-6,24),(Enabled?(SystemInformation.HighContrast && BackColor==Editor.Accent?SystemColors.HighlightText:AropixTheme.Text):AropixTheme.Disabled),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(HasMenu)using(Brush arrow=new SolidBrush(Color.White))g.FillPolygon(arrow,new Point[]{new Point(Width-9,Height-8),new Point(Width-3,Height-8),new Point(Width-6,Height-4)});
        if(BackColor==Editor.Accent)using(Pen selected=new Pen(AropixTheme.Focus,3))g.DrawLine(selected,8,Height-5,Width-8,Height-5);if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10),Color.White,BackColor);
    }
    static GraphicsPath Rounded(Rectangle r,int radius){GraphicsPath p=new GraphicsPath();int d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    public static void DrawIcon(Graphics g,Pen p,string name){
        if(name=="Varita mágica"){g.DrawLine(p,5,28,24,9);g.DrawLine(p,8,29,26,11);g.DrawLine(p,5,28,8,29);g.DrawLine(p,24,9,26,11);g.DrawLine(p,22,1,22,6);g.DrawLine(p,28,5,31,2);g.DrawLine(p,28,10,32,10);g.DrawLine(p,13,5,17,5);g.DrawLine(p,15,3,15,7);}else if(name=="Lápiz"){g.DrawPolygon(p,new Point[]{new Point(5,23),new Point(5,28),new Point(11,27),new Point(28,10),new Point(22,4)});g.DrawLine(p,19,7,25,13);g.DrawLine(p,5,23,11,27);}
        else if(name=="Borrador"){g.DrawPolygon(p,new Point[]{new Point(4,20),new Point(17,5),new Point(29,16),new Point(18,28),new Point(12,28)});g.DrawLine(p,10,13,23,23);g.DrawLine(p,12,28,29,28);}
        else if(name=="Mover vista"){g.DrawLine(p,16,3,16,29);g.DrawLine(p,3,16,29,16);g.DrawLines(p,new Point[]{new Point(12,7),new Point(16,3),new Point(20,7)});g.DrawLines(p,new Point[]{new Point(12,25),new Point(16,29),new Point(20,25)});g.DrawLines(p,new Point[]{new Point(7,12),new Point(3,16),new Point(7,20)});g.DrawLines(p,new Point[]{new Point(25,12),new Point(29,16),new Point(25,20)});}
        else if(name=="Cubeta"){g.DrawPolygon(p,new Point[]{new Point(3,16),new Point(15,4),new Point(26,15),new Point(14,27)});g.DrawLine(p,6,17,23,17);g.DrawArc(p,10,0,12,16,175,190);g.DrawBezier(p,new Point(28,20),new Point(20,31),new Point(35,31),new Point(28,20));}
        else if(name=="Cuentagotas"){g.DrawPolygon(p,new Point[]{new Point(6,23),new Point(6,28),new Point(11,28),new Point(24,15),new Point(19,10)});g.DrawLine(p,17,8,26,17);g.DrawEllipse(p,22,3,7,7);g.DrawLine(p,20,11,25,6);}
        else if(name=="Selección"){p.DashPattern=new float[]{2,2};g.DrawRectangle(p,4,5,24,22);}
        else if(name=="Línea recta"){g.DrawLine(p,5,27,27,5);g.DrawRectangle(p,3,25,4,4);g.DrawRectangle(p,25,3,4,4);} else if(name=="Rectángulo")g.DrawRectangle(p,3,7,26,19);
        else if(name=="Círculo")g.DrawEllipse(p,4,4,24,24);
        else if(name=="Triángulo")g.DrawPolygon(p,new Point[]{new Point(16,4),new Point(29,27),new Point(3,27)});
    }
}
}



