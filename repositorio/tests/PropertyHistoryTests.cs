// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace PixelArt
{
    partial class Editor
    {
        void CheckPropertyFailure(Action action)
        {
            string before = HistoryFingerprint(doc);
            bool dirtyBefore = dirty;
            HistoryEntry[] oldUndo = undo.ToArray(), oldRedo = redo.ToArray();
            HistoryEntry prepared = null;
            beforeHistoryCommit = delegate(HistoryEntry entry)
            {
                prepared = entry;
                throw new InvalidOperationException(InjectedHistoryFailure);
            };
            try { ExpectHistoryFailure(action); }
            finally { beforeHistoryCommit = null; }
            HistoryCheck(prepared is PropertySnapshot && prepared.Bytes == 0, "entrada ligera fallida liberada");
            HistoryCheck(before == HistoryFingerprint(doc) && dirty == dirtyBefore, "propiedades intactas tras fallo");
            SameHistory(undo, oldUndo, "deshacer ligero tras fallo");
            SameHistory(redo, oldRedo, "rehacer ligero tras fallo");
        }

        public void TestPropertyHistory()
        {
            SetDocument(DiagnosticDocument(8, 2));
            doc.Layers[0].Name = doc.Layers[1].Name = "Mismo nombre";
            Guid firstId = doc.Layers[0].HistoryId;
            using (Layer clone = doc.Layers[0].Clone())
                HistoryCheck(clone.HistoryId == firstId, "captura completa conserva identidad");
            HistoryCheck(doc.Layers[1].HistoryId != firstId, "capas diferentes tienen identidades diferentes");

            Bitmap firstPixels = doc.Layers[0].Pixels, secondPixels = doc.Layers[1].Pixels;
            // Any attempted full capture fails the test, including during undo/redo.
            historySnapshotFactory = delegate { throw new Exception("Las propiedades no deben clonar imágenes"); };
            try
            {
                HistoryRoundTrip("opacidad ligera", delegate { SetLayerOpacity(37); });
                HistoryRoundTrip("bloqueo ligero", delegate { SetLayerLocked(true); });
                HistoryRoundTrip("visibilidad de capa no activa", delegate { SetLayerVisible(1, false); });
                HistoryRoundTrip("nombre ligero", delegate { SetLayerName("Renombrada"); });
                HistoryRoundTrip("ejes ligeros", delegate { SetAxes(5, 8); });
                HistoryRoundTrip("centrar ejes", delegate { SetAxes(null, null); });
                foreach (HistoryEntry entry in undo) HistoryCheck(entry is PropertySnapshot, "solo entradas ligeras");
                HistoryCheck(ReferenceEquals(doc.Layers[0].Pixels, firstPixels) &&
                    ReferenceEquals(doc.Layers[1].Pixels, secondPixels), "propiedades no sustituyen bitmaps");

                // Selecting a different active layer is not itself a history entry.
                doc.ActiveLayer = 1;
                Undo(false);
                HistoryCheck(doc.ActiveLayer == 0, "deshacer restituye capa activa de la entrada");
                Undo(true);
                HistoryCheck(doc.ActiveLayer == 1, "rehacer restituye capa activa inversa");

                int count = undo.Count;
                SetLayerOpacity(doc.Layers[1].Opacity); SetLayerLocked(doc.Layers[1].Locked);
                SetLayerVisible(1, doc.Layers[1].Visible); SetLayerName(doc.Layers[1].Name);
                SetAxes(doc.MirrorAxisX2, doc.MirrorAxisY2);
                HistoryCheck(undo.Count == count, "propiedades idénticas no añaden entradas");

                Undo(false); // Both lists populated for the failure assertions.
                CheckPropertyFailure(delegate { SetLayerOpacity(65); });
                CheckPropertyFailure(delegate { SetLayerLocked(false); });
                CheckPropertyFailure(delegate { SetLayerVisible(1, true); });
                CheckPropertyFailure(delegate { SetLayerName("No debe aplicarse"); });
                CheckPropertyFailure(delegate { SetAxes(11, 12); });
                CheckPropertyFailure(delegate { Undo(false); });
                CheckPropertyFailure(delegate { Undo(true); });
            }
            finally { historySnapshotFactory = null; beforeHistoryCommit = null; }

            // Mix structural snapshots and metadata, verifying every pixel/property
            // at every step through a whole backwards/forwards traversal.
            SetDocument(new Document(16, 16));
            List<string> states = new List<string>();
            states.Add(HistoryFingerprint(doc));
            Action<Action> step = delegate(Action action) { action(); states.Add(HistoryFingerprint(doc)); };
            step(delegate { PushUndo(); doc.Image.SetPixel(2, 2, Color.FromArgb(123, 20, 80, 140)); });
            step(delegate { SetLayerOpacity(45); });
            step(DuplicateLayer);
            HistoryCheck(doc.Layers[0].HistoryId != doc.Layers[1].HistoryId, "duplicar crea una identidad nueva");
            step(delegate { SetLayerName("Mismo nombre"); });
            step(delegate { SetLayerOpacity(72); });
            step(delegate { ReorderLayer(0); });
            step(delegate { SetLayerLocked(true); });
            step(delegate { SetLayerLocked(false); });
            step(delegate { ResizeCanvas(18, 20, 4); });
            step(delegate { SetAxes(7, 9); });
            step(delegate { HistoryCheck(CreateShadow(Color.Black, 15, 1, 1), "sombra en historial mixto"); });
            step(delegate { SetLayerOpacity(30); });
            step(DeleteLayer);
            step(delegate { SetLayerName("Después de eliminar"); });
            using (Bitmap imported = new Bitmap(3, 3))
            {
                imported.SetPixel(1, 1, Color.Red);
                step(delegate { ImportAsLayer(imported, "Referencia", 3, 3, 60, false); });
            }
            step(delegate { SetLayerOpacity(80); });
            step(MergeLayer);
            step(delegate { SetLayerVisible(doc.ActiveLayer, false); });
            step(delegate { SetLayerVisible(doc.ActiveLayer, true); });
            for (int i = states.Count - 2; i >= 0; i--)
            {
                Undo(false); HistoryCheck(HistoryFingerprint(doc) == states[i], "deshacer mixto exacto " + i);
            }
            for (int i = 1; i < states.Count; i++)
            {
                Undo(true); HistoryCheck(HistoryFingerprint(doc) == states[i], "rehacer mixto exacto " + i);
            }
            Undo(false); HistoryEntry discarded = redo[0];
            SetLayerName("Rama nueva");
            HistoryCheck(redo.Count == 0 && discarded.Bytes == 0, "nueva propiedad invalida y libera rehacer");

            // Incorrect layer identities must be rejected before restoring anything.
            Layer original = doc.Layers[0];
            Layer replacement = new Layer(original.Name, new Bitmap(doc.Image.Width, doc.Image.Height));
            HistoryEntry[] beforeUndo = undo.ToArray(), beforeRedo = redo.ToArray();
            doc.Layers[0] = replacement;
            string wrongDocument = HistoryFingerprint(doc);
            bool rejected = false;
            try { Undo(false); }
            catch (InvalidOperationException) { rejected = true; HistoryCheck(HistoryFingerprint(doc) == wrongDocument, "validación no modifica ninguna propiedad"); }
            finally { doc.Layers[0] = original; replacement.Dispose(); }
            HistoryCheck(rejected, "rechaza identidad diferente aunque nombre y posición coincidan");
            SameHistory(undo, beforeUndo, "identidad inválida conserva deshacer");
            SameHistory(redo, beforeRedo, "identidad inválida conserva rehacer");
            Undo(false); Undo(true);

            // Pending paste keeps its own complete entry, followed by one light entry.
            SetDocument(new Document(8, 8));
            using (Bitmap pasted = new Bitmap(2, 2))
            {
                pasted.SetPixel(0, 0, Color.Blue);
                canvas.PasteSelection(pasted);
                SetLayerOpacity(50);
                HistoryCheck(undo.Count == 2 && undo[0] is LayerSnapshot && undo[1] is PropertySnapshot,
                    "confirmación de pegado y propiedad son dos acciones");
                Undo(false); HistoryCheck(doc.Layers[0].Opacity == 100 && doc.Image.GetPixel(0, 0).B == 255, "deshacer propiedad conserva pegado");
                Undo(false); HistoryCheck(doc.Image.GetPixel(0, 0).A == 0, "deshacer pegado");
                Undo(true); Undo(true);
            }

            // Property entries keep the same action limit and failure guarantees.
            SetDocument(new Document(8, 8));
            for (int i = 0; i < 35; i++) SetLayerOpacity(i);
            HistoryCheck(undo.Count == 30, "máximo de 30 propiedades");
            HistoryEntry oldestProperty = undo[0];
            CheckPropertyFailure(delegate { SetLayerName("Fallo al podar"); });
            HistoryCheck(oldestProperty.Bytes > 0, "fallo no poda entrada ligera antigua");
            SetLayerName("Poda correcta");
            HistoryCheck(undo.Count == 30 && oldestProperty.Bytes == 0, "poda ligera libera metadatos");
            using (Document loaded = Document.Deserialize(doc.Serialize().DocumentElement))
            {
                HistoryCheck(HistoryFingerprint(loaded) == HistoryFingerprint(doc), "reapertura conserva formato y propiedades");
                HistoryCheck(loaded.Layers[0].HistoryId != doc.Layers[0].HistoryId, "reapertura crea identidades transitorias nuevas");
            }

            // Independent histories through real tab switches and document disposal.
            ProjectState first = activeProject;
            string firstState = HistoryFingerprint(doc);
            List<HistoryEntry> firstEntries = undo;
            AddProject(new Document(8, 8) { Name = "Otro proyecto" }, null);
            ProjectState second = activeProject;
            SetLayerOpacity(25); string secondState = HistoryFingerprint(doc);
            ActivateProject(first); Undo(false); Undo(true);
            HistoryCheck(firstState == HistoryFingerprint(doc), "historial mixto de primera pestaña");
            ActivateProject(second); Undo(false); Undo(true);
            HistoryCheck(secondState == HistoryFingerprint(doc), "historial ligero de segunda pestaña");
            ActivateProject(first); dirty = false; CloseProject();
            HistoryCheck(firstEntries.Count == 0, "cerrar pestaña libera ambos tipos de entrada");
            HistoryCheck(!doc.Serialize().OuterXml.Contains("HistoryId"), "identidad interna no cambia formato de proyecto");
            SetDocument(new Document(100, 100)); SetTool("Lápiz");
        }
    }
}
