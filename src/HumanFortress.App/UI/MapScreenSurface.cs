using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// ScreenSurface specialized for the map area that exposes reliable mouse events
    /// for hover and left-click in local cell coordinates.
    /// </summary>
    internal sealed class MapScreenSurface : ScreenSurface
    {
        public event Action<Point>? MouseMovedLocal;        // local cell coords
        public event Action<Point>? LeftClickedLocal;       // local cell coords

        public MapScreenSurface(int width, int height) : base(width, height)
        {
            UseMouse = true;
            UseKeyboard = false;
            // Do not steal focus on click; keep keyboard on parent state
            FocusOnMouseClick = false;
            MoveToFrontOnMouseClick = false;
        }

        protected override void OnMouseMove(MouseScreenObjectState state)
        {
            base.OnMouseMove(state);
            // SurfaceCellPosition is already local to this surface
            var local = state.SurfaceCellPosition;
            MouseMovedLocal?.Invoke(local);
        }

        protected override void OnMouseLeftClicked(MouseScreenObjectState state)
        {
            base.OnMouseLeftClicked(state);
            // SurfaceCellPosition is already local to this surface
            var local = state.SurfaceCellPosition;
            LeftClickedLocal?.Invoke(local);
        }
    }
}
