// Chocobo Hot & Cold Radar for FINAL FANTASY IX (Memoria engine)
//
// The Hot & Cold mini-game (Chocobo's Forest 2950, Lagoon 2951, Air Garden 2952)
// creates nine hidden "dig point" objects (uid 128..136) every time the field is
// entered.  The event script picks one of them at random as the current treasure
// and stores its 1-based index in map variable #37 (SByte).  The distance to that
// object is also the value the game itself uses to decide Choco's "Kweh" level
// (global byte #166, 2..6 -> seek animation 1..5).
//
// This mod reads those two variables and draws:
//   * a beacon / ring directly on the dig point in the field (world marker),
//   * a HUD compass that points towards the dig point and shows the distance,
//   * an off-screen edge arrow when the dig point is outside the camera view.
//
// Nothing is patched: the data is read-only, so the mod is compatible with
// saves, other mods and any Memoria version that still uses the vanilla fields.

using System;
using System.Globalization;
using System.IO;
using FF9;
using Memoria;
using Memoria.Prime;
using UnityEngine;

namespace ChocoboHotCold
{
    // ------------------------------------------------------------------
    // Bootstrap
    //
    // Memoria only exposes gameplay hooks for battles, so a field HUD needs a
    // different entry point.  ScriptsLoader enumerates every type of this
    // assembly and calls GetCustomAttributes() on it, which instantiates the
    // attribute below.  That gives us code execution at start-up; we use it to
    // subscribe to Camera.onPreRender, which is invoked on the Unity main
    // thread, and create the persistent overlay GameObject from there.
    // ------------------------------------------------------------------
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ChocoboHotColdBootstrapAttribute : Attribute
    {
        public ChocoboHotColdBootstrapAttribute()
        {
            ChocoboHotColdOverlay.InstallMainThreadPump();
        }
    }

    [ChocoboHotColdBootstrap]
    public sealed class ChocoboHotColdBootstrap : IOverloadOnBattleInitScript
    {
        // Fallback: if the pump above could not be installed for some reason,
        // the overlay is at least created when a battle starts.
        public void OnBattleInit()
        {
            ChocoboHotColdOverlay.Ensure();
        }
    }

    // ------------------------------------------------------------------
    // Overlay
    // ------------------------------------------------------------------
    public sealed class ChocoboHotColdOverlay : MonoBehaviour
    {
        // ---------------- configuration ----------------
        private static Boolean s_enabled = true;
        private static Boolean s_showWorldMarker = true;
        private static Boolean s_showCompass = true;
        private static Boolean s_showDistance = true;
        private static Boolean s_showAllPoints = false;
        private static Boolean s_onlyDuringMinigame = false;
        private static Boolean s_showKweh = true;
        private static Single s_unitsPerMeter = 100f;
        private static Single s_scale = 1f;
        private static Single s_markerHeight = 70f;

        private static ChocoboHotColdOverlay s_instance;
        private static Boolean s_pumpInstalled;
        private static Boolean s_pumpErrorLogged;
        private static Boolean s_logPumpInstalled;
        private static Int32 s_createFailures;
        private static Int32 s_logAttempts;

        private const Single ReferenceHeight = 1080f;
        private const Int32 FieldForest = 2950;
        private const Int32 FieldLagoon = 2951;
        private const Int32 FieldAirGarden = 2952;
        private const Int32 TreasureVarIndex = 37;   // map SByte
        private const Int32 KwehVarIndex = 166;      // global Byte
        private const Int32 FirstDigPointUid = 128;  // .. 136

        // ---------------- runtime state ----------------
        private Camera _camera;
        private Boolean _errorLogged;
        private Int32 _errorCount;
        private Int32 _lastScreenHeight = -1;
        private Int32 _lastMapNo = -1;

        private Boolean _hasTarget;
        private Vector3 _targetWorld;
        private Vector3 _playerWorld;
        private Single _distance;
        private Int32 _kweh;
        private Int32 _pointIndex;
        private String _lastLogKey;

        private readonly System.Collections.Generic.List<Vector3> _allPoints =
            new System.Collections.Generic.List<Vector3>(9);

        // ---------------- styles / textures ----------------
        private GUIStyle _numberStyle;
        private Texture2D _ringTex;
        private Texture2D _discTex;
        private Texture2D _arrowTex;
        private Texture2D _dotTex;
        private Texture2D _lineTex;
        private Single _uiScale = 1f;

