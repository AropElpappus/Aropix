// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PixelArt
{
    partial class Editor
    {
        Panel welcome;
        Button welcomeNew, welcomeOpen;

        void BuildWelcome()
        {
            welcome = new Panel { Dock = DockStyle.Fill, BackColor = AropixTheme.Surface, AccessibleName = "Bienvenida a Aropix" };
            TableLayoutPanel card = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 5, BackColor = AropixTheme.Surface,
                Padding = new Padding(20), Margin = new Padding(0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            using(Stream stream = typeof(Editor).Assembly.GetManifestResourceStream("Aropix.Logo"))
            {
                if(stream != null)
                {
                    using(Image original = Image.FromStream(stream))
                    {
                        PictureBox logo = new PictureBox { Image = new Bitmap(original), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, AccessibleName = "Logo de Aropix" };
                        logo.Disposed += delegate { logo.Image.Dispose(); };
                        card.Controls.Add(logo, 0, 0);
                    }
                }
            }
            Label heading = new Label { Text = "Bienvenido a Aropix", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = AropixTheme.Text, Font = new Font(AropixTheme.FontFamily, 19, FontStyle.Bold) };
            Label description = new Label { Text = "Crea un nuevo proyecto o continúa trabajando en uno existente.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = AropixTheme.Secondary };
            TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            welcomeNew = Button("Crear nuevo proyecto\nCtrl + N", 220);
            welcomeOpen = Button("Abrir proyecto\nCtrl + O", 220);
            welcomeNew.Dock = welcomeOpen.Dock = DockStyle.Fill;
            welcomeNew.Margin = new Padding(0, 0, 6, 0); welcomeOpen.Margin = new Padding(6, 0, 0, 0);
            welcomeNew.BackColor = Accent;
            welcomeNew.AccessibleName = "Crear nuevo proyecto"; welcomeOpen.AccessibleName = "Abrir proyecto";
            welcomeNew.Click += delegate { ExecuteCommand("new"); };
            welcomeOpen.Click += delegate { ExecuteCommand("open"); };
            actions.Controls.Add(welcomeNew, 0, 0); actions.Controls.Add(welcomeOpen, 1, 0);
            card.Controls.Add(heading, 0, 1); card.Controls.Add(description, 0, 2); card.Controls.Add(actions, 0, 3);
            welcome.Controls.Add(card);
            Action arrange = delegate {
                float scale = Font.Size / AropixTheme.ControlSize;
                card.Size = new Size(Math.Max(1, Math.Min((int)(620 * scale), welcome.ClientSize.Width)), Math.Max(1, Math.Min((int)(370 * scale), welcome.ClientSize.Height)));
                card.Location = new Point((welcome.ClientSize.Width - card.Width) / 2, (welcome.ClientSize.Height - card.Height) / 2);
            };
            welcome.SizeChanged += delegate { arrange(); };
            canvasHost.Controls.Add(welcome); arrange();
        }

        // Empty workspace is a real state: no document, tab, image or project history.
        void EnterEmptyWorkspace()
        {
            activeProject = null; doc = null; canvas.Doc = null;
            projectPath = null; dirty = false;
            undo = new List<HistoryEntry>(); redo = new List<HistoryEntry>(); history = new List<Color>();
            canvas.CancelSelection(); canvas.CancelShape(); canvas.ClearPointer();
            Text = "Aropix";
            RefreshDocumentUI();
        }

        void RefreshDocumentUI()
        {
            bool available = doc != null;
            canvasArea.Visible = available; canvas.Enabled = available;
            welcome.Visible = !available && !closingPersistence;
            if(welcome.Visible)welcome.BringToFront();
            projectTabs.Visible = available;
            TableLayoutPanel root = projectTabs.Parent as TableLayoutPanel;
            if(root != null)root.RowStyles[1].Height = available ? 32 : 0;
            foreach(ToolPanel panel in new ToolPanel[] { basicsPanel, shapesPanel, colorPanel, layersPanel })panel.Content.Enabled = available;
            if(statusLayout != null)statusLayout.GetControlFromPosition(2, 0).Enabled = available;
            RefreshColors(); UpdateStatus(); UpdateCoordinates(); SyncCommands();
            canvas.Invalidate();
            if(!available && Visible && !closingPersistence)welcomeNew.Focus();
        }

        void FocusWorkspace()
        {
            if(doc != null)canvas.Focus();
            else if(welcomeNew != null && welcomeNew.Visible)welcomeNew.Focus();
        }

        void OpenProjectFile(string path)
        {
            try
            {
                foreach(ProjectState existing in projects)
                    if(string.Equals(existing == activeProject ? projectPath : existing.Path, path, StringComparison.OrdinalIgnoreCase)) { ActivateProject(existing); return; }
                Document next = Document.Load(path);
                AddProject(next, path);
            }
            catch(Exception ex) { Error(ex); }
        }
    }
}
