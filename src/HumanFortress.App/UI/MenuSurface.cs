using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// ScreenSurface specialized for menu that exposes mouse events.
    /// </summary>
    internal sealed class MenuSurface : ScreenSurface
    {
        public event Action<Point>? MouseMovedLocal;
        public event Action<Point>? LeftClickedLocal;
        public event Action<Point>? RightClickedLocal;

        public MenuSurface(int width, int height) : base(width, height)
        {
            UseMouse = true;
            UseKeyboard = false;
            FocusOnMouseClick = false; // Don't steal focus
            MoveToFrontOnMouseClick = false;
        }

        protected override void OnMouseMove(MouseScreenObjectState state)
        {
            base.OnMouseMove(state);
            var local = state.SurfaceCellPosition;
            MouseMovedLocal?.Invoke(local);
        }

        protected override void OnMouseLeftClicked(MouseScreenObjectState state)
        {
            base.OnMouseLeftClicked(state);
            var local = state.SurfaceCellPosition;
            LeftClickedLocal?.Invoke(local);
        }

        public override bool ProcessMouse(MouseScreenObjectState state)
        {
            // Check for right-click and fire event
            if (state.Mouse.RightClicked)
            {
                var local = state.SurfaceCellPosition;
                RightClickedLocal?.Invoke(local);
            }

            return base.ProcessMouse(state);
        }
    }
}
