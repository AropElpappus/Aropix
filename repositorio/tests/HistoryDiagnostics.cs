// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace PixelArt
{
    // Diagnostic-only entry point. No change to the production history policy.
    static class HistoryDiagnostics
    {
        public const long MiB = 1048576;
        [StructLayout(LayoutKind.Sequential)]
        class MemoryStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage;
            public ulong TotalVirtual, AvailableVirtual, AvailableExtended;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);

        public static long PrivateBytes()
        {
            using (Process process = Process.GetCurrentProcess()) return process.PrivateMemorySize64;
        }
        public static void Guard(long extra)
        {
            MemoryStatus status = new MemoryStatus();
            if (!GlobalMemoryStatusEx(status)) throw new InvalidOperationException("No se pudo medir memoria disponible.");
            if (extra > 1024 * MiB || PrivateBytes() + extra > 1536 * MiB ||
                status.AvailablePhysical < (ulong)(extra + 512 * MiB) ||
                status.AvailablePage < (ulong)(extra + 512 * MiB))
                throw new InvalidOperationException("OMITIDO por límite de seguridad: 1536 MiB privados y reserva de 512 MiB.");
        }
        public static double Time(Action action)
        {
            Stopwatch watch = Stopwatch.StartNew();
            action();
            return watch.Elapsed.TotalMilliseconds;
        }
        public static double Median(List<double> samples)
        {
            samples.Sort();
            int count = samples.Count;
            return count % 2 == 1 ? samples[count / 2] : (samples[count / 2 - 1] + samples[count / 2]) / 2;
        }
        public static void Collect()
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        public static void Csv(string name, params object[] values)
        {
            List<string> text = new List<string>();
            foreach (object value in values) text.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
            File.AppendAllText(Path.Combine(TestArtifacts.DirectoryPath, name), string.Join(",", text.ToArray()) + "\n");
        }
        public static int Run(string[] args)
        {
            try
            {
                using (Editor editor = new Editor(true))
                {
                    if (args.Length == 2 && args[1] == "multi") editor.MeasureMultipleHistories();
                    else if (args.Length == 2 && args[1] == "budget") editor.MeasureHistoryBudget();
                    else
                    {
                        int size, layers;
                        if (args.Length != 3 || !int.TryParse(args[1], out size) || !int.TryParse(args[2], out layers) ||
                            Array.IndexOf(new[] { 64, 256, 512, 1024, 2048 }, size) < 0 ||
                            Array.IndexOf(new[] { 1, 4, 16 }, layers) < 0)
                            throw new ArgumentException("Use --history-diagnostic <64|256|512|1024|2048> <1|4|16> o multi.");
                        editor.MeasureHistory(size, layers);
                    }
                }
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath, "error.txt"), error.ToString());
                return 1;
            }
        }
    }

    partial class Editor
    {
        static long SnapshotBytes(List<HistoryEntry> entries)
        {
            long result = 0;
            foreach (HistoryEntry entry in entries) result += entry.Bytes;
            return result;
        }
        static Document DiagnosticDocument(int size, int count)
        {
            Document result = new Document(size, size);
            try
            {
                for (int i = 1; i < count; i++)
                    result.Layers.Add(new Layer("Diagnóstico " + i, new Bitmap(size, size, PixelFormat.Format32bppArgb)));
                // Touch all bitmap pages, not only a single transparent pixel.
                for (int i = 0; i < count; i++)
                    using (Graphics graphics = Graphics.FromImage(result.Layers[i].Pixels))
                        graphics.Clear(Color.FromArgb(128 + i, 20 + i, 40, 60));
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        public void MeasureHistory(int size, int layerCount)
        {
            long bytes = (long)size * size * 4 * layerCount;
            // Document + retained history + an in-flight undo clone; no parallel cases.
            HistoryDiagnostics.Guard(bytes * 3 + 128 * HistoryDiagnostics.MiB);
            SetDocument(DiagnosticDocument(size, layerCount));
            HistoryDiagnostics.Collect();
            long baseline = HistoryDiagnostics.PrivateBytes(), managed = GC.GetTotalMemory(false);
            LayerSnapshot raw = null;
            double capture = HistoryDiagnostics.Time(delegate { raw = new LayerSnapshot(doc); });
            long rawDelta = HistoryDiagnostics.PrivateBytes() - baseline;
            long managedDelta = GC.GetTotalMemory(false) - managed;
            long estimated = raw.Bytes;
            double detachAll = HistoryDiagnostics.Time(delegate
            {
                foreach (Layer layer in doc.Layers) layer.Pixels.SetPixel(0, 1, Color.Yellow);
            });
            long detachedDelta = HistoryDiagnostics.PrivateBytes() - baseline;
            foreach (Layer layer in raw.Layers)
                HistoryCheck(layer.Pixels.GetPixel(0, 1).ToArgb() != Color.Yellow.ToArgb(), "clon independiente tras escritura");
            double dispose = HistoryDiagnostics.Time(raw.Dispose);
            long afterDispose = HistoryDiagnostics.PrivateBytes();
            HistoryDiagnostics.Collect();
            long afterGc = HistoryDiagnostics.PrivateBytes();

            List<double> pushes = new List<double>(), edits = new List<double>(), backwards = new List<double>(), forwards = new List<double>();
            long maxSample = HistoryDiagnostics.PrivateBytes();
            for (int i = 0; i < 6; i++)
            {
                HistoryDiagnostics.Guard(bytes + 16 * HistoryDiagnostics.MiB);
                pushes.Add(HistoryDiagnostics.Time(PushUndo));
                int point = i;
                edits.Add(HistoryDiagnostics.Time(delegate { doc.Image.SetPixel(point, 0, Color.FromArgb(100 + point, 200, 40, 50)); }));
                maxSample = Math.Max(maxSample, HistoryDiagnostics.PrivateBytes());
            }
            long retained = SnapshotBytes(undo), afterSix = HistoryDiagnostics.PrivateBytes();
            int undoCount = undo.Count;
            int expected = (int)Math.Max(1, Math.Min(6, 67108864 / bytes));
            HistoryCheck(undoCount == expected, "retención según presupuesto, incluida captura sobredimensionada");
            for (int i = 0; i < undoCount; i++)
            {
                HistoryDiagnostics.Guard(bytes + 16 * HistoryDiagnostics.MiB);
                backwards.Add(HistoryDiagnostics.Time(delegate { Undo(false); }));
                maxSample = Math.Max(maxSample, HistoryDiagnostics.PrivateBytes());
            }
            long redoBytes = SnapshotBytes(redo);
            for (int i = 0; i < undoCount; i++)
            {
                HistoryDiagnostics.Guard(bytes + 16 * HistoryDiagnostics.MiB);
                forwards.Add(HistoryDiagnostics.Time(delegate { Undo(true); }));
                maxSample = Math.Max(maxSample, HistoryDiagnostics.PrivateBytes());
            }
            double clear = HistoryDiagnostics.Time(delegate { ClearHistory(undo); ClearHistory(redo); });
            long afterClear = HistoryDiagnostics.PrivateBytes();
            double gcMs = HistoryDiagnostics.Time(HistoryDiagnostics.Collect);
            long finalPrivate = HistoryDiagnostics.PrivateBytes();
            long allBaseline = HistoryDiagnostics.PrivateBytes();
            List<double> allActions = new List<double>();
            for (int i = 0; i < 6; i++)
            {
                HistoryDiagnostics.Guard(bytes + 16 * HistoryDiagnostics.MiB);
                int point = i;
                allActions.Add(HistoryDiagnostics.Time(delegate
                {
                    PushUndo();
                    foreach (Layer layer in doc.Layers) layer.Pixels.SetPixel(point, 2, Color.Green);
                }));
            }
            long allAfter = HistoryDiagnostics.PrivateBytes();
            ClearHistory(undo);
            HistoryDiagnostics.Collect();
            long propertyBaseline = HistoryDiagnostics.PrivateBytes();
            List<double> properties = new List<double>();
            for (int i = 0; i < 6; i++)
            {
                int opacity = 90 - i;
                HistoryDiagnostics.Guard(bytes + 16 * HistoryDiagnostics.MiB);
                properties.Add(HistoryDiagnostics.Time(delegate { SetLayerOpacity(opacity); }));
            }
            long propertyDelta = HistoryDiagnostics.PrivateBytes() - propertyBaseline;
            long propertyBytes = HistoryEstimatedBytes;
            int propertyCount = undo.Count;
            HistoryCheck(propertyCount == 6, "se conservan seis propiedades incluso en documentos grandes");
            List<double> propertyUndo = new List<double>(), propertyRedo = new List<double>();
            for (int i = 0; i < propertyCount; i++) propertyUndo.Add(HistoryDiagnostics.Time(delegate { Undo(false); }));
            HistoryCheck(doc.Layers[0].Opacity == 100, "deshacer seis propiedades");
            for (int i = 0; i < propertyCount; i++) propertyRedo.Add(HistoryDiagnostics.Time(delegate { Undo(true); }));
            HistoryCheck(doc.Layers[0].Opacity == 85, "rehacer seis propiedades");
            HistoryEntry firstProperty = undo[0];
            for (int i = 0; i < 29; i++) SetLayerOpacity(84 - i);
            HistoryCheck(undo.Count == 30 && firstProperty.Bytes == 0, "límite y liberación de entradas ligeras");
            foreach (HistoryEntry entry in undo) HistoryCheck(entry is PropertySnapshot, "ninguna captura de píxeles para propiedades");
            HistoryDiagnostics.Csv("history.csv", "size,layers,snapshot_bytes,baseline_private,clone_private_delta,clone_managed_delta,clone_ms,detach_all_ms,detached_private_delta,dispose_ms,private_after_dispose,private_after_gc,push_median_ms,edit_median_ms,undo_median_ms,redo_median_ms,retained_entries,retained_bytes,redo_bytes,private_after_six,max_sample_private,clear_ms,private_after_clear,gc_ms,final_private,all_baseline_private,all_after_private,all_action_median_ms,property_private_delta,property_median_ms,property_entries_six,property_bytes_six,property_undo_median_ms,property_redo_median_ms,property_bytes_thirty");
            HistoryDiagnostics.Csv("history.csv", size, layerCount, estimated, baseline, rawDelta, managedDelta, capture,
                detachAll, detachedDelta, dispose, afterDispose, afterGc, HistoryDiagnostics.Median(pushes), HistoryDiagnostics.Median(edits), HistoryDiagnostics.Median(backwards),
                HistoryDiagnostics.Median(forwards), undoCount, retained, redoBytes, afterSix, maxSample, clear, afterClear, gcMs, finalPrivate,
                allBaseline, allAfter, HistoryDiagnostics.Median(allActions), propertyDelta, HistoryDiagnostics.Median(properties),
                propertyCount, propertyBytes, HistoryDiagnostics.Median(propertyUndo), HistoryDiagnostics.Median(propertyRedo), HistoryEstimatedBytes);
        }

        public void MeasureMultipleHistories()
        {
            HistoryDiagnostics.Guard(200 * HistoryDiagnostics.MiB);
            List<ProjectState> states = new List<ProjectState>();
            List<string> fingerprints = new List<string>();
            for (int p = 0; p < 3; p++)
            {
                Document next = DiagnosticDocument(256, 4); next.Name = "Diagnóstico " + p;
                if (p == 0) SetDocument(next); else AddProject(next, null);
                for (int i = 0; i < 6; i++) { PushUndo(); doc.Image.SetPixel(i, p, Color.Red); }
                StoreActive(); states.Add(activeProject); fingerprints.Add(HistoryFingerprint(doc));
            }
            long total = 0;
            foreach (ProjectState state in states) total += SnapshotBytes(state.Undo) + SnapshotBytes(state.Redo);
            long before = HistoryDiagnostics.PrivateBytes();
            double switching = HistoryDiagnostics.Time(delegate
            {
                for (int i = 0; i < states.Count; i++)
                {
                    ActivateProject(states[i]); Undo(false); Undo(true);
                    HistoryCheck(HistoryFingerprint(doc) == fingerprints[i], "independencia entre proyectos");
                }
            });
            // The same close path as the UI, without asking to save a diagnostic document.
            double closing = HistoryDiagnostics.Time(delegate
            {
                foreach (ProjectState state in states)
                {
                    ActivateProject(state); dirty = false; CloseProject();
                    HistoryCheck(state.Doc.Layers.Count == 0 && state.Undo.Count == 0 && state.Redo.Count == 0,
                        "cierre libera documento e historiales");
                }
            });
            long after = HistoryDiagnostics.PrivateBytes();
            HistoryDiagnostics.Collect();
            HistoryDiagnostics.Csv("multi.csv", "projects,history_bytes,private_before,switch_undo_redo_total_ms,close_total_ms,private_after,private_after_gc");
            HistoryDiagnostics.Csv("multi.csv", 3, total, before, switching, closing, after, HistoryDiagnostics.PrivateBytes());
        }

        public void MeasureHistoryBudget()
        {
            HistoryDiagnostics.Guard(256 * HistoryDiagnostics.MiB);
            SetDocument(DiagnosticDocument(64, 16));
            for (int i = 0; i < 30; i++) { PushUndo(); doc.Image.SetPixel(i, 0, Color.Red); }
            ResizeCanvas(1024, 1024, 0);
            long before = SnapshotBytes(undo) + SnapshotBytes(redo);
            Undo(false);
            long after = SnapshotBytes(undo) + SnapshotBytes(redo);
            HistoryCheck(after == 74711040, "presupuesto combinado tras deshacer ampliación: 71.25 MiB");
            HistoryDiagnostics.Csv("budget.csv", "before_bytes,after_bytes,undo_entries,redo_entries,private_bytes");
            HistoryDiagnostics.Csv("budget.csv", before, after, undo.Count, redo.Count, HistoryDiagnostics.PrivateBytes());
        }

        static void HistoryCheck(bool condition, string message)
        {
            if (!condition) throw new Exception("Historial: " + message);
        }
        // Exact stored ARGB and metadata, not just visible composition or PNG appearance.
        static string HistoryFingerprint(Document document)
        {
            using (MemoryStream buffer = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(buffer))
            using (SHA256 hash = SHA256.Create())
            {
                writer.Write(document.ActiveLayer);
                writer.Write(document.MirrorAxisX2 ?? int.MinValue);
                writer.Write(document.MirrorAxisY2 ?? int.MinValue);
                writer.Write(document.Layers.Count);
                foreach (Layer layer in document.Layers)
                {
                    writer.Write(layer.Name); writer.Write(layer.Visible); writer.Write(layer.Locked); writer.Write(layer.Opacity);
                    writer.Write(layer.Pixels.Width); writer.Write(layer.Pixels.Height);
                    for (int y = 0; y < layer.Pixels.Height; y++)
                        for (int x = 0; x < layer.Pixels.Width; x++) writer.Write(layer.Pixels.GetPixel(x, y).ToArgb());
                }
                writer.Flush(); return Convert.ToBase64String(hash.ComputeHash(buffer.ToArray()));
            }
        }
        void HistoryRoundTrip(string name, Action action)
        {
            canvas.CancelSelection(); canvas.CancelShape();
            string before = HistoryFingerprint(doc);
            int count = undo.Count;
            action();
            string after = HistoryFingerprint(doc);
            HistoryCheck(undo.Count == count + 1, name + " debe ser una acción");
            Undo(false); HistoryCheck(HistoryFingerprint(doc) == before, name + " deshacer exacto");
            Undo(true); HistoryCheck(HistoryFingerprint(doc) == after, name + " rehacer exacto");
        }
        public void TestHistoryIntegrity()
        {
            SetDocument(new Document(12, 12)); SetTool("Lápiz");
            HistoryRoundTrip("píxel semitransparente", delegate { PushUndo(); doc.Image.SetPixel(2, 2, Color.FromArgb(123, 80, 40, 200)); });
            HistoryRoundTrip("borrar", delegate { PushUndo(); doc.Line(new Point(2, 2), new Point(2, 2), Color.Transparent); });
            HistoryRoundTrip("crear capa", AddLayer);
            HistoryRoundTrip("duplicar capa", DuplicateLayer);
            HistoryRoundTrip("opacidad", delegate { SetLayerOpacity(37); });
            HistoryRoundTrip("bloqueo", delegate { SetLayerLocked(true); });
            SetLayerLocked(false);
            HistoryRoundTrip("orden", delegate { ReorderLayer(0); });
            HistoryRoundTrip("eliminar capa", DeleteLayer);
            doc.ActiveLayer = 1;
            HistoryRoundTrip("combinar", MergeLayer);
            HistoryRoundTrip("ejes", delegate { SetAxes(7, 9); });
            HistoryRoundTrip("dimensiones", delegate { ResizeCanvas(16, 18, 4); });
            doc.Image.SetPixel(3, 3, Color.FromArgb(128, 50, 70, 90));
            HistoryRoundTrip("sombra", delegate { HistoryCheck(CreateShadow(Color.Black, 15, 1, 1), "sombra creada"); });
            using (Bitmap imported = new Bitmap(3, 3, PixelFormat.Format32bppArgb))
            {
                imported.SetPixel(1, 1, Color.FromArgb(97, 10, 150, 30));
                HistoryRoundTrip("importación", delegate { ImportAsLayer(imported, "Referencia", 3, 3, 45, false); });
            }

            SetDocument(new Document(12, 12)); SetTool("Varita mágica");
            doc.Image.SetPixel(1, 1, Color.Red); doc.Image.SetPixel(1, 2, Color.Red); doc.Image.SetPixel(2, 2, Color.Red);
            doc.Image.SetPixel(2, 1, Color.Blue); // A nonselected pixel within the bounding box.
            HistoryRoundTrip("borrar máscara L", delegate { canvas.WandSelect(new Point(1, 1)); canvas.DeleteSelection(); });
            Undo(false); // Restore L for copy/transform checks.
            HistoryRoundTrip("mover máscara L", delegate
            {
                canvas.WandSelect(new Point(1, 1)); canvas.BeginSelection(new Point(1, 1));
                canvas.UpdateSelection(new Point(5, 5)); canvas.EndSelectionGesture();
            });
            HistoryRoundTrip("girar máscara L", delegate
            {
                canvas.WandSelect(new Point(5, 5)); canvas.RotateSelection(RotateFlipType.Rotate90FlipNone); canvas.CommitSelection();
            });
            // Select one known remaining original pixel for a pending paste/cancel check.
            canvas.CancelSelection(); canvas.WandSelect(new Point(2, 1)); CopyPixels();
            string beforePending = HistoryFingerprint(doc); int beforePendingCount = undo.Count;
            PastePixels(); Undo(false);
            HistoryCheck(!canvas.HasPendingSelection && undo.Count == beforePendingCount &&
                HistoryFingerprint(doc) == beforePending, "deshacer cancela pegado pendiente sin consumir acción");
            HistoryRoundTrip("pegar", delegate { PastePixels(); canvas.MoveSelectionTo(new Point(8, 8)); canvas.CommitSelection(); });
            HistoryRoundTrip("redimensionar selección", delegate
            {
                canvas.WandSelect(new Point(8, 8)); canvas.ResizeSelection(2, 3); canvas.CommitSelection();
            });

            SetDocument(new Document(12, 12));
            List<string> states = new List<string>(); states.Add(HistoryFingerprint(doc));
            for (int i = 0; i < 10; i++)
            {
                PushUndo(); doc.Image.SetPixel(i, 0, Color.FromArgb(100 + i, 20, 30, 40));
                states.Add(HistoryFingerprint(doc));
            }
            for (int i = 9; i >= 0; i--) { Undo(false); HistoryCheck(HistoryFingerprint(doc) == states[i], "orden al deshacer"); }
            for (int i = 1; i <= 10; i++) { Undo(true); HistoryCheck(HistoryFingerprint(doc) == states[i], "orden al rehacer"); }
            Undo(false); HistoryEntry removedRedo = redo[0];
            PushUndo(); doc.Image.SetPixel(0, 1, Color.White);
            HistoryCheck(redo.Count == 0 && removedRedo.Bytes == 0, "nueva edición libera rehacer");
            ClearHistory(undo);
            PushUndo(); HistoryEntry evicted = undo[0];
            for (int i = 0; i < 35; i++) { PushUndo(); doc.Image.SetPixel(0, 0, Color.FromArgb(255, i, 0, 0)); }
            HistoryCheck(undo.Count == 30 && evicted.Bytes == 0, "límite de 30 y liberación");
            List<HistoryEntry> previous = new List<HistoryEntry>(undo);
            SetDocument(new Document(12, 12));
            foreach (HistoryEntry snapshot in previous) HistoryCheck(snapshot.Bytes == 0, "reemplazo libera capturas");

            // No-op observations are recorded, not disguised as an optimized guarantee.
            int start = undo.Count; SetLayerOpacity(100); SetLayerLocked(false); ReorderLayer(doc.ActiveLayer);
            ResizeCanvas(12, 12, 4); SetAxes(null, null);
            HistoryCheck(undo.Count == start, "propiedades idénticas no añaden capturas");
            canvas.BeginSelection(Point.Empty); canvas.UpdateSelection(new Point(2, 2)); canvas.EndSelectionGesture();
            HistoryCheck(undo.Count == start, "seleccionar no añade capturas");
            canvas.DeleteSelection();
            string observations = "Borrar selección transparente: " + (undo.Count - start) + " captura(s).\n";
            canvas.CancelSelection();
            SetTool("Lápiz"); canvas.Ink = Color.Red; canvas.PrecisionKey(Keys.Enter);
            string unchanged = HistoryFingerprint(doc); int oldCount = undo.Count;
            canvas.PrecisionKey(Keys.Enter);
            HistoryCheck(HistoryFingerprint(doc) == unchanged, "segundo píxel idéntico");
            observations += "Repetir lápiz idéntico: " + (undo.Count - oldCount) + " captura(s).\n";
            File.WriteAllText(Path.Combine(TestArtifacts.DirectoryPath, "history-observations.txt"), observations +
                "Comportamientos anteriores observados; no se han optimizado en la Fase A.\n");

            // Full editor disposal must clear all owned documents and snapshots.
            Editor temporary = new Editor(true);
            List<HistoryEntry> owned = null; Document ownedDocument = null;
            try
            {
                temporary.SetDocument(new Document(8, 8)); temporary.PushUndo();
                owned = temporary.undo; ownedDocument = temporary.doc;
            }
            finally { temporary.Dispose(); }
            HistoryCheck(owned.Count == 0 && ownedDocument.Layers.Count == 0, "Dispose del editor libera recursos propios");

            using (Editor failure = new Editor(true))
            {
                failure.SetDocument(new Document(8, 8)); failure.PushUndo();
                failure.doc.Image.SetPixel(0, 0, Color.Red); failure.Undo(false);
                Bitmap invalid = new Bitmap(8, 8); invalid.Dispose();
                failure.doc.Layers.Add(new Layer("Fallo simulado", invalid));
                bool failed = false;
                try { failure.PushUndo(); }
                catch (ArgumentException) { failed = true; }
                finally { failure.doc.Layers.RemoveAt(failure.doc.Layers.Count - 1); }
                HistoryCheck(failed, "fallo de clonación simulado sin agotar memoria");
                HistoryCheck(failure.redo.Count==1, "B1: fallo de captura conserva rehacer");
                File.AppendAllText(Path.Combine(TestArtifacts.DirectoryPath, "history-observations.txt"),
                    "Tras fallar la captura simulada, rehacer conserva " + failure.redo.Count + " entradas (antes: 1).\n");
            }
        }
    }
}
