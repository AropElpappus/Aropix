// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
namespace PixelArt {
partial class Editor {
    static void PersistCheck(bool value,string message){if(!value)throw new Exception("Persistencia: "+message);}
    public void TestPersistence(){
        string root=Path.Combine(Path.GetTempPath(),"aropix-storage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            string local=Path.Combine(root,"local"),legacy=Path.Combine(root,"application");Directory.CreateDirectory(legacy);
            InternalStore store=new InternalStore(local,legacy);PersistCheck(!Directory.Exists(local),"primera ejecución aislada");store.EnsureDirectory();PersistCheck(Directory.Exists(local),"crear directorio");
            using(Editor writer=new Editor(true)){
                writer.Show();writer.internalStore=store;writer.SetDocument(new Document(8,8){Name="Anterior"});writer.doc.Image.SetPixel(1,1,Color.Red);writer.MarkDirty();
                string oldPreferences=Path.Combine(legacy,"preferencias.xml"),oldSession=Path.Combine(legacy,"sesion.xml"),oldRecovery=Path.Combine(legacy,"recuperacion-automatica.xml");
                writer.canvas.CoordinateMode=2;writer.SaveSettings(oldPreferences);writer.SaveSession(oldSession);DateTime old=DateTime.UtcNow.AddHours(-3);File.SetLastWriteTimeUtc(oldPreferences,old);File.SetLastWriteTimeUtc(oldSession,old);
                string original=InternalStore.Fingerprint(oldSession);store.BeforeWrite=delegate(string path){if(Path.GetDirectoryName(path)==legacy)throw new UnauthorizedAccessException("Carpeta de instalación protegida (simulada)");};
                using(Editor reader=new Editor(true)){reader.Show();reader.internalStore=store;reader.LoadInternalSettings();PersistCheck(reader.canvas.CoordinateMode==2 && File.Exists(reader.SettingsPath),"migrar preferencias desde instalación sin escribir allí");PersistCheck(reader.RestoreLatestSession() && reader.doc.Name=="Anterior","migrar sesión");}
                PersistCheck(File.Exists(oldSession) && InternalStore.Fingerprint(oldSession)==original,"original intacto");DateTime migrated=File.GetLastWriteTimeUtc(store.PathFor("sesion.xml"));
                using(Editor reader=new Editor(true)){reader.internalStore=store;PersistCheck(reader.RestoreLatestSession(),"repetir migración");}PersistCheck(File.GetLastWriteTimeUtc(store.PathFor("sesion.xml"))==migrated,"migración idempotente");
                store.BeforeWrite=null;writer.doc.Name="Recuperación reciente";writer.SaveSession(oldRecovery);File.SetLastWriteTimeUtc(oldRecovery,DateTime.UtcNow.AddHours(-1));
                writer.doc.Name="Sesión nueva pero anterior";writer.SaveSession(writer.SessionPath);File.SetLastWriteTimeUtc(writer.SessionPath,DateTime.UtcNow.AddHours(-2));
                using(Editor reader=new Editor(true)){reader.internalStore=store;PersistCheck(reader.RestoreLatestSession() && reader.doc.Name=="Recuperación reciente","elegir recuperación antigua ubicación más reciente que sesión local");}
                writer.doc.Name="Última recuperación local";writer.SaveSession(writer.RecoveryPath);
                File.WriteAllText(writer.SessionPath,"<session incompleta");File.SetLastWriteTimeUtc(writer.SessionPath,DateTime.UtcNow.AddMinutes(1));
                using(Editor reader=new Editor(true)){reader.internalStore=store;PersistCheck(reader.RestoreLatestSession() && reader.doc.Name=="Última recuperación local","sesión dañada con recuperación válida");}PersistCheck(store.Warnings.Count>0,"informar archivo dañado");
                writer.canvas.CoordinateMode=1;writer.SaveSettings(writer.SettingsPath);writer.LoadInternalSettings();PersistCheck(writer.canvas.CoordinateMode==1,"preferencias nuevas más recientes conservadas");
                File.WriteAllText(writer.SettingsPath,"<preferences corrupta");writer.LoadInternalSettings();PersistCheck(writer.canvas.CoordinateMode==2 && File.Exists(oldPreferences),"preferencias dañadas con respaldo antiguo");
                XmlDocument sample=new XmlDocument();sample.LoadXml("<test value='old'/>");string atomic=Path.Combine(root,"atomic.xml");store.Write(atomic,sample);string atomicBefore=File.ReadAllText(atomic);
                try{InternalStore.AtomicWrite(atomic,delegate(Stream stream){stream.WriteByte(60);throw new IOException("Espacio insuficiente simulado");});throw new Exception("Fallo no propagado");}catch(IOException){}
                PersistCheck(File.ReadAllText(atomic)==atomicBefore && Directory.GetFiles(root,"*.writing").Length==0,"fallo parcial conserva anterior y limpia temporal");
                using(FileStream locked=new FileStream(atomic,FileMode.Open,FileAccess.Read,FileShare.None)){try{store.Write(atomic,sample);throw new Exception("Archivo bloqueado no detectado");}catch(IOException){}}
                PersistCheck(File.ReadAllText(atomic)==atomicBefore,"sustitución bloqueada conserva archivo");
                writer.dirty=false;writer.StoreActive();store.BeforeWrite=delegate(string path){if(path==writer.SettingsPath)throw new UnauthorizedAccessException("Preferencias sin permisos");};int warnings=0;
                PersistCheck(writer.PersistClose(delegate{warnings++;},delegate{return false;}) && warnings>0,"fallo preferencias permite cierre");
                store.BeforeWrite=null;writer.doc.Image.SetPixel(2,2,Color.Blue);writer.MarkDirty();writer.SaveSession(writer.RecoveryPath);string preserved=InternalStore.Fingerprint(writer.RecoveryPath);
                store.BeforeWrite=delegate(string path){if(path==writer.SessionPath)throw new IOException("Sesión sin espacio");};
                PersistCheck(!writer.PersistClose(delegate{},delegate{return false;}) && InternalStore.Fingerprint(writer.RecoveryPath)==preserved,"pérdida potencial requiere decisión y conserva recuperación");
                writer.discardedOnExit.Add(writer.activeProject);PersistCheck(writer.PersistClose(delegate{},delegate{return false;}),"descarte explícito permite cierre aun sin sesión");
                using(Editor reader=new Editor(true)){reader.internalStore=store;PersistCheck(!reader.RestoreLatestSession(),"no resucitar copias descartadas antiguas o nuevas");}
                store.BeforeWrite=null;writer.discardedOnExit.Clear();writer.SaveSession(writer.RecoveryPath);
                using(Editor reader=new Editor(true)){reader.internalStore=store;PersistCheck(reader.RestoreLatestSession(),"nuevo snapshot no se confunde con uno descartado");}
                // Several projects: cancel and failed explicit save retain changes.
                writer.SetDocument(new Document(8,8){Name="A"});writer.doc.Image.SetPixel(0,0,Color.Red);writer.MarkDirty();ProjectState first=writer.activeProject;
                writer.AddProject(new Document(8,8){Name="B"},null);writer.doc.Image.SetPixel(1,1,Color.Blue);writer.MarkDirty();ProjectState second=writer.activeProject;
                int prompts=0;PersistCheck(!writer.ConfirmAllProjects(delegate{prompts++;return prompts==1?DialogResult.No:DialogResult.Cancel;}) && writer.discardedOnExit.Count==0 && writer.activeProject==second,"cancelar cierre múltiple revoca descartes");
                PersistCheck(!writer.ConfirmAllProjects(delegate{return DialogResult.Yes;},delegate{return false;}) && writer.dirty,"guardar fallido no descarta cambios");
                string document=Path.Combine(root,"propio.pixelart");first.Doc.Save(document);first.Path=document;first.Doc.Image.SetPixel(0,0,Color.Green);
                PersistCheck(writer.ConfirmAllProjects(delegate{return DialogResult.No;}),"descarte explícito de varios proyectos");
                writer.SaveSession(oldRecovery);File.SetLastWriteTimeUtc(oldRecovery,DateTime.UtcNow.AddDays(1)); // Future timestamp must not resurrect a retired snapshot.
                PersistCheck(writer.PersistClose(delegate{},delegate{return false;}),"cierre seguro con copia antigua futura");
                using(Editor reader=new Editor(true)){reader.internalStore=store;PersistCheck(reader.RestoreLatestSession() && reader.projects.Count==1 && reader.doc.Image.GetPixel(0,0).ToArgb()==Color.Red.ToArgb(),"descartar mantiene versión guardada y excluye borrador");}
                using(Document reopened=Document.Load(document))PersistCheck(reopened.Image.GetPixel(0,0).ToArgb()==Color.Red.ToArgb(),"pixelart original no alterado");
                PersistCheck(File.Exists(oldRecovery),"recuperación anterior conservada físicamente");
                writer.discardedOnExit.Clear();writer.ActivateProject(first);writer.SetDocument(new Document(8,8){Name="Archivo seguro"});writer.projectPath=document;writer.doc.Save(document);writer.dirty=false;writer.StoreActive();
                // Other tabs were explicitly discarded; only the safe named tab is retained.
                foreach(ProjectState state in writer.projects)if(state!=writer.activeProject)writer.discardedOnExit.Add(state);
                store.BeforeWrite=delegate(string path){if(path==writer.SessionPath)throw new UnauthorizedAccessException("Sesión bloqueada");};bool prompted=false;
                PersistCheck(writer.PersistClose(delegate{},delegate{prompted=true;return false;}) && !prompted,"fallo de pestañas sin pérdida no bloquea cierre");
                File.Delete(document);writer.discardedOnExit.Clear();PersistCheck(!writer.PersistClose(delegate{},delegate{return false;}),"documento desaparecido requiere proteger recuperación");
                store.BeforeWrite=null;
            }
        }finally{
            // Only the uniquely-created isolated test root is removed.
            string full=Path.GetFullPath(root),tempRoot=Path.GetFullPath(Path.GetTempPath());if(full.StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("aropix-storage-"))Directory.Delete(full,true);
        }
    }
}
}
