// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Windows.Forms;
namespace PixelArt {
partial class PixelCanvas {
    public event Action<int> ToolWheel;
    int toolWheelRemainder;
    protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Menu)toolWheelRemainder=0;if(e.KeyCode==Keys.ControlKey)variantWheelRemainder=0;base.OnKeyUp(e);}
    protected override void OnLostFocus(EventArgs e){toolWheelRemainder=variantWheelRemainder=0;base.OnLostFocus(e);}
    public void HandleCanvasWheel(MouseEventArgs e,bool alt){
        if(!ClientRectangle.Contains(e.Location)){toolWheelRemainder=0;return;}
        if(alt){
            HandledMouseEventArgs handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
            if(IsEditing || panning){toolWheelRemainder=0;return;}
            if(Math.Sign(e.Delta)!=Math.Sign(toolWheelRemainder))toolWheelRemainder=0;
            long delta=(long)toolWheelRemainder+e.Delta;
            int steps=(int)(delta/120);toolWheelRemainder=(int)(delta%120);
            if(steps!=0 && ToolWheel!=null)ToolWheel(-steps);
            return;
        }
        toolWheelRemainder=0;base.OnMouseWheel(e);ChangeZoom(e.Delta>0?1:-1);TrackPointer(e.Location);
    }
}
partial class Editor {
    static readonly string[][] toolGroups={
        new[]{"Lápiz","Borrador"},new[]{"Selección","Varita mágica"},new[]{"Cubeta","Cuentagotas"},
        new[]{"Rectángulo","Círculo","Triángulo","Línea recta"},new[]{"Mover vista"}
    };
    void CycleTool(int steps){
        if(canvas.IsEditing)return;
        foreach(string[] group in toolGroups){int index=Array.IndexOf(group,canvas.Tool);if(index<0)continue;
            int target=((index+steps%group.Length)%group.Length+group.Length)%group.Length;
            if(target!=index)SetTool(group[target]);return;
        }
    }
    public void TestQuickTools(){
        SetDocument(new Document(16,16));SetTool("Rectángulo");canvas.Zoom=4;
        Action<int,bool> wheel=delegate(int delta,bool alt){canvas.HandleCanvasWheel(new HandledMouseEventArgs(MouseButtons.None,0,10,10,delta),alt);};
        wheel(-120,true);if(canvas.Tool!="Círculo" || canvas.Zoom!=4 || optionsTitle.Text!="Círculo" || !circleCenter.Visible)throw new Exception("Alt rueda sincroniza círculo sin zoom");
        wheel(120,true);wheel(120,true);if(canvas.Tool!="Línea recta")throw new Exception("Recorrido circular inverso");wheel(-120,true);if(canvas.Tool!="Rectángulo")throw new Exception("Recorrido circular");
        wheel(-60,true);if(canvas.Tool!="Rectángulo")throw new Exception("Rueda parcial");wheel(-60,true);if(canvas.Tool!="Círculo")throw new Exception("Acumular rueda parcial");
        wheel(-240,true);if(canvas.Tool!="Línea recta")throw new Exception("Rueda de varios pasos");
        canvas.PencilSize=5;canvas.EraserSize=3;SetTool("Lápiz");wheel(-120,true);if(canvas.Tool!="Borrador" || canvas.EraserSize!=3)throw new Exception("Grupo básico");wheel(120,true);if(canvas.PencilSize!=5)throw new Exception("Conservar tamaño");
        SetTool("Cubeta");wheel(-120,true);if(canvas.Tool!="Cuentagotas")throw new Exception("Grupo color");SetTool("Mover vista");wheel(-120,true);if(canvas.Tool!="Mover vista")throw new Exception("Grupo único");
        wheel(120,false);if(canvas.Zoom!=5)throw new Exception("Zoom normal");wheel(-120,false);if(canvas.Zoom!=4)throw new Exception("Zoom inverso");
        canvas.HandleCanvasWheel(new HandledMouseEventArgs(MouseButtons.None,0,-1,-1,-120),true);if(canvas.Tool!="Mover vista")throw new Exception("Rueda fuera del lienzo");
        SetTool("Rectángulo");canvas.ShapeClick(Point.Empty);wheel(-120,true);if(canvas.Tool!="Rectángulo" || canvas.PendingPoints!=1)throw new Exception("Conservar figura pendiente");canvas.CancelShape();
        SetTool("Selección");using(Bitmap image=new Bitmap(2,2))canvas.PasteSelection(image);wheel(-120,true);if(canvas.Tool!="Selección" || !canvas.HasPendingSelection)throw new Exception("Conservar pegado pendiente");canvas.CancelSelection();
        SetTool("Selección");canvas.BeginSelection(Point.Empty);wheel(-120,true);if(!canvas.HasSelectionGesture || canvas.Tool!="Selección")throw new Exception("Conservar gesto de selección");canvas.EndSelectionGesture();canvas.CancelSelection();
        SetTool("Lápiz");HandledMouseEventArgs consumed=new HandledMouseEventArgs(MouseButtons.None,0,10,10,-120);canvas.HandleCanvasWheel(consumed,true);if(!consumed.Handled || canvas.Tool!="Borrador")throw new Exception("Consumir evento una sola vez");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}