        // ==================================================================
        // Bootstrap plumbing
        // ==================================================================
        public static void InstallMainThreadPump()
        {
            if (s_pumpInstalled)
                return;

            try
            {
                Camera.onPreRender -= PumpPreRender;
                Camera.onPreRender += PumpPreRender;
                Camera.onPostRender -= PumpPostRender;
                Camera.onPostRender += PumpPostRender;
                s_pumpInstalled = true;

                // Secondary main-thread trigger: Unity invokes logMessageReceived
                // on the main thread, and the game logs regularly.
                if (!s_logPumpInstalled)
                {
                    Application.logMessageReceived -= PumpFromLog;
                    Application.logMessageReceived += PumpFromLog;
                    s_logPumpInstalled = true;
                }

                Log.Message("[ChocoboHotCold] Main-thread pump installed.");
            }
            catch (Exception ex)
            {
                if (!s_pumpErrorLogged)
                {
                    s_pumpErrorLogged = true;
                    Log.Warning(ex, "[ChocoboHotCold] Failed to install the main-thread pump.");
                }
            }
        }

        private static void PumpFromLog(String condition, String stackTrace, LogType type)
        {
            if (s_instance != null)
            {
                try
                {
                    Application.logMessageReceived -= PumpFromLog;
                }
                catch (Exception)
                {
                    // ignored
                }
                return;
            }

            if (s_logAttempts > 20)
                return;

            s_logAttempts++;
            Pump();
        }

        private static void PumpPreRender(Camera cam)
        {
            Pump();
        }

        private static void PumpPostRender(Camera cam)
        {
            Pump();
        }

