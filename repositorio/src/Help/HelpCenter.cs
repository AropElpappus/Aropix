// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
namespace PixelArt {
class HelpEntry {
    public string Id,Name,Category,Icon,Description,Steps,Options,Shortcuts,Example,Tip,Keywords;
    public string[] Related=new string[0];
}
static partial class HelpCatalog {
    public static readonly List<HelpEntry> Entries=Build();
    public static HelpEntry Find(string id){return Entries.Find(delegate(HelpEntry e){return e.Id==id;})??Entries[0];}
    public static string Normalize(string text){StringBuilder b=new StringBuilder();foreach(char c in (text??"").Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)b.Append(char.ToLowerInvariant(c));return b.ToString().Normalize(NormalizationForm.FormC);}
    public static List<HelpEntry> Search(string query){string[] terms=Normalize(query).Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);return Entries.FindAll(delegate(HelpEntry e){string hay=Normalize(e.Name+" "+e.Category+" "+e.Description+" "+e.Keywords+" "+e.Shortcuts+" "+e.Options);foreach(string term in terms)if(!hay.Contains(term))return false;return true;});}
    public static string ToolId(string tool){foreach(HelpEntry e in Entries)if(e.Icon==tool && e.Name==tool)return e.Id;return tool=="Selección"?"selection":"welcome";}
    public static string Tooltip(string id){HelpEntry e=Find(id);return e.Name+"\n"+e.Description+(string.IsNullOrEmpty(e.Shortcuts)?"":"\n"+e.Shortcuts);}
    public static string CommandTooltip(string id,string fallback){string topic=id=="undo" || id=="redo"?"history":id=="saveas"?"save":id=="layermerge"?"merge":id=="layershadow"?"shadow":id=="resizeCanvas"?"canvasresize":id=="fit"?"zoom":id;foreach(HelpEntry e in Entries)if(e.Id==topic)return Tooltip(topic);return fallback;}
}
class HelpIcon : Control {
    public string Icon;
    public HelpIcon(string icon){Icon=icon;Size=new Size(48,48);DoubleBuffered=true;BackColor=Editor.Accent;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.TranslateTransform(8,8);using(Pen pen=new Pen(Color.White,1.8f)){if(!string.IsNullOrEmpty(Icon)){ToolTile.DrawIcon(e.Graphics,pen,Icon);return;}e.Graphics.DrawRectangle(pen,3,4,26,24);e.Graphics.DrawLine(pen,16,4,16,28);for(int y=10;y<=22;y+=6){e.Graphics.DrawLine(pen,7,y,12,y);e.Graphics.DrawLine(pen,20,y,25,y);}}}
}
class HelpCenter : ThemedWindow {
    readonly TreeView navigation=new TreeView();
    readonly TextBox search=new TextBox();
    readonly FlowLayoutPanel content=new FlowLayoutPanel();
    readonly Stack<string> previous=new Stack<string>();
    readonly Button back=Editor.Button("← Atrás",90);
    readonly Label results=new Label();
    readonly string settingsPath;
    public string CurrentId {get;private set;}
    bool updating;
    public HelpCenter(string path){
        settingsPath=path;Text="Centro de Ayuda — Aropix";Size=new Size(1060,760);MinimumSize=new Size(800,560);StartPosition=FormStartPosition.CenterParent;BackColor=Editor.PanelColor;ForeColor=Color.White;Font=new Font("Segoe UI",10);KeyPreview=true;
        TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
        TableLayoutPanel top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1};top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.Controls.Add(top,0,0);
        top.Controls.Add(back,0,0);back.Click+=delegate{GoBack();};Button home=Editor.Button("Inicio",90);home.Click+=delegate{Navigate("welcome");};top.Controls.Add(home,1,0);top.Controls.Add(new Label{Text="Buscar",AutoSize=true,Margin=new Padding(3,9,0,0)},2,0);search.Dock=DockStyle.Fill;search.Margin=new Padding(3,5,3,0);search.AccessibleName="Buscar en la ayuda";top.Controls.Add(search,3,0);
        SplitContainer split=new SplitContainer{Dock=DockStyle.Fill,SplitterWidth=6,BackColor=Editor.PanelColor};root.Controls.Add(split,0,1);split.Size=new Size(1000,650);split.SplitterDistance=250;split.Panel1MinSize=240;split.Panel2MinSize=400;
        results.Dock=DockStyle.Top;results.Height=40;results.ForeColor=Color.Silver;navigation.Dock=DockStyle.Fill;navigation.BackColor=Color.FromArgb(28,33,39);navigation.ForeColor=Color.White;navigation.BorderStyle=BorderStyle.None;navigation.HideSelection=false;navigation.FullRowSelect=true;navigation.ItemHeight=27;navigation.AccessibleName="Categorías y resultados de ayuda";split.Panel1.Controls.Add(navigation);split.Panel1.Controls.Add(results);
        content.Dock=DockStyle.Fill;content.AutoScroll=true;content.FlowDirection=FlowDirection.TopDown;content.WrapContents=false;content.Padding=new Padding(14,0,12,14);content.BackColor=Color.FromArgb(24,28,33);split.Panel2.Controls.Add(content);
        content.SizeChanged+=delegate{SizeCards();};search.TextChanged+=delegate{BuildNavigation();};navigation.AfterSelect+=delegate(object sender,TreeViewEventArgs e){if(updating)return;if(e.Node.Tag is string)Navigate((string)e.Node.Tag);else e.Node.Expand();};
        BuildNavigation();Navigate("welcome");LoadBounds();FormClosed+=delegate{SaveBounds();};
    }
    void BuildNavigation(){updating=true;navigation.BeginUpdate();navigation.Nodes.Clear();List<HelpEntry> entries=HelpCatalog.Search(search.Text);Dictionary<string,TreeNode> categories=new Dictionary<string,TreeNode>();foreach(HelpEntry e in entries){TreeNode category;if(!categories.TryGetValue(e.Category,out category)){category=new TreeNode(e.Category);categories[e.Category]=category;navigation.Nodes.Add(category);}category.Nodes.Add(new TreeNode(e.Name){Tag=e.Id,ToolTipText=e.Description});}navigation.ShowNodeToolTips=true;navigation.EndUpdate();results.Text=string.IsNullOrWhiteSpace(search.Text)?"Explora las categorías":""+entries.Count+" resultados"+(entries.Count==0?" · Prueba otra palabra":"");if(!string.IsNullOrWhiteSpace(search.Text))navigation.ExpandAll();else if(navigation.Nodes.Count>0)navigation.Nodes[0].Expand();updating=false;}
    public void Navigate(string id){HelpEntry entry=HelpCatalog.Find(id);if(CurrentId!=null && CurrentId!=entry.Id)previous.Push(CurrentId);CurrentId=entry.Id;Render(entry);back.Enabled=previous.Count>0;}
    public void GoBack(){if(previous.Count==0)return;CurrentId=previous.Pop();Render(HelpCatalog.Find(CurrentId));back.Enabled=previous.Count>0;}
    void TextBlock(Control parent,string text,int size,Color color,bool bold){if(string.IsNullOrWhiteSpace(text))return;Label label=new Label{Text=text,AutoSize=true,Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color,Margin=new Padding(12,8,12,8),UseMnemonic=false};parent.Controls.Add(label);}
    void Card(string title,string text){if(string.IsNullOrWhiteSpace(text))return;FlowLayoutPanel card=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Editor.PanelColor,Margin=new Padding(0,0,0,12),Padding=new Padding(0,2,0,6)};TextBlock(card,title,11,Color.FromArgb(91,218,211),true);TextBlock(card,text,10,Color.White,false);content.Controls.Add(card);}
    void Render(HelpEntry entry){
        content.SuspendLayout();while(content.Controls.Count>0){Control child=content.Controls[0];content.Controls.RemoveAt(0);child.Dispose();}content.AutoScrollPosition=Point.Empty;
        TextBlock(content,entry.Category.ToUpperInvariant(),9,Color.Silver,false);content.Controls.Add(new HelpIcon(entry.Icon){Margin=new Padding(12,4,12,4)});TextBlock(content,entry.Name,22,Color.White,true);TextBlock(content,entry.Description??"Esta entrada todavía no tiene descripción.",11,Color.White,false);
        if(entry.Id=="welcome"){
            FlowLayoutPanel links=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=new Padding(0,0,0,12)};
            foreach(string id in new[]{"first","pencil","rectangle","selection","color","layernew","zoom","symv","new","shortcuts"}){HelpEntry target=HelpCatalog.Find(id);Button button=Editor.Button(id=="first"?"Primeros pasos":target.Category,205);button.Height=40;button.Tag=id;button.Click+=delegate{Navigate((string)button.Tag);};links.Controls.Add(button);}content.Controls.Add(links);
        }
        if(!string.IsNullOrWhiteSpace(entry.Steps)){string[] steps=entry.Steps.Split('|');StringBuilder text=new StringBuilder();for(int i=0;i<steps.Length;i++)text.Append((i+1)+". "+steps[i]+(i+1<steps.Length?"\n\n":""));Card("Cómo utilizarlo",text.ToString());}
        Card("Opciones disponibles",entry.Options);Card("Atajos y controles",entry.Shortcuts);Card("Ejemplo práctico",entry.Example);Card("Consejo",entry.Tip);
        if(entry.Related!=null && entry.Related.Length>0){Card("Continúa explorando","Abre una función relacionada:");foreach(string id in entry.Related){HelpEntry target=HelpCatalog.Find(id);LinkLabel link=new LinkLabel{Text=target.Name,Tag=target.Id,AutoSize=true,LinkColor=Color.FromArgb(91,218,211),ActiveLinkColor=Color.White,VisitedLinkColor=Color.FromArgb(91,218,211),Margin=new Padding(12,4,12,8)};link.LinkClicked+=delegate{Navigate((string)link.Tag);};content.Controls.Add(link);}}
        content.ResumeLayout();SizeCards();content.AutoScrollPosition=Point.Empty;
        updating=true;foreach(TreeNode category in navigation.Nodes)foreach(TreeNode node in category.Nodes)if((string)node.Tag==entry.Id){navigation.SelectedNode=node;node.EnsureVisible();}updating=false;
    }
    void SizeCards(){int width=Math.Max(300,content.ClientSize.Width-content.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-4);foreach(Control c in content.Controls){FlowLayoutPanel card=c as FlowLayoutPanel;if(card!=null){card.MinimumSize=Size.Empty;card.MaximumSize=new Size(width,0);card.MinimumSize=new Size(width,0);card.Width=width;foreach(Control label in card.Controls)if(label is Label)label.MaximumSize=new Size(width-28,0);}else if(c is Label)c.MaximumSize=new Size(width-28,0);}}
    void LoadBounds(){if(string.IsNullOrEmpty(settingsPath))return;try{XmlDocument xml=new XmlDocument();xml.Load(settingsPath);XmlElement r=xml.DocumentElement;int x=int.Parse(r.GetAttribute("x")),y=int.Parse(r.GetAttribute("y")),w=int.Parse(r.GetAttribute("width")),h=int.Parse(r.GetAttribute("height"));Rectangle wanted=new Rectangle(x,y,w,h);if(w<800 || h<560 || w>10000 || h>10000)return;foreach(Screen screen in Screen.AllScreens)if(Rectangle.Intersect(screen.WorkingArea,wanted).Width>=200 && Rectangle.Intersect(screen.WorkingArea,wanted).Height>=100){Rectangle a=screen.WorkingArea;Size=new Size(Math.Min(w,a.Width),Math.Min(h,a.Height));StartPosition=FormStartPosition.Manual;Location=new Point(Math.Max(a.Left,Math.Min(x,a.Right-Width)),Math.Max(a.Top,Math.Min(y,a.Bottom-Height)));break;}}catch(Exception ex){if(!(ex is IOException || ex is XmlException || ex is FormatException || ex is OverflowException || ex is UnauthorizedAccessException || ex is NullReferenceException))throw;}}
    void SaveBounds(){if(string.IsNullOrEmpty(settingsPath))return;try{Rectangle r=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;XmlDocument xml=new XmlDocument();XmlElement e=xml.CreateElement("helpWindow");xml.AppendChild(e);e.SetAttribute("x",r.X.ToString());e.SetAttribute("y",r.Y.ToString());e.SetAttribute("width",r.Width.ToString());e.SetAttribute("height",r.Height.ToString());InternalStore.AtomicWrite(settingsPath,delegate(Stream output){xml.Save(output);});}catch(IOException){}catch(UnauthorizedAccessException){}}
    protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==(Keys.Control|Keys.F)){search.Focus();return true;}if(keys==(Keys.Alt|Keys.Left)){GoBack();return true;}if(keys==Keys.Escape){Close();return true;}return base.ProcessCmdKey(ref msg,keys);}
    public void TestNavigation(){
        if(navigation.Nodes.Count!=10)throw new Exception("Categorías de ayuda");navigation.Nodes[1].Expand();navigation.Nodes[1].Collapse();if(navigation.Nodes[1].IsExpanded)throw new Exception("Contraer categoría");
        Navigate("pencil");Navigate("shadow");GoBack();if(CurrentId!="pencil")throw new Exception("Atrás en ayuda");
        foreach(HelpEntry entry in HelpCatalog.Entries){Navigate(entry.Id);Application.DoEvents();if(content.Controls.Count<6)throw new Exception("Contenido incompleto: "+entry.Id);foreach(string id in entry.Related)if(HelpCatalog.Find(id).Id!=id)throw new Exception("Enlace inexistente: "+id);}
        search.Text="sombra";if(navigation.Nodes.Count==0)throw new Exception("Buscar sombra");search.Text="zzzxxyy";if(navigation.Nodes.Count!=0 || !results.Text.Contains("0 resultados"))throw new Exception("Sin resultados");search.Clear();
        Navigate("welcome");foreach(Control control in content.Controls){LinkLabel link=control as LinkLabel;if(link!=null){Navigate((string)link.Tag);break;}}if(CurrentId!="first")throw new Exception("Enlace de inicio");
        HelpEntry incomplete=new HelpEntry{Id="incomplete",Name="Prueba",Category="Prueba"};Render(incomplete);if(content.Controls.Count<4)throw new Exception("Entrada incompleta tolerada");
        Size=new Size(800,560);Navigate("shadow");Application.DoEvents();if(content.HorizontalScroll.Visible || content.Width<350 || content.PointToScreen(new Point(content.Width,0)).X>PointToScreen(new Point(ClientSize.Width,0)).X)throw new Exception("Ayuda en ventana mínima");CaptureHelp("ayuda-compacta.png");
        Size=new Size(1060,760);Navigate("welcome");Application.DoEvents();CaptureHelp("ayuda-inicio.png");Navigate("shadow");Application.DoEvents();CaptureHelp("ayuda-sombra.png");search.Text="rueda";Application.DoEvents();CaptureHelp("ayuda-busqueda.png");search.Clear();
    }
    void CaptureHelp(string name){using(Bitmap image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(TestArtifacts.DirectoryPath,name));}}
}
partial class Editor {
    HelpCenter helpCenter;
    void OpenHelp(string id){if(helpCenter==null || helpCenter.IsDisposed)helpCenter=new HelpCenter(previewEditor?null:internalStore.PathFor("ayuda-preferencias.xml"));helpCenter.Navigate(id);if(!helpCenter.Visible)helpCenter.Show(this);if(helpCenter.WindowState==FormWindowState.Minimized)helpCenter.WindowState=FormWindowState.Normal;helpCenter.Activate();}
    void OpenToolHelp(){OpenHelp(HelpCatalog.ToolId(canvas.Tool));}
    public void TestHelpCenter(){
        foreach(string query in new[]{"sombra","rueda","transparencia","PNG","rotar","borrar","Alt","CIRCULO"})if(HelpCatalog.Search(query).Count==0)throw new Exception("Búsqueda: "+query);
        if(HelpCatalog.Search("círculo").Count!=HelpCatalog.Search("circulo").Count)throw new Exception("Normalizar acentos");
        HashSet<string> ids=new HashSet<string>();foreach(HelpEntry entry in HelpCatalog.Entries)if(!ids.Add(entry.Id))throw new Exception("Identificador de ayuda duplicado");
        SetDocument(new Document(12,12));SetTool("Cubeta");string before=doc.Serialize().OuterXml;int historyCount=undo.Count;bool wasDirty=dirty;float zoom=canvas.Zoom;
        Message keyMessage=new Message();ProcessCmdKey(ref keyMessage,Keys.F1);Application.DoEvents();if(helpCenter.CurrentId!="bucket" || !Enabled || canvas.Tool!="Cubeta")throw new Exception("Ayuda contextual no modal");HelpCenter same=helpCenter;OpenHelp("welcome");if(helpCenter!=same)throw new Exception("Reutilizar ventana");
        helpCenter.TestNavigation();helpCenter.Close();if(before!=doc.Serialize().OuterXml || undo.Count!=historyCount || dirty!=wasDirty || canvas.Zoom!=zoom || canvas.Tool!="Cubeta")throw new Exception("Ayuda no cambia editor");
        SetTool("Lápiz");OpenHelp("welcome");Activate();canvas.Focus();canvas.PrecisionKey(Keys.Enter);if(undo.Count!=historyCount+1)throw new Exception("Editor funciona con ayuda abierta");helpCenter.Close();
        string path=Path.Combine(Path.GetTempPath(),"aropix-help-"+Guid.NewGuid()+".xml");try{using(HelpCenter help=new HelpCenter(path)){help.Show();help.Size=new Size(900,650);help.Close();}using(HelpCenter restored=new HelpCenter(path)){if(restored.Width!=900 || restored.Height!=650)throw new Exception("Persistir ventana ayuda");}File.WriteAllText(path,"<invalid");using(HelpCenter fallback=new HelpCenter(path))if(fallback.Width!=1060)throw new Exception("Preferencias de ayuda inválidas");}finally{if(File.Exists(path))File.Delete(path);}
        if(!tips.GetToolTip(toolButtons["Lápiz"]).Contains("B"))throw new Exception("Tooltip herramienta");SetDocument(new Document(100,100));SetTool("Lápiz");
    }
}
}
