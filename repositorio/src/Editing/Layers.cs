// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace PixelArt {
class LayerList : CheckedListBox {
    public bool Updating;
    bool allowToggle=true;
    protected override void OnMouseDown(MouseEventArgs e){allowToggle=e.X<22;base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);allowToggle=true;}
    protected override void OnKeyDown(KeyEventArgs e){allowToggle=true;base.OnKeyDown(e);}
    protected override void OnItemCheck(ItemCheckEventArgs e){if(!allowToggle && !Updating)e.NewValue=e.CurrentValue;base.OnItemCheck(e);}
}
class Layer : IDisposable {
    // Transient identity for mixed history; never serialized into .pixelart.
    public readonly Guid HistoryId;
    public string Name;
    public bool Visible=true;
    public bool Locked;
    public int Opacity=100;
    public Bitmap Pixels;
    public Layer(string name,Bitmap pixels):this(name,pixels,Guid.NewGuid()){}
    Layer(string name,Bitmap pixels,Guid historyId){Name=name;Pixels=pixels;HistoryId=historyId;}
    public Layer Clone(){
        Bitmap pixels=(Bitmap)Pixels.Clone();
        try{return new Layer(Name,pixels,HistoryId){Visible=Visible,Opacity=Opacity,Locked=Locked};}
        catch{pixels.Dispose();throw;}
    }
    public void Draw(Graphics g,RectangleF destination,Bitmap replacement=null,bool ignoreVisibility=false){
        if((!Visible && !ignoreVisibility) || Opacity==0)return;Bitmap image=replacement??Pixels;
        if(Opacity==100){g.DrawImage(image,destination,new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel);return;}
        using(ImageAttributes attributes=new ImageAttributes()){ColorMatrix matrix=new ColorMatrix();matrix.Matrix33=Opacity/100f;attributes.SetColorMatrix(matrix);g.DrawImage(image,Rectangle.Round(destination),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}
    }
    public void Dispose(){Pixels.Dispose();}
}
class LayerSnapshot : HistoryEntry {
    public List<Layer> Layers=new List<Layer>();public int Active;public int? AxisX,AxisY;
    public override long Bytes {get{long sum=0;foreach(Layer layer in Layers)sum+=(long)layer.Pixels.Width*layer.Pixels.Height*4;return sum;}}
    public override HistoryEntry CaptureInverse(Document doc,Func<Document,LayerSnapshot> captureFull){return captureFull==null?new LayerSnapshot(doc):captureFull(doc);}
    public LayerSnapshot(Document doc):this(doc,null){}
    // The optional factory permits deterministic failure tests without exhausting memory.
    internal LayerSnapshot(Document doc,Func<Layer,Layer> clone){
        Active=doc.ActiveLayer;AxisX=doc.MirrorAxisX2;AxisY=doc.MirrorAxisY2;
        try{
            Layers.Capacity=doc.Layers.Count;
            foreach(Layer layer in doc.Layers){
                Layer copy=clone==null?layer.Clone():clone(layer);
                try{Layers.Add(copy);}catch{copy.Dispose();throw;}
            }
        }catch{Dispose();throw;}
    }
    public override void Restore(Document doc){
        // Transfer ownership without allocating after disposing the live images.
        List<Layer> previous=doc.Layers;
        foreach(Layer layer in previous)layer.Dispose();
        doc.Layers=Layers;doc.ActiveLayer=Active;doc.MirrorAxisX2=AxisX;doc.MirrorAxisY2=AxisY;
        Layers=previous;Layers.Clear();
    }
    public override void Dispose(){foreach(Layer layer in Layers)layer.Dispose();Layers.Clear();}
}
partial class Document {
    public List<Layer> Layers=new List<Layer>();
    public int ActiveLayer;
    public bool Editable {get{return Layers[ActiveLayer].Visible && !Layers[ActiveLayer].Locked;}}
    public Bitmap Image {get{return Layers[ActiveLayer].Pixels;}set{Layers[ActiveLayer].Pixels=value;}}
    public Bitmap Composite(Bitmap activeOverride=null){
        Bitmap output=new Bitmap(Image.Width,Image.Height,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(output)){g.CompositingMode=CompositingMode.SourceOver;for(int i=0;i<Layers.Count;i++)Layers[i].Draw(g,new RectangleF(0,0,output.Width,output.Height),i==ActiveLayer?activeOverride:null);}
        return output;
    }
    public void SerializeLayers(XmlDocument xml){
        XmlElement root=xml.DocumentElement;root.SetAttribute("version","2");root.SetAttribute("activeLayer",ActiveLayer.ToString());
        foreach(Layer layer in Layers){XmlElement node=xml.CreateElement("layer");node.SetAttribute("name",layer.Name);node.SetAttribute("visible",layer.Visible.ToString());node.SetAttribute("opacity",layer.Opacity.ToString());node.SetAttribute("locked",layer.Locked.ToString());using(MemoryStream stream=new MemoryStream()){layer.Pixels.Save(stream,ImageFormat.Png);node.InnerText=Convert.ToBase64String(stream.ToArray());}root.AppendChild(node);}
    }
    public void ReadLayers(XmlElement root){
        if(root.GetAttribute("version")!="2")return;
        XmlNodeList nodes=root.SelectNodes("layer");if(nodes.Count<1 || nodes.Count>16)throw new Exception("El proyecto debe contener entre 1 y 16 capas.");
        List<Layer> loaded=new List<Layer>();
        try{
            foreach(XmlElement node in nodes)using(MemoryStream stream=new MemoryStream(Convert.FromBase64String(node.InnerText)))using(Bitmap source=new Bitmap(stream)){
                if(source.Width!=Image.Width || source.Height!=Image.Height)throw new Exception("Dimensiones de capa no válidas.");
                Bitmap bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);using(Graphics g=Graphics.FromImage(bitmap)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(source,Point.Empty);}
                int opacity=100,parsed;if(node.HasAttribute("opacity")){if(!int.TryParse(node.GetAttribute("opacity"),out parsed) || parsed<0 || parsed>100){bitmap.Dispose();throw new Exception("Opacidad de capa no válida.");}opacity=parsed;}
                loaded.Add(new Layer(node.GetAttribute("name"),bitmap){Visible=node.GetAttribute("visible")!="False",Opacity=opacity,Locked=node.GetAttribute("locked")=="True"});
            }
            int active;if(!int.TryParse(root.GetAttribute("activeLayer"),out active) || active<0 || active>=loaded.Count)throw new Exception("Capa activa no válida.");
            foreach(Layer layer in Layers)layer.Dispose();Layers=loaded;ActiveLayer=active;
        }catch{foreach(Layer layer in loaded)layer.Dispose();throw;}
    }
}
partial class Editor {
    ToolPanel layersPanel;
    LayerList layerList=new LayerList();
    bool updatingLayers; CheckBox lockLayer=new CheckBox{Text="Bloquear capa",Width=235,Height=28};
    Label layerHint=new Label{Width=235,Height=56,ForeColor=Color.Silver};
    NumericUpDown opacityInput=new NumericUpDown{Minimum=0,Maximum=100,Value=100,Width=100};
    void BuildLayersPanel(){
        layersPanel=new ToolPanel(this,"Capas",330,dockLeft,dockRight,LayoutPanels){MinimumDockHeight=200,DockWeight=520};
        FlowLayoutPanel content=layersPanel.Content;
        layerList.Size=new Size(235,175);layerList.BackColor=PanelColor;layerList.ForeColor=Color.White;layerList.BorderStyle=BorderStyle.FixedSingle;layerList.CheckOnClick=false;content.Controls.Add(layerList);
        BuildLayerQuickActions(content);
        layerList.SelectedIndexChanged+=delegate{if(updatingLayers || doc==null || layerList.SelectedIndex<0)return;canvas.CommitSelection();canvas.CancelSelection();canvas.CancelShape();doc.ActiveLayer=doc.Layers.Count-1-layerList.SelectedIndex;MarkDirty();RefreshLayers();canvas.Invalidate();};
        layerList.ItemCheck+=delegate(object sender,ItemCheckEventArgs e){if(updatingLayers || doc==null || e.NewValue==e.CurrentValue)return;int index=doc.Layers.Count-1-e.Index;SetLayerVisible(index,e.NewValue==CheckState.Checked);BeginInvoke(new Action(RefreshLayers));};
        layersPanel.DockTo(dockRight);
    }
    void RefreshLayers(){
        if(layersPanel==null)return;if(doc==null){updatingLayers=true;layerList.Items.Clear();opacityInput.Value=100;lockLayer.Checked=false;opacityValue.Text="%";layerHint.Text="No hay un proyecto activo.";updatingLayers=false;return;}updatingLayers=true;layerList.Updating=true;layerList.Items.Clear();
        for(int i=doc.Layers.Count-1;i>=0;i--)layerList.Items.Add((doc.Layers[i].Locked?"[B] ":"")+doc.Layers[i].Name,doc.Layers[i].Visible);
        layerList.SelectedIndex=doc.Layers.Count-1-doc.ActiveLayer;opacityInput.Value=doc.Layers[doc.ActiveLayer].Opacity;lockLayer.Checked=doc.Layers[doc.ActiveLayer].Locked;opacityValue.Text="%";layerList.Updating=false;updatingLayers=false;
        layerHint.Text=doc.Layers[doc.ActiveLayer].Locked?"Capa bloqueada. Desmarca Bloquear capa para editar su contenido.":doc.Editable?"Arriba = delante. Marca la casilla para mostrar cada capa. Dibujas en la capa seleccionada.":"Esta capa está oculta. Activa su casilla para poder dibujar en ella.";
    }
    void SetLayerLocked(bool value){if(doc.Layers[doc.ActiveLayer].Locked==value)return;FinishPendingLayerEdit();PushProperties();doc.Layers[doc.ActiveLayer].Locked=value;LayerEdited();}
    void SetLayerVisible(int index,bool value){
        if(index<0 || index>=doc.Layers.Count)throw new ArgumentOutOfRangeException("index");
        if(doc.Layers[index].Visible==value)return;
        FinishPendingLayerEdit();PushProperties();doc.Layers[index].Visible=value;
        // ItemCheck is still in progress: defer rebuilding that control to its caller.
        MarkDirty();canvas.Invalidate();
    }
    void AddLayer(){
        if(doc.Layers.Count>=16){MessageBox.Show(this,"Este proyecto admite hasta 16 capas.");return;}
        FinishPendingLayerEdit();
        InsertLayerPixels("Capa "+(doc.Layers.Count+1),new Bitmap(doc.Image.Width,doc.Image.Height,PixelFormat.Format32bppArgb),doc.ActiveLayer+1);
        LayerEdited();
    }
    void MoveLayer(int direction){
        ReorderLayer(doc.ActiveLayer+direction);
    }
    void ReorderLayer(int target){
        if(target<0 || target>=doc.Layers.Count || target==doc.ActiveLayer)return;
        canvas.CommitSelection();canvas.CancelSelection();canvas.CancelShape();PushUndo();Layer layer=doc.Layers[doc.ActiveLayer];doc.Layers.RemoveAt(doc.ActiveLayer);doc.Layers.Insert(target,layer);doc.ActiveLayer=target;MarkDirty();RefreshLayers();canvas.Invalidate();
    }
    void FinishPendingLayerEdit(){canvas.CommitSelection();canvas.CancelSelection();canvas.CancelShape();}
    void PrepareLayerEdit(){FinishPendingLayerEdit();PushUndo();}
    // Diagnostic observer is unset in production. Ownership always transfers here.
    Action<Bitmap> preparedImageObserver;
    void InsertLayerPixels(string name,Bitmap pixels,int index,int opacity=100,bool visible=true,bool locked=false){
        bool inserted=false;
        try{
            if(index<0 || index>doc.Layers.Count)throw new ArgumentOutOfRangeException("index");
            Layer layer=new Layer(name,pixels){Opacity=opacity,Visible=visible,Locked=locked};
            if(doc.Layers.Capacity<doc.Layers.Count+1)doc.Layers.Capacity=doc.Layers.Count+1;
            if(preparedImageObserver!=null)preparedImageObserver(pixels);
            PushUndo();
            doc.Layers.Insert(index,layer);doc.ActiveLayer=index;inserted=true;
        }finally{if(!inserted)pixels.Dispose();}
    }
    void LayerEdited(){MarkDirty();RefreshLayers();canvas.Invalidate();}
    void DuplicateLayer(){
        if(doc.Layers.Count>=16){MessageBox.Show(this,"El proyecto admite hasta 16 capas.");return;}
        FinishPendingLayerEdit();Layer source=doc.Layers[doc.ActiveLayer];
        InsertLayerPixels(source.Name+" (copia)",(Bitmap)source.Pixels.Clone(),doc.ActiveLayer+1,source.Opacity,source.Visible,source.Locked);
        LayerEdited();
    }
    void DeleteLayer(){
        if(doc.Layers[doc.ActiveLayer].Locked)return;
        FinishPendingLayerEdit();Bitmap pixels=null;
        try{
            Layer replacement=null;
            if(doc.Layers.Count==1){
                pixels=new Bitmap(doc.Image.Width,doc.Image.Height,PixelFormat.Format32bppArgb);
                replacement=new Layer("Fondo",pixels);
                if(preparedImageObserver!=null)preparedImageObserver(pixels);
            }
            PushUndo();
            doc.Layers[doc.ActiveLayer].Dispose();
            if(replacement!=null){doc.Layers[0]=replacement;pixels=null;}
            else doc.Layers.RemoveAt(doc.ActiveLayer);
            doc.ActiveLayer=Math.Min(doc.ActiveLayer,doc.Layers.Count-1);
        }finally{if(pixels!=null)pixels.Dispose();}
        LayerEdited();
    }
    void SetLayerOpacity(int value){if(doc.Layers[doc.ActiveLayer].Opacity==value)return;FinishPendingLayerEdit();PushProperties();doc.Layers[doc.ActiveLayer].Opacity=value;LayerEdited();}
    void MergeLayer(){
        if(doc.ActiveLayer==0)return;
        Layer top=doc.Layers[doc.ActiveLayer],bottom=doc.Layers[doc.ActiveLayer-1];
        if(top.Locked || bottom.Locked)return; if(!top.Visible || !bottom.Visible){MessageBox.Show(this,"Muestra ambas capas antes de combinarlas.","Combinar capas");return;}
        FinishPendingLayerEdit();Bitmap merged=new Bitmap(doc.Image.Width,doc.Image.Height,PixelFormat.Format32bppArgb);
        try{
            using(Graphics g=Graphics.FromImage(merged)){RectangleF area=new RectangleF(0,0,merged.Width,merged.Height);bottom.Draw(g,area);top.Draw(g,area);}
            if(preparedImageObserver!=null)preparedImageObserver(merged);
            PushUndo();
            bottom.Pixels.Dispose();bottom.Pixels=merged;merged=null;bottom.Opacity=100;
            top.Dispose();doc.Layers.RemoveAt(doc.ActiveLayer);doc.ActiveLayer--;
        }finally{if(merged!=null)merged.Dispose();}
        LayerEdited();
    }
    void SetupLayerDrag(){
        Point start=Point.Empty;Layer dragged=null;layerList.AllowDrop=true;
        layerList.MouseDown+=delegate(object sender,MouseEventArgs e){int i=layerList.IndexFromPoint(e.Location);dragged=e.Button==MouseButtons.Left && e.X>22 && i>=0?doc.Layers[doc.Layers.Count-1-i]:null;start=e.Location;};
        layerList.MouseMove+=delegate(object sender,MouseEventArgs e){if(dragged==null || e.Button!=MouseButtons.Left)return;if(Math.Abs(e.X-start.X)+Math.Abs(e.Y-start.Y)<8)return;Layer item=dragged;dragged=null;layerList.DoDragDrop(new DataObject("PixelArt.Layer",item),DragDropEffects.Move);};
        layerList.MouseUp+=delegate{dragged=null;};
        layerList.DragOver+=delegate(object sender,DragEventArgs e){Layer item=e.Data.GetData("PixelArt.Layer") as Layer;e.Effect=item!=null && doc.Layers.Contains(item)?DragDropEffects.Move:DragDropEffects.None;};
        layerList.DragDrop+=delegate(object sender,DragEventArgs e){Layer item=e.Data.GetData("PixelArt.Layer") as Layer;if(item==null || !doc.Layers.Contains(item))return;Point p=layerList.PointToClient(new Point(e.X,e.Y));int row=layerList.IndexFromPoint(p);if(row<0)row=p.Y<0?0:doc.Layers.Count-1;canvas.CommitSelection();canvas.CancelSelection();doc.ActiveLayer=doc.Layers.IndexOf(item);ReorderLayer(doc.Layers.Count-1-row);};
        tips.SetToolTip(layerList,"Arrastra el nombre de una capa hasta otra posición. La casilla controla su visibilidad.");
    }
    void RenameLayer(){
        using(Form dialog=new SecondaryDialog{Text="Nombre de la capa",ClientSize=new Size(320,115),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=PanelColor,ForeColor=Color.White,Font=Font}){
            TextBox name=new TextBox{Text=doc.Layers[doc.ActiveLayer].Name,MaxLength=80,Location=new Point(15,18),Width=290};dialog.Controls.Add(name);Button ok=Button("Aceptar",110);ok.Location=new Point(195,65);ok.DialogResult=DialogResult.OK;dialog.Controls.Add(ok);dialog.AcceptButton=ok;
            if(dialog.ShowDialog(this)!=DialogResult.OK || string.IsNullOrWhiteSpace(name.Text) || name.Text.Trim()==doc.Layers[doc.ActiveLayer].Name)return;
            SetLayerName(name.Text);
        }
    }
    void SetLayerName(string name){
        if(string.IsNullOrWhiteSpace(name))return;
        name=name.Trim();if(name==doc.Layers[doc.ActiveLayer].Name)return;
        canvas.CommitSelection();PushProperties();doc.Layers[doc.ActiveLayer].Name=name;MarkDirty();RefreshLayers();
    }
    public void TestLayers(){
        SetDocument(new Document(12,12));using(Graphics g=Graphics.FromImage(doc.Image))g.Clear(Color.Blue);
        AddLayer();doc.Layers[1].Name="Personaje";doc.Line(new Point(4,4),new Point(5,5),Color.Red);
        if(doc.Layers[0].Pixels.GetPixel(4,4).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Dibujo por capa independiente");
        AddLayer();doc.Layers[2].Name="Objeto";doc.Image.SetPixel(4,4,Color.Yellow);
        using(Bitmap result=doc.Composite())if(result.GetPixel(4,4).ToArgb()!=Color.Yellow.ToArgb() || result.GetPixel(0,0).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Composición y transparencia de capas");
        MoveLayer(-1);using(Bitmap result=doc.Composite())if(result.GetPixel(4,4).ToArgb()!=Color.Red.ToArgb())throw new Exception("Reordenar capas");
        Undo(false);if(doc.ActiveLayer!=2 || doc.Layers[2].Name!="Objeto")throw new Exception("Deshacer reordenación conserva capa activa");
        layerList.SetItemChecked(0,false);Application.DoEvents();using(Bitmap result=doc.Export())if(result.GetPixel(16,16).ToArgb()!=Color.Red.ToArgb())throw new Exception("Ocultar capa afecta exportación");
        SetTool("Cubeta");SetColor(Color.Green);canvas.UsePointTool(new Point(4,4));if(doc.Image.GetPixel(4,4).ToArgb()!=Color.Yellow.ToArgb())throw new Exception("No editar capa oculta");
        Undo(false);if(!doc.Layers[2].Visible)throw new Exception("Deshacer visibilidad");
        canvas.SelectTool("Cuentagotas");canvas.UsePointTool(new Point(4,4));if(canvas.Ink.ToArgb()!=Color.Yellow.ToArgb())throw new Exception("Cuentagotas toma composición");
        string path=Path.Combine(Path.GetTempPath(),"pixelart-layers-"+Guid.NewGuid()+".pixelart");
        try{
            doc.Layers[2].Visible=false;doc.Save(path);
            using(Document loaded=Document.Load(path)){if(loaded.Layers.Count!=3 || loaded.ActiveLayer!=2 || loaded.Layers[1].Name!="Personaje" || loaded.Layers[2].Visible || loaded.Layers[2].Pixels.GetPixel(4,4).ToArgb()!=Color.Yellow.ToArgb())throw new Exception("Guardar todas las capas, incluso ocultas");}
            XmlDocument legacy=doc.Serialize();foreach(XmlNode layer in legacy.DocumentElement.SelectNodes("layer"))legacy.DocumentElement.RemoveChild(layer);legacy.DocumentElement.SetAttribute("version","1");
            using(Document old=Document.Deserialize(legacy.DocumentElement)){if(old.Layers.Count!=1 || old.Layers[0].Name!="Fondo" || old.Image.GetPixel(4,4).ToArgb()!=Color.Red.ToArgb())throw new Exception("Importar proyecto antiguo como Fondo");}
            SaveSession(path+".session");using(Editor restored=new Editor(true)){restored.Show();restored.LoadSession(path+".session");if(restored.doc.Layers.Count!=3 || restored.doc.ActiveLayer!=2 || restored.doc.Layers[2].Visible)throw new Exception("Recuperar sesión con capas");}
        }finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".session"))File.Delete(path+".session");}
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    public void TestLockedLayers(){
        SetDocument(new Document(8,8));doc.Image.SetPixel(1,1,Color.Red);
        SetLayerLocked(true);if(doc.Editable || !lockLayer.Checked)throw new Exception("Bloquear capa activa");
        int history=undo.Count;
        canvas.TestBlockedStroke();canvas.SelectBounds(new Rectangle(1,1,2,2));
        canvas.BeginSelection(new Point(1,1));canvas.MoveSelectionTo(new Point(4,4));canvas.EndSelectionGesture();
        canvas.RotateSelection(RotateFlipType.Rotate90FlipNone);canvas.ResizeSelection(4,4);canvas.DeleteSelection();
        using(Bitmap paste=new Bitmap(2,2)){paste.SetPixel(0,0,Color.Blue);canvas.PasteSelection(paste);canvas.CommitSelection();}
        SetTool("Cubeta");SetColor(Color.Blue);canvas.UsePointTool(new Point(1,1));
        SetTool("Rectángulo");canvas.ShapeClick(Point.Empty);canvas.ShapeClick(new Point(7,7));DeleteLayer();
        if(undo.Count!=history || doc.Image.GetPixel(1,1).ToArgb()!=Color.Red.ToArgb() || doc.Image.GetPixel(0,0).A!=0 || doc.Layers.Count!=1)throw new Exception("Contenido bloqueado protegido");
        using(Document loaded=Document.Deserialize(doc.Serialize().DocumentElement))if(!loaded.Layers[0].Locked || loaded.Editable)throw new Exception("Guardar bloqueo");
        XmlDocument legacy=doc.Serialize();((XmlElement)legacy.DocumentElement.SelectSingleNode("layer")).RemoveAttribute("locked");using(Document loaded=Document.Deserialize(legacy.DocumentElement))if(!loaded.Editable)throw new Exception("Compatibilidad sin atributo bloqueo");
        Undo(false);if(!doc.Editable)throw new Exception("Deshacer bloqueo");Undo(true);if(doc.Editable)throw new Exception("Rehacer bloqueo");
        AddLayer();if(!doc.Editable)throw new Exception("Nueva capa editable sobre referencia");doc.Image.SetPixel(1,1,Color.Blue);history=undo.Count;MergeLayer();if(doc.Layers.Count!=2 || undo.Count!=history)throw new Exception("Proteger capa inferior bloqueada");
        SetLayerLocked(true);SetLayerLocked(false);if(!doc.Editable)throw new Exception("Desbloquear capa");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
    public void TestAdvancedLayers(){
        SetDocument(new Document(8,8));using(Graphics g=Graphics.FromImage(doc.Image))g.Clear(Color.Blue);
        AddLayer();using(Graphics g=Graphics.FromImage(doc.Image))g.Clear(Color.Red);SetLayerOpacity(50);
        using(Bitmap composite=doc.Composite()){Color c=composite.GetPixel(4,4);if(c.A!=255 || Math.Abs(c.R-127)>2 || Math.Abs(c.B-128)>2)throw new Exception("Composición al 50%");}
        if(doc.Image.GetPixel(4,4).ToArgb()!=Color.Red.ToArgb())throw new Exception("Opacidad no altera píxeles fuente");
        DuplicateLayer();if(doc.Layers.Count!=3 || doc.Layers[2].Opacity!=50)throw new Exception("Duplicar conserva opacidad");doc.Image.SetPixel(0,0,Color.Green);if(doc.Layers[1].Pixels.GetPixel(0,0).ToArgb()!=Color.Red.ToArgb())throw new Exception("Copia independiente");
        DeleteLayer();if(doc.Layers.Count!=2)throw new Exception("Eliminar capa");Undo(false);if(doc.Layers.Count!=3 || doc.Image.GetPixel(0,0).ToArgb()!=Color.Green.ToArgb())throw new Exception("Deshacer eliminación");Undo(true);
        using(Bitmap before=doc.Composite()){MergeLayer();using(Bitmap after=doc.Composite())for(int y=0;y<8;y++)for(int x=0;x<8;x++){Color a=before.GetPixel(x,y),b=after.GetPixel(x,y);if(Math.Abs(a.R-b.R)>1 || Math.Abs(a.G-b.G)>1 || Math.Abs(a.B-b.B)>1 || Math.Abs(a.A-b.A)>1)throw new Exception("Combinar conserva apariencia");}}
        if(doc.Layers.Count!=1 || doc.Layers[0].Opacity!=100)throw new Exception("Combinar produce capa al 100%");Undo(false);if(doc.Layers.Count!=2 || doc.Layers[1].Opacity!=50)throw new Exception("Deshacer combinación");
        ReorderLayer(0);if(doc.ActiveLayer!=0 || doc.Layers[0].Opacity!=50)throw new Exception("Reordenar a posición de arrastre");Undo(false);
        XmlDocument saved=doc.Serialize();using(Document reopened=Document.Deserialize(saved.DocumentElement))if(reopened.Layers[1].Opacity!=50)throw new Exception("Guardar opacidad");
        SetLayerOpacity(0);using(Bitmap composite=doc.Composite())if(composite.GetPixel(4,4).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Opacidad cero");Undo(false);
        DeleteLayer();DeleteLayer();if(doc.Layers.Count!=1 || doc.Image.GetPixel(0,0).A!=0)throw new Exception("Eliminar última capa deja una vacía");Undo(false);if(doc.Image.GetPixel(0,0).ToArgb()!=Color.Blue.ToArgb())throw new Exception("Recuperar última capa");
        foreach(string tool in new string[]{"Lápiz","Borrador","Cubeta","Cuentagotas","Rectángulo","Círculo","Triángulo"})if(ToolCursors.For(tool).Handle==IntPtr.Zero || ToolCursors.For(tool).HotSpot!=new Point(4,4))throw new Exception("Cursor válido y punto exacto");
        if(ToolCursors.For("Rectángulo")!=ToolCursors.For("Círculo"))throw new Exception("Cursor compartido para figuras");
        SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}




