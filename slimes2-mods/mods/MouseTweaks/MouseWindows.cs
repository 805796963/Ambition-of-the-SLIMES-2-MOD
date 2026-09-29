using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace SlimesMouse
{
    // ---------------------------------------------------------------- window clicks
    //
    // Every option window in this game is a uGUI list the game walks with the keyboard: it holds a highlight
    // index, draws a cursor at that index, and its own per-frame input read commits whatever the index points
    // at when KeyData reports BtnOK. None of the windows has a click handler, so a click is turned into exactly
    // what a key press is: the highlight index is written first, then a one-frame confirm is injected, and the
    // window's own Update commits the entry the pointer landed on.
    //
    // Nothing here is invoked by name. Each window is reached from the published MainFlow/BattleFlow through the
    // game's own public references, and every member is an interop property read or write. .NET reflection over
    // an IL2CPP method jumps outside the interop trampoline and hung the process once, so it is not used at all
    // on this path.
    //
    // Each window is one row of the table in MouseWindows.BuildTable: how to tell it is open, which component
    // owns it, how many entries it presents, the layout rect of each entry, and which member carries the
    // highlight. Adding a window is adding a row.
    //
    // Highlight drawing. A window's own SetCursor is a native method, so it cannot be re-run after the index is
    // written; without more the click would activate the right entry while the drawn cursor stayed behind. Each
    // Select below therefore writes the same local position that window's own SetCursor writes, using only
    // Transform and Vector3, and cites the game location that formula was read from. A wrong formula is cosmetic
    // and is corrected by the game as soon as the player navigates with the keyboard.

    /// <summary>
    /// One window that presents selectable entries to the pointer.
    /// </summary>
    internal abstract class WindowDescriptor
    {
        /// <summary>Short name used in the log and as the once-per-failure warning key.</summary>
        internal abstract string Name { get; }

        /// <summary>Whether the window would react to a key press during this frame.</summary>
        /// <param name="host">Adapter that resolves the published MainFlow and BattleFlow.</param>
        /// <returns>True when the window owns input right now.</returns>
        internal abstract bool IsOpen(GameMouseAdapter host);

        /// <summary>The component that owns the entries, or null while the window is not resolvable.</summary>
        /// <param name="host">Adapter that resolves the published MainFlow and BattleFlow.</param>
        /// <returns>The live component, or null.</returns>
        internal abstract Component Focus(GameMouseAdapter host);

        /// <summary>How many entries the window presents at this moment.</summary>
        /// <param name="target">Component returned by <see cref="Focus"/>.</param>
        /// <returns>Entry count; 0 or less means nothing is selectable.</returns>
        internal abstract int Count(Component target);

        /// <summary>The layout rect of one entry, or null when that entry has none.</summary>
        /// <param name="target">Component returned by <see cref="Focus"/>.</param>
        /// <param name="index">Entry index, below <see cref="Count"/>.</param>
        /// <returns>The entry rect, or null.</returns>
        internal abstract RectTransform Entry(Component target, int index);

        /// <summary>Writes the highlight index the window's own input read consumes.</summary>
        /// <param name="target">Component returned by <see cref="Focus"/>.</param>
        /// <param name="index">Index the pointer landed on.</param>
        internal abstract void Select(Component target, int index);

        /// <summary>
        /// Where every entry sits on screen, for the verbose log when a click inside the window misses them all.
        /// A hit test that fails while the pointer is visibly over an entry means the rects and the pointer are in
        /// different coordinate spaces, which this line is what tells apart from a window that is simply closed.
        /// </summary>
        /// <param name="target">Component returned by <see cref="Focus"/>.</param>
        /// <returns>One fragment per entry, appended to the log line.</returns>
        internal virtual string Describe(Component target) => $" {Name} has no geometry to report";

        /// <summary>
        /// Whether an open window keeps a click that misses every entry. This is what stops the tile cursor from
        /// moving behind a window; the battle action list and the deployment grid, which both float over a
        /// battlefield the player is still aiming on, are the windows that let a miss through.
        /// </summary>
        /// <param name="host">Adapter that resolves the published state the answer depends on.</param>
        /// <returns>True when a miss stays with the window.</returns>
        internal virtual bool OwnsEveryClick(GameMouseAdapter host) => true;

        /// <summary>
        /// Horizontal margin, in layout units, added to an entry before hit testing. Rows whose drawn rectangle is
        /// wider than the entry anchor set this to reach the whole row.
        /// </summary>
        internal virtual float SpreadX => 0f;

        /// <summary>Vertical margin, in layout units, added to an entry before hit testing.</summary>
        internal virtual float SpreadY => 0f;
    }

    /// <summary>
    /// A window whose detail is supplied as delegates, so each row of the table is one expression.
    /// </summary>
    internal sealed class EntriesWindow : WindowDescriptor
    {
        private readonly string _name;
        private readonly Func<GameMouseAdapter, bool> _isOpen;
        private readonly Func<GameMouseAdapter, Component> _focus;
        private readonly Func<Component, int> _count;
        private readonly Func<Component, int, RectTransform> _entry;
        private readonly Action<Component, int> _select;
        private readonly Func<GameMouseAdapter, bool> _ownsEveryClick;
        private readonly Func<Component, string> _describe;
        private readonly float _spreadX;
        private readonly float _spreadY;

        /// <summary>Describes one window.</summary>
        /// <param name="name">Short name used in the log.</param>
        /// <param name="isOpen">Whether the window owns input, read from the same state its own input read tests.</param>
        /// <param name="focus">Resolves the live component.</param>
        /// <param name="count">Reads the current entry count.</param>
        /// <param name="entry">Reads one entry rect.</param>
        /// <param name="select">Writes the highlight index and repositions the window's cursor object.</param>
        /// <param name="ownsEveryClick">
        /// Decides whether a miss stays with the window. Null keeps every click, which is the default; the battle
        /// action list and the deployment grid let a miss through because both float over a battlefield the player
        /// is still aiming on.
        /// </param>
        /// <param name="describe">Extra state the verbose miss line adds, when the generic entry list is not enough.</param>
        /// <param name="spreadX">Horizontal margin added to an entry before hit testing, in layout units.</param>
        /// <param name="spreadY">Vertical margin added to an entry before hit testing, in layout units.</param>
        internal EntriesWindow(
            string name,
            Func<GameMouseAdapter, bool> isOpen,
            Func<GameMouseAdapter, Component> focus,
            Func<Component, int> count,
            Func<Component, int, RectTransform> entry,
            Action<Component, int> select,
            Func<GameMouseAdapter, bool> ownsEveryClick = null,
            Func<Component, string> describe = null,
            float spreadX = 0f,
            float spreadY = 0f)
        {
            _name = name;
            _isOpen = isOpen;
            _focus = focus;
            _count = count;
            _entry = entry;
            _select = select;
            _ownsEveryClick = ownsEveryClick;
            _describe = describe;
            _spreadX = spreadX;
            _spreadY = spreadY;
        }

        internal override string Name => _name;
        internal override bool IsOpen(GameMouseAdapter host) => _isOpen(host);
        internal override Component Focus(GameMouseAdapter host) => _focus(host);
        internal override int Count(Component target) => _count(target);
        internal override RectTransform Entry(Component target, int index) => _entry(target, index);
        internal override void Select(Component target, int index) => _select(target, index);
        internal override bool OwnsEveryClick(GameMouseAdapter host)
            => _ownsEveryClick == null || _ownsEveryClick(host);
        internal override float SpreadX => _spreadX;
        internal override float SpreadY => _spreadY;

        internal override string Describe(Component target)
        {
            int count = Count(target);
            var text = new StringBuilder($" count={count}");

            if (_describe != null) text.Append(_describe(target));

            for (int index = 0; index < count; index++)
            {
                var entry = Entry(target, index);
                if (entry == null) { text.Append($" [#{index} no rect]"); continue; }

                // The aim rect, not the entry's own: that is what the hit test actually uses, so a log that
                // disagreed with the clicks would be worse than no log.
                var rect = WindowHitTest.AimRect(entry);
                var camera = WindowHitTest.UiCameraFor(rect);
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
                text.Append($" [#{index} {rect.name} centre={centre.x:F0},{centre.y:F0}"
                          + $" size={rect.rect.width:F0}x{rect.rect.height:F0} scale={rect.lossyScale.x:F2}"
                          + $" active={rect.gameObject.activeInHierarchy} camera={(camera == null ? "none" : camera.name)}]");
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// A window with no list, where the whole window is the single confirm target: clicking anywhere on it does
    /// what pressing the confirm key does, and the window's own Update advances it.
    /// </summary>
    internal sealed class WholeWindow : WindowDescriptor
    {
        private readonly string _name;
        private readonly Func<GameMouseAdapter, bool> _isOpen;
        private readonly Func<GameMouseAdapter, Component> _focus;
        private readonly Func<Component, RectTransform> _rect;

        /// <summary>Describes one confirm-only window.</summary>
        /// <param name="name">Short name used in the log.</param>
        /// <param name="isOpen">Whether the window owns input.</param>
        /// <param name="focus">Resolves the live component.</param>
        /// <param name="rect">The rect the pointer has to be inside.</param>
        internal WholeWindow(
            string name,
            Func<GameMouseAdapter, bool> isOpen,
            Func<GameMouseAdapter, Component> focus,
            Func<Component, RectTransform> rect)
        {
            _name = name;
            _isOpen = isOpen;
            _focus = focus;
            _rect = rect;
        }

        internal override string Name => _name;
        internal override bool IsOpen(GameMouseAdapter host) => _isOpen(host);
        internal override Component Focus(GameMouseAdapter host) => _focus(host);
        internal override int Count(Component target) => _rect(target) == null ? 0 : 1;
        internal override RectTransform Entry(Component target, int index) => _rect(target);

        internal override string Describe(Component target)
        {
            var rect = _rect(target);
            if (rect == null) return " count=0";

            var camera = WindowHitTest.UiCameraFor(rect);
            Vector2 centre = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            return $" count=1 [{rect.name} centre={centre.x:F0},{centre.y:F0}"
                 + $" size={rect.rect.width:F0}x{rect.rect.height:F0}"
                 + $" camera={(camera == null ? "none" : camera.name)}]";
        }

        /// <summary>Nothing to write: this window's own input read commits on the confirm bit alone.</summary>
        /// <param name="target">Component returned by <see cref="Focus"/>.</param>
        /// <param name="index">Always 0.</param>
        internal override void Select(Component target, int index)
        {
        }
    }

    /// <summary>
    /// Screen-point tests and highlight placement shared by every window.
    /// </summary>
    internal static class WindowHitTest
    {
        /// <summary>The layout rect of a UI object, or null when it has none.</summary>
        /// <param name="owner">Object carrying the rect.</param>
        /// <returns>The rect transform, or null.</returns>
        internal static RectTransform RectOf(GameObject owner)
        {
            if (owner == null) return null;

            var own = owner.GetComponent<RectTransform>();
            if (own != null) return own;

            // BattleFlow's ActionButton(Clone) rows are plain Transform roots that draw through a child rect,
            // so a row with no rect of its own falls back to the largest rect underneath it.
            return LargestChildRect(owner);
        }

        /// <summary>
        /// Most transforms the fallback looks at, so a row sitting under a scene-sized hierarchy cannot make a
        /// single hit test walk thousands of nodes.
        /// </summary>
        private const int MaxRectsScanned = 128;

        /// <summary>
        /// The widest rect in an object's children, which is the drawn area of a row whose root has none.
        /// </summary>
        /// <param name="owner">Object to search.</param>
        /// <returns>The largest child rect, or null when there is none.</returns>
        private static RectTransform LargestChildRect(GameObject owner)
        {
            RectTransform best = null;
            float bestArea = 0f;
            int scanned = 0;

            // Breadth first with a budget. GetComponentsInChildren would be one call, but it walks the whole
            // subtree, which is unbounded for a row that happens to parent scene content.
            var pending = new Queue<Transform>();
            pending.Enqueue(owner.transform);

            while (pending.Count > 0 && scanned < MaxRectsScanned)
            {
                var node = pending.Dequeue();
                int children = node.childCount;

                for (int index = 0; index < children; index++)
                {
                    if (++scanned > MaxRectsScanned) break;

                    var child = node.GetChild(index);
                    if (child == null) continue;

                    var candidate = child.TryCast<RectTransform>();
                    if (candidate != null)
                    {
                        var scale = candidate.lossyScale;
                        float area = Mathf.Abs(candidate.rect.width * scale.x)
                                   * Mathf.Abs(candidate.rect.height * scale.y);

                        // An active rect wins over an inactive one even when it is smaller: the row's spare
                        // graphics stay in the prefab switched off.
                        if (candidate.gameObject.activeInHierarchy) area += 1e9f;
                        if (area > bestArea) { best = candidate; bestArea = area; }
                    }

                    if (child.childCount > 0) pending.Enqueue(child);
                }
            }

            return best;
        }

        /// <summary>The layout rect of a UI component, or null when it has none.</summary>
        /// <param name="owner">Component carrying the rect.</param>
        /// <returns>The rect transform, or null.</returns>
        internal static RectTransform RectOf(Component owner)
        {
            if (owner == null) return null;
            return owner.GetComponent<RectTransform>();
        }

        /// <summary>
        /// A component on an object, for the cases where MainFlow holds the object but not the component.
        /// </summary>
        /// <typeparam name="T">Component to fetch.</typeparam>
        /// <param name="owner">Object to search.</param>
        /// <param name="what">Name used in the once-only failure key.</param>
        /// <returns>The component, or null.</returns>
        internal static T ComponentOn<T>(GameObject owner, string what) where T : Component
        {
            if (owner == null) return null;

            try { return owner.GetComponent<T>(); }
            catch (Exception e) { MouseDiagnostics.WarnOnce($"GetComponent<{typeof(T).Name}> on {what}", e.Message); return null; }
        }

        /// <summary>
        /// The rectangle an entry presents to the player, which is not always the entry's own.
        ///
        /// Several of this game's rows are small anchors that draw through a bigger child: the card of a system
        /// menu tile, the sheet of a save slot, the panel of an item row. Hit testing the anchor alone left the
        /// visible rectangle mostly dead - "那么大一个长方形结果就里面的重心有判定". So an entry is aimed at through
        /// whichever of itself and its largest child is bigger.
        /// </summary>
        /// <param name="entry">Entry rect.</param>
        /// <returns>The rect to test the pointer against.</returns>
        internal static RectTransform AimRect(RectTransform entry)
        {
            if (entry == null) return null;

            var child = LargestChildRect(entry.gameObject);
            if (child == null || child == entry) return entry;

            // A switched-off child is not on screen, however large it is.
            if (!child.gameObject.activeInHierarchy) return entry;

            float own = Mathf.Abs(entry.rect.width * entry.rect.height);
            float nested = Mathf.Abs(child.rect.width * child.rect.height);

            return nested > own ? child : entry;
        }

        /// <summary>Whether a screen point falls inside a UI rect.</summary>
        /// <param name="rect">Rect to test.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>True when the point is inside.</returns>
        internal static bool Contains(RectTransform rect, Vector3 screenPoint)
        {
            if (rect == null) return false;

            return RectTransformUtility.RectangleContainsScreenPoint(
                rect, new Vector2(screenPoint.x, screenPoint.y), UiCameraFor(rect));
        }

        /// <summary>
        /// Whether the pointer falls inside a rect grown by a margin on each axis.
        ///
        /// Some of this game's entries are anchors much smaller than the row or tile drawn around them - an item
        /// only occupies the icon of the row it labels - so the drawn area is reached by growing the entry by the
        /// margin that closes the gap to its neighbour. Grown areas overlap, which the nearest centre resolves.
        /// The margin is applied in the rect's own layout units, so it survives a resolution change.
        /// </summary>
        /// <param name="rect">Entry rect.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <param name="spreadX">Margin in layout units on the horizontal axis.</param>
        /// <param name="spreadY">Margin in layout units on the vertical axis.</param>
        /// <returns>True when the point is inside the grown rect.</returns>
        internal static bool ContainsWithin(
            RectTransform rect, Vector3 screenPoint, float spreadX, float spreadY)
        {
            if (rect == null) return false;
            if (spreadX <= 0f && spreadY <= 0f) return Contains(rect, screenPoint);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, new Vector2(screenPoint.x, screenPoint.y), UiCameraFor(rect), out Vector2 local))
                return false;

            var area = rect.rect;
            area.xMin -= spreadX;
            area.xMax += spreadX;
            area.yMin -= spreadY;
            area.yMax += spreadY;

            return area.Contains(local);
        }

        /// <summary>
        /// Screen pixel distance from the pointer to the centre of a UI rect. Overlapping entry rects would
        /// otherwise always resolve to the lowest index, so the entry the player aimed at is the nearest centre.
        /// </summary>
        /// <param name="rect">Rect to measure.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>Distance in pixels.</returns>
        internal static float Distance(RectTransform rect, Vector3 screenPoint)
        {
            if (rect == null) return float.MaxValue;

            Vector2 centre = RectTransformUtility.WorldToScreenPoint(UiCameraFor(rect), rect.position);
            return Vector2.Distance(centre, new Vector2(screenPoint.x, screenPoint.y));
        }

        /// <summary>
        /// The camera a canvas is drawn with. A Screen Space - Overlay canvas is drawn without a camera, and
        /// RectTransformUtility requires null for it.
        /// </summary>
        /// <param name="rect">Rect inside the canvas.</param>
        /// <returns>The canvas camera, or null for an overlay canvas.</returns>
        internal static Camera UiCameraFor(RectTransform rect)
        {
            if (rect == null) return null;

            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;

            return canvas.worldCamera;
        }

        /// <summary>Moves a window's cursor object to a local Y, keeping the X and Z the game gave it.</summary>
        /// <param name="cursor">Cursor object, or null.</param>
        /// <param name="y">Local Y the window's own SetCursor would write.</param>
        internal static void MoveCursorY(GameObject cursor, float y)
        {
            if (cursor == null) return;

            var transform = cursor.transform;
            Vector3 position = transform.localPosition;
            position.y = y;
            transform.localPosition = position;
        }

        /// <summary>Moves a window's cursor object to a local X, keeping the Y and Z the game gave it.</summary>
        /// <param name="cursor">Cursor object, or null.</param>
        /// <param name="x">Local X the window's own SetCursor would write.</param>
        internal static void MoveCursorX(GameObject cursor, float x)
        {
            if (cursor == null) return;

            var transform = cursor.transform;
            Vector3 position = transform.localPosition;
            position.x = x;
            transform.localPosition = position;
        }

        /// <summary>Moves a window's cursor object to a full local position.</summary>
        /// <param name="cursor">Cursor object, or null.</param>
        /// <param name="position">Local position the window's own SetCursor would write.</param>
        internal static void MoveCursor(GameObject cursor, Vector3 position)
        {
            if (cursor == null) return;

            cursor.transform.localPosition = position;
        }
    }

    /// <summary>
    /// The window table and the click router. Rows are walked in order and the first open window decides the
    /// click, so a modal window listed above the battle action list takes precedence over it.
    /// </summary>
    internal static class MouseWindows
    {
        /// <summary>Entries in the pause menu, matching PauseItemLabel_1 .. PauseItemLabel_6.</summary>
        private const int PauseItemCount = 6;

        /// <summary>Entries in the title menu, matching ItemLabel_1 .. ItemLabel_6.</summary>
        private const int TitleItemCount = 6;

        /// <summary>
        /// Entries in the in-game system menu: six in the two rows of three, plus 返回标题 on its own.
        /// </summary>
        private const int SystemMenuMax = 7;

        /// <summary>Selectable rows in the setting window: eight settings plus the keyboard config row.</summary>
        private const int SettingRowCount = 9;

        private static WindowDescriptor[] _table;

        private static WindowDescriptor[] Table => _table ?? (_table = BuildTable());

        /// <summary>
        /// Activates the entry under a screen point in whichever window is open, or reports that an open window
        /// kept the click away from the battlefield.
        /// </summary>
        /// <param name="host">Adapter that resolves the windows and injects the confirm.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <param name="what">Receives a short description of what consumed the click.</param>
        /// <returns>True when a window consumed the click.</returns>
        internal static bool TryActivate(GameMouseAdapter host, Vector3 screenPoint, out string what)
        {            what = null;

            var table = Table;
            for (int row = 0; row < table.Length; row++)
            {
                var window = table[row];

                bool open;
                try { open = window.IsOpen(host); }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce($"probe the {window.Name}", e.Message);
                    continue;
                }
                if (!open) continue;

                Component target;
                try { target = window.Focus(host); }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce($"resolve the {window.Name}", e.Message);
                    target = null;
                }

                if (target == null)
                {
                    // The window says it owns input but its component could not be resolved. Nothing can be
                    // activated, but the click still has to stay away from the battlefield behind it.
                    if (!window.OwnsEveryClick(host)) continue;

                    what = $"the {window.Name} holds the click";
                    return true;
                }

                try
                {
                    int hit = HitEntry(window, target, screenPoint);
                    if (hit >= 0)
                    {
                        window.Select(target, hit);
                        host.Confirm();
                        what = $"{window.Name} entry {hit} selected and confirmed";

                        if (MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value)
                            MousePlugin.Verbose($"window click: {what}; pointer={screenPoint.x:F0},{screenPoint.y:F0}"
                                              + $" screen={Screen.width}x{Screen.height}{window.Describe(target)}");
                        return true;
                    }

                    if (window.OwnsEveryClick(host))
                    {
                        what = $"the {window.Name} holds the click";
                        return true;
                    }

                    // The action list floats over the battlefield, so a miss is a battlefield click after all.
                    if (MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value)
                        MousePlugin.Verbose($"window click: the pointer is not over an entry of the {window.Name};"
                                          + $" pointer={screenPoint.x:F0},{screenPoint.y:F0}"
                                          + $" screen={Screen.width}x{Screen.height}{window.Describe(target)}");
                }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce($"click in the {window.Name}", e.Message);
                    what = $"the {window.Name} holds the click";
                    return true;
                }
            }

            what = null;
            return false;
        }

        // ------------------------------------------------------------------ bottom hint bar

        /// <summary>The button bit a hint bar entry stands for, from its BUTTON_MENU_TYPE.</summary>
        private static uint HintBarMask(int type)
        {
            switch (type)
            {
                case 1: case 2: case 3: return KeyData.BtnOK;       // OK_DECIDE / OK_NEXT / OK_USE
                case 4: case 5: case 6: return KeyData.BtnCancel;   // CANCEL_CLOSE / CANCEL_BACK / CANCEL_CANCEL
                case 7: case 9: case 11: case 12: return (uint)ButtonSts.X;
                case 8: case 10: return (uint)ButtonSts.Y;          // Y_KEY_CONFIG / Y_SORT
                case 13: case 14: return (uint)ButtonSts.START;     // START_LAYOUT_END / START_STAFF
                default: return 0;                                  // NONE: labelled, see LabelledMask
            }
        }

        /// <summary>
        /// The button a type-less bar entry stands for, read from the text the game wrote into it.
        ///
        /// BUTTON_MENU_TYPE stops at START_STAFF, so the entries a screen adds on top of it carry type NONE and a
        /// label instead - that is how the world map offers 主菜单 and 地图移动 beside its 决定 and 返回. Only those
        /// labels are recognised; an unknown one stays unclickable rather than pressing a key the screen may not
        /// accept.
        /// </summary>
        /// <param name="label">Entry text, or null.</param>
        /// <returns>The button bit, or zero when the label is not one of the known ones.</returns>
        private static uint LabelledMask(string label)
        {
            if (string.IsNullOrEmpty(label)) return 0;

            // 主菜单 / main menu: START opens the system menu over the world map.
            if (label.IndexOf("菜单", StringComparison.Ordinal) >= 0
             || label.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0)
                return (uint)ButtonSts.START;

            // 地图移动 1 / 2: the shoulder keys walk the map, the same two the wheel drives.
            if (label == "1" || label == "L" || label == "LB") return (uint)ButtonSts.L;
            if (label == "2" || label == "R" || label == "RB") return (uint)ButtonSts.R;

            return 0;
        }

        /// <summary>Short name of a hint bar entry for the log line.</summary>
        private static string HintBarName(int type)
        {
            switch (type)
            {
                case 1: case 2: case 3: return "ok";
                case 4: case 5: case 6: return "cancel";
                case 7: case 9: case 11: case 12: return "x";
                case 8: case 10: return "y";
                case 13: case 14: return "start";
                default: return "none";
            }
        }

        /// <summary>
        /// Presses the bottom hint bar entry under the pointer. ButtonMenuWin lays the entries out as
        /// <c>_objButton[i]</c>, with the key each one advertises at <c>_buttonMenuType[i]</c>, so a click can
        /// press exactly the key the bar shows (决定 / 取消 / 排序 / 单位详情 / 配置完毕). The bar draws over the
        /// window behind it, so this runs before the window table.
        /// </summary>
        /// <param name="host">Adapter that resolves the bar and injects the press.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <param name="what">Receives a short description of what consumed the click.</param>
        /// <returns>True when a bar entry took the click.</returns>
        internal static bool TryActivateHintBar(GameMouseAdapter host, Vector3 screenPoint, out string what)
        {
            what = null;

            var bar = host.ButtonMenuComponent;
            if (bar == null) return false;

            try
            {
                if (!bar.gameObject.activeInHierarchy) return false;

                var buttons = bar._objButton;
                if (buttons == null) return false;

                var labels = bar._buttonLabel;
                var types = bar._buttonMenuType;
                int count = types == null ? buttons.Length : Mathf.Min(buttons.Length, types.Length);
                bool verbose = MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value;

                for (int index = 0; index < count; index++)
                {
                    var button = buttons[index];
                    if (button == null || !button.activeInHierarchy) continue;

                    // The button root and its label together are the rectangle the player sees. The root alone is
                    // narrower than that, which left a dead gap between neighbouring entries.
                    if (!CoversEntry(button, labels, index, screenPoint)) continue;

                    int type = types == null ? 0 : (int)types[index];
                    uint mask = HintBarMask(type);
                    if (mask == 0) mask = LabelledMask(EntryLabel(labels, index));
                    if (mask == 0) continue;

                    host.Press(mask, $"hint bar {HintBarName(type)}");
                    what = $"hint bar entry {index} ({HintBarName(type)})";

                    if (verbose)
                        MousePlugin.Verbose($"window click: {what}; pointer={screenPoint.x:F0},{screenPoint.y:F0}"
                                          + $" screen={Screen.width}x{Screen.height}{DescribeHintBar(bar)}");
                    return true;
                }

                if (verbose)
                    MousePlugin.Verbose($"window click: the hint bar holds no entry at"
                                      + $" {screenPoint.x:F0},{screenPoint.y:F0}"
                                      + $" screen={Screen.width}x{Screen.height}{DescribeHintBar(bar)}");
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("click in the hint bar", e.Message);
            }
            finally
            {
                // A bar that is closed, or whose array is missing, leaves no trace at all otherwise, which makes
                // "the bar entry is not clickable" impossible to tell apart from "the bar was never reached".
                if (MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value
                    && !_hintBarStateLogged)
                {
                    _hintBarStateLogged = true;
                    MousePlugin.Verbose($"hint bar state: {DescribeHintBarState(host)}");
                }
            }

            return false;
        }

        private static bool _hintBarStateLogged;

        private static string DescribeHintBarState(GameMouseAdapter host)
        {
            var bar = host.ButtonMenuComponent;
            if (bar == null) return "no ButtonMenuWinComponent";

            try
            {
                return $"active={bar.gameObject.activeInHierarchy}"
                     + $" buttons={(bar._objButton == null ? "null" : bar._objButton.Length.ToString())}"
                     + $" types={(bar._buttonMenuType == null ? "null" : bar._buttonMenuType.Length.ToString())}"
                     + $" max={bar.MAX_BUTTON_NUM}";
            }
            catch (Exception e) { return $"err({e.Message})"; }
        }

        /// <summary>The text of a bar entry, for the entries the game labels instead of typing.</summary>
        /// <param name="labels">One label per entry, or null.</param>
        /// <param name="index">Entry index.</param>
        /// <returns>The label text, or null.</returns>
        private static string EntryLabel(Il2CppReferenceArray<Text> labels, int index)
        {
            if (labels == null || index >= labels.Length) return null;

            try
            {
                var label = labels[index];
                return label == null ? null : label.text;
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read a hint bar label", e.Message);
                return null;
            }
        }

        /// <summary>
        /// The world map's own hint bar. MapSelectHandler draws this one itself - ButtonMenuObj with Icon_ok,
        /// Icon_menu and Icon_moveL/R - instead of going through ButtonMenuWin, so it is a separate hit test rather
        /// than a row in the table. Its three groups are 决定 (Z), 主菜单 (TAB) and the two map-move shoulders.
        /// </summary>
        /// <param name="host">Adapter that resolves the map and injects the press.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <param name="what">Receives a short description of what consumed the click.</param>
        /// <returns>True when the bar took the click.</returns>
        internal static bool TryActivateMapBar(GameMouseAdapter host, Vector3 screenPoint, out string what)
        {
            what = null;

            var map = FromMainFlow(host, flow => flow.MapSelectComponent, "MapSelectComponent");
            if (map == null) return false;

            var bar = map.ButtonMenuObj;
            if (bar == null || !bar.activeInHierarchy) return false;

            try
            {
                // The parts overlap: each label is a wide text box that covers its neighbours, so the smallest rect
                // holding the pointer is the one the player aimed at - an icon over a label, and the nearer label
                // over a wider one.
                uint mask = 0;
                string name = null;
                float best = float.MaxValue;

                Consider(map.Icon_ok, KeyData.BtnOK, "map bar 决定");
                Consider(map.Label_ok, KeyData.BtnOK, "map bar 决定");
                Consider(map.Icon_menu, (uint)ButtonSts.START, "map bar 主菜单");
                Consider(map.Label_menu, (uint)ButtonSts.START, "map bar 主菜单");
                Consider(map.Icon_moveL, (uint)ButtonSts.L, "map bar 地图移动 1");
                Consider(map.Icon_moveR, (uint)ButtonSts.R, "map bar 地图移动 2");
                Consider(map.Label_move, (uint)ButtonSts.R, "map bar 地图移动 2");

                // 现在 is not on the bar: it is the transpose button below it, and MapSelectHandler.Update runs
                // StartWorldChange on ButtonSts.X while TransButtonObj is showing.
                Consider(map.ButtonTransSheet, (uint)ButtonSts.X, "map bar 现在");
                Consider(map.TransButtonIcon, (uint)ButtonSts.X, "map bar 现在");
                Consider(map.LabelTrans, (uint)ButtonSts.X, "map bar 现在");

                bool verbose = MousePlugin.CfgVerboseLog != null && MousePlugin.CfgVerboseLog.Value;

                if (mask == 0)
                {
                    if (verbose)
                        MousePlugin.Verbose($"window click: the world map bar holds no part at"
                                          + $" {screenPoint.x:F0},{screenPoint.y:F0}{DescribeMapBar(map)}");
                    return false;
                }

                host.Press(mask, name);
                what = name;
                if (verbose) MousePlugin.Verbose($"window click: {name}{DescribeMapBar(map)}");
                return true;

                void Consider(Component part, uint partMask, string partName)
                {
                    float area = PartHit(part, screenPoint);
                    if (area < 0f || area >= best) return;

                    best = area;
                    mask = partMask;
                    name = partName;
                }
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("click in the world map bar", e.Message);
            }

            return false;
        }

        /// <summary>The layout area of a bar part when it holds the pointer, or a negative number when it does not.</summary>
        /// <param name="owner">Icon or label.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>The area in layout units, or -1.</returns>
        private static float PartHit(Component owner, Vector3 screenPoint)
        {
            var rect = WindowHitTest.RectOf(owner);
            if (rect == null || !rect.gameObject.activeInHierarchy) return -1f;
            if (!WindowHitTest.Contains(rect, screenPoint)) return -1f;

            return Mathf.Abs(rect.rect.width * rect.rect.height);
        }

        /// <summary>Every part of the world map's bar with its screen rect, for the verbose log.</summary>
        /// <param name="map">Live map handler.</param>
        /// <returns>One fragment per part.</returns>
        private static string DescribeMapBar(MapSelectHandler map)
        {
            var text = new StringBuilder();
            Part(text, "okIcon", map.Icon_ok);
            Part(text, "okLabel", map.Label_ok);
            Part(text, "menuIcon", map.Icon_menu);
            Part(text, "menuLabel", map.Label_menu);
            Part(text, "moveL", map.Icon_moveL);
            Part(text, "moveR", map.Icon_moveR);
            Part(text, "moveLabel", map.Label_move);
            Part(text, "transSheet", map.ButtonTransSheet);
            Part(text, "transIcon", map.TransButtonIcon);
            Part(text, "transLabel", map.LabelTrans);
            return text.ToString();
        }

        private static void Part(StringBuilder text, string name, Component owner)
        {
            var rect = WindowHitTest.RectOf(owner);
            if (rect == null) { text.Append($" [{name} none]"); return; }

            var camera = WindowHitTest.UiCameraFor(rect);
            Vector2 centre = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            text.Append($" [{name} centre={centre.x:F0},{centre.y:F0}"
                      + $" size={rect.rect.width:F0}x{rect.rect.height:F0}"
                      + $" active={rect.gameObject.activeInHierarchy}]");
        }

        /// <summary>Whether the pointer is on a bar part, which may be an icon or the label beside it.</summary>
        /// <param name="owner">Icon or label.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>True when the part is on screen and holds the pointer.</returns>
        private static bool Over(Component owner, Vector3 screenPoint)
        {
            var rect = WindowHitTest.RectOf(owner);
            return rect != null && rect.gameObject.activeInHierarchy && WindowHitTest.Contains(rect, screenPoint);
        }

        /// <summary>
        /// Whether the pointer sits on the rectangle a bar entry occupies on screen. An entry's root and its label
        /// are separate rects, so testing both is what makes the whole visible entry clickable instead of a
        /// narrower middle strip with dead gaps beside it.
        /// </summary>
        /// <param name="button">Entry root.</param>
        /// <param name="labels">One label per entry, or null.</param>
        /// <param name="index">Entry index.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>True when either rect contains the pointer.</returns>
        private static bool CoversEntry(
            GameObject button, Il2CppReferenceArray<Text> labels, int index, Vector3 screenPoint)
        {
            var rect = WindowHitTest.RectOf(button);
            if (rect != null && WindowHitTest.Contains(rect, screenPoint)) return true;

            if (labels == null || index >= labels.Length) return false;

            var label = WindowHitTest.RectOf(labels[index]);
            return label != null && WindowHitTest.Contains(label, screenPoint);
        }

        /// <summary>
        /// Every bar entry with its type and screen rect, for the verbose log. ButtonMenuWin.Open lays the bar
        /// out, so a click that lands on the wrong entry shows up here as rects in an unexpected order.
        /// </summary>
        /// <param name="bar">Live hint bar.</param>
        /// <returns>One fragment per entry.</returns>
        private static string DescribeHintBar(ButtonMenuWin bar)
        {
            var text = new StringBuilder();
            var buttons = bar._objButton;
            var types = bar._buttonMenuType;
            if (buttons == null) return " array=none";

            text.Append($" array={buttons.Length}");
            for (int index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                if (button == null) { text.Append($" [{index} null]"); continue; }

                int type = types == null || index >= types.Length ? -1 : (int)types[index];
                var rect = WindowHitTest.RectOf(button);
                if (rect == null) { text.Append($" [{index} t={type} no rect]"); continue; }

                var camera = WindowHitTest.UiCameraFor(rect);
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
                text.Append($" [{index} t={type} {HintBarName(type)} mask={HintBarMask(type)}"
                          + $" centre={centre.x:F0},{centre.y:F0}"
                          + $" size={rect.rect.width:F0}x{rect.rect.height:F0}"
                          + $" active={button.activeInHierarchy}]");
            }

            return text.ToString();
        }

        /// <summary>
        /// Whether a window that keeps every click is open. The battle action list is excluded: it is on screen
        /// for the whole player turn, so it must not stop the wheel from zooming.
        /// </summary>
        /// <param name="host">Adapter that resolves the windows.</param>
        /// <returns>True when a blocking window is open.</returns>
        internal static bool AnyWindowOpen(GameMouseAdapter host)
        {
            var table = Table;
            for (int row = 0; row < table.Length; row++)
            {
                var window = table[row];
                if (!window.OwnsEveryClick(host)) continue;

                try
                {
                    if (window.IsOpen(host)) return true;
                }
                catch (Exception e)
                {
                    MouseDiagnostics.WarnOnce($"probe the {window.Name}", e.Message);
                }
            }

            return false;
        }

        /// <summary>Index of the entry under the pointer, or -1 when the pointer misses them all.</summary>
        /// <param name="window">Window being clicked.</param>
        /// <param name="target">Component returned by <see cref="WindowDescriptor.Focus"/>.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>Entry index, or -1.</returns>
        private static int HitEntry(WindowDescriptor window, Component target, Vector3 screenPoint)
        {
            int count = window.Count(target);
            if (count <= 0) return -1;

            int hit = -1;
            float hitDistance = float.MaxValue;

            for (int index = 0; index < count; index++)
            {
                var entry = window.Entry(target, index);
                if (entry == null || !entry.gameObject.activeInHierarchy) continue;

                // The rectangle the player sees, which for a row drawn through a bigger child is not the entry.
                var rect = WindowHitTest.AimRect(entry);
                if (rect == null) continue;
                if (!WindowHitTest.ContainsWithin(rect, screenPoint, window.SpreadX, window.SpreadY)) continue;
                if (!IsVisibleThroughMask(rect, screenPoint)) continue;

                float distance = WindowHitTest.Distance(rect, screenPoint);
                if (distance >= hitDistance) continue;

                hit = index;
                hitDistance = distance;
            }

            return hit;
        }

        /// <summary>
        /// Whether a rect is actually on show, given the mask its list scrolls inside.
        ///
        /// A scrolled uGUI list keeps every row active and keeps its position, and only the mask hides the ones
        /// above and below the viewport. Without this a click in the strip a hidden row still occupies lands on
        /// that row: on the deployment screen the rows scrolled past the bottom sat right on the button bar and
        /// swallowed its clicks.
        /// </summary>
        /// <param name="rect">Entry rect.</param>
        /// <param name="screenPoint">Pointer position in screen pixels.</param>
        /// <returns>True when no mask hides the rect, or the pointer is inside that mask.</returns>
        private static bool IsVisibleThroughMask(RectTransform rect, Vector3 screenPoint)
        {
            var mask = rect.GetComponentInParent<RectMask2D>();
            if (mask != null) return WindowHitTest.Contains(mask.rectTransform, screenPoint);

            var legacy = rect.GetComponentInParent<Mask>();
            if (legacy != null) return WindowHitTest.Contains(legacy.rectTransform, screenPoint);

            return true;
        }

        // ------------------------------------------------------------------ resolving a window

        private static T FromMainFlow<T>(GameMouseAdapter host, Func<MainFlow, T> read, string what)
            where T : Component
        {
            var flow = host.MainFlowComponent;
            if (flow == null) return null;

            try { return read(flow); }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read MainFlow.{what}", e.Message);
                return null;
            }
        }

        private static T FromBattleFlow<T>(GameMouseAdapter host, Func<BattleFlow, T> read, string what)
            where T : Component
        {
            var flow = host.BattleFlowComponent;
            if (flow == null) return null;

            try { return read(flow); }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read BattleFlow.{what}", e.Message);
                return null;
            }
        }

        /// <summary>
        /// Whether a window reached from BattleFlow runs its own input read. The game's reads all start with a
        /// flag test followed by an early return, so that flag is also the gate a click has to pass.
        /// </summary>
        /// <param name="host">Adapter that resolves the published BattleFlow.</param>
        /// <param name="read">Reads the component from the flow.</param>
        /// <param name="armed">Reads the flag the window's own input read tests first.</param>
        /// <param name="what">Member name, used in the once-per-failure warning.</param>
        /// <returns>True when the window would process input.</returns>
        private static bool BattleWindowOpen<T>(
            GameMouseAdapter host, Func<BattleFlow, T> read, Func<T, bool> armed, string what)
            where T : Component
        {
            var component = FromBattleFlow(host, read, what);
            if (component == null) return false;

            try { return armed(component) && component.gameObject.activeInHierarchy; }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read {what} state", e.Message);
                return false;
            }
        }

        /// <summary>MainFlow counterpart of <see cref="BattleWindowOpen{T}"/>.</summary>
        /// <param name="host">Adapter that resolves the published MainFlow.</param>
        /// <param name="read">Reads the component from the flow.</param>
        /// <param name="armed">Reads the flag the window's own input read tests first.</param>
        /// <param name="what">Member name, used in the once-per-failure warning.</param>
        /// <returns>True when the window would process input.</returns>
        private static bool MainWindowOpen<T>(
            GameMouseAdapter host, Func<MainFlow, T> read, Func<T, bool> armed, string what)
            where T : Component
        {
            var component = FromMainFlow(host, read, what);
            if (component == null) return false;

            try { return armed(component) && component.gameObject.activeInHierarchy; }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read {what} state", e.Message);
                return false;
            }
        }

        /// <summary>
        /// Whether a window object BattleFlow owns is showing. Several windows keep their handler armed after
        /// CloseWin deactivates the window object, so the object is what tells the two apart.
        /// </summary>
        /// <param name="host">Adapter that resolves the published BattleFlow.</param>
        /// <param name="read">Reads the window object from the flow.</param>
        /// <param name="what">Member name, used in the once-per-failure warning.</param>
        /// <returns>True when the window object is active in the hierarchy.</returns>
        private static bool BattleWindowObjectOpen(GameMouseAdapter host, Func<BattleFlow, GameObject> read, string what)
        {
            var flow = host.BattleFlowComponent;
            if (flow == null) return false;

            try
            {
                var window = read(flow);
                return window != null && window.activeInHierarchy;
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read BattleFlow.{what}", e.Message);
                return false;
            }
        }

        private static bool MainFlowSays(GameMouseAdapter host, Func<MainFlow, bool> read, string what)
        {
            var flow = host.MainFlowComponent;
            if (flow == null) return false;

            try { return read(flow); }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read MainFlow.{what}", e.Message);
                return false;
            }
        }

        private static bool BattleFlowSays(GameMouseAdapter host, Func<BattleFlow, bool> read, string what)
        {
            var flow = host.BattleFlowComponent;
            if (flow == null) return false;

            try { return read(flow); }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce($"read BattleFlow.{what}", e.Message);
                return false;
            }
        }

        private static RectTransform Of(GameObject owner) => WindowHitTest.RectOf(owner);
        private static RectTransform Of(Component owner) => WindowHitTest.RectOf(owner);

        // ------------------------------------------------------------------ the table

        /// <summary>
        /// Every window that presents entries, most modal first. A row is only reached when every row above it
        /// reports itself closed, so the order is the click priority.
        ///
        /// The title menu comes first. It is the only list on the title screen, and the intro message that screen
        /// also shows is an open message window below it: with the menu further down, that message box took every
        /// click and the menu never saw one.
        /// </summary>
        /// <returns>The ordered window table.</returns>
        private static WindowDescriptor[] BuildTable()
        {
            return new WindowDescriptor[]
            {
                // TitleWin.TitleMenuComponent. The title menu.
                new EntriesWindow(
                    "title menu",
                    host => TitleMenuOpen(host),
                    host => TitleMenuOf(host),
                    target => Mathf.Clamp(((TitleMenu)target).m_nItemNum, 0, TitleItemCount),
                    (target, index) => Of(TitleItemLabel((TitleMenu)target, index)),
                    (target, index) => SelectTitle((TitleMenu)target, index)),

                // MainFlow.ConfirmWin. Yes/No, plus a third entry when the dialog asked for one. Its Update reads
                // m_nCurrentOffs after a confirm and also reacts to a raw left click, so writing the index is
                // what decides the outcome.
                new EntriesWindow(
                    "confirm window",
                    host => MainFlowSays(host, flow => flow.IsConfirmWinOpen(), "IsConfirmWinOpen")
                        && MainWindowOpen(host, flow => flow.ConfirmWinComponent, c => c.m_bInit, "ConfirmWinComponent"),
                    host => FromMainFlow(host, flow => flow.ConfirmWinComponent, "ConfirmWinComponent"),
                    target => ((ConfirmWinHandler)target).isThirdActive ? 3 : 2,
                    (target, index) => ConfirmEntry((ConfirmWinHandler)target, index),
                    (target, index) => SelectConfirm((ConfirmWinHandler)target, index)),

                // MapSelectHandler.MapSelectMenuComponent. The in-game system menu over the world map:
                // 单位情报 / 道具 / 设定 / 存档 / 读取 / 说明 / 返回标题. Open and Close are the GameObject's own
                // SetActive, so that - not the always-true m_bInit - is what says the menu is up. The entries are
                // 100x100 anchors inside tiles drawn about 230 across on a 300 by 260 pitch, so both axes are grown
                // by a little over half the gap; the nearest centre still decides between the overlaps.
                new EntriesWindow(
                    "system menu",
                    host => MapSelectMenuOpen(host),
                    host => MapSelectMenuOf(host),
                    target => Mathf.Clamp(((MapSelectMenu)target).s_nMenuItemNum, 0, SystemMenuMax),
                    (target, index) => Of(SystemMenuItem((MapSelectMenu)target, index)),
                    (target, index) => SelectSystemMenu((MapSelectMenu)target, index),
                    spreadX: 100f,
                    spreadY: 80f),

                // BattleFlow.PauseWin. The TAB menu, unchanged from the dedicated path it replaces.
                new EntriesWindow(
                    "pause menu",
                    host => BattleFlowSays(host, flow => flow.IsPauseWinOpen(), "IsPauseWinOpen"),
                    host => host.PauseWinComponent(),
                    target => Mathf.Clamp(((PauseWinHandler)target).m_nItemNum, 0, PauseItemCount),
                    (target, index) => Of(PauseItemLabel((PauseWinHandler)target, index)),
                    (target, index) => SelectPause((PauseWinHandler)target, index)),

                // MainFlow.MesWin. A message box has no list, so the whole box is one target.
                new WholeWindow(
                    "message window",
                    host => MainWindowOpen(host, flow => flow.MesWinComponent, c => c.m_bInit, "MesWinComponent"),
                    host => FromMainFlow(host, flow => flow.MesWinComponent, "MesWinComponent"),
                    target => Of(((MesWinHandler)target).labelObject)),

                // BattleFlow.ConvWin. The conversation box advances on the same confirm bit.
                new WholeWindow(
                    "conversation window",
                    host => BattleWindowObjectOpen(host, flow => flow.ConvWin, "ConvWin")
                        && BattleWindowOpen(host, flow => flow.ConvWinComponent, c => c.IsWhileConv(), "ConvWinComponent"),
                    host => FromBattleFlow(host, flow => flow.ConvWinComponent, "ConvWinComponent"),
                    target => Of(((FaceConvWinHandler)target).ConvText)),

                // BattleFlow.TutorialWin. A help window that turns its page on the same confirm bit, but only
                // while a page is on screen: its input read sits inside the displayed-page step.
                new WholeWindow(
                    "tutorial window",
                    host => BattleWindowObjectOpen(host, flow => flow.TutorialWin, "TutorialWin")
                        && BattleWindowOpen(host, flow => flow.TutorialWinComponent, c => c._step == 2, "TutorialWinHandler"),
                    host => FromBattleFlow(host, flow => flow.TutorialWinComponent, "TutorialWinComponent"),
                    target => Of(((TutorialWinHandler)target).TutorialText)),

                // MainFlow.UnitWin. The unit detail window, which shows a join prompt during a result. It does not
                // own a miss: on the item screen it is only the stat panel beside the slime grid, and owning the
                // miss there swallowed every click meant for the grid underneath.
                new EntriesWindow(
                    "unit window",
                    host => MainWindowOpen(host, flow => flow.UnitWinComponent, c => c.m_bInit, "UnitWinComponent"),
                    host => FromMainFlow(host, flow => flow.UnitWinComponent, "UnitWinComponent"),
                    target => ((UnitWinHandler)target).m_bDisableJoinNGButton ? 1 : 2,
                    (target, index) => Of(UnitWinButton((UnitWinHandler)target, index)),
                    (target, index) => SelectUnitWin((UnitWinHandler)target, index),
                    ownsEveryClick: _ => false),

                // MainFlow.UnitSelectComponent. The slime list, laid out as a grid of UNIT_BLOCK_X columns. In the
                // pre-battle layout the grid floats over the battlefield the player is still aiming on, so a click
                // that misses it belongs to the field: that is what places the unit the grid just picked.
                new EntriesWindow(
                    "unit list",
                    host => MainWindowOpen(host, flow => flow.UnitSelectComponent, c => c.m_bInit, "UnitSelectComponent"),
                    host => FromMainFlow(host, flow => flow.UnitSelectComponent, "UnitSelectComponent"),
                    target => UnitBlockCount((UnitSelectWin)target),
                    (target, index) => UnitBlockEntry((UnitSelectWin)target, index),
                    (target, index) => SelectUnitSelect((UnitSelectWin)target, index),
                    ownsEveryClick: host => !IsUnitDeployLayout(host)),

                // MainFlow.ItemSelectWinComponent. A 4x2 grid whose index is the item type itself. An entry is only
                // the icon block, while the drawn row carries the item name and its count far to the right, so the
                // hit area is grown sideways to reach them. Rows sit 75 apart and are 100 tall, so they already
                // meet vertically and need no vertical margin.
                new EntriesWindow(
                    "item list",
                    host => MainWindowOpen(host, flow => flow.ItemSelectWinComponent, c => c.m_bInit, "ItemSelectWinComponent"),
                    host => FromMainFlow(host, flow => flow.ItemSelectWinComponent, "ItemSelectWinComponent"),
                    target => ItemListCount((ItemSelectWin)target),
                    (target, index) => ItemListEntry((ItemSelectWin)target, index),
                    (target, index) => SelectItem((ItemSelectWin)target, index),
                    spreadX: 190f),

                // MainFlow.SaveLoadWinComponent. The save slots.
                new EntriesWindow(
                    "save window",
                    host => MainWindowOpen(host, flow => flow.SaveLoadWinComponent, c => c.m_bInit, "SaveLoadWinComponent"),
                    host => FromMainFlow(host, flow => flow.SaveLoadWinComponent, "SaveLoadWinComponent"),
                    target => SlotCount((SaveLoadWinHandler)target),
                    (target, index) => SlotEntry((SaveLoadWinHandler)target, index),
                    (target, index) => SelectSlot((SaveLoadWinHandler)target, index)),

                // MainFlow.SettingWinComponent. Eight setting rows plus the keyboard config row. The row count the
                // game keeps is a static whose storage IL2CPP does not emit, so the fixed list is used instead and
                // the per-entry active test in HitEntry drops whichever rows the window is not showing.
                new EntriesWindow(
                    "setting window",
                    host => MainWindowOpen(host, flow => flow.SettingWinComponent, c => c.m_bInit, "SettingWinComponent"),
                    host => FromMainFlow(host, flow => flow.SettingWinComponent, "SettingWinComponent"),
                    target => SettingRowCount,
                    (target, index) => Of(SettingRow((SettingWinHandler)target, index)),
                    (target, index) => SelectSetting((SettingWinHandler)target, index)),

                // BattleFlow.ActionButtonComponent. The action list on the left of the battlefield, which is also
                // the two entry occupation dialog: at ACTION_STEP 1 it holds only occupy and not_occupy, and
                // UpdateKey commits m_nCursorPos == 0 as occupy and anything else as not_occupy. This row is last
                // and does not own a miss, so a click beside the list still reaches the battlefield.
                new EntriesWindow(
                    "action list",
                    host => ActionListOpen(host),
                    host => FromBattleFlow(host, flow => flow.ActionButtonComponent, "ActionButtonComponent"),
                    target => ActionRows((ActionButtonHandler)target).Count,
                    (target, index) => Of(ActionRow((ActionButtonHandler)target, index)),
                    (target, index) => SelectAction((ActionButtonHandler)target, index),
                    ownsEveryClick: _ => false,
                    describe: DescribeActionList),
            };
        }

        /// <summary>Upper bound on the slime list, standing in until the list's own block array is readable.</summary>
        private const int UnitBlockMax = 256;

        // ------------------------------------------------------------------ confirm window

        private static RectTransform ConfirmEntry(ConfirmWinHandler confirm, int index)
        {
            switch (index)
            {
                case 0: return Of(confirm.YesButtonObj);
                case 1: return Of(confirm.NoButtonObj);
                case 2: return Of(confirm.ThirdButtonObj);
                default: return null;
            }
        }

        /// <summary>
        /// Writes the index and moves the yes/no cursor. ConfirmWinHandler.SetCursor places it at
        /// <c>YesX - 74</c>, <c>NoX - 74</c> or <c>ThirdX - 104</c> (Assembly-CSharp2.cpp:12915).
        /// </summary>
        /// <param name="confirm">Live confirm window.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectConfirm(ConfirmWinHandler confirm, int index)
        {
            confirm.m_nCurrentOffs = index;

            float x = index == 0 ? confirm.YesX : index == 1 ? confirm.NoX : confirm.ThirdX;
            WindowHitTest.MoveCursorX(confirm.CursorObj, x - (index == 2 ? 104f : 74f));
        }

        // ------------------------------------------------------------------ pause menu

        private static Text PauseItemLabel(PauseWinHandler pause, int index)
        {
            switch (index)
            {
                case 0: return pause.PauseItemLabel_1;
                case 1: return pause.PauseItemLabel_2;
                case 2: return pause.PauseItemLabel_3;
                case 3: return pause.PauseItemLabel_4;
                case 4: return pause.PauseItemLabel_5;
                case 5: return pause.PauseItemLabel_6;
                default: return null;
            }
        }

        /// <summary>
        /// Writes the index and moves the cursor onto the clicked row. PauseWinHandler.SetCursor places it at
        /// <c>124 - index * 50</c> (Assembly-CSharp4.cpp:23777).
        /// </summary>
        /// <param name="pause">Live pause menu.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectPause(PauseWinHandler pause, int index)
        {
            pause.m_nCurrentOffs = index;
            WindowHitTest.MoveCursorY(pause.CursorObj, 124f - index * 50f);
        }

        // ------------------------------------------------------------------ unit detail window

        private static GameObject UnitWinButton(UnitWinHandler unit, int index)
        {
            return index == 0 ? unit.ButtonJoinOK : unit.ButtonJoinNG;
        }

        /// <summary>
        /// Writes the index and moves the cursor between the two join buttons. UnitWinHandler.SetCursor places it
        /// at <c>-205</c> for index 0 and <c>8</c> otherwise (Assembly-CSharp7.cpp:20493).
        /// </summary>
        /// <param name="unit">Live unit detail window.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectUnitWin(UnitWinHandler unit, int index)
        {
            unit.m_nCursorOffs = index;
            WindowHitTest.MoveCursorX(unit.CursorObj, index == 0 ? -205f : 8f);
        }

        // ------------------------------------------------------------------ slime list

        private static int UnitBlockCount(UnitSelectWin list)        {
            int wanted = Mathf.Clamp(list.m_nUnitNum, 0, UnitBlockMax);

            var blocks = list.UnitBlock;
            if (blocks == null) return 0;

            return Mathf.Min(wanted, blocks.Length);
        }

        /// <summary>
        /// Whether the pre-battle deployment is running. Only there may a click that misses the grid fall through
        /// to the battlefield, because only there is the player still aiming a tile cursor at a placement
        /// position. The other modes open the list as a picker over a battle that is waiting on the choice, so
        /// there a miss stays with the list.
        /// </summary>
        /// <param name="host">Adapter that resolves the running battle.</param>
        /// <returns>True while the deployment is in its layout step.</returns>
        private static bool IsUnitDeployLayout(GameMouseAdapter host) => host.IsUnitDeployLayout;

        private static RectTransform UnitBlockEntry(UnitSelectWin list, int index)
        {
            // UnitBlock is indexed by slime offset, not by grid slot: SetUnitBlock places
            // UnitBlock[m_nSlimeOffsTable[i]] at grid slot i (Assembly-CSharp7.cpp:12738).
            var table = list.m_nSlimeOffsTable;
            var blocks = list.UnitBlock;
            if (table == null || blocks == null || index >= table.Length) return null;

            int slimeOffs = table[index];
            if (slimeOffs < 0 || slimeOffs >= blocks.Length) return null;

            return Of(blocks[slimeOffs]);
        }

        /// <summary>
        /// Splits the flat entry index into the grid cursor and moves the cursor onto the cell.
        /// UnitSelectWin.SetCursor places it at <c>UNIT_BLOCK_POS_X + x * INTER_W - 46</c> by
        /// <c>UNIT_BLOCK_POS_Y - y * INTER_H - 13</c> (Assembly-CSharp7.cpp:12860), and SelectUnit rebuilds the
        /// flat index as <c>m_nCursorX + m_nCursorY * UNIT_BLOCK_X</c> (Assembly-CSharp7.cpp:13554).
        /// </summary>
        /// <param name="list">Live slime list.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectUnitSelect(UnitSelectWin list, int index)
        {
            int columns = Mathf.Max(1, list.UNIT_BLOCK_X);
            int x = index % columns;
            int y = index / columns;

            list.m_nCursorX = x;
            list.m_nCursorY = y;

            WindowHitTest.MoveCursor(list.CursorObj, new Vector3(
                list.UNIT_BLOCK_POS_X + x * list.UNIT_BLOCK_INTER_W - 46f,
                list.UNIT_BLOCK_POS_Y - y * list.UNIT_BLOCK_INTER_H - 13f,
                0f));
        }

        // ------------------------------------------------------------------ item list

        private static int ItemListCount(ItemSelectWin items)
        {
            var list = items.m_ItemList;
            return list == null ? 0 : Mathf.Clamp(items.m_nListNum, 0, list.Length);
        }

        private static RectTransform ItemListEntry(ItemSelectWin items, int index)
        {
            var list = items.m_ItemList;
            if (list == null || index >= list.Length) return null;

            return Of(list[index]);
        }

        /// <summary>
        /// Writes the index and moves the cursor onto the cell. ItemSelectWin.SetCursor puts the cursor at the
        /// cell's own list position shifted by <c>(-60, +18)</c> (Assembly-CSharp3.cpp:11908). That list position
        /// comes from the native GetListPos, so the same shift is applied to the cell object's local position.
        /// </summary>
        /// <param name="items">Live item list.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectItem(ItemSelectWin items, int index)
        {
            items.m_nCursorOffs = index;

            var cell = ItemListEntry(items, index);
            if (cell == null) return;

            Vector3 position = cell.localPosition;
            position.x -= 60f;
            position.y += 18f;
            position.z = 0f;
            WindowHitTest.MoveCursor(items.m_CursorObj, position);
        }

        // ------------------------------------------------------------------ save slots

        private static int SlotCount(SaveLoadWinHandler save)
        {
            var sheets = save.BGImage;
            return sheets == null ? 0 : sheets.Length;
        }

        /// <summary>
        /// The row itself, not the block inside it. <c>BGImage</c> is the sheet <c>SetBGColor</c> paints and turns
        /// yellow for the active slot, so it is the rectangle the player sees and aims at; <c>DataObj</c> is only
        /// the 100x100 inner icon, and hit testing that left most of each row dead.
        /// </summary>
        /// <param name="save">Live save window.</param>
        /// <param name="index">Slot index.</param>
        /// <returns>The slot's row rect, or null.</returns>
        private static RectTransform SlotEntry(SaveLoadWinHandler save, int index)
        {
            var sheets = save.BGImage;
            if (sheets == null || index >= sheets.Length) return null;

            return Of(sheets[index]);
        }

        /// <summary>
        /// Writes the index and moves the cursor onto the slot. SaveLoadWinHandler.SetCursor fixes X at
        /// <c>-387</c> and places Y at <c>158 - index * 132</c> (Assembly-CSharp5.cpp:11010).
        /// </summary>
        /// <param name="save">Live save window.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectSlot(SaveLoadWinHandler save, int index)
        {
            save.m_nCurrentOffs = index;
            WindowHitTest.MoveCursorY(save.CursorObj, 158f - index * 132f);
        }

        // ------------------------------------------------------------------ setting rows

        private static Text SettingRow(SettingWinHandler setting, int index)
        {
            switch (index)
            {
                case 0: return setting.labelBgmSetting;
                case 1: return setting.labelSeSetting;
                case 2: return setting.labelHighSpeedSetting;
                case 3: return setting.labelVibrationSetting;
                case 4: return setting.labelLanguageSetting;
                case 5: return setting.labelButtonModeSetting;
                case 6: return setting.labelResolutionSetting;
                case 7: return setting.labelKeyDirModeSetting;
                case 8: return setting.labelKeyboardTitle;
                default: return null;
            }
        }

        /// <summary>
        /// Writes the index and moves the cursor onto the row. SettingWinHandler.SetCursor places it at
        /// <c>x = index even ? -482 : 18</c> and <c>y = 165 - (index / 2) * 100</c>
        /// (Assembly-CSharp5.cpp:14545).
        /// </summary>
        /// <param name="setting">Live setting window.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectSetting(SettingWinHandler setting, int index)
        {
            setting.m_nCursorOffs = index;
            WindowHitTest.MoveCursor(setting.m_CursorObj, new Vector3(
                index % 2 == 0 ? -482f : 18f,
                165f - (index / 2) * 100f,
                0f));
        }

        // ------------------------------------------------------------------ title menu

        /// <summary>The in-game system menu over the world map, reached from MapSelectHandler.PushMenuButton.</summary>
        private static MapSelectMenu MapSelectMenuOf(GameMouseAdapter host)
        {
            var map = FromMainFlow(host, flow => flow.MapSelectComponent, "MapSelectComponent");
            if (map == null) return null;

            try { return map.MapSelectMenuComponent; }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read MapSelectHandler.MapSelectMenuComponent", e.Message);
                return null;
            }
        }

        private static bool MapSelectMenuOpen(GameMouseAdapter host)
        {
            var menu = MapSelectMenuOf(host);
            if (menu == null) return false;

            // MapSelectMenu.Open/Close are the GameObject's own SetActive, so this is the menu's real state.
            try { return menu.gameObject.activeInHierarchy; }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read MapSelectMenu state", e.Message);
                return false;
            }
        }

        private static GameObject SystemMenuItem(MapSelectMenu menu, int index)
        {
            var items = menu.MenuItem;
            if (items == null || index >= items.Length) return null;

            return items[index];
        }

        /// <summary>
        /// Writes the index and moves the cursor onto the entry. MapSelectMenu.SetCursor is a three column grid:
        /// entries 0-2 at <c>y = 166</c>, 3-5 at <c>y = -94</c>, both at <c>x = -442 + (offs % 3) * 300</c>, and
        /// the last entry (返回标题) at <c>(-200, -280)</c> (Assembly-CSharp4.cpp:21111).
        /// </summary>
        /// <param name="menu">Live system menu.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectSystemMenu(MapSelectMenu menu, int index)
        {
            menu.m_nCurrentOffs = index;

            Vector3 position;
            if (index < 3) position = new Vector3(-442f + (index % 3) * 300f, 166f, 0f);
            else if (index < 6) position = new Vector3(-442f + (index % 3) * 300f, -94f, 0f);
            else position = new Vector3(-200f, -280f, 0f);

            WindowHitTest.MoveCursor(menu.CursorObj, position);
        }

        private static bool TitleMenuOpen(GameMouseAdapter host)
        {
            var title = FromMainFlow(host, flow => flow.TitleWinComponent, "TitleWinComponent");
            if (title == null) return false;

            try { return title.IsTitleMenuOpen(); }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read TitleWin.IsTitleMenuOpen", e.Message);
                return false;
            }
        }

        private static TitleMenu TitleMenuOf(GameMouseAdapter host)
        {
            var title = FromMainFlow(host, flow => flow.TitleWinComponent, "TitleWinComponent");
            if (title == null) return null;

            try { return title.TitleMenuComponent; }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read TitleWin.TitleMenuComponent", e.Message);
                return null;
            }
        }

        private static Text TitleItemLabel(TitleMenu title, int index)
        {
            switch (index)
            {
                case 0: return title.ItemLabel_1;
                case 1: return title.ItemLabel_2;
                case 2: return title.ItemLabel_3;
                case 3: return title.ItemLabel_4;
                case 4: return title.ItemLabel_5;
                case 5: return title.ItemLabel_6;
                default: return null;
            }
        }

        /// <summary>
        /// Writes the index and moves the cursor onto the row. TitleMenu.SetCursor places it at
        /// <c>80 - index * 40</c> (Assembly-CSharp5.cpp:20894).
        /// </summary>
        /// <param name="title">Live title menu.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectTitle(TitleMenu title, int index)
        {
            title.m_nCurrentOffs = index;
            WindowHitTest.MoveCursorY(title.CursorObj, 80f - index * 40f);
        }

        // ------------------------------------------------------------------ action list

        /// <summary>
        /// Whether the action list is showing rows a click may act on. The guards mirror the top of
        /// ActionButtonHandler.UpdateKey, which returns before it reads any button unless it is the player's turn
        /// and the unit is neither moving nor busy, so a click can only act when the key would have.
        /// </summary>
        /// <param name="host">Adapter that resolves the published BattleFlow.</param>
        /// <returns>True when the list would process input.</returns>
        private static bool ActionListOpen(GameMouseAdapter host)
        {
            var flow = host.BattleFlowComponent;
            if (flow == null) return false;

            try
            {
                if (!flow.m_bUserTurn || flow.m_bUnitMoving || flow.m_bUnitBusy) return false;

                var actions = flow.ActionButtonComponent;
                if (actions == null || actions.m_nActiveButtonNum <= 0) return false;

                // SetButtons leaves CursorObj off and adds no row for every step that has no list, so an active
                // count with a cursor object is the list's own notion of being open.
                return actions.CursorObj != null;
            }
            catch (Exception e)
            {
                MouseDiagnostics.WarnOnce("read ActionButtonHandler state", e.Message);
                return false;
            }
        }

        /// <summary>
        /// The list's entries in the order the cursor walks them, capped at the number of rows the list reports.
        /// SetActionButtonActive activates <c>ActionButtonObj[(int)type]</c> while it stores the row's type at
        /// <c>m_nActionButtonType[row]</c>, so the object array is indexed by action type, not by row. SetActionButtonPos
        /// then places row i at <c>fButtonY_Bottom + i * fButtonY_Interval</c>, which makes the active objects
        /// ordered by their local y exactly rows 0 .. rowCount-1 of the dispatch UpdateKey reads
        /// (Assembly-CSharp1.cpp:6651, 7100).
        /// </summary>
        /// <param name="actions">Live action list.</param>
        /// <returns>Entries in cursor order.</returns>
        private static List<GameObject> ActionRows(ActionButtonHandler actions)
        {
            var rows = new List<GameObject>();

            var objects = actions.ActionButtonObj;
            if (objects == null) return rows;

            // AllActionButtonsOff clears indices below ACTION_BUTTON_TYPE.max only, and SetActionButtonActive
            // only ever activates one of those, so anything past that end of the array carries the active flag
            // from the prefab and would otherwise be counted as a row (Assembly-CSharp1.cpp:6584).
            int managed = Mathf.Min(objects.Length, ActionTypeMax);

            for (int index = 0; index < managed; index++)
            {
                // A type this step does not need is inactive, which also skips the spares the handler keeps.
                var entry = objects[index];
                if (entry == null || !entry.activeInHierarchy) continue;

                rows.Add(entry);
            }

            rows.Sort((left, right) => left.transform.localPosition.y.CompareTo(right.transform.localPosition.y));

            int active = Mathf.Clamp(actions.m_nActiveButtonNum, 0, rows.Count);
            if (rows.Count > active) rows.RemoveRange(active, rows.Count - active);

            return rows;
        }

        private static RectTransform ActionRow(ActionButtonHandler actions, int index)
        {
            var rows = ActionRows(actions);
            return index < rows.Count ? Of(rows[index]) : null;
        }

        /// <summary>
        /// Writes the row index and moves the cursor onto the row. ActionButtonHandler.SetCursorPos places it at
        /// <c>m_fCursorBaseY + pos * fButtonY_Interval + _currentPosYAmend</c> (Assembly-CSharp1.cpp:8307).
        /// </summary>
        /// <param name="actions">Live action list.</param>
        /// <param name="index">Entry the pointer landed on.</param>
        private static void SelectAction(ActionButtonHandler actions, int index)
        {
            actions.m_nCursorPos = index;
            WindowHitTest.MoveCursorY(actions.CursorObj,
                actions.m_fCursorBaseY + index * actions.fButtonY_Interval + actions._currentPosYAmend);
        }

        /// <summary>Indices the action list manages: AllActionButtonsOff clears 0 .. ACTION_BUTTON_TYPE.max - 1.</summary>
        private const int ActionTypeMax = 6;

        /// <summary>
        /// Raw action list state for the verbose miss line: the array the handler manages, the row count it
        /// reports, and where every object in the array sits right now. A miss with the right rows here and the
        /// wrong screen centres points at the coordinate space, and a wrong row set points at the array.
        /// </summary>
        /// <param name="target">Live ActionButtonHandler.</param>
        /// <returns>The state, as one log fragment.</returns>
        private static string DescribeActionList(Component target)
        {
            var actions = (ActionButtonHandler)target;
            var text = new StringBuilder();
            text.Append($" active={actions.m_nActiveButtonNum} cursorPos={actions.m_nCursorPos}"
                      + $" baseY={actions.m_fCursorBaseY:F1} interval={actions.fButtonY_Interval:F1}"
                      + $" amend={actions._currentPosYAmend:F1}");

            var objects = actions.ActionButtonObj;
            text.Append($" array={(objects == null ? -1 : objects.Length)}");
            if (objects == null) return text.ToString();

            for (int index = 0; index < objects.Length; index++)
            {
                var entry = objects[index];
                if (entry == null) { text.Append($" [{index} null]"); continue; }

                var rect = Of(entry);
                string size = rect == null ? "none" : $"{rect.rect.width:F0}x{rect.rect.height:F0}";
                text.Append($" [{index} {entry.name} y={entry.transform.localPosition.y:F1}"
                          + $" rect={size} active={entry.activeInHierarchy}]");
            }

            var cursor = Of(actions.CursorObj);
            if (cursor == null) text.Append(" cursor=none");
            else text.Append($" cursor={cursor.name} y={cursor.localPosition.y:F1}"
                           + $" parent={cursor.parent?.name}");

            return text.ToString();
        }
    }
}
