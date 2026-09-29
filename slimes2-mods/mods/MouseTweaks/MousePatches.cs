using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SlimesMouse
{
    // -------------------------------------------------------------------- mouse
    //
    // The PC release maps only a keyboard, so pointing at the screen does nothing. Everything below turns the
    // mouse into the same actions the keyboard produces and hands them to the game's own input hub, KeyData, so
    // every consumer keeps reading a single input source.
    //
    // Injection lifecycle. KeyData.UpdateKeyData rebuilds button/trigger/release/repeat from the keyboard and
    // copies the result into prevButton/prevRepeatButton, all inside one call; every game handler reads those
    // fields after it returns in the same frame. Patch_UpdateKeyData's postfix reads the mouse and overlaps the
    // requested BtnOK/BtnCancel bits onto button and trigger, and its prefix removes exactly the bits the mouse
    // produced from the live and previous-frame state, so an injected press is visible for one frame and the
    // game never reports a release or repeat that nobody performed. An injected BtnOK is what the bound key
    // writes, so a click can only ever reach a handler that would also have taken the key press. Writes are
    // limited to those input bits, the tile cursor position, and - for the window under the pointer - the
    // highlight index and the local position of that window's own cursor object. MouseWindows carries which
    // window that is and where those two members live.

    /// <summary>
    /// The confirm, cancel and direction bits the mouse asks <see cref="KeyData"/> to report, carried for
    /// exactly one frame.
    /// </summary>
    internal static class MouseButtonInjection
    {
        private static uint _pending;
        private static uint _pendingRepeat;
        private static uint _carriedOver;
        private static uint _carriedOverRepeat;

        /// <summary>Adds bits to the press the game reports for the frame that is being read.</summary>
        /// <param name="mask">Button bits, normally <see cref="KeyData.BtnOK"/> or <see cref="KeyData.BtnCancel"/>.</param>
        /// <param name="asRepeat">
        /// True for the direction bits, which the game's lists read through <c>KeyData.GetButtonRepeat</c> rather
        /// than through the trigger (TitleMenu.Update, Assembly-CSharp5.cpp:20909).
        /// </param>
        internal static void Request(uint mask, bool asRepeat = false)
        {
            _pending |= mask;
            if (asRepeat) _pendingRepeat |= mask;
        }

        /// <summary>Takes the bits requested for this frame and clears the request.</summary>
        /// <param name="repeat">Receives the bits that also have to appear as a repeat.</param>
        /// <returns>The button bits to inject, or 0 when the mouse asked for nothing.</returns>
        internal static uint TakePending(out uint repeat)
        {
            uint mask = _pending;
            repeat = _pendingRepeat;
            _pending = 0;
            _pendingRepeat = 0;
            return mask;
        }

        /// <summary>
        /// Overlaps <paramref name="mask"/> onto the button state and trigger. Bits the keyboard already holds or
        /// already triggered are recorded as the player's own, so <see cref="DropCarriedOver"/> leaves them and
        /// the real key edge intact.
        /// </summary>
        /// <param name="mask">Button bits to report for this frame; 0 does nothing.</param>
        /// <param name="repeat">Bits that also have to appear as a repeat this frame.</param>
        internal static void Apply(uint mask, uint repeat)
        {
            if (mask == 0) return;

            var data = KeyData.button;
            uint fromKeyboard = data.button | data.trigger;

            data.button |= mask;
            data.trigger |= mask;
            data.repeat |= repeat;
            KeyData.button = data;

            _carriedOver = mask & ~fromKeyboard;
            _carriedOverRepeat = repeat;
        }

        /// <summary>
        /// Clears the bits the mouse injected on the previous frame from the button state and from the edge
        /// detection state the incoming frame computes against, so one injection cannot repeat or release.
        /// </summary>
        internal static void DropCarriedOver()
        {
            uint mask = _carriedOver;
            uint repeat = _carriedOverRepeat;
            _carriedOver = 0;
            _carriedOverRepeat = 0;
            if (mask == 0 && repeat == 0) return;

            var data = KeyData.button;
            data.button &= ~mask;
            data.trigger &= ~mask;
            data.repeat &= ~repeat;
            KeyData.button = data;

            KeyData.prevButton &= ~mask;
            KeyData.prevRepeatButton &= ~repeat;
        }
    }

    /// <summary>
    /// Runs the mouse from the game's single per-frame input read. The prefix ends the previous frame's
    /// injection, and the postfix reads the mouse and injects the confirm/cancel bits it produced.
    /// </summary>
    [HarmonyPatch(typeof(KeyData), "UpdateKeyData")]
    internal static class Patch_UpdateKeyData
    {
        private static readonly MouseInput Mouse = new MouseInput();
        private static readonly GameMouseAdapter Adapter = new GameMouseAdapter();

        [HarmonyPrefix]
        private static void Prefix()
        {
            try { MouseButtonInjection.DropCarriedOver(); }
            catch (Exception e) { MouseDiagnostics.WarnOnce("clear injected buttons", e.Message); }
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (MousePlugin.CfgEnabled.Value && MousePlugin.CfgMouse != null && MousePlugin.CfgMouse.Value)
            {
                try { Mouse.Update(Adapter); }
                catch (Exception e) { MouseDiagnostics.WarnOnce("read mouse", e.Message); }
            }

            try { MouseButtonInjection.Apply(MouseButtonInjection.TakePending(out uint repeat), repeat); }
            catch (Exception e) { MouseDiagnostics.WarnOnce("inject buttons", e.Message); }
        }
    }

    /// <summary>
    /// Publishes the running MainFlow, which is the root of every object the mouse needs and the component that
    /// owns the per-frame input read. Resolving from it keeps the mouse off scene searches entirely.
    /// </summary>
    [HarmonyPatch(typeof(MainFlow), "Update")]
    internal static class Patch_MainFlowUpdate
    {
        [HarmonyPostfix]
        private static void Postfix(MainFlow __instance)
        {
            GameMouseAdapter.PublishMainFlow(__instance);
        }
    }

    /// <summary>
    /// Failure reporting for the mouse path. A game call that fails on every frame would otherwise flood the
    /// log, so each distinct failure is reported once.
    /// </summary>
    internal static class MouseDiagnostics
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>Reports a failure once per distinct operation.</summary>
        /// <param name="operation">Names the game call that failed; also the once-only key.</param>
        /// <param name="detail">The failure detail, typically an exception message.</param>
        internal static void WarnOnce(string operation, string detail)
        {
            if (Reported.Add(operation)) MousePlugin.Warn($"mouse: {operation}: {detail}");
        }
    }

    /// <summary>
    /// Carries mouse requests out against the running battle: screen point to field cell, tile cursor movement,
    /// confirm and cancel through <see cref="KeyData"/>, camera zoom, and the window hit tests.
    ///
    /// <see cref="IMouseAdapter.TryActivateUiAt"/> reports true both when an entry was activated and when an open
    /// window holds focus without the pointer having hit one, because in both cases the click must not reach the
    /// battlefield behind the window. The command pane is the exception: it stays open over the battlefield for
    /// most of the player's turn, so it only claims the clicks that land on one of its entries. It is therefore
    /// tried before the "some window is open" guard, which a battle can satisfy on its own and which would
    /// otherwise make every command button unclickable.
    ///
    /// Every game object is reached from the published MainFlow through the game's own references, and each one
    /// is cached; a destroyed Unity object compares equal to null, which is what re-runs a lookup.
    /// </summary>
    internal sealed class GameMouseAdapter : IMouseAdapter
    {
        /// <summary>World distance between adjacent field cells, fixed by GetFieldPosX/GetFieldPosZ.</summary>
        private const float CellSize = 0.1f;

        /// <summary>Slack allowed outside the field bounds, matching the 0.05f offset GetFieldPosX2 uses.</summary>
        private const float CellEdgeSlack = 0.05f;

        /// <summary>
        /// Ray passes per cell lookup. The first pass hits a plane at the cursor's height; each further pass
        /// re-hits a plane at the height of the cell the previous pass resolved, which converges on sloped maps.
        /// </summary>
        private const int PlaneRefinements = 3;

        private static MainFlow _published;

        private MainFlow _mainFlow;
        private MainCamera _mainCamera;
        private BattleField _battleField;
        private Camera _battleCamera;
        private PauseWinHandler _pauseWin;
        private ButtonMenuWin _buttonMenu;
        private UnitSelectWin _unitSelect;

        /// <summary>Records the running MainFlow. Called from the MainFlow.Update patch every frame.</summary>
        /// <param name="flow">The instance MainFlow.Update ran on.</param>
        internal static void PublishMainFlow(MainFlow flow)
        {
            if (flow != null) _published = flow;
        }

        // ------------------------------------------------------------------ lookups

        /// <summary>The running MainFlow, the root of every window the mouse reaches.</summary>
        internal MainFlow MainFlowComponent
        {
            get
            {
                if (_mainFlow == null) _mainFlow = _published;
                return _mainFlow;
            }
        }

        /// <summary>
        /// Whether the world map is the current screen. Its selection is laid out left/right, so the wheel must
        /// move it east/west there instead of up/down.
        ///
        /// MainFlow.nGameMode is the game's own "which screen" flag. MapSelectHandler.IsOpening() is not usable
        /// here: it reports the map's intro state (<c>m_nCurrentMapID == MAP_ID.opening</c>,
        /// Assembly-CSharp4.cpp:16413), which is false for the whole time the screen is actually usable.
        /// </summary>
        private bool IsMapSelectOpen
        {
            get
            {
                var flow = MainFlowComponent;
                if (flow == null) return false;

                try { return flow.nGameMode == GAME_MODE.map_sel; }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce("read MainFlow.nGameMode", e.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Whether the screen is waiting for a button press before anything else happens: the publisher logo, and
        /// the title screen before its menu opens. Both advance on any button, which is also why the right button
        /// already works there - cancel is injected without consulting the screen at all.
        /// </summary>
        private bool IsPressButtonScreen
        {
            get
            {
                var flow = MainFlowComponent;
                if (flow == null) return false;

                try
                {
                    var mode = flow.nGameMode;
                    if (mode == GAME_MODE.logo) return true;
                    if (mode != GAME_MODE.title) return false;

                    // Once the title menu is up it owns the screen, so only the press-any-button step counts here.
                    var title = flow.TitleWinComponent;
                    return title == null || !title.IsTitleMenuOpen();
                }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce("read the press button screen", e.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Whether the pre-battle deployment is running. There the battlefield cursor is the player's own tool:
        /// they pick a slime in the grid and then aim a tile to place it, before any turn has started.
        ///
        /// The signal is <c>BattleFlow.nBattleMode == BATTLE_MODE.layout</c> (1). The deployment screen reports
        /// <c>nGameMode == battle</c> and keeps <c>m_bUserTurn</c> false, and the grid's own GameObject goes
        /// inactive as soon as placement starts, so none of those can stand in for it.
        /// </summary>
        internal bool IsUnitDeployLayout
        {
            get
            {
                var flow = BattleFlowComponent;
                if (flow == null) return false;

                try { return (int)flow.nBattleMode == 1; }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce("read BattleFlow.nBattleMode", e.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// The deployment grid. A null result is not cached, for the same reason as the hint bar: MainFlow builds
        /// this window lazily and an early read would otherwise never be retried.
        /// </summary>
        private UnitSelectWin UnitSelectComponent
        {
            get
            {
                if (_unitSelect != null) return _unitSelect;

                var flow = MainFlowComponent;
                if (flow == null) return null;

                try
                {
                    var units = flow.UnitSelectComponent;
                    if (units != null) _unitSelect = units;
                }
                catch (Exception e) { MouseDiagnostics.WarnOnce("read MainFlow.UnitSelectComponent", e.Message); }

                return _unitSelect;
            }
        }

        /// <summary>
        /// The hint bar at one of the screen corners. Its entries name the keys the current screen accepts, so each
        /// one can be pressed by clicking it. Every screen that shows it goes through MainFlow.OpenButtonMenuWin.
        ///
        /// A null result is deliberately not cached: MainFlow builds the bar lazily, so an early read before any
        /// screen has opened it returns nothing, and caching that would leave the bar unreachable all session.
        /// </summary>
        internal ButtonMenuWin ButtonMenuComponent
        {
            get
            {
                if (_buttonMenu != null) return _buttonMenu;

                var flow = MainFlowComponent;
                if (flow == null) return null;

                try
                {
                    var bar = flow.ButtonMenuWinComponent;
                    if (bar == null)
                    {
                        // The field is not always populated, but the object it belongs to is: fall back to it
                        // rather than leaving every bar entry dead.
                        _buttonMenu = WindowHitTest.ComponentOn<ButtonMenuWin>(flow.ButtonMenuWinObj, "ButtonMenuWinObj");
                    }
                    else _buttonMenu = bar;
                }
                catch (Exception e) { MouseDiagnostics.WarnOnce("read MainFlow.ButtonMenuWinComponent", e.Message); }

                if (_buttonMenu == null)
                    MouseDiagnostics.WarnOnce("find the hint bar", "MainFlow has no ButtonMenuWin yet");

                return _buttonMenu;
            }
        }

        /// <summary>Asks for a one-frame press of a raw button bit, which a hint bar entry stands for.</summary>
        /// <param name="mask">Button bits to press.</param>
        /// <param name="action">Name used in the log and in the once-only failure key.</param>
        internal void Press(uint mask, string action) => RequestButton(mask, action);

        /// <summary>
        /// Whether the mouse may touch the battlefield. Outside a battle there is no turn flag and the mouse stays
        /// active; inside one it is the player's turn only. The game sets this true in LoadUnit (deployment) and
        /// StartTurn, so the title, menus and the pre-battle layout all keep the mouse working, and only the enemy
        /// turn is withheld.
        /// </summary>
        public bool IsUserTurn
        {
            get
            {
                // The pre-battle deployment grid floats over a battlefield the player is still aiming on, so the
                // field has to stay live there even though the turn has not formally started.
                if (IsUnitDeployLayout) return true;

                var flow = BattleFlowComponent;
                if (flow == null) return true;

                try { return flow.m_bUserTurn; }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce("read m_bUserTurn", e.Message);
                    return true;
                }
            }
        }

        private MainCamera MainCameraComponent
        {
            get
            {
                if (_mainCamera != null) return _mainCamera;

                var flow = MainFlowComponent;
                if (flow == null) return null;

                try { _mainCamera = flow.MainCameraComponent; }
                catch (Exception e) { MouseDiagnostics.WarnOnce("read MainFlow.MainCameraComponent", e.Message); }

                return _mainCamera;
            }
        }

        /// <summary>The running BattleFlow, the root of the battle windows and the action list.</summary>
        internal BattleFlow BattleFlowComponent
        {
            get
            {
                var field = BattleFieldComponent;
                if (field != null)
                {
                    // BattleField.SetMapCursor dereferences this without a check, so it is also the gate a
                    // cursor move has to pass.
                    try
                    {
                        var battleFlow = field.BattleFlowComponent;
                        if (battleFlow != null) return battleFlow;
                    }
                    catch (Exception e) { MouseDiagnostics.WarnOnce("read BattleField.BattleFlowComponent", e.Message); }
                }

                var camera = MainCameraComponent;
                if (camera == null) return null;

                try { return camera.BattleFlowComponent; }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce("read MainCamera.BattleFlowComponent", e.Message);
                    return null;
                }
            }
        }

        private BattleField BattleFieldComponent
        {
            get
            {
                if (_battleField != null) return _battleField;

                var camera = MainCameraComponent;
                if (camera == null) return null;

                try
                {
                    _battleField = camera.BattleFieldComponent;
                    if (_battleField != null) return _battleField;
                }
                catch (Exception e) { MouseDiagnostics.WarnOnce("read MainCamera.BattleFieldComponent", e.Message); }

                try
                {
                    var battleFlow = camera.BattleFlowComponent;
                    if (battleFlow != null) _battleField = battleFlow.BattleFieldComponent;
                }
                catch (Exception e) { MouseDiagnostics.WarnOnce("read BattleFlow.BattleFieldComponent", e.Message); }

                return _battleField;
            }
        }

        /// <summary>The camera that draws the battlefield, resolved without Camera.main.</summary>
        private Camera BattleCamera
        {
            get
            {
                if (_battleCamera != null) return _battleCamera;

                var camera = MainCameraComponent;
                if (camera != null) _battleCamera = CameraOn(camera);

                var flow = MainFlowComponent;
                if (_battleCamera == null && flow != null) _battleCamera = CameraOn(flow.MainCameraObject);

                if (_battleCamera == null)
                    MouseDiagnostics.WarnOnce("find battle camera", "no Camera on MainFlow or MainCamera yet");

                return _battleCamera;
            }
        }

        private static Camera CameraOn(GameObject owner)
        {
            if (owner == null) return null;

            try { return owner.GetComponent<Camera>(); }
            catch (Exception e) { MouseDiagnostics.WarnOnce("GetComponent<Camera>", e.Message); return null; }
        }

        private static Camera CameraOn(Component owner)
        {
            if (owner == null) return null;

            try { return owner.GetComponent<Camera>(); }
            catch (Exception e) { MouseDiagnostics.WarnOnce("GetComponent<Camera>", e.Message); return null; }
        }

        /// <summary>
        /// The pause window handler, reached through BattleFlow.PauseWin because GetComponent finds a component
        /// on an inactive GameObject while a scene search does not.
        /// </summary>
        internal PauseWinHandler PauseWinComponent()
        {
            if (_pauseWin != null) return _pauseWin;

            try
            {
                var battleFlow = BattleFlowComponent;
                var pauseObject = battleFlow == null ? null : battleFlow.PauseWin;
                if (pauseObject != null) _pauseWin = pauseObject.GetComponent<PauseWinHandler>();
            }
            catch (Exception e) { MouseDiagnostics.WarnOnce("find PauseWinHandler", e.Message); }

            return _pauseWin;
        }

        // ------------------------------------------------------------------ windows

        /// <summary>
        /// Whether a window the table did not claim holds focus. Such a click only has to be kept away from the
        /// battlefield: ConfirmWinHandler reads the left and right buttons itself.
        /// </summary>
        private bool IsForeignWindowOpen(out string what)
        {
            what = null;
            var flow = MainFlowComponent;
            if (flow == null) return false;

            try
            {
                if (flow.IsConfirmWinOpen()) { what = "confirm window"; return true; }
                if (flow.IsGenWinOpen()) { what = "open window"; return true; }
            }
            catch (Exception e) { MouseDiagnostics.WarnOnce("read window state", e.Message); }

            return false;
        }

        // ------------------------------------------------------------------ IMouseAdapter

        /// <inheritdoc />
        public bool TryActivateUiAt(Vector3 screenPoint)
        {
            // The bottom hint bar is tried first: it draws over the window behind it, so a window row that owns
            // every click would otherwise swallow the press before the bar ever saw it.
            if (MouseWindows.TryActivateHintBar(this, screenPoint, out string barHit))
            {
                if (barHit != null) MousePlugin.Verbose($"window click: {barHit}");
                return true;
            }

            // The world map draws its own bar rather than using the shared one, so it is a separate hit test.
            if (MouseWindows.TryActivateMapBar(this, screenPoint, out string mapBarHit))
            {
                if (mapBarHit != null) MousePlugin.Verbose($"window click: {mapBarHit}");
                return true;
            }

            // Every window that presents entries is one row of MouseWindows' table, tried in click priority
            // order; the row's own input read commits whatever the click wrote.
            if (MouseWindows.TryActivate(this, screenPoint, out string hit))
            {
                if (hit != null) MousePlugin.Verbose($"window click: {hit}");
                return true;
            }

            // Anything the table does not cover still has to keep its click away from the battlefield, which is
            // what the game's own "a window is open" flags are for.
            if (IsForeignWindowOpen(out string what))
            {
                MouseDiagnostics.WarnOnce($"click in the {what}",
                    "no entry was hit under the pointer, so this click is ignored");
                MousePlugin.Verbose($"window click: the {what} holds the click;{ClickRoute()}");
                return true;
            }

            // The stage select has no entry list to hit, so a left click confirms the focused node, the same as Z.
            // It runs after the window guards, so an open menu still takes the click first.
            if (IsMapSelectOpen)
            {
                Confirm();
                MousePlugin.Verbose($"window click: map select confirmed;{ClickRoute()}");
                return true;
            }

            // The logo and the title screen's press-any-button step advance on a key press; a click stands in for it.
            if (IsPressButtonScreen)
            {
                Confirm();
                MousePlugin.Verbose($"window click: press button screen advanced;{ClickRoute()}");
                return true;
            }

            MousePlugin.Verbose($"window click: nothing took the click, so it goes to the battlefield;{ClickRoute()}");
            return false;
        }

        /// <summary>
        /// Every state the click router consults, for the verbose log. A click that reaches neither a window nor
        /// the battlefield otherwise leaves no trace at all, which is what makes "I clicked and nothing happened"
        /// impossible to tell apart from "the pointer was off the field".
        /// </summary>
        /// <returns>The state, as one log fragment.</returns>
        private string ClickRoute()
        {
            var flow = MainFlowComponent;
            if (flow == null) return " mainFlow=none";

            var units = UnitSelectComponent;
            string unitMode = units == null ? "none" : Describe(() => (int)units.m_nMode);

            return $" nGameMode={Describe(() => (int)flow.nGameMode)}"
                 + $" deployLayout={IsUnitDeployLayout} userTurn={IsUserTurn}"
                 + $" pressButton={IsPressButtonScreen} mapSelect={IsMapSelectOpen}"
                 + $" genWin={Describe(() => flow.IsGenWinOpen())} confirmWin={Describe(() => flow.IsConfirmWinOpen())}"
                 + $" unitSelect={unitMode}"
                 + $" battleFlow={BattleFlowComponent != null} field={BattleFieldComponent != null} camera={BattleCamera != null}";
        }

        /// <summary>Reads a game flag for the log, reporting a failure instead of throwing into the click path.</summary>
        /// <param name="read">Flag to read.</param>
        /// <returns>The value, or the failure.</returns>
        private static string Describe(Func<bool> read)
        {
            try { return read().ToString(); }
            catch (Exception e) { return $"err({e.Message})"; }
        }

        /// <summary>Reads a game number for the log, reporting a failure instead of throwing into the click path.</summary>
        /// <param name="read">Number to read.</param>
        /// <returns>The value, or the failure.</returns>
        private static string Describe(Func<int> read)
        {
            try { return read().ToString(); }
            catch (Exception e) { return $"err({e.Message})"; }
        }

        /// <inheritdoc />
        public bool TryGetCellUnderPointer(Vector3 screenPoint, out int cell)
        {
            cell = 0;

            var field = BattleFieldComponent;
            var camera = BattleCamera;
            if (field == null || camera == null) return false;

            int sizeX, sizeZ;
            try
            {
                sizeX = field.nFieldSize_X;
                sizeZ = field.nFieldSize_Z;
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read field size", e.Message);
                return false;
            }
            if (sizeX <= 0 || sizeZ <= 0) return false;

            float height = CursorCellHeight(field);
            int x = 0, z = 0;

            for (int pass = 0; pass < PlaneRefinements; pass++)
            {
                if (!TryProjectToPlane(camera, screenPoint, height, out var world)) return false;

                // Only the first pass tests the bounds: later passes follow the resolved cell's height, which can
                // move the hit point just outside them for a click on the edge of the map.
                if (pass == 0 && !IsInsideField(field, world, sizeX, sizeZ)) return false;

                try
                {
                    x = field.GetFieldPosX2(world.x);
                    z = field.GetFieldPosZ2(world.z);
                }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce("world to field cell", e.Message);
                    return false;
                }

                height = CellHeight(field, x, z, height);
            }

            cell = EncodeCell(x, z);
            return true;
        }

        /// <summary>Height of the field surface under the tile cursor, the plane the first pass hits.</summary>
        private static float CursorCellHeight(BattleField field)
        {
            try { return field.GetFieldPos(field.nMapCursorX, field.nMapCursorZ).y; }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read cursor height", e.Message);
                return 0f;
            }
        }

        private static float CellHeight(BattleField field, int x, int z, float fallback)
        {
            try { return field.GetFieldHeight(x, z); }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read field height", e.Message);
                return fallback;
            }
        }

        /// <summary>
        /// Intersects the pointer ray with a horizontal plane. The game has no screen-to-field helper, and the
        /// field is a grid of CellSize squares laid out from fFieldBaseX/fFieldBaseZ.
        /// </summary>
        private static bool TryProjectToPlane(Camera camera, Vector3 screenPoint, float height, out Vector3 world)
        {
            world = new Vector3(0f, height, 0f);

            try
            {
                var ray = camera.ScreenPointToRay(screenPoint);
                Vector3 origin = ray.origin;
                Vector3 direction = ray.direction;

                if (Mathf.Abs(direction.y) < 1e-5f) return false;   // parallel to the field

                float distance = (height - origin.y) / direction.y;
                if (distance <= 0f) return false;                   // the field is behind the camera

                world = new Vector3(
                    origin.x + direction.x * distance, height, origin.z + direction.z * distance);
                return true;
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("screen point to world", e.Message);
                return false;
            }
        }

        /// <summary>
        /// Whether a world point lies on the field. GetFieldPosX2/GetFieldPosZ2 clamp to the field, so a click
        /// outside it would otherwise land on the nearest edge cell.
        /// </summary>
        private static bool IsInsideField(BattleField field, Vector3 world, int sizeX, int sizeZ)
        {
            float baseX, baseZ;
            try
            {
                baseX = field.fFieldBaseX;
                baseZ = field.fFieldBaseZ;
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read field origin", e.Message);
                return false;
            }

            return world.x >= baseX - CellEdgeSlack
                && world.x <= baseX + CellSize * sizeX + CellEdgeSlack
                && world.z >= baseZ - CellSize * sizeZ - CellEdgeSlack
                && world.z <= baseZ + CellEdgeSlack;
        }

        /// <inheritdoc />
        public void MoveCursorTo(int cell)
        {
            var field = BattleFieldComponent;
            if (field == null) return;
            if (BattleFlowComponent == null) return;   // not in a battle; SetMapCursor would dereference null

            DecodeCell(cell, out int x, out int z);

            try
            {
                if (x < 0 || z < 0 || x >= field.nFieldSize_X || z >= field.nFieldSize_Z) return;
                if (field.nMapCursorX == x && field.nMapCursorZ == z) return;

                // Every call site in the game passes bSetCameraFocus true, which is what keeps the camera on a
                // cursor that a drag can move far away from where the player is looking.
                field.SetMapCursor(x, z, true);
                MousePlugin.Verbose($"cursor -> cell ({x},{z})");
            }
            catch (Exception e) { MouseDiagnostics.WarnOnce("SetMapCursor", e.Message); }
        }

        /// <summary>
        /// Asks for a one-frame press of the confirm button. The mask is read from the game because BtnOK and
        /// BtnCancel swap between A and B with the button mode setting.
        /// </summary>
        public void Confirm()
        {
            try { RequestButton(KeyData.BtnOK, "confirm"); }
            catch (Exception e) { MouseDiagnostics.WarnOnce("read BtnOK", e.Message); }
        }

        /// <summary>Asks for a one-frame press of the cancel button, the same bits the bound cancel key writes.</summary>
        public void Cancel()
        {
            try { RequestButton(KeyData.BtnCancel, "cancel"); }
            catch (Exception e) { MouseDiagnostics.WarnOnce("read BtnCancel", e.Message); }
        }

        /// <summary>
        /// Asks for a one-frame press of the up or down direction. ButtonSts.UP and ButtonSts.DOWN are the
        /// directional bits themselves, unlike BtnOK and BtnCancel, which the button mode setting swaps. The
        /// press is also reported as a repeat because the game's lists read directions that way.
        /// </summary>
        /// <param name="direction">Positive moves up, negative moves down.</param>
        public void Navigate(int direction)
        {
            if (direction == 0) return;

            bool up = direction > 0;

            if (IsMapSelectOpen)
            {
                // The stage select's own move keys are the shoulder buttons, which walk the map list one step at a
                // time: ChangeFocusByKey_dir(-1/+1) over m_nCurrentMapID (Assembly-CSharp4.cpp:19896, 18178). The
                // arrow bits are the wrong tool here - they go through ChangeFocusByKey(m_nMoveMapID[DIR.w/e]),
                // which only moves where the current node actually has an east or west neighbour, so the wheel
                // runs out of moves after a dozen nodes.
                if (!MousePlugin.CfgMouseWheelMapSelect.Value) return;

                RequestButton(up ? (uint)ButtonSts.R : (uint)ButtonSts.L,
                              up ? "navigate next map" : "navigate previous map",
                              asRepeat: true);
                return;
            }

            RequestButton(up ? (uint)ButtonSts.UP : (uint)ButtonSts.DOWN,
                          up ? "navigate up" : "navigate down",
                          asRepeat: true);
        }

        /// <summary>Records a one-frame press of the given button bits.</summary>
        /// <param name="mask">Button bits the click stands for.</param>
        /// <param name="action">Name used in the log and in the once-only failure key.</param>
        /// <param name="asRepeat">True when the game reads this button as a repeat rather than as a trigger.</param>
        private static void RequestButton(uint mask, string action, bool asRepeat = false)
        {
            if (mask == 0)
            {
                MouseDiagnostics.WarnOnce($"{action} button", "KeyData reported a zero button mask");
                return;
            }

            MouseButtonInjection.Request(mask, asRepeat);
            MousePlugin.Verbose($"mouse {action} -> button mask {mask}");
        }

        // ------------------------------------------------------------------ cell identity

        /// <summary>Packs a field cell into the single integer <see cref="IMouseAdapter"/> passes around.</summary>
        /// <param name="x">Field column.</param>
        /// <param name="z">Field row.</param>
        /// <returns>Cell identity, compared for equality only.</returns>
        private static int EncodeCell(int x, int z)
        {
            return (z << 16) | (x & 0xFFFF);
        }

        /// <summary>Unpacks a cell identity produced by <see cref="EncodeCell"/>.</summary>
        /// <param name="cell">Packed cell identity.</param>
        /// <param name="x">Receives the field column.</param>
        /// <param name="z">Receives the field row.</param>
        private static void DecodeCell(int cell, out int x, out int z)
        {
            x = cell & 0xFFFF;
            z = (cell >> 16) & 0xFFFF;
        }
    }
}
