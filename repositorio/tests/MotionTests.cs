// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace PixelArt
{
    partial class Editor
    {
        public void TestMotionPanelsAndControls()
        {
            bool preference=UiMotion.Enabled;
            try
            {
                using(Editor view=new Editor(true))
                {
                    view.Show(); Application.DoEvents(); view.RestoreDefaultLayout();
                    UiMotion.Enabled=true; UiMotion.ManualClock=true; UiMotion.ManualTime=0;
                    int original=view.basicsPanel.Height; double weight=view.basicsPanel.DockWeight;
                    view.basicsPanel.Collapse(true);
                    if(view.basicsPanel.Height!=original || !view.basicsPanel.IsCollapsed)throw new Exception("Estado lógico inmediato y altura inicial conservada");
                    UiMotion.ManualTime=60;UiMotion.Advance();
                    if(view.basicsPanel.Height<=35 || view.basicsPanel.Height>=original)throw new Exception("Altura intermedia de plegado");
                    view.CaptureUI("animacion-panel-intermedia.png");
                    UiMotion.ManualTime=200;UiMotion.Advance();
                    if(view.basicsPanel.Height!=35 || view.basicsPanel.Content.Visible || view.basicsPanel.DockWeight!=weight)throw new Exception("Plegado conserva peso");
                    view.basicsPanel.Collapse(false); UiMotion.ManualTime=250;UiMotion.Advance();
                    view.basicsPanel.Collapse(true); UiMotion.ManualTime=500;UiMotion.Advance();
                    if(view.basicsPanel.Height!=35 || UiMotion.Count!=0)throw new Exception("Inversión rápida del plegado");
                    view.basicsPanel.Collapse(false);UiMotion.Enabled=false;
                    if(view.basicsPanel.Height!=original || UiMotion.Count!=0)throw new Exception("Desactivar completa desplegado");
                    UiMotion.Enabled=true;view.colorPanel.Collapse(true);view.colorPanel.DockTo(view.dockLeft);
                    if(UiMotion.Count!=0)throw new Exception("Reacoplar cancela geometría anterior");
                    view.RestoreDefaultLayout(); UiMotion.FinishAll();
                    int total=view.basicsPanel.Height+view.shapesPanel.Height;
                    view.ResizeDockPair(view.basicsPanel,view.shapesPanel,220,total);
                    if(view.basicsPanel.Height!=220)throw new Exception("Redimensionar después de animar");
                    using(AropixButton button=new AropixButton())
                    using(ToolTile tool=new ToolTile("Lápiz"))
                    {
                        view.Controls.Add(button);view.Controls.Add(tool);button.BringToFront();tool.BringToFront();
                        System.Reflection.MethodInfo enter=typeof(Control).GetMethod("OnMouseEnter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                        System.Reflection.MethodInfo leave=typeof(Control).GetMethod("OnMouseLeave",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                        for(int i=0;i<20;i++){enter.Invoke(button,new object[]{EventArgs.Empty});leave.Invoke(button,new object[]{EventArgs.Empty});enter.Invoke(tool,new object[]{EventArgs.Empty});leave.Invoke(tool,new object[]{EventArgs.Empty});}
                        if(UiMotion.Count!=2)throw new Exception("Una transición por control");
                        int canvasPaints=0;PaintEventHandler paint=delegate{canvasPaints++;};view.canvas.Paint+=paint;
                        Application.DoEvents();canvasPaints=0;
                        UiMotion.ManualTime+=50;UiMotion.Advance();button.Update();tool.Update();
                        view.canvas.Paint-=paint;
                        if(canvasPaints!=0)throw new Exception("Hover no repinta lienzo");
                        button.Dispose();tool.Dispose();if(UiMotion.Count!=0)throw new Exception("Controles destruidos sin transiciones huérfanas");
                    }
                }
            }
            finally{UiMotion.FinishAll();UiMotion.ManualClock=false;UiMotion.Enabled=preference;}
        }
        public void TestMotion()
        {
            bool preference = UiMotion.Enabled;
            try
            {
                UiMotion.Enabled = true;
                double value = -1;
                UiMotion.Start(this, 160, delegate(double p) { value = p; });
                if (value != 1 || UiMotion.Count != 0) throw new Exception("Modo de pruebas inmediato");
                UiMotion.ManualClock = true; UiMotion.ManualTime = 0;
                UiMotion.Start(this, 160, delegate(double p) { value = p; });
                UiMotion.ManualTime = 80; UiMotion.Advance();
                if (value <= 0 || value >= 1 || UiMotion.TimerRunning) throw new Exception("Progreso determinista sin temporizador");
                UiMotion.ManualTime = 200; UiMotion.Advance();
                if (value != 1 || UiMotion.Count != 0) throw new Exception("Finalización por tiempo transcurrido");
                UiMotion.Start(this, 160, delegate(double p) { value = p; });
                UiMotion.Start(this, 120, delegate(double p) { value = 2 + p; });
                if (UiMotion.Count != 1 || value != 2) throw new Exception("Reemplazar transición");
                UiMotion.Enabled = false;
                if (value != 3 || UiMotion.Count != 0) throw new Exception("Desactivar finaliza transiciones");
                UiMotion.Enabled = true;
                using (ThemedWindow window = new ThemedWindow())
                {
                    window.Show(this); Application.DoEvents();
                    if (window.Opacity >= 1) throw new Exception("Apertura progresiva");
                    window.Hide();
                    if (window.Opacity != 1 || UiMotion.Count != 0) throw new Exception("Ocultar libera transición");
                }
                using (ThemedWindow window = new ThemedWindow())
                {
                    window.Show(this); Application.DoEvents(); window.Dispose();
                    if (UiMotion.Count != 0) throw new Exception("Destruir libera transición");
                }
                for (int i = 0; i < 4; i++)
                {
                    int action = i, closings = 0;
                    using (NewProject dialog = new NewProject())
                    {
                        dialog.FormClosing += delegate { closings++; };
                        dialog.Shown += delegate
                        {
                            dialog.BeginInvoke((Action)delegate
                            {
                                if (action == 0) ((Button)dialog.AcceptButton).PerformClick();
                                else if (action == 1) ((Button)dialog.CancelButton).PerformClick();
                                else if (action == 2) typeof(Form).GetMethod("ProcessDialogKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(dialog, new object[] { Keys.Escape });
                                else dialog.Close();
                            });
                        };
                        DialogResult result = dialog.ShowDialog(this);
                        if (result != (action == 0 ? DialogResult.OK : DialogResult.Cancel) || closings != 1 || UiMotion.Count != 0)
                            throw new Exception("Cierre único durante apertura: " + action);
                    }
                }
                UiMotion.ManualClock = false;
                string path = Path.Combine(TestArtifacts.DirectoryPath, "motion-preferences.xml");
                UiMotion.Enabled = false; SaveSettings(path); UiMotion.Enabled = true; LoadSettings(path);
                if (UiMotion.Enabled) throw new Exception("Preferencia persistente");
                System.Xml.XmlDocument xml = new System.Xml.XmlDocument(); xml.Load(path);
                xml.DocumentElement.SetAttribute("animations", "inválido"); xml.Save(path); LoadSettings(path);
                if (!UiMotion.Enabled) throw new Exception("Preferencia inválida utiliza predeterminado");
            }
            finally
            {
                UiMotion.FinishAll(); UiMotion.ManualClock = false; UiMotion.Enabled = preference;
            }
        }
    }
}
