// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
namespace PixelArt {
partial class Editor {
    ToolTile figureButton;
    string lastFigure="Rectángulo";
    readonly Dictionary<string,List<Control>> optionGroups=new Dictionary<string,List<Control>>();
    Label optionsTitle;ComboBox pencilSizes,eraserSizes,circleCenter;CheckBox shapeFill;NumericUpDown shapeStroke;
    Label strokeTitle;bool syncingOptions;
    FlowLayoutPanel basicColors;
    Button recentToggle,basicToggle;
    bool recentExpanded,basicExpanded;
    Label opacityValue=new Label{Width=45,Height=28,TextAlign=ContentAlignment.MiddleLeft};
    Button layerMore;FlowLayoutPanel layerActions;
    readonly Dictionary<Button,string> actionButtons=new Dictionary<Button,string>();
    Panel canvasHost;
    TableLayoutPanel canvasArea;
    ToolStrip selectionContext;ToolStripButton confirmContext,cancelContext;ToolStripDropDownButton transformContext;
    Label autoSaveLabel=new Label{AutoEllipsis=true,TextAlign=ContentAlignment.MiddleRight};
    TableLayoutPanel statusLayout;
    void BuildCanvasHost(){canvasHost=new Panel{Dock=DockStyle.Fill};canvasArea=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0)};canvasArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));canvasArea.RowStyles.Add(new RowStyle(SizeType.Absolute,0));canvasArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));canvas.Margin=new Padding(0);canvasArea.Controls.Add(canvas,0,1);canvasHost.Controls.Add(canvasArea);workspace.Controls.Add(canvasHost,1,0);}
    void BuildSelectionContext(){
        selectionContext=new ToolStrip{Dock=DockStyle.Top,GripStyle=ToolStripGripStyle.Hidden,BackColor=Color.FromArgb(34,43,49),ForeColor=Color.White,Visible=false,Renderer=new AropixRenderer()};
        transformContext=new ToolStripDropDownButton("Transformar selección"){ForeColor=Color.White};AddCommands(transformContext.DropDownItems,"rotateR","rotateL","flipH","flipV","resizeSelection");selectionContext.Items.Add(transformContext);
        confirmContext=new ToolStripButton("✓ Confirmar"){ForeColor=Color.White,BackColor=Accent};cancelContext=new ToolStripButton("Cancelar"){ForeColor=Color.White};confirmContext.Click+=delegate{ExecuteCommand("confirm");canvas.Focus();};cancelContext.Click+=delegate{ExecuteCommand("cancel");canvas.Focus();};selectionContext.Items.Add(confirmContext);selectionContext.Items.Add(cancelContext);selectionContext.Dock=DockStyle.Fill;canvasArea.Controls.Add(selectionContext,0,0);
    }
    void RefreshContextBar(){
        if(selectionContext==null)return;bool visible=doc!=null && canvas.IsSelectionTool && !canvas.Selection.IsEmpty;canvasArea.RowStyles[0].Height=visible?30:0;selectionContext.Visible=visible;transformContext.Enabled=HasEditableSelection();confirmContext.Visible=cancelContext.Visible=canvas.HasPendingSelection;confirmContext.Enabled=CanRun("confirm");
        autoSaveLabel.Text=autoSaveEnabled?"Auto: activo · "+autoSaveSeconds+" s"+(dirty?" · Cambios pendientes":""):"Auto: desactivado";if(autoSaveStatus.StartsWith("Error:"))autoSaveLabel.Text="Autoguardado: error";tips.SetToolTip(autoSaveLabel,autoSaveStatus+(dirty?"\nEl proyecto tiene cambios sin guardar.":""));
        if(statusLayout!=null)statusLayout.ColumnStyles[1].Width=doc!=null && canvas.CoordinateMode==1?105:0;
    }
    void BuildStatusBar(TableLayoutPanel root){
        statusLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Padding=new Padding(6,0,6,0)};statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,162));statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
        status.Dock=DockStyle.Fill;status.AutoEllipsis=true;status.TextAlign=ContentAlignment.MiddleLeft;coordinates.Dock=DockStyle.Fill;coordinates.TextAlign=ContentAlignment.MiddleLeft;autoSaveLabel.Dock=DockStyle.Fill;autoSaveLabel.Font=new Font(AropixTheme.FontFamily,AropixTheme.CaptionSize);
        FlowLayoutPanel zoom=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};Button minus=AddButton(zoom,"−",27,delegate{canvas.ChangeZoom(-1);}),plus=AddButton(zoom,"+",27,delegate{canvas.ChangeZoom(1);}),fit=AddButton(zoom,"Ajustar",86,delegate{ExecuteCommand("fit");});minus.Height=plus.Height=fit.Height=26;minus.AccessibleName="Alejar lienzo";plus.AccessibleName="Acercar lienzo";fit.AccessibleName="Ajustar lienzo a la vista";
        statusLayout.Controls.Add(status,0,0);statusLayout.Controls.Add(coordinates,1,0);statusLayout.Controls.Add(zoom,2,0);statusLayout.Controls.Add(autoSaveLabel,3,0);root.Controls.Add(statusLayout,0,3);canvas.PointerChanged+=UpdateCoordinates;
    }
    void BuildCompactColor(){
        FlowLayoutPanel right=colorPanel.Content;
        current.Size=new Size(232,25);current.Cursor=Cursors.Hand;current.Click+=delegate{PickColor();};right.Controls.Add(current);tips.SetToolTip(current,HelpCatalog.Tooltip("currentcolor"));
        FlowLayoutPanel row=new FlowLayoutPanel{Width=238,Height=33,Margin=new Padding(0)};hex.Width=108;hex.MaxLength=7;hex.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){ApplyHex();e.SuppressKeyPress=true;}};hex.Leave+=delegate{ApplyHex();};row.Controls.Add(hex);Button choose=AddButton(row,"Elegir…",112,PickColor);choose.Height=26;right.Controls.Add(row);colorValidation=new Label{Text="Color inválido. Usa #RRGGBB (ejemplo: #65B879).",Width=232,Height=40,ForeColor=AropixTheme.Error,Visible=false};right.Controls.Add(colorValidation);hex.AccessibleName="Color hexadecimal RGB";hex.TextChanged+=delegate{colorValidation.Visible=false;hex.AccessibleDescription="Color en formato #RRGGBB.";};
        right.Controls.Add(new Label{Text="Paleta del proyecto",Width=232,Height=22});palette=SwatchPanel(60);right.Controls.Add(palette);Button add=AddButton(right,"+ Añadir color actual",232,delegate{if(doc.Palette.Count>=64){MessageBox.Show(this,"La paleta admite hasta 64 colores.");return;}if(!Contains(doc.Palette,canvas.Ink)){doc.Palette.Add(canvas.Ink);MarkDirty();RefreshColors();}});add.Height=26;tips.SetToolTip(palette,"Clic para elegir. Clic derecho para quitar de la paleta.");
        recentToggle=AddButton(right,"▸ Colores recientes",232,delegate{recentExpanded=!recentExpanded;RefreshColorSections();});recentToggle.Height=24;recent=SwatchPanel(72);right.Controls.Add(recent);
        basicToggle=AddButton(right,"▸ Colores básicos",232,delegate{basicExpanded=!basicExpanded;RefreshColorSections();});basicToggle.Height=24;basicColors=SwatchPanel(108);right.Controls.Add(basicColors);
        foreach(string value in new string[]{"#000000","#454B54","#818A96","#BAC2CC","#FFFFFF","#D94343","#F28C28","#F7D747","#65B879","#18834D","#00B7B7","#3E94E8","#3544A0","#854FC2","#E574AF","#794B36","#BA8257","#F0C8A0"})AddSwatch(basicColors,ColorTranslator.FromHtml(value),false);
        RefreshColorSections();
    }
    void RefreshColorSections(){if(recentToggle==null)return;recent.Visible=recentExpanded;basicColors.Visible=basicExpanded;recentToggle.Text=(recentExpanded?"▾ ":"▸ ")+"Colores recientes";basicToggle.Text=(basicExpanded?"▾ ":"▸ ")+"Colores básicos";}
    Button ActionButton(Control parent,string id,string text,int width){Button button=AddButton(parent,text,width,delegate{ExecuteCommand(id);});button.Height=27;actionButtons[button]=id;button.AccessibleName=text;return button;}
    void BuildLayerQuickActions(FlowLayoutPanel content){
        layerActions=new FlowLayoutPanel{Width=238,Height=34,Margin=new Padding(0)};content.Controls.Add(layerActions);Button add=ActionButton(layerActions,"layernew","+",32),duplicate=ActionButton(layerActions,"layerduplicate","×2",38),delete=ActionButton(layerActions,"layerdelete","−",32);tips.SetToolTip(add,HelpCatalog.Tooltip("layernew"));tips.SetToolTip(duplicate,HelpCatalog.Tooltip("layerduplicate"));tips.SetToolTip(delete,HelpCatalog.Tooltip("layerdelete"));((AropixButton)delete).Destructive=true;add.AccessibleName="Nueva capa";duplicate.AccessibleName="Duplicar capa";delete.AccessibleName="Eliminar capa";layerMore=AddButton(layerActions,"⋯",28,delegate{});layerMore.Height=27;
        FlowLayoutPanel opacityRow=new FlowLayoutPanel{Width=238,Height=32,Margin=new Padding(0)};opacityRow.Controls.Add(new Label{Text="Opacidad",Width=70,Height=28,TextAlign=ContentAlignment.MiddleLeft});opacityInput.Width=85;opacityValue.Width=20;opacityRow.Controls.Add(opacityInput);opacityRow.Controls.Add(opacityValue);content.Controls.Add(opacityRow);content.Controls.Add(lockLayer);
        opacityInput.ValueChanged+=delegate{if(!updatingLayers && doc!=null)SetLayerOpacity((int)opacityInput.Value);};lockLayer.CheckedChanged+=delegate{if(!updatingLayers && doc!=null)SetLayerLocked(lockLayer.Checked);};SetupLayerDrag();
        tips.SetToolTip(layerMore,"Más acciones de capa");tips.SetToolTip(lockLayer,"Protege los píxeles de la capa contra cambios accidentales.");
        layersPanel.SizeChanged+=delegate{ResizeLayerList();};
    }
    void ResizeLayerList(){if(layersPanel==null || layersPanel.IsCollapsed)return;layerList.Height=Math.Max(75,layersPanel.Content.ClientSize.Height-117);}
    void ConnectLayerMenu(){ContextMenuStrip menu=new ContextMenuStrip{Renderer=new AropixRenderer()};AddCommands(menu.Items,"layerrename","layerup","layerdown","layermerge","layershadow","-","layerdelete","-","import");layerMore.Click+=delegate{SyncCommands();menu.Show(layerMore,new Point(0,layerMore.Height));};layerList.ContextMenuStrip=menu;layerList.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right){SelectLayerContext(e.Location);}};menu.Opening+=delegate{SyncCommands();};Disposed+=delegate{menu.Dispose();};}
    void SelectLayerContext(Point point){int i=layerList.IndexFromPoint(point);if(i>=0)layerList.SelectedIndex=i;SyncCommands();}
    void AddOptionGroup(string key,params Control[] controls){optionGroups[key]=new List<Control>(controls);foreach(Control c in controls){c.Margin=new Padding(3,3,3,3);shapesPanel.Content.Controls.Add(c);}}
    Label OptionLabel(string text){return new Label{Text=text,Width=160,Height=22};}
    void BuildToolPanels(){
        basicsPanel=new ToolPanel(this,"Herramientas",150,dockLeft,dockRight,LayoutPanels);shapesPanel=new ToolPanel(this,"Opciones",300,dockLeft,dockRight,LayoutPanels);colorPanel=new ToolPanel(this,"Color",620,dockLeft,dockRight,LayoutPanels);
        basicsPanel.MinimumDockHeight=150;shapesPanel.MinimumDockHeight=100;colorPanel.MinimumDockHeight=160;colorPanel.DockWeight=480;FlowLayoutPanel tiles=new FlowLayoutPanel{Width=160,Height=90,Margin=new Padding(0),WrapContents=true};basicsPanel.Content.Controls.Add(tiles);
        foreach(string name in new string[]{"Lápiz","Borrador","Cubeta","Cuentagotas","Selección","Mover vista"}){string tool=name;ToolTile b=new ToolTile(name){IconOnly=true,Width=36,Height=36,Margin=new Padding(2),AccessibleName=name};b.HasMenu=tool=="Selección";b.Click+=delegate{if(tool=="Selección")ShowSelectionTools(b);else SetTool(tool);};tiles.Controls.Add(b);toolButtons[name]=b;tips.SetToolTip(b,HelpCatalog.Tooltip(HelpCatalog.ToolId(name)));}
        figureButton=new ToolTile("Triángulo"){IconOnly=true,Width=36,Height=36,Margin=new Padding(2),HasMenu=true,AccessibleName="Figuras geométricas"};tiles.Controls.Add(figureButton);ContextMenuStrip figures=new ContextMenuStrip{Renderer=new AropixRenderer()};
        foreach(string name in new string[]{"Rectángulo","Círculo","Triángulo","Línea recta"}){string tool=name;ToolStripMenuItem item=new ToolStripMenuItem(name){ForeColor=Color.White};item.Click+=delegate{SetTool(tool);};figures.Items.Add(item);figures.Opening+=delegate{item.Checked=canvas.Tool==tool;};}
        figureButton.Click+=delegate{figures.Show(figureButton,new Point(0,figureButton.Height));};tips.SetToolTip(figureButton,"Figuras ▾: rectángulo, círculo, triángulo y línea");Disposed+=delegate{figures.Dispose();};
        optionsTitle=OptionLabel("Lápiz");optionsTitle.Font=new Font(Font,FontStyle.Bold);shapesPanel.Content.Controls.Add(optionsTitle);
        pencilSizes=SizePicker();eraserSizes=SizePicker();pencilSizes.SelectedIndexChanged+=delegate{if(!syncingOptions){canvas.PencilSize=pencilSizes.SelectedIndex+1;UpdateStatus();canvas.Invalidate();}};eraserSizes.SelectedIndexChanged+=delegate{if(!syncingOptions){canvas.EraserSize=eraserSizes.SelectedIndex+1;UpdateStatus();canvas.Invalidate();}};
        AddOptionGroup("pencil",OptionLabel("Tamaño del lápiz"),pencilSizes);BuildPencilTips();AddOptionGroup("eraser",OptionLabel("Tamaño del borrador"),eraserSizes);
        shapeFill=new CheckBox{Text="Figura con relleno",Width=165,Height=26};shapeFill.CheckedChanged+=delegate{if(syncingOptions)return;canvas.FilledShapes=shapeFill.Checked;RefreshToolOptions();canvas.Invalidate();UpdateStatus();};AddOptionGroup("fill",shapeFill);
        strokeTitle=OptionLabel("Grosor del contorno");shapeStroke=new NumericUpDown{Minimum=1,Maximum=10,Value=1,Width=125};shapeStroke.ValueChanged+=delegate{if(syncingOptions)return;canvas.ShapeThickness=(int)shapeStroke.Value;canvas.Invalidate();UpdateStatus();};AddOptionGroup("stroke",strokeTitle,shapeStroke);
        circleCenter=new ComboBox{Width=165,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Color.White,ForeColor=Color.Black};circleCenter.Items.AddRange(new object[]{"1 × 1 — impar","2 × 2 — par"});circleCenter.SelectedIndex=0;circleCenter.SelectedIndexChanged+=delegate{if(syncingOptions)return;canvas.EvenCircleCenter=circleCenter.SelectedIndex==1;canvas.Invalidate();UpdateStatus();};AddOptionGroup("circle",OptionLabel("Centro del círculo"),circleCenter);
        toolHelp.Width=160;toolHelp.Height=125;toolHelp.ForeColor=Color.Silver;shapesPanel.Content.Controls.Add(toolHelp);refreshShapeOptions=RefreshToolOptions;
    }
    void RefreshToolOptions(){
        if(optionsTitle==null)return;syncingOptions=true;
        string tool=canvas.Tool;bool shape=canvas.IsShape,line=tool=="Línea recta";
        shapesPanel.Content.SuspendLayout();foreach(KeyValuePair<string,List<Control>> group in optionGroups){bool show=(group.Key=="pencil" || group.Key=="pencilTip")?tool=="Lápiz":group.Key=="eraser"?tool=="Borrador":group.Key=="fill"?shape && !line:group.Key=="stroke"?shape:tool=="Círculo";foreach(Control c in group.Value)c.Visible=show;}
        pencilSizes.SelectedIndex=canvas.PencilSize-1;eraserSizes.SelectedIndex=canvas.EraserSize-1;shapeFill.Checked=canvas.FilledShapes;shapeStroke.Value=canvas.ShapeThickness;circleCenter.SelectedIndex=canvas.EvenCircleCenter?1:0;shapeStroke.Enabled=strokeTitle.Enabled=line || !canvas.FilledShapes;strokeTitle.Text=line?"Grosor de la línea":"Grosor del contorno";optionsTitle.Text=tool;toolHelp.Visible=shape;
        shapesPanel.Content.ResumeLayout();syncingOptions=false;LayoutPanels();
        if(shape){lastFigure=tool;figureButton.Text="Triángulo";figureButton.AccessibleName="Figuras: "+tool;tips.SetToolTip(figureButton,HelpCatalog.Tooltip(HelpCatalog.ToolId(tool)));figureButton.Invalidate();}figureButton.BackColor=shape?Accent:Color.FromArgb(47,56,66);foreach(KeyValuePair<string,Button> pair in toolButtons)pair.Value.BackColor=pair.Key==tool?Accent:Color.FromArgb(47,56,66);
    }
}
}






