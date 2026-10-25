// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace PixelArt {
class EditorCommand {
    public bool RequiresDocument=true;public string Text,Tooltip;public Action Run;public Func<bool> Enabled,Checked;
    public readonly List<ToolStripItem> Items=new List<ToolStripItem>();
}
class AropixColors : ProfessionalColorTable { public override Color MenuBorder {get{return AropixTheme.Border;}} public override Color ToolStripBorder {get{return AropixTheme.Border;}} public override Color SeparatorDark {get{return AropixTheme.Border;}} public override Color SeparatorLight {get{return AropixTheme.Raised;}}
    public override Color ToolStripDropDownBackground {get{return AropixTheme.Raised;}}
    public override Color ImageMarginGradientBegin {get{return AropixTheme.Raised;}}
    public override Color ImageMarginGradientMiddle {get{return AropixTheme.Raised;}}
    public override Color ImageMarginGradientEnd {get{return AropixTheme.Raised;}}
    public override Color MenuItemSelected {get{return Color.FromArgb(54,76,83);}}
    public override Color MenuItemSelectedGradientBegin {get{return MenuItemSelected;}}
    public override Color MenuItemSelectedGradientEnd {get{return MenuItemSelected;}}
    public override Color MenuItemPressedGradientBegin {get{return MenuItemSelected;}}
    public override Color MenuItemPressedGradientEnd {get{return MenuItemSelected;}}
}
partial class Editor {
    readonly Dictionary<string,EditorCommand> commands=new Dictionary<string,EditorCommand>();
    MenuStrip mainMenus;ToolStrip quickBar;
    EventHandler uiIdle;
    void RegisterCommand(string id,string text,Action run,Func<bool> enabled=null,Func<bool> check=null){commands[id]=new EditorCommand{Text=text,Tooltip=HelpCatalog.CommandTooltip(id,text),Run=run,Enabled=enabled,Checked=check};}
    bool CanRun(string id){EditorCommand command;return commands.TryGetValue(id,out command) && (!command.RequiresDocument || doc!=null) && (command.Enabled==null || command.Enabled());}
    void ExecuteCommand(string id){if(!CanRun(id))return;commands[id].Run();SyncCommands();}
    bool TextInputFocused(){Control control=this;while(control!=null){if(control is TextBoxBase || control is NumericUpDown || control is ComboBox)return true;Control child=null;foreach(Control candidate in control.Controls)if(candidate.ContainsFocus){child=candidate;break;}control=child;}return false;}
    static string ShortcutLabel(string id){switch(id){case "new":return "Ctrl+N";case "open":return "Ctrl+O";case "save":return "Ctrl+S";case "saveas":return "Ctrl+Shift+S";case "undo":return "Ctrl+Z";case "redo":return "Ctrl+Y";case "copy":return "Ctrl+C";case "paste":return "Ctrl+V";case "toolhelp":return "F1";default:return null;}} ToolStripMenuItem CommandItem(string id){EditorCommand command=commands[id];ToolStripMenuItem item=new ToolStripMenuItem(command.Text){ForeColor=Color.White};string shortcut=ShortcutLabel(id);if(shortcut!=null)item.ShortcutKeyDisplayString=shortcut;item.Click+=delegate{ExecuteCommand(id);};command.Items.Add(item);return item;}
    ToolStripMenuItem MenuGroup(ToolStripItemCollection target,string text){ToolStripMenuItem menu=new ToolStripMenuItem(text){ForeColor=Color.White};target.Add(menu);menu.DropDown.Renderer=new AropixRenderer();((ToolStripDropDownMenu)menu.DropDown).ShowItemToolTips=true;return menu;}
    void AddCommands(ToolStripItemCollection target,params string[] ids){foreach(string id in ids)if(id=="-")target.Add(new ToolStripSeparator());else target.Add(CommandItem(id));}
    bool HasEditableSelection(){return doc!=null && doc.Editable && !canvas.Selection.IsEmpty;}
    void BuildMainMenus(Panel header,bool preview){
        InitializeAutoSave(preview);
        RegisterCommand("new","Nuevo",CreateProject);RegisterCommand("open","Abrir…",OpenProject);RegisterCommand("save","Guardar",delegate{SaveProject(false);});RegisterCommand("saveas","Guardar como…",delegate{SaveProject(true);});
        RegisterCommand("import","Importar imagen…",ImportImage,delegate{return doc.Layers.Count<16;});RegisterCommand("export","Exportar PNG…",Export);RegisterCommand("exportlayers","Exportar capas como PNG…",ExportLayers);
        RegisterCommand("autotoggle","Activar guardado automático",delegate{autoSaveEnabled=!autoSaveEnabled;ApplyAutoSave();PersistAutoSave();},null,delegate{return autoSaveEnabled;});RegisterCommand("autoconfig","Configurar…",ConfigureAutoSave);
        RegisterCommand("undo","Deshacer",delegate{Undo(false);},delegate{return undo.Count>0 || canvas.HasPendingSelection || canvas.PendingPoints>0;});RegisterCommand("redo","Rehacer",delegate{Undo(true);},delegate{return redo.Count>0 && !canvas.HasPendingSelection && canvas.PendingPoints==0;});
        RegisterCommand("copy","Copiar",CopyPixels,delegate{return !canvas.Selection.IsEmpty;});RegisterCommand("paste","Pegar",PastePixels,delegate{return copiedSelection!=null && doc.Editable;});
        RegisterCommand("wand","Varita mágica",delegate{SetTool("Varita mágica");});RegisterCommand("select","Selección rectangular",delegate{SetTool("Selección");});RegisterCommand("deselect","Deseleccionar",delegate{canvas.CommitSelection();canvas.CancelSelection();},delegate{return !canvas.Selection.IsEmpty;});RegisterCommand("deletepixels","Borrar área seleccionada",delegate{canvas.DeleteSelection();},HasEditableSelection);
        RegisterCommand("confirm","Confirmar",delegate{canvas.CommitSelection();},delegate{return canvas.HasPendingSelection && doc.Editable;});RegisterCommand("cancel","Cancelar",delegate{canvas.CancelSelection();},delegate{return canvas.HasPendingSelection;});
        RegisterCommand("rotateR","Girar 90° a la derecha",delegate{canvas.RotateSelection(RotateFlipType.Rotate90FlipNone);},HasEditableSelection);RegisterCommand("rotateL","Girar 90° a la izquierda",delegate{canvas.RotateSelection(RotateFlipType.Rotate270FlipNone);},HasEditableSelection);
        RegisterCommand("flipH","Voltear horizontalmente",delegate{canvas.RotateSelection(RotateFlipType.RotateNoneFlipX);},HasEditableSelection);RegisterCommand("flipV","Voltear verticalmente",delegate{canvas.RotateSelection(RotateFlipType.RotateNoneFlipY);},HasEditableSelection);RegisterCommand("resizeSelection","Cambiar tamaño…",ResizeSelected,HasEditableSelection);
        RegisterCommand("resizeCanvas","Cambiar tamaño del lienzo…",ChangeCanvasSize);
        RegisterCommand("layernew","Nueva capa",AddLayer,delegate{return doc.Layers.Count<16;});RegisterCommand("layerrename","Renombrar capa…",RenameLayer);RegisterCommand("layerduplicate","Duplicar capa",DuplicateLayer,delegate{return doc.Layers.Count<16;});
        RegisterCommand("layerup","Subir capa",delegate{MoveLayer(1);},delegate{return doc.ActiveLayer<doc.Layers.Count-1;});RegisterCommand("layerdown","Bajar capa",delegate{MoveLayer(-1);},delegate{return doc.ActiveLayer>0;});RegisterCommand("layerdelete","Eliminar capa",DeleteLayer,delegate{return !doc.Layers[doc.ActiveLayer].Locked;});
        RegisterCommand("layershadow","Crear sombra…",ConfigureShadow,CanCreateShadow);RegisterCommand("layermerge","Combinar con la inferior",MergeLayer,delegate{return doc.ActiveLayer>0 && doc.Editable && !doc.Layers[doc.ActiveLayer-1].Locked && doc.Layers[doc.ActiveLayer-1].Visible;});
        RegisterCommand("symV","Simetría vertical (izquierda ↔ derecha)",delegate{doc.MirrorVertical=!doc.MirrorVertical;MarkDirty();canvas.Invalidate();},null,delegate{return doc.MirrorVertical;});RegisterCommand("symH","Simetría horizontal (arriba ↔ abajo)",delegate{doc.MirrorHorizontal=!doc.MirrorHorizontal;MarkDirty();canvas.Invalidate();},null,delegate{return doc.MirrorHorizontal;});RegisterCommand("axes","Posición de los ejes…",ConfigureAxes);RegisterCommand("centeraxes","Centrar ambos ejes",delegate{SetAxes(null,null);});
        RegisterCommand("grid","Cuadrícula",delegate{canvas.Grid=!canvas.Grid;canvas.Invalidate();},null,delegate{return canvas.Grid;});RegisterCommand("fit","Ajustar vista",delegate{canvas.Fit();});
        string[] positions={"No mostrar","En la barra inferior","Junto al cursor"};for(int i=0;i<3;i++){int mode=i;RegisterCommand("coords"+i,positions[i],delegate{canvas.CoordinateMode=mode;UpdateCoordinates();canvas.Invalidate();},null,delegate{return canvas.CoordinateMode==mode;});}
        ToolPanel[] panels={basicsPanel,shapesPanel,colorPanel,layersPanel};string[] panelNames={"Herramientas","Opciones de herramienta","Color","Capas"};for(int i=0;i<4;i++){ToolPanel panel=panels[i];RegisterCommand("panel"+i,panelNames[i],delegate{if(panel.IsHidden)panel.Reveal();else panel.HidePanel();},null,delegate{return !panel.IsHidden;});}
        RegisterCommand("animations","Animaciones de interfaz",delegate{UiMotion.Enabled=!UiMotion.Enabled;},null,delegate{return UiMotion.Enabled;});
        RegisterCommand("resetlayout","Restablecer distribución de paneles",ResetPanelLayout);
        RegisterCommand("help","Centro de Ayuda",delegate{OpenHelp("welcome");});RegisterCommand("toolhelp","Ayuda de la herramienta actual",OpenToolHelp);RegisterCommand("shortcuts","Atajos de teclado",delegate{OpenHelp("shortcuts");});RegisterCommand("about","Acerca de Aropix",delegate{OpenHelp("about");});
        foreach(string id in new string[]{"new","open","autotoggle","autoconfig","coords0","coords1","coords2","panel0","panel1","panel2","panel3","animations","resetlayout","help","toolhelp","shortcuts","about"})commands[id].RequiresDocument=false;mainMenus=new MenuStrip{Dock=DockStyle.Top,BackColor=PanelColor,ForeColor=Color.White,Renderer=new AropixRenderer()};MainMenuStrip=mainMenus;
        ToolStripMenuItem file=MenuGroup(mainMenus.Items,"Archivo");AddCommands(file.DropDownItems,"new","open","save","saveas","-","import","export","exportlayers","-");AddCommands(MenuGroup(file.DropDownItems,"Guardado automático").DropDownItems,"autotoggle","autoconfig");
        AddCommands(MenuGroup(mainMenus.Items,"Editar").DropDownItems,"undo","redo","-","copy","paste");ToolStripMenuItem selection=MenuGroup(mainMenus.Items,"Selección");AddCommands(selection.DropDownItems,"select","wand","deselect","deletepixels","-");ToolStripMenuItem transforms=MenuGroup(selection.DropDownItems,"Transformar selección");AddCommands(transforms.DropDownItems,"rotateR","rotateL","flipH","flipV","resizeSelection");selection.DropDownOpening+=delegate{transforms.Enabled=HasEditableSelection();};
        AddCommands(MenuGroup(mainMenus.Items,"Lienzo").DropDownItems,"resizeCanvas");AddCommands(MenuGroup(mainMenus.Items,"Capas").DropDownItems,"layernew","layerrename","layerduplicate","layerup","layerdown","layerdelete","layermerge","layershadow","-","import");AddCommands(MenuGroup(MenuGroup(mainMenus.Items,"Dibujo").DropDownItems,"Simetría").DropDownItems,"symV","symH","axes","centeraxes");
        ToolStripMenuItem view=MenuGroup(mainMenus.Items,"Ver");AddCommands(view.DropDownItems,"grid","fit","-");AddCommands(MenuGroup(view.DropDownItems,"Coordenadas").DropDownItems,"coords0","coords1","coords2");view.DropDownItems.Add(new ToolStripSeparator());AddCommands(MenuGroup(view.DropDownItems,"Paneles").DropDownItems,"panel0","panel1","panel2","panel3");AddCommands(view.DropDownItems,"resetlayout","-","animations");AddCommands(MenuGroup(mainMenus.Items,"Ayuda").DropDownItems,"help","toolhelp","-","shortcuts","about");
        quickBar=new ToolStrip{Dock=DockStyle.Fill,GripStyle=ToolStripGripStyle.Hidden,BackColor=PanelColor,ForeColor=Color.White,Renderer=new AropixRenderer()};
        foreach(string id in new string[]{"new","open","save","undo","redo","grid","export"}){if(id=="undo" || id=="grid" || id=="export")quickBar.Items.Add(new ToolStripSeparator());string key=id;ToolStripButton button=new ToolStripButton(commands[id].Text.Replace("…","")){ForeColor=Color.White,ToolTipText=HelpCatalog.CommandTooltip(id,commands[id].Text),AutoToolTip=false,DisplayStyle=ToolStripItemDisplayStyle.Text,Margin=new Padding(3,2,3,2)};button.Click+=delegate{ExecuteCommand(key);};commands[id].Items.Add(button);quickBar.Items.Add(button);}
        ConnectLayerMenu();header.Controls.Add(quickBar);header.Controls.Add(mainMenus);uiIdle=delegate{SyncCommands();};Application.Idle+=uiIdle;Disposed+=delegate{Application.Idle-=uiIdle;};
    }
    void SyncCommands(){if(IsDisposed)return;foreach(KeyValuePair<string,EditorCommand> pair in commands){bool enabled=CanRun(pair.Key),check=pair.Value.Checked!=null && (!pair.Value.RequiresDocument || doc!=null) && pair.Value.Checked();foreach(ToolStripItem item in pair.Value.Items){if(item.IsDisposed)continue;item.Enabled=enabled;string hint=pair.Value.Tooltip+(enabled?"":"\nNo disponible: "+UnavailableReason(pair.Key));if(item.ToolTipText!=hint)item.ToolTipText=hint;item.AccessibleDescription=enabled?pair.Value.Text:UnavailableReason(pair.Key);ToolStripMenuItem menu=item as ToolStripMenuItem;if(menu!=null && pair.Value.Checked!=null)menu.Checked=check;ToolStripButton button=item as ToolStripButton;if(button!=null && pair.Value.Checked!=null)button.Checked=check;}}SyncContextUI();}
    void SyncContextUI(){RefreshContextBar();foreach(KeyValuePair<Button,string> pair in actionButtons)if(!pair.Key.IsDisposed)pair.Key.Enabled=CanRun(pair.Value);}
    void ResetPanelLayout(){RestoreDefaultLayout();}
}
}



