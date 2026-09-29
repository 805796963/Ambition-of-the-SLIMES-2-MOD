using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SlimesMouse
{
    // -------------------------------------------------------------------- mouse
    //
    // The PC release only maps a keyboard, so pointing at the screen does nothing. This turns the mouse into
    // the same actions the keyboard produces: a click on a UI entry activates it, a click on the battlefield
    // moves the tile cursor, a double click confirms, and a drag sweeps the cursor along the drag direction.
    // Actions are injected through the game's own input path; no battle state is written directly.

    /// <summary>
    /// Recognises a double click from two click times, so the first click can still move the cursor while the
    /// second one means confirm.
    /// </summary>
    internal sealed class DoubleClickDetector
    {
        private float _lastClickTime = float.NegativeInfinity;
        private int _lastClickCell = int.MinValue;

        /// <summary>
        /// Records a click on a battlefield cell and reports whether it completes a double click on that same
        /// cell within the configured window.
        /// </summary>
        /// <param name="cell">Encoded identity of the cell the cursor was moved to.</param>
        /// <returns>True when this click should be treated as a confirm.</returns>
        internal bool Register(int cell)
        {
            float now = Time.unscaledTime;
            float window = Mathf.Max(0.05f, MousePlugin.CfgMouseDoubleClickMs.Value / 1000f);
            bool isDouble = cell == _lastClickCell && now - _lastClickTime <= window;

            _lastClickCell = cell;
            _lastClickTime = now;
            return isDouble;
        }

        /// <summary>Forgets the pending click so the next one starts a fresh pair.</summary>
        internal void Reset()
        {
            _lastClickCell = int.MinValue;
            _lastClickTime = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// What the mouse asks the game to do. The adapter carries the game-specific calls - hit testing a UI
    /// entry, converting a screen point to a battlefield cell, and issuing the cursor move, confirm, cancel and
    /// navigation commands the keyboard would have produced.
    /// </summary>
    internal interface IMouseAdapter
    {
        /// <summary>
        /// Whether the mouse may touch the battlefield. Inside a battle this is the player's turn only, so the
        /// tile cursor stays put while the enemy acts. Windows, cancel and the wheel stay usable either way, so
        /// the pause menu and message boxes keep working during the enemy's turn.
        /// </summary>
        bool IsUserTurn { get; }

        /// <summary>Activates the UI entry under a screen point.</summary>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>True when a UI entry consumed the click.</returns>
        bool TryActivateUiAt(Vector3 screenPoint);

        /// <summary>Converts a screen point to the battlefield cell under it.</summary>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <param name="cell">Encoded identity of the cell, for change detection.</param>
        /// <returns>True when the point lies on the battlefield.</returns>
        bool TryGetCellUnderPointer(Vector3 screenPoint, out int cell);

        /// <summary>Moves the tile cursor to a cell.</summary>
        void MoveCursorTo(int cell);

        /// <summary>Issues the confirm command (the Z key).</summary>
        void Confirm();

        /// <summary>Issues the cancel command (the X key).</summary>
        void Cancel();

        /// <summary>Moves the selection one step, the same as the up and down keys.</summary>
        /// <param name="direction">Positive moves up, negative moves down.</param>
        void Navigate(int direction);
    }

    /// <summary>
    /// Turns raw mouse input into those intents: a left click activates the UI entry under the pointer or moves
    /// the cursor, a second click on the same cell confirms, dragging sweeps the cursor, the right button
    /// cancels and the wheel navigates up and down.
    /// </summary>
    internal sealed class MouseInput
    {
        /// <summary>
        /// Shortest gap between two wheel steps. The scroll axis stays above the dead zone for several frames
        /// after one notch, and one notch must be one step: a list that answers a step with real work (the stage
        /// select reloads an area per focus change) stalls if the wheel walks it many times in a row.
        /// </summary>
        private const float WheelStepSeconds = 0.14f;

        private readonly DoubleClickDetector _doubleClick = new DoubleClickDetector();
        private bool _dragging;
        private bool _dragEngaged;
        private Vector3 _pressPoint;
        private float _pressTime;
        private int _dragCell = int.MinValue;
        private int _lastFrame = -1;
        private float _lastWheelStep = float.NegativeInfinity;

        /// <summary>Reads the mouse for this frame and issues the matching commands.</summary>
        /// <param name="adapter">Game-specific command target.</param>
        internal void Update(IMouseAdapter adapter)
        {
            if (adapter == null) return;
            if (MousePlugin.CfgMouse == null || !MousePlugin.CfgMouse.Value) return;

            // KeyData.UpdateKeyData runs more than once per rendered frame, and Unity answers the same
            // GetMouseButtonDown edge to every call in that frame. Reading the mouse once per frame is what
            // keeps one click from being acted on several times, which otherwise also reads as a double click.
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;

            // The enemy's attack is a timer-driven state machine, so a stray battlefield write during the enemy's
            // turn can cut it short. Only the battlefield interaction (moving the tile cursor and the double click
            // confirm that comes from it) is withheld; windows stay clickable, and cancel and the wheel keep
            // working, because those mirror the keyboard the game already gates on m_bUserTurn itself.
            bool userTurn = adapter.IsUserTurn;

            var pointer = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
            {
                _pressPoint = pointer;
                _pressTime = Time.unscaledTime;
                _dragEngaged = false;

                if (adapter.TryActivateUiAt(pointer))
                {
                    _dragging = false;
                    _dragCell = int.MinValue;
                    _doubleClick.Reset();
                }
                else if (userTurn)
                {
                    _dragging = true;
                    ClickMap(adapter, pointer);
                }
                else
                {
                    // No window took the click, and the battlefield must not take it during the enemy's turn.
                    if (MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value)
                        MousePlugin.Verbose("window click: the battlefield is withheld outside the player's turn");

                    _dragging = false;
                    _dragCell = int.MinValue;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _dragEngaged = false;
                _dragCell = int.MinValue;
            }
            else if (userTurn && _dragging && MousePlugin.CfgMouseDragCursor.Value && Input.GetMouseButton(0))
            {
                // A click that merely lasted a moment must not sweep the cursor, so the drag only engages once
                // the pointer has travelled far enough and the button has been held long enough.
                if (!_dragEngaged)
                {
                    float travelled = (pointer - _pressPoint).magnitude;
                    float held = Time.unscaledTime - _pressTime;
                    _dragEngaged = travelled >= MousePlugin.CfgMouseDragThresholdPixels.Value
                                && held >= MousePlugin.CfgMouseDragDelayMs.Value / 1000f;
                }

                if (_dragEngaged) DragMap(adapter, pointer);
            }

            if (MousePlugin.CfgMouseRightCancels.Value && Input.GetMouseButtonDown(1))
            {
                adapter.Cancel();
                _doubleClick.Reset();
            }

            if (MousePlugin.CfgMouseWheelNavigate.Value)
            {
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                float now = Time.unscaledTime;

                // The axis stays off centre and even changes sign for several frames after one notch, so a step
                // is taken at most once per interval rather than on every frame the axis reads hot.
                if (Mathf.Abs(wheel) > 0.01f && now - _lastWheelStep >= WheelStepSeconds)
                {
                    _lastWheelStep = now;
                    adapter.Navigate(wheel > 0f ? 1 : -1);
                }
            }
        }

        private void ClickMap(IMouseAdapter adapter, Vector3 pointer)
        {
            if (!adapter.TryGetCellUnderPointer(pointer, out int cell))
            {
                if (MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value)
                    MousePlugin.Verbose("window click: the pointer is not over the battlefield");

                _doubleClick.Reset();
                return;
            }

            adapter.MoveCursorTo(cell);
            _dragCell = cell;
            if (_doubleClick.Register(cell)) adapter.Confirm();
        }

        private void DragMap(IMouseAdapter adapter, Vector3 pointer)
        {
            if (!adapter.TryGetCellUnderPointer(pointer, out int cell)) return;
            if (cell == _dragCell) return;

            _dragCell = cell;
            adapter.MoveCursorTo(cell);
        }
    }
}
