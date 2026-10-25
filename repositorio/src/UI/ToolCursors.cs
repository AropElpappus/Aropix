// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Forms;
namespace PixelArt {
static class ToolCursors {
    static Dictionary<string,Cursor> cursors=new Dictionary<string,Cursor>();
        // Cursor(Stream) uses OLE conversion, which can discard the light strokes.
    // Own the native handles explicitly; individual controls only borrow them.
    static readonly List<IntPtr> handles=new List<IntPtr>();
    [StructLayout(LayoutKind.Sequential)]
    struct IconInfo { public bool IsIcon; public int XHotspot,YHotspot; public IntPtr Mask,Color; }
    [DllImport("user32.dll",SetLastError=true)]
    static extern IntPtr CreateIconIndirect(ref IconInfo info);
    [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr handle);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    static ToolCursors() {
        Application.ApplicationExit+=delegate {
            foreach(Cursor cursor in cursors.Values)cursor.Dispose();
            cursors.Clear();
            foreach(IntPtr handle in handles)DestroyCursor(handle);
            handles.Clear();
        };
    }
    static Cursor CreateNative(Bitmap bitmap) {
        IntPtr color=IntPtr.Zero,mask=IntPtr.Zero,handle=IntPtr.Zero;
        try {
            color=bitmap.GetHbitmap(Color.FromArgb(0));
            using(Bitmap maskImage=new Bitmap(bitmap.Width,bitmap.Height,PixelFormat.Format1bppIndexed))
                mask=maskImage.GetHbitmap();
            IconInfo info=new IconInfo { IsIcon=false,XHotspot=4,YHotspot=4,Mask=mask,Color=color };
            handle=CreateIconIndirect(ref info);
            if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            Cursor cursor=new Cursor(handle);
            handles.Add(handle);handle=IntPtr.Zero;
            return cursor;
        } finally {
            if(handle!=IntPtr.Zero)DestroyCursor(handle);
            if(color!=IntPtr.Zero)DeleteObject(color);
            if(mask!=IntPtr.Zero)DeleteObject(mask);
        }
    }
    public static Cursor For(string tool){return For(tool,false);}
    public static Cursor For(string tool,bool darkInk){
        if(tool=="Mover vista")return Cursors.SizeAll;
        if(tool=="Rectángulo" || tool=="Círculo" || tool=="Triángulo")tool="Triángulo";
        string key=tool+(darkInk?"/dark":"/light");Cursor cursor;if(cursors.TryGetValue(key,out cursor))return cursor;
        using(Bitmap bitmap=new Bitmap(32,32)){
            using(Graphics g=Graphics.FromImage(bitmap)){
                g.SmoothingMode=SmoothingMode.None;
                using(Pen outline=new Pen(darkInk?Color.White:Color.Black,3))using(Pen white=new Pen(darkInk?Color.Black:Color.White,1)){
                    g.DrawLine(outline,0,4,8,4);g.DrawLine(outline,4,0,4,8);g.DrawLine(white,0,4,8,4);g.DrawLine(white,4,0,4,8);
                }
                g.TranslateTransform(9,9);g.ScaleTransform(.65f,.65f);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(Pen dark=new Pen(darkInk?Color.White:Color.Black,4))using(Pen light=new Pen(darkInk?Color.Black:Color.White,1.8f)){dark.LineJoin=light.LineJoin=LineJoin.Round;ToolTile.DrawIcon(g,dark,tool);ToolTile.DrawIcon(g,light,tool);}
            }
            cursor=CreateNative(bitmap);cursors.Add(key,cursor);return cursor;
        }
    }
}
}