        private static void Pump()
        {
            if (s_instance != null || s_createFailures > 60)
                return;

            try
            {
                Ensure();
                if (s_instance != null)
                {
                    try
                    {
                        Camera.onPreRender -= PumpPreRender;
                        Camera.onPostRender -= PumpPostRender;
                        Application.logMessageReceived -= PumpFromLog;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
            catch (Exception ex)
            {
                s_createFailures++;
                if (s_createFailures <= 3)
                    Log.Warning(ex, "[ChocoboHotCold] Could not create the overlay, retrying.");
            }
        }

        public static void Ensure()
        {
            if (s_instance != null)
                return;

            LoadConfig();

            GameObject go = new GameObject("ChocoboHotCold.Overlay");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_instance = go.AddComponent<ChocoboHotColdOverlay>();

            Log.Message("[ChocoboHotCold] Version 1.0 ready. WorldMarker={0}, Compass={1}, UnitsPerMeter={2}.",
                s_showWorldMarker, s_showCompass, s_unitsPerMeter);
        }

        private static void LoadConfig()
        {
            try
            {
                String dllPath = typeof(ChocoboHotColdOverlay).Assembly.Location;
                if (String.IsNullOrEmpty(dllPath))
                    return;

                String scriptsDir = Path.GetDirectoryName(dllPath);
                String modDir = Path.GetFullPath(Path.Combine(scriptsDir, "../.."));
                String iniPath = Path.Combine(modDir, "ChocoboHotCold.ini");
                if (!File.Exists(iniPath))
                {
                    Log.Message("[ChocoboHotCold] No config at {0}; using defaults.", iniPath);
                    return;
                }

                foreach (String rawLine in File.ReadAllLines(iniPath))
                {
                    String line = rawLine.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[')
                        continue;

                    Int32 eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    String key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    String value = line.Substring(eq + 1).Trim();
                    if (value.Length == 0)
                        continue;

                    switch (key)
                    {
                        case "enabled":
                            s_enabled = ParseBool(value);
                            break;
                        case "showworldmarker":
                            s_showWorldMarker = ParseBool(value);
                            break;
                        case "showcompass":
                            s_showCompass = ParseBool(value);
                            break;
                        case "showdistance":
                            s_showDistance = ParseBool(value);
                            break;
                        case "showallpoints":
                            s_showAllPoints = ParseBool(value);
                            break;
                        case "onlyduringminigame":
                            s_onlyDuringMinigame = ParseBool(value);
                            break;
                        case "showkweh":
                            s_showKweh = ParseBool(value);
                            break;
                        case "unitspermeter":
                            s_unitsPerMeter = Mathf.Clamp(ParseFloat(value, s_unitsPerMeter), 1f, 10000f);
                            break;
                        case "scale":
                            s_scale = Mathf.Clamp(ParseFloat(value, s_scale), 0.5f, 3f);
                            break;
                        case "markerheight":
                            s_markerHeight = Mathf.Clamp(ParseFloat(value, s_markerHeight), 0f, 500f);
                            break;
                    }
                }

                Log.Message("[ChocoboHotCold] Config loaded from {0}.", iniPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ChocoboHotCold] Failed to read ChocoboHotCold.ini; using defaults.");
            }
        }

        private static Boolean ParseBool(String value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
                default:
                    return false;
            }
        }

        private static Single ParseFloat(String value, Single fallback)
        {
            Single result;
            if (Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return result;
            return fallback;
        }

        // ==================================================================
        // Data acquisition
        // ==================================================================
        private void Update()
        {
            if (_errorLogged)
                return;

            try
            {
                UpdateTarget();
            }
            catch (Exception ex)
            {
                _errorCount++;
                if (_errorCount == 1 || _errorCount % 600 == 0)
                    Log.Warning(ex, "[ChocoboHotCold] Update failed ({0} times); the overlay keeps trying.", _errorCount);
                if (_errorCount > 600)
                    _errorLogged = true;
            }
        }

        private void UpdateTarget()
        {
            _hasTarget = false;

            if (!s_enabled)
                return;

            CommonState common = FF9StateSystem.Common;
            if (common == null || common.FF9 == null)
                return;

            Int32 mapNo = common.FF9.fldMapNo;
            if (mapNo != FieldForest && mapNo != FieldLagoon && mapNo != FieldAirGarden)
                return;

            EventEngine ee = PersistenSingleton<EventEngine>.Instance;
            if (ee == null || ee.gMode != 1 || ee.eBin == null)
                return;

            // Do not draw over the main menu / pause menu.
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            if (ui != null && (ui.IsPause || ui.HUDState != UIManager.UIState.FieldHUD))
                return;

            if (s_onlyDuringMinigame && !(TimerUI.Play && TimerUI.Time > 0f))
                return;

            Boolean newField = mapNo != _lastMapNo;

            Byte[] mapVar = ee.GetMapVar();
            Int32 index = mapVar != null && mapVar.Length > TreasureVarIndex ? (SByte)mapVar[TreasureVarIndex] : 0;

            if (newField)
            {
                _lastMapNo = mapNo;
                _allPoints.Clear();
                _lastLogKey = null;
                Log.Message("[ChocoboHotCold] Dig field {0} detected (mapVar[{1}]={2}).",
                    mapNo, TreasureVarIndex, index);
            }

            _allPoints.Clear();
            if (s_showAllPoints)
            {
                for (Int32 i = 0; i < 9; i++)
                {
                    PosObj p = ResolveDigPoint(ee, FirstDigPointUid + i);
                    if (p != null)
                        _allPoints.Add(new Vector3(p.pos[0], p.pos[1], p.pos[2]));
                }
            }

            if (index < 1 || index > 9)
                return;

            PosObj target = ResolveDigPoint(ee, FirstDigPointUid + index - 1);
            if (target == null)
            {
                String key = "missing" + index.ToString(CultureInfo.InvariantCulture);
                if (!String.Equals(_lastLogKey, key))
                {
                    _lastLogKey = key;
                    Log.Warning("[ChocoboHotCold] Treasure index {0} but object uid {1} was not found.",
                        index, FirstDigPointUid + index - 1);
                }
                return;
            }

            PosObj player = ResolvePlayer(ee);
            if (player == null)
                return;

            _pointIndex = index;
            _targetWorld = new Vector3(target.pos[0], target.pos[1], target.pos[2]);
            _playerWorld = new Vector3(player.pos[0], player.pos[1], player.pos[2]);

            Single dx = _targetWorld.x - _playerWorld.x;
            Single dz = _targetWorld.z - _playerWorld.z;
            _distance = Mathf.Sqrt(dx * dx + dz * dz);

            _kweh = 0;
            try
            {
                Byte[] global = FF9StateSystem.EventState != null ? FF9StateSystem.EventState.gEventGlobal : null;
                if (global != null && global.Length > KwehVarIndex)
                    _kweh = Mathf.Clamp(global[KwehVarIndex] - 1, 0, 5);
            }
            catch (Exception)
            {
                _kweh = 0;
            }

            _hasTarget = true;

            if (s_showDistance && !String.Equals(_lastLogKey, index.ToString(CultureInfo.InvariantCulture)))
            {
                _lastLogKey = index.ToString(CultureInfo.InvariantCulture);
                Log.Message("[ChocoboHotCold] Target #{0} at ({1}, {2}), distance {3}.",
                    index, Mathf.RoundToInt(_targetWorld.x), Mathf.RoundToInt(_targetWorld.z), Mathf.RoundToInt(_distance));
            }
        }

        private static PosObj ResolveDigPoint(EventEngine ee, Int32 uid)
        {
            return ee.FindObjByUID(uid) as PosObj;
        }

        private static PosObj ResolvePlayer(EventEngine ee)
        {
            return ee.FindObjByUID(ee.GetControlUID()) as PosObj;
        }

        // ==================================================================
        // Camera
        // ==================================================================
        private Camera GetCamera()
        {
            if (_camera != null && _camera.gameObject.activeInHierarchy)
                return _camera;

            // The field renderer uses a dedicated camera called "FieldMap Camera";
            // it is not necessarily the one tagged MainCamera.
            GameObject camGo = GameObject.Find("FieldMap Camera");
            if (camGo != null)
                _camera = camGo.GetComponent<Camera>();

            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                for (Int32 i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].gameObject.activeInHierarchy && cameras[i].enabled)
                    {
                        _camera = cameras[i];
                        break;
                    }
                }
            }
            return _camera;
        }

        // ==================================================================
        // Drawing
        // ==================================================================
        private void OnGUI()
        {
            if (!s_enabled || !_hasTarget)
                return;
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (Screen.height != _lastScreenHeight || _numberStyle == null)
                RebuildStyles();

            Color oldColor = GUI.color;
            Int32 oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;

            try
            {
                GUI.depth = -100;

                Camera cam = GetCamera();
                if (cam == null)
                    return;

                if (s_showAllPoints)
                    DrawAllPoints(cam);

                if (s_showWorldMarker)
                    DrawWorldMarker(cam);

                if (s_showCompass)
                    DrawCompass(cam);
            }
            catch (Exception ex)
            {
                _errorCount++;
                if (_errorCount == 1 || _errorCount % 600 == 0)
                    Log.Warning(ex, "[ChocoboHotCold] OnGUI failed ({0} times).", _errorCount);
                if (_errorCount > 600)
                    _errorLogged = true;
            }
            finally
            {
                GUI.color = oldColor;
                GUI.depth = oldDepth;
                GUI.matrix = oldMatrix;
            }
        }

        private Vector3 ToGui(Vector3 screenPoint)
        {
            return new Vector3(screenPoint.x, Screen.height - screenPoint.y, screenPoint.z);
        }

        private static Boolean IsInsideScreen(Vector3 guiPoint, Single margin)
        {
            return guiPoint.x >= margin && guiPoint.x <= Screen.width - margin
                && guiPoint.y >= margin && guiPoint.y <= Screen.height - margin;
        }

        private void DrawAllPoints(Camera cam)
        {
            for (Int32 i = 0; i < _allPoints.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(_allPoints[i] + Vector3.up * s_markerHeight * 0.4f);
                if (sp.z <= 0f)
                    continue;
                Vector3 gp = ToGui(sp);
                if (!IsInsideScreen(gp, 8f))
                    continue;

                Single size = 8f * _uiScale;
                GUI.color = new Color(1f, 0.86f, 0.52f, 0.30f);
                GUI.DrawTexture(new Rect(gp.x - size * 0.5f, gp.y - size * 0.5f, size, size), _dotTex);
            }
        }

        private void DrawWorldMarker(Camera cam)
        {
            Vector3 worldTop = _targetWorld + Vector3.up * s_markerHeight;
            Vector3 spTop = cam.WorldToScreenPoint(worldTop);
            Vector3 spGround = cam.WorldToScreenPoint(_targetWorld);

            Boolean visible = spTop.z > 0.05f && spGround.z > 0.05f;
            Vector3 gpTop = ToGui(spTop);
            Vector3 gpGround = ToGui(spGround);

            if (!visible || !IsInsideScreen(gpTop, 12f))
            {
                DrawEdgeArrow(cam);
                return;
            }

            Single pulse = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));
            Color hot = HeatColor(_kweh);

