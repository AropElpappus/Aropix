// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;

namespace PixelArt
{
    partial class Editor
    {
        const string InjectedHistoryFailure = "Fallo de historial simulado por B1";

        static bool HistoryBitmapDisposed(Bitmap image)
        {
            try { int width = image.Width; return false; }
            catch (ArgumentException) { return true; }
        }

        static void SameHistory(List<HistoryEntry> actual, HistoryEntry[] expected, string name)
        {
            HistoryCheck(actual.Count == expected.Length, name + ": cantidad de entradas");
            for (int i = 0; i < expected.Length; i++)
            {
                HistoryCheck(ReferenceEquals(actual[i], expected[i]), name + ": identidad y orden");
                LayerSnapshot full = actual[i] as LayerSnapshot;
                HistoryCheck(actual[i].Bytes > 0 && (full == null || (full.Layers.Count > 0 && !HistoryBitmapDisposed(full.Layers[0].Pixels))),
                    name + ": captura conservada y utilizable");
            }
        }

        void ExpectHistoryFailure(Action action)
        {
            bool failed = false;
            try { action(); }
            catch (InvalidOperationException error)
            {
                if (error.Message != InjectedHistoryFailure) throw;
                failed = true;
            }
            HistoryCheck(failed, "se alcanzó el punto de fallo previsto");
        }

        void CheckCaptureFailure(Action action, string name)
        {
            string before = HistoryFingerprint(doc);
            HistoryEntry[] undoBefore = undo.ToArray(), redoBefore = redo.ToArray();
            bool dirtyBefore = dirty;
            List<Bitmap> copies = new List<Bitmap>();
            historySnapshotFactory = delegate(Document source)
            {
                int index = 0;
                return new LayerSnapshot(source, delegate(Layer layer)
                {
                    if (++index == 2) throw new InvalidOperationException(InjectedHistoryFailure);
                    Layer copy = layer.Clone(); copies.Add(copy.Pixels); return copy;
                });
            };
            try { ExpectHistoryFailure(action); }
            finally { historySnapshotFactory = null; }
            HistoryCheck(copies.Count == 1 && HistoryBitmapDisposed(copies[0]), name + ": libera captura parcial");
            HistoryCheck(before == HistoryFingerprint(doc) && dirty == dirtyBefore, name + ": documento intacto");
            SameHistory(undo, undoBefore, name + ": deshacer");
            SameHistory(redo, redoBefore, name + ": rehacer");
        }

        void CheckPreparedImageFailure(string name, Action action)
        {
            // Before snapshot creation and during snapshot creation, respectively.
            for (int stage = 0; stage < 2; stage++)
            {
                string before = HistoryFingerprint(doc);
                bool dirtyBefore = dirty;
                HistoryEntry[] undoBefore = undo.ToArray(), redoBefore = redo.ToArray();
                List<Bitmap> prepared = new List<Bitmap>();
                int captures = 0;
                preparedImageObserver = delegate(Bitmap image)
                {
                    prepared.Add(image);
                    if (stage == 0) throw new InvalidOperationException(InjectedHistoryFailure);
                };
                historySnapshotFactory = delegate
                {
                    captures++;
                    throw new InvalidOperationException(InjectedHistoryFailure);
                };
                try { ExpectHistoryFailure(action); }
                finally { preparedImageObserver = null; historySnapshotFactory = null; }
                HistoryCheck(prepared.Count > 0, name + ": temporales preparados");
                foreach (Bitmap image in prepared)
                    HistoryCheck(HistoryBitmapDisposed(image), name + ": temporal liberado");
                HistoryCheck(captures == stage, name + ": etapa de fallo");
                HistoryCheck(before == HistoryFingerprint(doc) && dirty == dirtyBefore, name + ": documento intacto");
                SameHistory(undo, undoBefore, name + ": deshacer");
                SameHistory(redo, redoBefore, name + ": rehacer");
            }
        }

        public void TestHistorySafety()
        {
            SetDocument(DiagnosticDocument(8, 3));
            for (int i = 0; i < 3; i++) { PushUndo(); doc.Image.SetPixel(i, 0, Color.Red); }
            Undo(false); // Both lists contain usable entries.
            CheckCaptureFailure(PushUndo, "registrar");
            CheckCaptureFailure(delegate { Undo(false); }, "deshacer");
            CheckCaptureFailure(delegate { Undo(true); }, "rehacer");
            string current = HistoryFingerprint(doc);
            Undo(false); Undo(true);
            HistoryCheck(current == HistoryFingerprint(doc), "historial utilizable tras fallos");
            HistoryCheck(HistoryEstimatedBytes == SnapshotBytes(undo) + SnapshotBytes(redo), "contabilidad combinada");

            ClearHistory(undo); ClearHistory(redo);
            for (int i = 0; i < HistoryEntryLimit; i++) { PushUndo(); doc.Image.SetPixel(0, 0, Color.FromArgb(255, i, 0, 0)); }
            LayerSnapshot oldest = (LayerSnapshot)undo[0]; Bitmap oldestPixels = oldest.Layers[0].Pixels;
            CheckCaptureFailure(PushUndo, "fallo antes de podar 30 entradas");
            PushUndo();
            HistoryCheck(undo.Count == HistoryEntryLimit && HistoryBitmapDisposed(oldestPixels), "poda normal dispone imagen antigua");

            SetDocument(DiagnosticDocument(8, 2));
            doc.ActiveLayer = 1;
            PushUndo(); doc.Image.SetPixel(0, 0, Color.Red); PushUndo(); doc.Image.SetPixel(1, 0, Color.Blue); Undo(false);
            CheckPreparedImageFailure("crear capa", AddLayer);
            CheckPreparedImageFailure("duplicar", DuplicateLayer);
            CheckPreparedImageFailure("combinar", MergeLayer);
            CheckPreparedImageFailure("dimensiones", delegate { ResizeCanvas(10, 12, 4); });
            CheckPreparedImageFailure("sombra", delegate { CreateShadow(Color.Black, 15, 1, 1); });
            using (Bitmap source = new Bitmap(3, 3))
            {
                source.SetPixel(1, 1, Color.FromArgb(128, 30, 40, 50));
                CheckPreparedImageFailure("importar", delegate { ImportAsLayer(source, "Prueba", 3, 3, 50, false); });
                HistoryCheck(source.GetPixel(1, 1).A == 128, "importación conserva imagen prestada");
            }
            SetDocument(new Document(8, 8));
            PushUndo(); doc.Image.SetPixel(0, 0, Color.Red); Undo(false);
            CheckPreparedImageFailure("eliminar última capa", DeleteLayer);

            // A failed pending selection commit is a separate transaction: no prepared
            // import image may survive, and the selection must still be cancellable.
            using (Bitmap source = new Bitmap(2, 2))
            {
                canvas.PasteSelection(source);
                int count = undo.Count;
                historySnapshotFactory = delegate { throw new InvalidOperationException(InjectedHistoryFailure); };
                try { ExpectHistoryFailure(delegate { ImportAsLayer(source, "Pendiente", 2, 2, 100, false); }); }
                finally { historySnapshotFactory = null; }
                HistoryCheck(canvas.HasPendingSelection && undo.Count == count, "fallo al confirmar selección conserva operación pendiente");
                canvas.CancelSelection();
            }
            SetDocument(new Document(100, 100)); SetTool("Lápiz");
        }
    }
}
