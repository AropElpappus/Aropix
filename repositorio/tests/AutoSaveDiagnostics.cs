// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace PixelArt
{
    // Opt-in diagnostics only: the production autosave implementation is unchanged.
    static class AutoSaveDiagnostics
    {
        public static int Run(string[] args)
        {
            try
            {
                if (args.Length != 2) throw new ArgumentException("Expected scenario name.");
                Control.CheckForIllegalCrossThreadCalls = true;
                using (Editor editor = new Editor(true)) editor.MeasureAutoSave(args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath, "error.txt"), ex.ToString());
                return 1;
            }
        }

        public static Document Create(int size, int count, bool noise)
        {
            HistoryDiagnostics.Guard((long)size * size * count * 32 + 64 * HistoryDiagnostics.MiB);
            Document result = new Document(size, size);
            try
            {
                Random random = new Random(72819);
                for (int layer = 0; layer < count; layer++)
                {
                    if (layer > 0) result.Layers.Add(new Layer("Capa " + layer, new Bitmap(size, size, PixelFormat.Format32bppArgb)));
                    Bitmap bitmap = result.Layers[layer].Pixels;
                    int[] row = new int[size];
                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        for (int y = 0; y < size; y++)
                        {
                            for (int x = 0; x < size; x++)
                                row[x] = noise ? unchecked((int)0xff000000) | random.Next(0x1000000) :
                                    Color.FromArgb(layer == 0 ? 255 : 128, (x / 8 * 17 + layer * 11) % 256, (y / 8 * 13) % 256, (x / 32 + y / 32) % 2 * 200).ToArgb();
                            Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), size);
                        }
                    }
                    finally { bitmap.UnlockBits(data); }
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
    }

    partial class Editor
    {
        public void MeasureAutoSave(string scenario)
        {
            string root = TestArtifacts.DirectoryPath;
            internalStore = new InternalStore(root, Path.Combine(root, "unused"));
            if (scenario == "safety") { DiagnoseAutoSaveSafety(); return; }
            int size, layers, documents = 1;
            bool named = true, noise = false, clean = false;
            switch (scenario)
            {
                case "small": size = 64; layers = 1; break;
                case "medium": size = 512; layers = 4; break;
                case "large": size = 2048; layers = 4; break;
                case "noise": size = 1024; layers = 4; noise = true; break;
                case "multi": size = 512; layers = 4; documents = 3; break;
                case "unsaved": size = 1024; layers = 4; named = false; break;
                case "clean": size = 1024; layers = 4; clean = true; break;
                case "slow": size = 512; layers = 4; break;
                default: throw new ArgumentException("Unknown scenario.");
            }
            for (int i = 0; i < documents; i++)
            {
                Document next = AutoSaveDiagnostics.Create(size, layers, noise);
                if (i == 0) SetDocument(next); else AddProject(next, null);
                doc.Name = "Synthetic " + i;
                if (named) { projectPath = Path.Combine(root, "project-" + i + ".pixelart"); doc.Save(projectPath); }
                dirty = !clean; StoreActive();
            }
            // Create a dispatcher handle without showing a window or loading user settings.
            IntPtr handle = Handle;
            Application.DoEvents();
            double idleQueue = 0;
            Stopwatch idle = Stopwatch.StartNew();
            BeginInvoke((Action)delegate { idleQueue = idle.Elapsed.TotalMilliseconds; });
            Application.DoEvents();
            HistoryDiagnostics.Csv("autosave.csv", "scenario", "iteration", "size", "layers", "documents", "serialize_session_ms", "write_session_ms", "autosave_total_ms", "ui_queue_ms", "idle_queue_ms", "peak_private_delta_bytes", "peak_managed_delta_bytes", "private_after_gc_delta_bytes", "session_bytes", "simulated_delay_ms");
            for (int repeat = 1; repeat <= 3; repeat++)
            {
                foreach (ProjectState state in projects) state.Dirty = !clean;
                dirty = !clean; StoreActive();
                // Separate component measurement using the exact SaveSession implementation.
                double serialization = 0;
                Stopwatch components = Stopwatch.StartNew();
                internalStore.BeforeWrite = delegate { serialization = components.Elapsed.TotalMilliseconds; };
                SaveSession(RecoveryPath);
                double write = components.Elapsed.TotalMilliseconds - serialization;
                internalStore.BeforeWrite = scenario == "slow" ? (Action<string>)delegate { Thread.Sleep(500); } : null;
                HistoryDiagnostics.Collect();
                long beforePrivate = HistoryDiagnostics.PrivateBytes(), beforeManaged = GC.GetTotalMemory(false);
                long peakPrivate = beforePrivate, peakManaged = beforeManaged;
                using (ManualResetEvent stop = new ManualResetEvent(false))
                {
                    // Sampling thread never reads a document, bitmap or control.
                    Thread sampler = new Thread(delegate()
                    {
                        do
                        {
                            peakPrivate = Math.Max(peakPrivate, HistoryDiagnostics.PrivateBytes());
                            peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
                        } while (!stop.WaitOne(5));
                    });
                    sampler.IsBackground = true;
                    sampler.Start();
                    double queue = -1;
                    Stopwatch total = Stopwatch.StartNew();
                    BeginInvoke((Action)delegate { queue = total.Elapsed.TotalMilliseconds; });
                    double elapsed;
                    try { RunAutoSave(RecoveryPath); elapsed = total.Elapsed.TotalMilliseconds; }
                    finally { stop.Set(); sampler.Join(); internalStore.BeforeWrite = null; }
                    Application.DoEvents();
                    PersistCheck(queue >= elapsed, "dispatcher waits for synchronous autosave");
                    foreach (ProjectState state in projects)
                        PersistCheck(state.Dirty == (!named && !clean), "dirty flag after autosave");
                    HistoryDiagnostics.Collect();
                    HistoryDiagnostics.Csv("autosave.csv", scenario, repeat, size, layers, documents,
                        serialization, write, elapsed, queue, idleQueue, peakPrivate - beforePrivate,
                        peakManaged - beforeManaged, HistoryDiagnostics.PrivateBytes() - beforePrivate,
                        new FileInfo(RecoveryPath).Length, scenario == "slow" ? 500 : 0);
                }
            }
            using (Editor reader = new Editor(true))
            {
                reader.internalStore = internalStore;
                PersistCheck(reader.LoadSession(RecoveryPath) && reader.projects.Count == documents, "recovery roundtrip");
                for (int i = 0; i < documents; i++)
                    PersistCheck(reader.projects[i].Doc.Layers.Count == layers && reader.projects[i].Doc.Image.GetPixel(0, 0).ToArgb() == projects[i].Doc.Image.GetPixel(0, 0).ToArgb(), "stored synthetic pixels");
            }
            File.WriteAllText(Path.Combine(root, "passed.txt"), scenario + ": 3 autosaves; recovery reopened; dirty flags checked.");
            // Remove only this invocation's generated project payloads; retain CSV and results.
            foreach (ProjectState state in projects) if (state.Path != null) File.Delete(state.Path);
            File.Delete(RecoveryPath);
        }

        void DiagnoseAutoSaveSafety()
        {
            SetDocument(new Document(16, 16));
            projectPath = Path.Combine(internalStore.Root, "safe.pixelart");
            doc.Image.SetPixel(1, 1, Color.Red); MarkDirty(); RunAutoSave(RecoveryPath);
            string original = InternalStore.Fingerprint(projectPath);
            string recovery = InternalStore.Fingerprint(RecoveryPath);
            doc.Image.SetPixel(1, 1, Color.Blue); MarkDirty();
            internalStore.BeforeWrite = delegate { throw new UnauthorizedAccessException("Simulated denied recovery directory"); };
            try { RunAutoSave(RecoveryPath); throw new Exception("Expected denied write"); } catch (UnauthorizedAccessException) { }
            PersistCheck(dirty && InternalStore.Fingerprint(projectPath) == original && InternalStore.Fingerprint(RecoveryPath) == recovery, "failed recovery preserves both files and dirty");
            internalStore.BeforeWrite = null;
            using (FileStream locked = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { RunAutoSave(RecoveryPath); throw new Exception("Expected locked destination"); } catch (IOException) { }
            }
            PersistCheck(dirty && InternalStore.Fingerprint(projectPath) == original, "failed original write leaves dirty and previous original");
            using (Editor reader = new Editor(true))
            {
                reader.internalStore = internalStore;
                PersistCheck(reader.LoadSession(RecoveryPath) && reader.doc.Image.GetPixel(1, 1).ToArgb() == Color.Blue.ToArgb(), "recovery survives original failure");
            }
            RunAutoSave(RecoveryPath);
            PersistCheck(!dirty, "next interval retries successfully");
            recovery = InternalStore.Fingerprint(RecoveryPath);
            canvas.DiagnoseEditingGuards(delegate { RunAutoSave(RecoveryPath); PersistCheck(InternalStore.Fingerprint(RecoveryPath) == recovery, "editing postpones autosave"); });
            SetTool("Línea recta"); canvas.ShapeClick(new Point(2, 2));
            RunAutoSave(RecoveryPath);
            PersistCheck(InternalStore.Fingerprint(RecoveryPath) == recovery, "pending figure postpones autosave");
            canvas.CancelShape();
            canvas.BeginSelection(new Point(1, 1)); canvas.UpdateSelection(new Point(3, 3)); canvas.EndSelectionGesture();
            CopyPixels(); PastePixels(); RunAutoSave(RecoveryPath);
            PersistCheck(canvas.HasPendingSelection && InternalStore.Fingerprint(RecoveryPath) == recovery, "pending paste not committed");
            canvas.CancelSelection();
            closingPersistence = true;
            try { RunAutoSave(RecoveryPath); PersistCheck(InternalStore.Fingerprint(RecoveryPath) == recovery, "closing guard"); }
            finally { closingPersistence = false; }
            // A queued edit cannot run while the current synchronous save is writing.
            IntPtr handle = Handle;
            BeginInvoke((Action)delegate { doc.Image.SetPixel(1, 1, Color.Green); MarkDirty(); });
            RunAutoSave(RecoveryPath);
            PersistCheck(!dirty, "queued edit has not executed during save");
            Application.DoEvents(); PersistCheck(dirty, "later edit remains dirty");
            bool manualFinished = false, closeFinished = false;
            BeginInvoke((Action)delegate
            {
                doc.Image.SetPixel(2, 2, Color.Purple); MarkDirty();
                manualFinished = SaveProject(false);
                closeFinished = PersistClose(delegate(string warning) { throw new Exception(warning); }, delegate { return false; });
            });
            RunAutoSave(RecoveryPath);
            PersistCheck(!manualFinished && !closeFinished, "manual save and close wait for autosave");
            Application.DoEvents();
            PersistCheck(manualFinished && closeFinished && !dirty, "queued manual save and close persistence succeed");
            using (Document saved = Document.Load(projectPath)) PersistCheck(saved.Image.GetPixel(1, 1).ToArgb() == Color.Green.ToArgb(), "next interval captures later edit");
            // Refresh recovery after the close simulation retired earlier snapshots.
            RunAutoSave(RecoveryPath);
            File.WriteAllText(RecoveryPath + ".interrupted.writing", "<incomplete");
            using (Editor reader = new Editor(true))
            {
                reader.internalStore = internalStore;
                PersistCheck(reader.RestoreLatestSession() && reader.doc.Image.GetPixel(1, 1).ToArgb() == Color.Green.ToArgb(), "incomplete temporary ignored");
            }
            File.WriteAllText(Path.Combine(internalStore.Root, "passed.txt"), "Safety checks passed: denied recovery (simulated), locked original, retry, dirty flags, editing guards, pending figure/paste, close guard, queued edit/manual save/close persistence, incomplete temporary, recovery. See existing persistence suite for atomic partial-write and safe-close failure tests.");
        }
    }

    partial class PixelCanvas
    {
        internal void DiagnoseEditingGuards(Action check)
        {
            try { drawing = true; check(); } finally { drawing = false; }
            try { rightHeld = true; check(); } finally { rightHeld = false; }
            BeginSelection(new Point(0, 0));
            try { check(); } finally { CancelSelection(); }
        }
    }
}