            // Vertical beam from the ground to the floating ring.
            Single beamHeight = Mathf.Abs(gpGround.y - gpTop.y);
            if (beamHeight > 4f)
            {
                Rect beam = new Rect(gpTop.x - 1f * _uiScale, Mathf.Min(gpTop.y, gpGround.y),
                                     2f * _uiScale, beamHeight);
                GUI.color = new Color(1f, 0.86f, 0.45f, 0.20f * pulse);
                GUI.DrawTexture(beam, _lineTex);
            }

            // Pulsing ground ring.
            Single ringSize = 96f * _uiScale * (0.92f + 0.08f * pulse);
            GUI.color = new Color(1f, 0.84f, 0.38f, 0.22f * pulse);
            GUI.DrawTexture(new Rect(gpGround.x - ringSize * 0.5f, gpGround.y - ringSize * 0.18f,
                                     ringSize, ringSize * 0.36f), _ringTex);

            // Floating pin.
            Single pinSize = 54f * _uiScale;
            GUI.color = new Color(hot.r, hot.g, hot.b, 0.95f * pulse);
            GUI.DrawTexture(new Rect(gpTop.x - pinSize * 0.5f, gpTop.y - pinSize * 0.5f, pinSize, pinSize), _ringTex);

            Single coreSize = 16f * _uiScale;
            GUI.color = new Color(1f, 0.96f, 0.82f, 0.90f * pulse);
            GUI.DrawTexture(new Rect(gpTop.x - coreSize * 0.5f, gpTop.y - coreSize * 0.5f, coreSize, coreSize), _dotTex);

