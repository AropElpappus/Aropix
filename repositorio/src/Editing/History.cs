// SPDX-License-Identifier: GPL-3.0-only
using System;

namespace PixelArt
{
    abstract class HistoryEntry : IDisposable
    {
        public abstract long Bytes { get; }
        public abstract HistoryEntry CaptureInverse(Document document, Func<Document, LayerSnapshot> captureFull);
        public abstract void Restore(Document document);
        public abstract void Dispose();
    }

    // Metadata only. No references to Layer, Document or Bitmap are retained.
    // Capture all metadata to preserve the scope of the previous full snapshot,
    // including active layer and axes when the user changes layers between actions.
    sealed class PropertySnapshot : HistoryEntry
    {
        sealed class Properties
        {
            public Guid Id;
            public string Name;
            public bool Visible, Locked;
            public int Opacity;
        }

        Properties[] layers;
        readonly int active;
        readonly int? axisX, axisY;
        long bytes;

        public PropertySnapshot(Document document)
        {
            active = document.ActiveLayer;
            axisX = document.MirrorAxisX2;
            axisY = document.MirrorAxisY2;
            layers = new Properties[document.Layers.Count];
            // Conservative managed size estimate, not a measurement of process RAM.
            // Count strings even though immutable strings may be shared.
            bytes = 128;
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = document.Layers[i];
                layers[i] = new Properties
                {
                    Id = layer.HistoryId, Name = layer.Name, Visible = layer.Visible,
                    Locked = layer.Locked, Opacity = layer.Opacity
                };
                bytes += 128 + 2L * (layer.Name == null ? 0 : layer.Name.Length);
            }
        }

        public override long Bytes { get { return bytes; } }

        void Validate(Document document)
        {
            if (layers == null || document.Layers.Count != layers.Length || active < 0 || active >= layers.Length)
                throw new InvalidOperationException("La entrada de propiedades no corresponde al documento actual.");
            // Structural edits use full snapshots and must have been undone first.
            // Validate the whole sequence before touching any property.
            for (int i = 0; i < layers.Length; i++)
                if (document.Layers[i].HistoryId != layers[i].Id)
                    throw new InvalidOperationException("El orden o la identidad de las capas no coincide con el historial.");
        }

        public override HistoryEntry CaptureInverse(Document document, Func<Document, LayerSnapshot> captureFull)
        {
            Validate(document);
            return new PropertySnapshot(document);
        }

        public override void Restore(Document document)
        {
            Validate(document);
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = document.Layers[i];
                Properties saved = layers[i];
                layer.Name = saved.Name; layer.Visible = saved.Visible;
                layer.Locked = saved.Locked; layer.Opacity = saved.Opacity;
            }
            document.ActiveLayer = active;
            document.MirrorAxisX2 = axisX; document.MirrorAxisY2 = axisY;
        }

        public override void Dispose() { layers = null; bytes = 0; }
    }
}
