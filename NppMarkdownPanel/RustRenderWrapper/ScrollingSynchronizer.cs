// RustRenderWrapper — bidirectional scroll synchronization.
//
// Three-stage pipeline per v4.0 定稿:
//   1. editor → preview : first visible editor line → data-line element
//   2. preview → editor : topmost preview block's data-line → editor line
//   3. direction lock   : suppresses echo/feedback while a programmatic
//                         scroll propagates (auto-release after 150 ms)
// plus a 60 ms debounce that coalesces editor scroll bursts.
//
// Self-contained (WinForms timer only) so it compiles against net472 without
// extra dependencies; the host wires the two callbacks.

using System;
using System.Windows.Forms;

namespace RustRenderWrapper
{
    public sealed class ScrollingSynchronizer : IDisposable
    {
        private const int LockMs = 150;
        private const int DebounceMs = 60;

        private enum Direction { None = 0, EditorToPreview = 1, PreviewToEditor = 2 }

        private Direction _locked;
        private long _lockTicks;
        private readonly Timer _debounce;
        private Action _pending;

        public ScrollingSynchronizer()
        {
            _debounce = new Timer { Interval = DebounceMs };
            _debounce.Tick += FlushPending;
        }

        /// <summary>
        /// Editor scrolled: debounced, direction-locked call into the preview.
        /// <paramref name="apply"/> performs the actual preview scroll.
        /// </summary>
        public void OnEditorScrolled(int firstVisibleLine, Action<int> apply)
        {
            if (apply == null) return;
            if (IsLocked(Direction.PreviewToEditor)) return;   // echo suppression

            Lock(Direction.EditorToPreview);
            int line = firstVisibleLine;
            Debounce(() => apply(line));
        }

        /// <summary>
        /// Preview scrolled: direction-locked call back into the editor.
        /// </summary>
        public void OnPreviewScrolled(int topBlockLine, Action<int> apply)
        {
            if (apply == null || topBlockLine <= 0) return;
            if (IsLocked(Direction.EditorToPreview)) return;   // echo suppression

            Lock(Direction.PreviewToEditor);
            apply(topBlockLine);
        }

        /// <summary>Cancel any queued preview update (e.g., during content swap).</summary>
        public void CancelPending()
        {
            _pending = null;
            _debounce.Stop();
        }

        private void Debounce(Action action)
        {
            _pending = action;
            _debounce.Stop();
            _debounce.Start();
        }

        private void FlushPending(object sender, EventArgs e)
        {
            _debounce.Stop();
            Action action = _pending;
            _pending = null;
            action?.Invoke();
        }

        private void Lock(Direction direction)
        {
            _locked = direction;
            _lockTicks = DateTime.UtcNow.Ticks;
        }

        private bool IsLocked(Direction against)
        {
            if (_locked != against) return false;
            long age = DateTime.UtcNow.Ticks - _lockTicks;
            if (age > LockMs * TimeSpan.TicksPerMillisecond)
            {
                _locked = Direction.None;   // auto-release
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            _debounce.Dispose();
        }
    }
}