            // Label under the pin.
            if (s_showDistance)
            {
                String text = FormatDistance(_distance);
                Int32 fontSize = Mathf.RoundToInt(20f * _uiScale);
                Rect label = new Rect(gpTop.x - 80f * _uiScale, gpTop.y + pinSize * 0.45f,
                                      160f * _uiScale, 26f * _uiScale);
                DrawText(label, text, new Color(hot.r, hot.g, hot.b), 1f, fontSize);
            }
        }

        private void DrawEdgeArrow(Camera cam)
        {
            // Direction in camera space: +x right, +z forward.  Convert to a
            // screen direction (+x right, +y up) before placing the arrow.
            Vector3 local = cam.transform.InverseTransformPoint(_targetWorld);
            Vector2 dir = new Vector2(local.x, local.z);
            if (dir.sqrMagnitude < 1f)
                dir = new Vector2(0f, 1f);
            dir.Normalize();

            Vector2 screenDir = new Vector2(dir.x, -dir.y);

            Single radius = Mathf.Min(Screen.width, Screen.height) * 0.36f;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.52f);
            Vector2 pos = center + screenDir * radius;
            Single margin = 46f * _uiScale;
            pos.x = Mathf.Clamp(pos.x, margin, Screen.width - margin);
            pos.y = Mathf.Clamp(pos.y, margin, Screen.height - margin);

