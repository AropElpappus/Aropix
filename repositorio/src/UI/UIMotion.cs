// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PixelArt
{
    // UI-thread only. One temporary timer, independent immutable targets, elapsed-time frames.
    static class UiMotion
    {
        sealed class Transition
        {
            public Control Owner;
            public double Started;
            public int Duration;
            public Action<double> Frame;
            public EventHandler Disposed, Hidden;
        }
        static readonly Dictionary<Control, Transition> active = new Dictionary<Control, Transition>();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static Timer timer;
        static bool enabled = true;
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint flags);
        static bool SystemAnimations
        {
            get
            {
                try { int value; return SystemParametersInfo(0x1042, 0, out value, 0) && value != 0; }
                catch (DllNotFoundException) { return false; }
                catch (EntryPointNotFoundException) { return false; }
            }
        }
        internal static bool TestMode;
        internal static bool ManualClock;
        internal static double ManualTime;
        static double Now { get { return ManualClock ? ManualTime : clock.Elapsed.TotalMilliseconds; } }
        public static bool Enabled
        {
            get { return enabled; }
            set { enabled = value; if (!value) FinishAll(); }
        }
        public static bool Allowed
        {
            get { return enabled && (ManualClock || (!TestMode && !SystemInformation.HighContrast
                && SystemAnimations && !SystemInformation.TerminalServerSession)); }
        }
        internal static int Count { get { return active.Count; } }
        internal static bool TimerRunning { get { return timer != null; } }
        public static void Start(Control owner, int milliseconds, Action<double> frame)
        {
            Cancel(owner, false);
            if (owner.IsDisposed) return;
            if (!Allowed || !owner.Visible || milliseconds <= 0) { frame(1); return; }
            Transition job = new Transition { Owner = owner, Started = Now, Duration = milliseconds, Frame = frame };
            job.Disposed = delegate { Cancel(owner, false); };
            job.Hidden = delegate { if (!owner.Visible) Cancel(owner, true); };
            owner.Disposed += job.Disposed; owner.VisibleChanged += job.Hidden;
            active.Add(owner, job);
            frame(0);
            if (!ManualClock && active.Count > 0 && timer == null)
            {
                timer = new Timer { Interval = 15 };
                timer.Tick += delegate { Advance(); };
                timer.Start();
            }
        }
        public static void Cancel(Control owner, bool finish)
        {
            Transition job;
            if (!active.TryGetValue(owner, out job)) return;
            active.Remove(owner);
            owner.Disposed -= job.Disposed; owner.VisibleChanged -= job.Hidden;
            if (active.Count == 0 && timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            if (finish && !owner.IsDisposed) job.Frame(1);
        }
        public static void FinishAll()
        {
            foreach (Control owner in new List<Control>(active.Keys)) Cancel(owner, true);
        }
        internal static void Advance()
        {
            foreach (Transition job in new List<Transition>(active.Values))
            {
                Transition current;
                if (!active.TryGetValue(job.Owner, out current) || current != job) continue;
                if (job.Owner.IsDisposed) { Cancel(job.Owner, false); continue; }
                double progress = Math.Min(1, Math.Max(0, (Now - job.Started) / job.Duration));
                if (progress >= 1 || !Allowed) Cancel(job.Owner, true);
                else job.Frame(1 - Math.Pow(1 - progress, 3));
            }
        }
        public static void Reveal(Form window)
        {
            if (!Allowed || window.IsDisposed) return;
            Start(window, 160, delegate(double p) { window.Opacity = .88 + .12 * p; });
        }
        public static Color Blend(Color from, Color to, double amount)
        {
            return Color.FromArgb(from.A, (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));
        }
    }
}
