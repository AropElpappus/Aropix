// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
namespace PixelArt {
// No writes during construction. Tests substitute both roots and the fault hook.
class InternalStore {
    public readonly string Root,Legacy;
    public Action<string> BeforeWrite;
    public readonly List<string> Warnings=new List<string>();
    public InternalStore(string root,string legacy){Root=root;Legacy=legacy;}
    public static InternalStore ForUser(){return new InternalStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aropix"),AppDomain.CurrentDomain.BaseDirectory);}
    public string PathFor(string name){return Path.Combine(Root,name);}
    public void EnsureDirectory(){Directory.CreateDirectory(Root);}
    public static XmlDocument ReadXml(string path){XmlDocument xml=new XmlDocument();xml.XmlResolver=null;using(XmlReader reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=1073741824}))xml.Load(reader);return xml;}
    public void Write(string path,XmlDocument xml){if(BeforeWrite!=null)BeforeWrite(path);AtomicWrite(path,delegate(Stream output){xml.Save(output);});}
    public static void AtomicWrite(string path,Action<Stream> write){
        string destination=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(destination));string temp=destination+"."+Guid.NewGuid().ToString("N")+".writing";
        try{using(FileStream stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){write(stream);stream.Flush(true);}if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}
        finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    public List<string> Candidates(params string[] names){List<string> paths=new List<string>();Dictionary<string,DateTime> dates=new Dictionary<string,DateTime>();foreach(string name in names)foreach(string root in new[]{Root,Legacy}){string path=Path.Combine(root,name);try{if(File.Exists(path) && !paths.Contains(path)){dates[path]=File.GetLastWriteTimeUtc(path);paths.Add(path);}}catch(Exception ex){Warnings.Add("No se pudo examinar "+path+": "+ex.Message);}}paths.Sort(delegate(string a,string b){int order=dates[b].CompareTo(dates[a]);if(order!=0)return order;int priorityA=Path.GetDirectoryName(a)==Root?0:1,priorityB=Path.GetDirectoryName(b)==Root?0:1;order=priorityA.CompareTo(priorityB);return order!=0?order:string.Compare(a,b,StringComparison.OrdinalIgnoreCase);});return paths;}
    public static string Fingerprint(string path){using(SHA256 sha=SHA256.Create())using(FileStream stream=File.OpenRead(path))return Convert.ToBase64String(sha.ComputeHash(stream));}
    public HashSet<string> Ignored(){HashSet<string> result=new HashSet<string>();string path=PathFor("cierres.xml");if(!File.Exists(path))return result;try{XmlDocument xml=ReadXml(path);if(xml.DocumentElement.Name!="closed")throw new InvalidDataException("Registro de cierres inválido.");foreach(XmlElement item in xml.DocumentElement.SelectNodes("snapshot"))result.Add(item.GetAttribute("hash"));}catch(Exception ex){Warnings.Add("No se pudo leer el registro de cierres: "+ex.Message);}return result;}
    public HashSet<string> ExistingSnapshots(){HashSet<string> hashes=Ignored();foreach(string path in Candidates("sesion.xml","recuperacion-automatica.xml"))try{hashes.Add(Fingerprint(path));}catch(Exception ex){Warnings.Add("No se pudo identificar "+path+": "+ex.Message);}return hashes;}
    public void SaveIgnored(HashSet<string> hashes){XmlDocument xml=new XmlDocument();XmlElement root=xml.CreateElement("closed");xml.AppendChild(root);foreach(string hash in hashes){XmlElement node=xml.CreateElement("snapshot");node.SetAttribute("hash",hash);root.AppendChild(node);}Write(PathFor("cierres.xml"),xml);}
    public void Migrate(string source,string name){string target=PathFor(name);if(string.Equals(Path.GetFullPath(source),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))return;
        try{if(File.Exists(target) && Fingerprint(target)==Fingerprint(source))return;
            // Preserve any displaced file, including a corrupt one, for manual recovery.
            if(File.Exists(target))File.Copy(target,target+".anterior-"+Guid.NewGuid().ToString("N"));
            DateTime stamp=File.GetLastWriteTimeUtc(source);Write(target,ReadXml(source));File.SetLastWriteTimeUtc(target,stamp);
        }catch(Exception ex){Warnings.Add("Se cargó "+source+", pero no se pudo migrar: "+ex.Message);}
    }
    public string LatestSettings(string name,string rootName){string selected=null;foreach(string path in Candidates(name))try{XmlDocument xml=ReadXml(path);if(xml.DocumentElement==null || xml.DocumentElement.Name!=rootName || (rootName=="preferences" && xml.DocumentElement.GetAttribute("version")!="1"))throw new InvalidDataException("Formato no compatible.");if(selected==null)selected=path;}catch(Exception ex){Warnings.Add("Se conserva el archivo inválido "+path+": "+ex.Message);}if(selected!=null)Migrate(selected,name);return selected;}
}
partial class Editor {
    InternalStore internalStore=InternalStore.ForUser();
    bool closingPersistence;
    void LoadInternalSettings(){try{internalStore.EnsureDirectory();}catch(Exception ex){internalStore.Warnings.Add("No se pudo crear la carpeta de datos: "+ex.Message);}string settings=internalStore.LatestSettings("preferencias.xml","preferences");if(settings!=null)LoadSettings(settings);internalStore.LatestSettings("ayuda-preferencias.xml","helpWindow");}
    void ShowStorageWarnings(){if(internalStore.Warnings.Count==0)return;MessageBox.Show(this,string.Join("\n\n",internalStore.Warnings.ToArray()),"Archivos internos de Aropix",MessageBoxButtons.OK,MessageBoxIcon.Warning);internalStore.Warnings.Clear();}
    bool RestoreLatestSession(){
        HashSet<string> ignored=internalStore.Ignored();string selected=null;
        foreach(string path in internalStore.Candidates("sesion.xml","recuperacion-automatica.xml"))try{
            if(ignored.Contains(InternalStore.Fingerprint(path)))continue;
            XmlDocument xml=InternalStore.ReadXml(path);ValidateSession(xml);
            if(selected==null)selected=path;
        }catch(Exception ex){internalStore.Warnings.Add("No se pudo recuperar "+path+". Se conserva sin modificar.\n"+ex.Message);}
        if(selected==null)return false;
        bool restored=LoadSession(selected);internalStore.Migrate(selected,Path.GetFileName(selected));return restored;
    }
    static void ValidateSession(XmlDocument xml){XmlElement root=xml.DocumentElement;if(root==null || root.Name!="session" || root.GetAttribute("version")!="1")throw new InvalidDataException("Formato de sesión no compatible.");foreach(XmlElement tab in root.SelectNodes("tab"))using(Document document=Document.Deserialize(tab["pixelart"])){} }
    bool RecoveryOnlyWork(){foreach(ProjectState state in projects){if(discardedOnExit.Contains(state))continue;if(state.Dirty || string.IsNullOrEmpty(state.Path) || !File.Exists(state.Path))return true;try{using(Document saved=Document.Load(state.Path)){} }catch{return true;}}return false;}
    // Call only after project save/discard decisions. Injectable UI keeps tests isolated.
    bool PersistClose(Action<string> warn,Func<string,bool> allowLoss){
        StoreActive();HashSet<string> obsolete=internalStore.ExistingSnapshots();
        try{SaveSession(SessionPath);obsolete.Remove(InternalStore.Fingerprint(SessionPath));}catch(Exception ex){
            bool atRisk=RecoveryOnlyWork();
            if(atRisk && !allowLoss("No se pudo guardar la sesión. Hay cambios sin guardar que podrían perderse. ¿Cerrar de todos modos?\n\n"+ex.Message))return false;
            warn("No se pudo guardar la sesión; podrían no restaurarse las pestañas.\n"+ex.Message);
        }
        try{internalStore.SaveIgnored(obsolete);}catch(Exception ex){
            if(obsolete.Count>0 && discardedOnExit.Count>0 && !allowLoss("No se pudo registrar el cierre seguro. Las copias antiguas podrían reaparecer al iniciar, incluidos cambios descartados. ¿Cerrar de todos modos?\n\n"+ex.Message))return false;
            warn("No se pudo registrar el cierre: "+ex.Message);
        }
        // Keep recovery bytes; the successful ledger excludes only exact obsolete snapshots.
        try{SaveSettings(SettingsPath);}catch(Exception ex){warn("No se pudieron guardar las preferencias. Puedes cerrar; la distribución podría no conservarse.\n"+ex.Message);}
        return true;
    }
    void HandleSafeClose(FormClosingEventArgs e){
        closingPersistence=true;try{
            if(!ConfirmAllProjects()){e.Cancel=true;return;}
            if(!previewEditor)e.Cancel=!PersistClose(delegate(string text){MessageBox.Show(this,text,"Cierre de Aropix",MessageBoxButtons.OK,MessageBoxIcon.Warning);},delegate(string text){return MessageBox.Show(this,text,"Cierre de Aropix",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;});
        }catch(Exception ex){e.Cancel=MessageBox.Show(this,"No se pudo completar el cierre seguro. Si quedan cambios sin guardar podrían perderse. ¿Cerrar de todos modos?\n\n"+ex.Message,"Cierre de Aropix",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes;}finally{
            if(e.Cancel){discardedOnExit.Clear();if(!previewEditor)try{SaveSession(RecoveryPath);}catch(Exception ex){MessageBox.Show(this,"El cierre se canceló y tus proyectos siguen abiertos. No se pudo actualizar la copia de recuperación; guarda tus cambios manualmente.\n\n"+ex.Message,"Recuperación",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
            closingPersistence=false;
        }
    }
}
}