            // Arrow texture points up: 0 deg = up, positive = clockwise.
            Single angle = Mathf.Atan2(screenDir.x, -screenDir.y) * Mathf.Rad2Deg;
            Single size = 46f * _uiScale;
            Single pulse = 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));

            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pos);
            GUI.color = new Color(1f, 0.85f, 0.40f, 0.92f * pulse);
            GUI.DrawTexture(new Rect(pos.x - size * 0.5f, pos.y - size * 0.5f, size, size), _arrowTex);
            GUI.matrix = old;

            if (s_showDistance)
            {
                String text = FormatDistance(_distance);
                Int32 fontSize = Mathf.RoundToInt(17f * _uiScale);
                Rect label = new Rect(pos.x - 70f * _uiScale, pos.y + size * 0.55f,
                                      140f * _uiScale, 24f * _uiScale);
                DrawText(label, text, new Color(1f, 0.88f, 0.55f), 1f, fontSize);
            }
        }

        private void DrawCompass(Camera cam)
        {
            Single s = _uiScale;
            Single discSize = 84f * s;
            // Top-right corner: the mini-game timer sits at the top centre and
            // the beak/found counters at the top left.
            Single cx = Screen.width - 68f * s;
            Single top = 30f * s;
            Rect disc = new Rect(cx - discSize * 0.5f, top, discSize, discSize);
            Vector2 pivot = new Vector2(cx, top + discSize * 0.5f);

            // Dark glass background.
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.55f);
            GUI.DrawTexture(disc, _discTex);

            // Compass ring.
            GUI.color = new Color(0.94f, 0.84f, 0.57f, 0.35f);
            GUI.DrawTexture(disc, _ringTex);

            // Direction relative to the camera (0 = straight ahead, clockwise positive).
            Vector3 local = cam.transform.InverseTransformPoint(_targetWorld);
            Single angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

            Single arrowSize = 46f * s;
            Single pulse = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));
            Color hot = HeatColor(_kweh);

            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.color = new Color(hot.r, hot.g, hot.b, 0.95f * pulse);
            GUI.DrawTexture(new Rect(pivot.x - arrowSize * 0.5f, pivot.y - arrowSize * 0.5f, arrowSize, arrowSize), _arrowTex);
            GUI.matrix = old;

            // Centre dot.
            GUI.color = new Color(1f, 0.96f, 0.82f, 0.95f);
            GUI.DrawTexture(new Rect(pivot.x - 4f * s, pivot.y - 4f * s, 8f * s, 8f * s), _dotTex);

            // Distance below.
            if (s_showDistance)
            {
                Rect label = new Rect(cx - 90f * s, top + discSize + 2f * s, 180f * s, 26f * s);
                DrawText(label, FormatDistance(_distance), new Color(1f, 0.92f, 0.70f), 1f, Mathf.RoundToInt(21f * s));
            }

            // Game's own "Kweh" level (0..5) as dots.
            if (s_showKweh && _kweh > 0)
            {
                Single dot = 9f * s;
                Single gap = 5f * s;
                Single total = 5f * dot + 4f * gap;
                Single x0 = cx - total * 0.5f;
                Single y0 = top + discSize + 30f * s;
                for (Int32 i = 0; i < 5; i++)
                {
                    Color c = i < _kweh ? hot : new Color(0.55f, 0.55f, 0.55f);
                    GUI.color = new Color(c.r, c.g, c.b, i < _kweh ? 0.95f : 0.30f);
                    GUI.DrawTexture(new Rect(x0 + i * (dot + gap), y0, dot, dot), _dotTex);
                }
            }
        }

        private String FormatDistance(Single units)
        {
            Single meters = units / Mathf.Max(1f, s_unitsPerMeter);
            if (meters < 10f)
                return meters.ToString("0.0", CultureInfo.InvariantCulture) + " m";
            return Mathf.RoundToInt(meters).ToString(CultureInfo.InvariantCulture) + " m";
        }

        private static Color HeatColor(Int32 kweh)
        {
            // 0 = far, 5 = "Kweh!" right under the beak.
            if (kweh >= 5)
                return new Color(1f, 0.32f, 0.24f);
            if (kweh >= 4)
                return new Color(1f, 0.55f, 0.18f);
            if (kweh >= 3)
                return new Color(1f, 0.78f, 0.24f);
            if (kweh >= 2)
                return new Color(0.98f, 0.90f, 0.42f);
            return new Color(0.86f, 0.88f, 0.72f);
        }

        // ==================================================================
        // Text + textures
        // ==================================================================
        private void DrawText(Rect rect, String text, Color color, Single alpha, Int32 fontSize)
        {
            GUI.color = Color.white;
            if (_numberStyle == null)
                return;

            _numberStyle.fontSize = fontSize;

            Single outline = Mathf.Max(1f, Mathf.Round(1.15f * _uiScale));
            _numberStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f * alpha);

            GUI.Label(new Rect(rect.x - outline, rect.y, rect.width, rect.height), text, _numberStyle);
            GUI.Label(new Rect(rect.x + outline, rect.y, rect.width, rect.height), text, _numberStyle);
            GUI.Label(new Rect(rect.x, rect.y - outline, rect.width, rect.height), text, _numberStyle);
            GUI.Label(new Rect(rect.x, rect.y + outline, rect.width, rect.height), text, _numberStyle);

            _numberStyle.normal.textColor = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, _numberStyle);
        }

        private void RebuildStyles()
        {
            _lastScreenHeight = Screen.height;
            _uiScale = Mathf.Clamp(Screen.height / ReferenceHeight, 0.72f, 1.9f) * s_scale;

            if (_ringTex == null)
                _ringTex = CreateRingTexture(128, 0.78f, 2.2f);
            if (_discTex == null)
                _discTex = CreateDiscTexture(128);
            if (_arrowTex == null)
                _arrowTex = CreateArrowTexture(96);
            if (_dotTex == null)
                _dotTex = CreateDotTexture(32, 0.6f);
            if (_lineTex == null)
                _lineTex = CreateSolidTexture();

            if (_numberStyle == null)
            {
                _numberStyle = new GUIStyle();
                _numberStyle.alignment = TextAnchor.MiddleCenter;
                _numberStyle.clipping = TextClipping.Overflow;
                _numberStyle.wordWrap = false;
                _numberStyle.richText = false;
                _numberStyle.fontStyle = FontStyle.Bold;
                try
                {
                    _numberStyle.font = Font.CreateDynamicFontFromOSFont(
                        new String[] { "Segoe UI Semibold", "Segoe UI", "Arial" }, 20);
                }
                catch (Exception)
                {
                    _numberStyle.font = null;
                }
            }
        }

        private static Texture2D CreateSolidTexture()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateDotTexture(Int32 size, Single softness)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            Single half = (size - 1) * 0.5f;
            for (Int32 y = 0; y < size; y++)
            {
                for (Int32 x = 0; x < size; x++)
                {
                    Single dx = (x - half) / half;
                    Single dy = (y - half) / half;
                    Single d = Mathf.Sqrt(dx * dx + dy * dy);
                    Single a = Mathf.Clamp01((1f - d) / Mathf.Max(0.001f, softness));
                    pixels[y * size + x] = new Color32(255, 255, 255, (Byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateDiscTexture(Int32 size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            Single half = (size - 1) * 0.5f;
            for (Int32 y = 0; y < size; y++)
            {
                for (Int32 x = 0; x < size; x++)
                {
                    Single dx = (x - half) / half;
                    Single dy = (y - half) / half;
                    Single d = Mathf.Sqrt(dx * dx + dy * dy);
                    Single a = Mathf.Clamp01((1f - d) * 5f);
                    Single shade = 0.55f + 0.45f * Mathf.Clamp01(1f - d);
                    Byte v = (Byte)Mathf.RoundToInt(255f * shade);
                    pixels[y * size + x] = new Color32(v, v, v, (Byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateRingTexture(Int32 size, Single thickness, Single feather)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            Single half = (size - 1) * 0.5f;
            Single outer = 0.96f;
            Single inner = outer - thickness;

            for (Int32 y = 0; y < size; y++)
            {
                for (Int32 x = 0; x < size; x++)
                {
                    Single dx = (x - half) / half;
                    Single dy = (y - half) / half;
                    Single d = Mathf.Sqrt(dx * dx + dy * dy);

                    Single a;
                    if (d > outer)
                        a = 0f;
                    else if (d > outer - feather * 0.06f)
                        a = (outer - d) / (feather * 0.06f);
                    else if (d < inner)
                        a = 0f;
                    else if (d < inner + feather * 0.06f)
                        a = (d - inner) / (feather * 0.06f);
                    else
                        a = 1f;

                    a = Mathf.Clamp01(a);
                    pixels[y * size + x] = new Color32(255, 255, 255, (Byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateArrowTexture(Int32 size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            for (Int32 y = 0; y < size; y++)
            {
                Single v = (Single)y / (size - 1); // 0 bottom .. 1 top
                for (Int32 x = 0; x < size; x++)
                {
                    Single u = ((Single)x / (size - 1)) * 2f - 1f; // -1 .. 1

                    // Arrow pointing up: wide at the bottom, tip at the top,
                    // with a notch cut out at the bottom centre.
                    Single halfWidth = 0.92f * (1f - v);
                    Single a = 0f;
                    if (v > 0.02f && v < 0.98f && Mathf.Abs(u) <= halfWidth)
                        a = 1f;

                    // Soft edge.
                    Single edge = halfWidth - Mathf.Abs(u);
                    if (a > 0f)
                        a = Mathf.Clamp01(edge * 14f);

                    // Notch at the bottom.
                    if (v < 0.30f && Mathf.Abs(u) < 0.34f * (1f - v / 0.30f))
                        a = 0f;

                    pixels[y * size + x] = new Color32(255, 255, 255, (Byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
