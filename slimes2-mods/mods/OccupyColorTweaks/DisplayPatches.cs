using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SlimesOccupyColor
{
    // ------------------------------------------------------------------ display
    //
    // Everything below only reads state and calls rendering methods that the game itself calls from
    // SetPossessioned/SetSucked. It never writes occupation state, side type, ability or stats: an earlier
    // attempt wrote m_nSideType and turned possessed units hostile, so that class of write is off the table.

    /// <summary>
    /// Unit sprites are looked up by name in the unit SpriteAtlas. UnitTypeTable builds two families:
    ///   map sprite   &lt;TEX&gt;_m (normal) / &lt;TEX&gt;_mr (occupied) / &lt;TEX&gt;_g_m (possessed)
    ///   portrait     &lt;TEX&gt;_face / &lt;TEX&gt;_b_face / &lt;TEX&gt;_g_face
    /// A unit the game never lets you possess has no _mr entry, so an occupation asks the atlas for a name that
    /// does not exist and Unity draws nothing. Falling back to the plain sprite keeps the unit looking exactly
    /// as it did before, which is the desired outcome.
    /// </summary>
    [HarmonyPatch(typeof(MainFlow), "GetAtlasSprite")]
    internal static class Patch_GetAtlasSprite
    {
        private static readonly Dictionary<string, string[]> Fallbacks = new Dictionary<string, string[]>
        {
            { "_mr",     new[] { "_m" } },
            { "_b_face", new[] { "_face" } },
            { "_g_m",    new[] { "_mr", "_m" } },
            { "_g_face", new[] { "_b_face", "_face" } },
        };

        private static bool _inFallback;
        private static readonly HashSet<string> ReportedMissing = new HashSet<string>();

        /// <summary>Latest MainFlow instance seen here, used to reach the unit SpriteAtlas from static hooks.</summary>
        internal static MainFlow CachedMainFlow;

        [HarmonyPostfix]
        private static void Postfix(MainFlow __instance, ATLAS_TYPE atlas, string str, ref Sprite __result)
        {
            if (__instance != null) CachedMainFlow = __instance;
            if (_inFallback || __result != null) return;
            if (!OccupyColorPlugin.CfgEnabled.Value || !OccupyColorPlugin.CfgDisplayFallback.Value) return;
            if (atlas != ATLAS_TYPE.ATLAS_UNIT && atlas != ATLAS_TYPE.ATLAS_FACE) return;
            if (str == null) return;

            string[] candidates = null;
            string stem = null;
            foreach (var rule in Fallbacks)
            {
                if (!str.EndsWith(rule.Key, StringComparison.Ordinal)) continue;
                candidates = rule.Value;
                stem = str.Substring(0, str.Length - rule.Key.Length);
                break;
            }
            if (candidates == null) return;

            string used = null;
            _inFallback = true;   // the nested lookups must not re-enter this postfix
            try
            {
                foreach (var suffix in candidates)
                {
                    __result = __instance.GetAtlasSprite(atlas, stem + suffix);
                    if (__result != null) { used = stem + suffix; break; }
                }
            }
            finally { _inFallback = false; }

            if (used != null && ReportedMissing.Add(str))
                OccupyColorPlugin.Instance.Log.LogInfo($"sprite fallback: '{str}' -> '{used}'");
        }
    }

    /// <summary>
    /// A unit taken over in battle has its quad driven to alpha 0 by the suck path, because the slime sitting on
    /// it is what the game normally draws; the possession finish is what restores it, and that step never runs
    /// for a unit the game refuses to let you possess. Only the alpha value is overridden, and only while the
    /// unit sits in OCCUPY_STATE.occupied, so ordinary fade animations are untouched.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), "SetUnitQuadTransparent")]
    internal static class Patch_SetUnitQuadTransparent
    {
        private const int StateOccupied = 3;
        private static readonly HashSet<IntPtr> Reported = new HashSet<IntPtr>();

        [HarmonyPrefix]
        private static void Prefix(UnitData __instance, ref float alpha)
        {
            if (!OccupyColorPlugin.CfgEnabled.Value || !OccupyColorPlugin.CfgDisplayFallback.Value) return;
            if (alpha > 0.01f) return;
            if (NativeMember.GetInt(__instance, "m_nUserUnitOffs", -1) < 0) return;
            if (NativeMember.GetInt(__instance, "m_nOccupyState", -1) != StateOccupied) return;

            alpha = 1.0f;
            if (Reported.Add(__instance.Pointer))
                OccupyColorPlugin.Verbose($"quad alpha 0 on an occupied player unit -> 1 (type {NativeMember.GetInt(__instance, "nType", -1)})");
        }
    }

    /// <summary>
    /// Two things are needed for an occupied player unit to draw at all, and neither is restored by the game
    /// when the occupation comes from a save:
    ///   1. the quad texture (SetUnitQuadTexture, rebuilt above), and
    ///   2. the material colour, because SetUnitQuadTransparent writes _Color with alpha 0 while the unit is
    ///      being sucked and nothing raises it again for a unit the game refuses to possess.
    /// So the alpha is re-asserted every frame, and the render parameters are reported once per unit so a
    /// failure can be read directly instead of guessed.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), "UpdateUnit")]
    internal static class Patch_UpdateUnit
    {
        private const int StateOccupied = 3;
        private static readonly HashSet<IntPtr> Repainted = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> Inspected = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> ReportedUv = new HashSet<IntPtr>();

        /// <summary>Unit types whose sheet cannot show the frames the game plays, so they need rebuilt art.</summary>
        private static readonly HashSet<int> TypesNeedingArt = new HashSet<int>();

        [HarmonyPostfix]
        private static void Postfix(UnitData __instance)
        {
            if (!OccupyColorPlugin.CfgEnabled.Value || !OccupyColorPlugin.CfgDisplayFallback.Value) return;

            int type = NativeMember.GetInt(__instance, "nType", -1);
            int state = NativeMember.GetInt(__instance, "m_nOccupyState", -1);
            bool ours = NativeMember.GetInt(__instance, "m_nUserUnitOffs", -1) >= 0;

            // The body is a quad sampling one cell of a multi-frame sprite sheet, so the material's UV scale and
            // offset decide which cell is drawn and an empty cell looks exactly like missing art. Report them for
            // every unit once, so an occupied unit can be compared with the working units next to it.
            if (type >= 0 && ReportedUv.Add(__instance.Pointer))
            {
                string uv = "(n/a)";
                try
                {
                    if (NativeMember.GetRaw(__instance, "materialUnitQuad") is Material m)
                        uv = $"scale={m.mainTextureScale} offset={m.mainTextureOffset} tex={m.mainTexture?.name}";
                }
                catch (Exception e) { uv = $"({e.Message})"; }

                OccupyColorPlugin.Instance.Log.LogInfo(
                    $"unit uv: type={(UNIT_TYPE)type} state={state} ours={ours} {uv}");
            }

            if (!ours) return;
            if (state != StateOccupied) return;

            var material = NativeMember.GetRaw(__instance, "materialUnitQuad") as Material;
            var quad = NativeMember.GetRaw(__instance, "unitQuad") as GameObject;
            Renderer renderer = null;
            try { if (quad != null) renderer = quad.GetComponent<Renderer>(); }
            catch (Exception e) { OccupyColorPlugin.Warn($"GetComponent<Renderer> failed: {e.Message}"); }

            if (Repainted.Add(__instance.Pointer))
            {
                try { __instance.SetUnitQuadTexture(false); }
                catch (Exception e) { OccupyColorPlugin.Warn($"SetUnitQuadTexture failed: {e.Message}"); }

                // The animation the game plays for an occupied unit belongs to its possessed sheet; the state
                // this unit is stuck in made it sample the plain sheet, whose cells for those frames are empty.
                // Preferring the game's own possessed texture therefore restores the animation and shows the
                // faction art the game would have used.
                Patch_SetUnitQuadTexture.PreferPossessedTexture(material, NativeMember.GetInt(__instance, "nType", -1));
            }

            if (material != null)
            {
                try
                {
                    material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));

                    int typeForLog = NativeMember.GetInt(__instance, "nType", -1);
                    if (Patch_SetUnitQuadTexture.EnsureVisibleCell(material, $"type={typeForLog}"))
                        TypesNeedingArt.Add(typeForLog);

                    // Every unit taken over by a forced possession gets the faction colour, so the "red means
                    // ours" language of the game holds for all of them. Only units whose sheet cannot show the
                    // played frames need it to be visible at all; the rest are tinted to match.
                    if (OccupyColorPlugin.CfgFactionTint.Value)
                    {
                        var tinted = Patch_SetUnitQuadTexture.FactionTinted(material.mainTexture, typeForLog);
                        var current = material.mainTexture;
                        if (tinted != null && (current == null || current.GetInstanceID() != tinted.GetInstanceID()))
                            material.mainTexture = tinted;
                    }
                }
                catch (Exception e) { OccupyColorPlugin.Warn($"material fix failed: {e.Message}"); }
            }

            // A disabled Renderer draws nothing whatever the material holds, and nothing re-enables it for a
            // unit whose occupation was restored from a save.
            if (renderer != null)
            {
                try
                {
                    if (!renderer.enabled) renderer.enabled = true;
                    if (renderer.sharedMaterial != null) renderer.sharedMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                }
                catch (Exception e) { OccupyColorPlugin.Warn($"renderer restore failed: {e.Message}"); }
            }

            if (Inspected.Add(__instance.Pointer))
            {
                string texture = "(none)";
                string color = "(n/a)";
                try
                {
                    if (material != null)
                    {
                        var main = material.mainTexture;
                        if (main != null) texture = $"{main.name} {main.width}x{main.height}";
                        color = material.GetColor("_Color").ToString();
                    }
                }
                catch (Exception e) { texture = $"({e.Message})"; }

                bool rendererEnabled = false;
                string shader = "(n/a)";
                try
                {
                    if (renderer != null)
                    {
                        rendererEnabled = renderer.enabled;
                        if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
                            shader = renderer.sharedMaterial.shader.name;
                    }
                }
                catch (Exception e) { shader = $"({e.Message})"; }

                // A quad with no mesh draws nothing whatever its material holds.
                string mesh = "(n/a)";
                try
                {
                    var filter = quad?.GetComponent<MeshFilter>();
                    if (filter == null) mesh = "(no MeshFilter)";
                    else
                    {
                        var shared = filter.sharedMesh;
                        mesh = shared == null ? "NULL" : $"{shared.name} verts={shared.vertexCount}";
                    }
                }
                catch (Exception e) { mesh = $"({e.Message})"; }

                OccupyColorPlugin.Instance.Log.LogInfo(
                    $"unit render: type={NativeMember.GetInt(__instance, "nType", -1)} state={StateOccupied} "
                  + $"hasMaterial={material != null} tex={texture} color={color} "
                  + $"hasRenderer={renderer != null} rendererEnabled={rendererEnabled} shader={shader} mesh={mesh}");
            }
        }
    }

    /// <summary>
    /// The visible body is a quad whose texture comes from UnitTex.GetTexture, which is
    /// UnitTex.TextureArray[UnitTypeTable.GetUnitTexID(type, isPossesioned)]. A unit the game never lets you
    /// possess has no entry for the flag its state produces, so that array yields null, the quad samples
    /// nothing and the unit renders as air. The sprite atlas fallback above cannot help here, because the body
    /// never asks the atlas. When the wanted texture is missing, take the other flag's texture instead, which
    /// is the faction-change art the game already draws for the same unit.
    /// </summary>
    [HarmonyPatch(typeof(UnitTex), "GetTexture")]
    internal static class Patch_UnitTexGetTexture
    {
        private static bool _inFallback;
        private static readonly HashSet<string> Reported = new HashSet<string>();

        [HarmonyPostfix]
        private static void Postfix(UNIT_TYPE type, bool isPossesioned, ref Texture __result)
        {
            if (_inFallback) return;
            if (!OccupyColorPlugin.CfgEnabled.Value || !OccupyColorPlugin.CfgDisplayFallback.Value) return;

            // The unit art for a unit that can never be possessed is a transparent placeholder rather than a
            // missing entry, so emptiness - not just null - has to trigger the swap to the other variant.
            bool blank = Patch_SetUnitQuadTexture.IsBlank(__result, $"{type}/possessed={isPossesioned}");
            if (!blank) return;

            _inFallback = true;
            try { __result = UnitTex.GetTexture(type, !isPossesioned); }
            catch (Exception e) { OccupyColorPlugin.Warn($"UnitTex fallback failed: {e.Message}"); }
            finally { _inFallback = false; }

            string key = $"{type}/{isPossesioned}";
            if (Reported.Add(key))
                OccupyColorPlugin.Instance.Log.LogInfo(
                    $"unit texture blank: type {type} possessed={isPossesioned} -> "
                  + (__result == null ? "(no texture for either variant)" : $"possessed={!isPossesioned}"));
        }
    }

    /// <summary>
    /// The body quad is a material whose texture the game takes from UnitTex. For a unit it never lets you
    /// possess that provider yields a blank placeholder, so the body draws nothing however valid the atlas
    /// entry is. This rebuilds the body texture from the unit's own map sprite in the unit atlas, as a
    /// standalone Texture2D, and assigns it after the game has set its own.
    ///
    /// The game's own texture is never replaced: an earlier attempt rebuilt it from the sprite atlas and that
    /// overwrote the multi-frame sheet of units which did have art, so those units lost their animation. What
    /// remains here only inspects and, where needed, tints or re-samples the sheet the game already uses.
    /// </summary>
    internal static class Patch_SetUnitQuadTexture
    {
        private const int StateOccupied = 3;
        private sealed class SheetMap
        {
            internal int Columns;
            internal int Rows;
            internal readonly HashSet<int> Filled = new HashSet<int>();                  // row * Columns + column
            internal readonly Dictionary<int, List<int>> RowsWithArt = new Dictionary<int, List<int>>();
            internal readonly List<int> ColumnsWithArt = new List<int>();

            internal bool Has(int column, int row) => Filled.Contains(row * Columns + column);

            internal int NearestRow(int column, int row)
            {
                if (!RowsWithArt.TryGetValue(column, out var rows) || rows.Count == 0) return -1;
                int best = rows[0];
                foreach (var candidate in rows)
                    if (Mathf.Abs(candidate - row) < Mathf.Abs(best - row)) best = candidate;
                return best;
            }

            internal int NearestColumn(int column)
            {
                if (ColumnsWithArt.Count == 0) return -1;
                int best = ColumnsWithArt[0];
                foreach (var candidate in ColumnsWithArt)
                    if (Mathf.Abs(candidate - column) < Mathf.Abs(best - column)) best = candidate;
                return best;
            }
        }

        private static readonly Dictionary<int, SheetMap> SheetMaps = new Dictionary<int, SheetMap>();
        private static readonly HashSet<string> ReportedSnaps = new HashSet<string>();

        /// <summary>
        /// The body quad shows one cell of a multi-frame sprite sheet, chosen by the material's UV scale and
        /// offset, and most cells of a unit sheet are empty. A unit stuck on an empty cell looks exactly like a
        /// unit with no art at all - texture, material, renderer and mesh all check out - which is why the body
        /// of a unit the game refuses to possess stays invisible once its animation stops.
        ///
        /// The mapping keeps the animation alive: the requested column is kept and only the row is moved, to the
        /// nearest row of that column that has art, so the frame sequence still plays. The column itself is only
        /// moved when it has no art in any row. Rendering only.
        /// </summary>
        internal static bool EnsureVisibleCell(Material material, string tag)
        {
            if (material == null) return false;

            var raw = material.mainTexture;
            if (raw == null) return false;

            // The interop cast is used instead of a CLR cast: mainTexture is an Il2Cpp object and the CLR cast
            // silently yields null for it, which is what kept the cell scan from ever running.
            var texture = raw.TryCast<Texture2D>();
            if (texture == null)
            {
                if (ReportedTextureType.Add(raw.GetType().Name))
                    OccupyColorPlugin.Instance.Log.LogInfo($"{tag}: mainTexture is not a Texture2D ({raw.GetType().Name})");
                return false;
            }

            var scale = material.mainTextureScale;
            if (scale.x <= 0.0001f || scale.y <= 0.0001f) return false;

            int id = texture.GetInstanceID();
            if (!SheetMaps.TryGetValue(id, out var map))
            {
                map = BuildSheetMap(texture, scale, tag);
                SheetMaps[id] = map;
            }
            if (map.Filled.Count == 0) return false;

            var offset = material.mainTextureOffset;
            int column = Mathf.FloorToInt(offset.x / scale.x + 0.5f);
            int row = Mathf.FloorToInt(offset.y / scale.y + 0.5f);
            if (map.Has(column, row)) return false;

            int targetColumn = column;
            int targetRow = map.NearestRow(column, row);
            if (targetRow < 0)
            {
                // No art anywhere in this column: the sheet's frames live in a block of consecutive columns, so
                // wrapping by the block size keeps consecutive animation frames on consecutive art columns
                // instead of collapsing them all onto one cell.
                int count = map.ColumnsWithArt.Count;
                targetColumn = count > 0 ? map.ColumnsWithArt[((column % count) + count) % count] : -1;
                targetRow = targetColumn < 0 ? -1 : map.NearestRow(targetColumn, row);
            }
            if (targetRow < 0) return false;

            material.mainTextureOffset = new Vector2(targetColumn * scale.x, targetRow * scale.y);

            string key = $"{id}/{column}/{row}";
            if (ReportedSnaps.Add(key))
                OccupyColorPlugin.Instance.Log.LogInfo(
                    $"{tag}: cell ({column},{row}) is empty -> ({targetColumn},{targetRow}) "
                  + $"[{map.Filled.Count}/{map.Columns * map.Rows} cells have art]");
            return true;
        }

        private static readonly HashSet<string> ReportedTextureType = new HashSet<string>();

        private static SheetMap BuildSheetMap(Texture2D texture, Vector2 scale, string tag)
        {
            var map = new SheetMap();
            map.Columns = 1;
            map.Rows = 1;

            var copy = CopyWhole(texture);
            if (copy == null) return map;

            int cellWidth = Mathf.Max(1, Mathf.RoundToInt(scale.x * copy.width));
            int cellHeight = Mathf.Max(1, Mathf.RoundToInt(scale.y * copy.height));
            map.Columns = Mathf.Max(1, copy.width / cellWidth);
            map.Rows = Mathf.Max(1, copy.height / cellHeight);

            var pixels = copy.GetPixels32();
            for (int row = 0; row < map.Rows; row++)
            {
                for (int column = 0; column < map.Columns; column++)
                {
                    int visible = 0;
                    for (int y = row * cellHeight; y < (row + 1) * cellHeight && y < copy.height; y++)
                    {
                        int line = y * copy.width;
                        for (int x = column * cellWidth; x < (column + 1) * cellWidth && x < copy.width; x++)
                            if (pixels[line + x].a > 8) visible++;
                    }

                    if (visible <= 4) continue;

                    map.Filled.Add(row * map.Columns + column);
                    if (!map.RowsWithArt.TryGetValue(column, out var rows))
                    {
                        rows = new List<int>();
                        map.RowsWithArt[column] = rows;
                        map.ColumnsWithArt.Add(column);
                    }
                    rows.Add(row);
                }
            }

            OccupyColorPlugin.Instance.Log.LogInfo(
                $"{tag}: '{copy.name}' {copy.width}x{copy.height} cell {cellWidth}x{cellHeight} -> "
              + $"{map.Filled.Count}/{map.Columns * map.Rows} cells have art, "
              + $"columns with art {map.ColumnsWithArt.Count}/{map.Columns}");
            return map;
        }

        private static readonly HashSet<int> ReportedPossessed = new HashSet<int>();
        private static readonly Dictionary<int, Texture2D> TintedCache = new Dictionary<int, Texture2D>();

        /// <summary>
        /// The game has no possessed art for a unit it never lets you possess (asking for it throws out of
        /// UnitTypeTable.GetUnitTexID because the UNIT_TEX entry does not exist), so the faction look has to be
        /// produced here: the unit's own sheet is copied and recoloured towards the bright pink-red the game uses
        /// for a taken-over unit, keeping the original shading through the luminance of every pixel.
        /// </summary>
        internal static Texture2D FactionTinted(Texture source, int type)
        {
            if (!OccupyColorPlugin.CfgFactionTint.Value || source == null) return source as Texture2D;
            if (TintedCache.TryGetValue(type, out var cached) && cached != null) return cached;

            var readable = source.TryCast<Texture2D>();
            if (readable == null) return null;

            var copy = CopyWhole(readable);
            if (copy == null) return readable;

            // A new Texture2D defaults to bilinear filtering, which blurs pixel art when the quad is scaled up;
            // the game's own sheets are point sampled, so the copy has to follow them.
            copy.filterMode = readable.filterMode;
            copy.wrapMode = readable.wrapMode;

            float red = OccupyColorPlugin.CfgTintRed.Value;
            float green = OccupyColorPlugin.CfgTintGreen.Value;
            float blue = OccupyColorPlugin.CfgTintBlue.Value;
            float glow = OccupyColorPlugin.CfgTintGlow.Value;
            float strength = Mathf.Clamp01(OccupyColorPlugin.CfgTintStrength.Value);

            var pixels = copy.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0) continue;

                // Blending towards the tint instead of replacing keeps the original shading, so highlights are
                // not clipped away and the sprite stays readable.
                float r0 = pixels[i].r, g0 = pixels[i].g, b0 = pixels[i].b;
                float r1 = Mathf.Clamp(r0 * red + glow, 0f, 255f);
                float g1 = Mathf.Clamp(g0 * green + glow * 0.25f, 0f, 255f);
                float b1 = Mathf.Clamp(b0 * blue + glow * 0.6f, 0f, 255f);

                pixels[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Lerp(r0, r1, strength)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(g0, g1, strength)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(b0, b1, strength)),
                    pixels[i].a);
            }

            copy.SetPixels32(pixels);
            copy.Apply();
            TintedCache[type] = copy;
            OccupyColorPlugin.Instance.Log.LogInfo(
                $"type {(UNIT_TYPE)type}: faction tint applied to '{copy.name}' "
              + $"(r*{red} g*{green} b*{blue} +{glow}, blend {strength:0.00}, filter {copy.filterMode})");
            return copy;
        }

        internal static void PreferPossessedTexture(Material material, int type)
        {
            if (material == null || type < 0) return;
            if (NoPossessedArt.Contains(type)) return;

            try
            {
                var possessed = UnitTex.GetTexture((UNIT_TYPE)type, true);
                if (possessed == null) return;

                var current = material.mainTexture;
                if (current != null && current.GetInstanceID() == possessed.GetInstanceID()) return;
                if (IsBlank(possessed, $"{type}/possessed-only")) return;

                material.mainTexture = possessed;
                if (ReportedPossessed.Add(type))
                    OccupyColorPlugin.Instance.Log.LogInfo(
                        $"type {(UNIT_TYPE)type}: using the game's possessed art '{possessed.name}' "
                      + $"({possessed.width}x{possessed.height})");
            }
            catch (Exception e)
            {
                // The unit's UNIT_TEX entry for the possessed variant does not exist, so GetUnitTexID throws
                // (Requested value 'unitNN_g' was not found). Remember it and stop asking.
                NoPossessedArt.Add(type);
                OccupyColorPlugin.Instance.Log.LogInfo($"type {(UNIT_TYPE)type}: the game has no possessed art ({e.Message.Trim()})");
            }
        }

        private static readonly HashSet<int> NoPossessedArt = new HashSet<int>();

        private static Texture2D CopyWhole(Texture2D source)
        {
            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.SetPixels(source.GetPixels());
                copy.Apply();
                return copy;
            }
            catch (Exception e)
            {
                OccupyColorPlugin.Verbose($"'{source.name}' not readable ({e.Message}); using a RenderTexture copy");
                return CopyWholeThroughRenderTexture(source);
            }
        }

        private static int CountOpaque(Texture2D texture)
        {
            try
            {
                var pixels = texture.GetPixels32();
                int visible = 0;
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].a > 8) visible++;
                return visible;
            }
            catch (Exception e)
            {
                OccupyColorPlugin.Warn($"reading back the copy failed: {e.Message}");
                return -1;
            }
        }

        private static readonly Dictionary<string, bool> BlankCache = new Dictionary<string, bool>();

        /// <summary>
        /// Reports whether a texture draws nothing. The unit art for a unit that can never be possessed is a
        /// transparent placeholder rather than a missing entry, so a null check alone never catches it.
        /// </summary>
        internal static bool IsBlank(Texture source, string cacheKey)
        {
            if (source == null) return true;
            if (BlankCache.TryGetValue(cacheKey, out bool known)) return known;

            bool blank;
            var copy = source is Texture2D readable ? CopyViaPixels(readable) : null;
            if (copy == null) copy = CopyWholeThroughRenderTexture(source);

            if (copy == null) blank = false;             // unreadable: assume it draws something
            else
            {
                int opaque = CountOpaque(copy);
                blank = opaque == 0;
                OccupyColorPlugin.Instance.Log.LogInfo(
                    $"texture check {cacheKey}: {copy.width}x{copy.height} opaque={opaque} -> "
                  + (opaque == 0 ? "BLANK" : "has art"));
            }

            BlankCache[cacheKey] = blank;
            return blank;
        }

        private static Texture2D CopyViaPixels(Texture2D source)
        {
            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.SetPixels(source.GetPixels());
                copy.Apply();
                return copy;
            }
            catch (Exception e)
            {
                OccupyColorPlugin.Verbose($"texture not readable ({e.Message})");
                return null;
            }
        }

        private static Texture2D CopyWholeThroughRenderTexture(Texture source)
        {
            RenderTexture temporary = null;
            var previous = RenderTexture.active;
            try
            {
                temporary = RenderTexture.GetTemporary(source.width, source.height, 0);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy;
            }
            catch (Exception e)
            {
                OccupyColorPlugin.Warn($"whole-texture copy failed: {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
