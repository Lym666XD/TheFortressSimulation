using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Full-screen UI overlay surface that captures mouse in overlay coordinates.
    /// </summary>
    public sealed class UiOverlaySurface : ScreenSurface
    {
        public event Action<Point>? MouseMovedLocal;   // local cell coords
        public event Action<Point>? LeftClickedLocal;  // local cell coords

        public UiOverlaySurface(int width, int height) : base(width, height)
        {
            UseMouse = true;
            UseKeyboard = false;
            FocusOnMouseClick = false; // never steal kb focus
            MoveToFrontOnMouseClick = false;
        }

        protected override void OnMouseMove(MouseScreenObjectState state)
        {
            base.OnMouseMove(state);
            var local = state.SurfaceCellPosition - Position;
            MouseMovedLocal?.Invoke(local);
        }

        protected override void OnMouseLeftClicked(MouseScreenObjectState state)
        {
            base.OnMouseLeftClicked(state);
            var local = state.SurfaceCellPosition - Position;
            LeftClickedLocal?.Invoke(local);
        }
    }
}

