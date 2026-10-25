// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace PixelArt {
partial class Editor {
    HashSet<ProjectState> discardedOnExit=new HashSet<ProjectState>();
    string SessionPath {get{return internalStore.PathFor("sesion.xml");}}
    public void SaveSession(string path){
        canvas.CommitSelection();StoreActive();XmlDocument xml=new XmlDocument();XmlElement root=xml.CreateElement("session");xml.AppendChild(root);root.SetAttribute("version","1");root.SetAttribute("snapshotId",Guid.NewGuid().ToString("N"));
        foreach(ProjectState state in projects){
            Document snapshot=state.Doc;bool temporary=false;
            if(discardedOnExit.Contains(state)){
                if(string.IsNullOrEmpty(state.Path) || !File.Exists(state.Path))continue;
                snapshot=Document.Load(state.Path);temporary=true;
            }
            try{
                XmlElement tab=xml.CreateElement("tab");root.AppendChild(tab);tab.SetAttribute("path",state.Path??"");tab.SetAttribute("dirty",(!temporary && state.Dirty).ToString());tab.SetAttribute("active",(state==activeProject).ToString());tab.SetAttribute("zoom",state.Zoom.ToString(CultureInfo.InvariantCulture));
                tab.SetAttribute("x",state.Origin.X.ToString(CultureInfo.InvariantCulture));tab.SetAttribute("y",state.Origin.Y.ToString(CultureInfo.InvariantCulture));
                tab.AppendChild(xml.ImportNode(snapshot.Serialize().DocumentElement,true));
            }finally{if(temporary)snapshot.Dispose();}
        }
        internalStore.Write(path,xml);
    }
    public bool LoadSession(string path){
        if(!File.Exists(path))return false;
        XmlDocument xml=new XmlDocument();xml.XmlResolver=null;using(XmlReader reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=1073741824}))xml.Load(reader);
        XmlElement root=xml.DocumentElement;if(root==null || root.Name!="session" || root.GetAttribute("version")!="1")throw new Exception("Formato de sesión no compatible.");
        List<ProjectState> loaded=new List<ProjectState>();ProjectState chosen=null;
        try{
            foreach(XmlElement tab in root.SelectNodes("tab")){
                Document document=Document.Deserialize(tab["pixelart"]);ProjectState state=new ProjectState{Doc=document,Path=tab.GetAttribute("path"),Dirty=tab.GetAttribute("dirty")=="True"};loaded.Add(state);
                if(state.Path.Length==0)state.Path=null;float zoom;state.Zoom=float.TryParse(tab.GetAttribute("zoom"),NumberStyles.Float,CultureInfo.InvariantCulture,out zoom) && !float.IsNaN(zoom)?Math.Max(0.125f,Math.Min(32,zoom)):4;
                float x,y;if(!float.TryParse(tab.GetAttribute("x"),NumberStyles.Float,CultureInfo.InvariantCulture,out x) || float.IsNaN(x) || float.IsInfinity(x))x=24;
                if(!float.TryParse(tab.GetAttribute("y"),NumberStyles.Float,CultureInfo.InvariantCulture,out y) || float.IsNaN(y) || float.IsInfinity(y))y=24;
                state.Origin=new PointF(Math.Max(-65536,Math.Min(65536,x)),Math.Max(-65536,Math.Min(65536,y)));
                state.Tab=new TabPage(document.Name+(state.Dirty?" *":"")){Tag=state};if(tab.GetAttribute("active")=="True")chosen=state;
            }
        }catch{foreach(ProjectState state in loaded){state.Doc.Dispose();if(state.Tab!=null)state.Tab.Dispose();}throw;}
        
        canvas.CancelSelection();canvas.CancelShape();StoreActive();changingTab=true;projectTabs.TabPages.Clear();foreach(ProjectState old in projects)ReleaseProject(old);projects.Clear();activeProject=null;doc=null;canvas.Doc=null;
        foreach(ProjectState state in loaded){projects.Add(state);projectTabs.TabPages.Add(state.Tab);}changingTab=false;
        if(loaded.Count==0)EnterEmptyWorkspace();else ActivateProject(chosen??loaded[0]);return true;
    }
    public void TestSession(){
        string path=Path.Combine(Path.GetTempPath(),"pixelart-session-"+Guid.NewGuid()+".xml");
        try{
            SetDocument(new Document(20,20){Name="Sesión A"});doc.Image.SetPixel(1,1,Color.Red);MarkDirty();canvas.Zoom=13;canvas.ViewOrigin=new PointF(-12,31);ProjectState first=activeProject;
            AddProject(new Document(30,40){Name="Sesión B"},"archivo-que-no-existe.pixelart");doc.Palette.Add(Color.Blue);canvas.Zoom=0.5f;canvas.ViewOrigin=new PointF(18,-22);StoreActive();ActivateProject(first);SaveSession(path);SaveSession(path);
            using(Editor restored=new Editor(true)){
                restored.Show();if(!restored.LoadSession(path) || restored.projects.Count!=2 || restored.doc.Name!="Sesión A" || restored.canvas.Zoom!=13 || restored.canvas.ViewOrigin!=new PointF(-12,31) || !restored.dirty || restored.doc.Image.GetPixel(1,1).ToArgb()!=Color.Red.ToArgb())throw new Exception("Recuperar sesión y pestaña activa");
                restored.ActivateProject(restored.projects[1]);if(restored.doc.Name!="Sesión B" || restored.canvas.Zoom!=0.5f || restored.canvas.ViewOrigin!=new PointF(18,-22) || restored.doc.Palette.Count!=1)throw new Exception("Orden, zoom y desplazamiento por proyecto");
            }
            discardedOnExit.Add(first);SaveSession(path);XmlDocument check=new XmlDocument();check.Load(path);if(check.DocumentElement.SelectNodes("tab").Count!=1)throw new Exception("No recuperar borrador descartado");discardedOnExit.Clear();
            first.Doc.Save(path+".pixelart");first.Path=path+".pixelart";first.Doc.Image.SetPixel(1,1,Color.Green);projectPath=first.Path;discardedOnExit.Add(first);SaveSession(path);
            using(Editor restored=new Editor(true)){restored.Show();restored.LoadSession(path);if(restored.projects[0].Doc.Image.GetPixel(1,1).ToArgb()!=Color.Red.ToArgb())throw new Exception("Cambios descartados no reaparecen");}discardedOnExit.Clear();
            ActivateProject(projects[1]);dirty=false;StoreActive();CloseProject();dirty=false;StoreActive();SetDocument(new Document(100,100));
        }finally{discardedOnExit.Clear();if(File.Exists(path))File.Delete(path);if(File.Exists(path+".pixelart"))File.Delete(path+".pixelart");}
    }
}
}



