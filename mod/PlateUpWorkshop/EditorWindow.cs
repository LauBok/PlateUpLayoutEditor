using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using Kitchen;
using PlateUpLayoutEditor.Core;
using UnityEngine;

namespace KitchenLayoutEditor
{
    /// <summary>
    /// The in-game editor (IMGUI). Runs on every player's machine; all changes are sent to the host
    /// through <see cref="LayoutEditorView"/>.
    /// </summary>
    public class EditorWindow : MonoBehaviour
    {
        private enum Tab { Place, Selection, Grabbers, Files, Options }
        private enum Tool { None, Place, Delete, Rotate, Select, Paste, MoveTarget, PickGrabber, PasteSettings }

        private const int WindowId = 0x4C45;
        private Rect _window = new Rect(20, 60, 380, 560);
        private bool _visible;
        private Tab _tab = Tab.Place;
        private Tool _tool = Tool.None;

        // Place tab
        private string _search = "";
        private Vector2 _catalogScroll;
        private int _selectedAppliance;
        private int _rotation;

        // Selection
        private (int x, int y)? _hover;
        private (int x, int y)? _dragStart;
        private GridRect? _selection;
        private string _dx = "0", _dy = "0";

        // Clipboard (a module document) and paste orientation
        private LayoutDocument? _clipboard;
        private string _clipboardJson = "";
        private int _pasteTurns;

        // Files
        private List<string> _files = new List<string>();
        private string? _selectedFile;
        private Vector2 _filesScroll;
        private string _saveName = "my-restaurant";

        // Requests and results
        private int _nextRequest = new System.Random().Next(1, 1 << 20) * 1000;
        private readonly Dictionary<int, Action<LayoutResult>> _callbacks = new Dictionary<int, Action<LayoutResult>>();
        private int _lastSequence = -1;
        private readonly List<string> _log = new List<string>();

        private TileOverlay? _overlay;

        // Grabbers tab: target tile, its current settings, copied settings and the item picker
        private (int x, int y)? _grabberTile;
        private ApplianceRecord? _grabberInfo;
        private ApplianceState? _copiedSettings;
        private bool _grabberUseSelection;
        private string _itemSearch = "";
        private Vector2 _itemScroll;

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey())) _visible = !_visible;
            PollResults();
            if (!_visible) { _overlay?.Hide(); return; }

            _hover = MouseTile();
            HandleMouse();
            DrawOverlay();
        }

        private static KeyCode ToggleKey() =>
            Enum.TryParse(EditorSettings.Current.ToggleKey, true, out KeyCode key) ? key : KeyCode.F7;

        // ----- Mouse tools -----

        private static (int x, int y)? MouseTile()
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return null;
            return GridMath.ToTile(ray.GetPoint(distance));
        }

        private bool MouseOverWindow()
        {
            var guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return _window.Contains(guiMouse);
        }

        private void HandleMouse()
        {
            if (_tool == Tool.None) return;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { SetTool(Tool.None); return; }
            if (_tool == Tool.Paste || _tool == Tool.Place)
            {
                // R / scroll: rotate clockwise; Shift+R / scroll down: counter-clockwise.
                int step = 0;
                if (Input.GetKeyDown(KeyCode.R)) step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 3 : 1;
                if (Input.mouseScrollDelta.y > 0.1f) step = 1;
                if (Input.mouseScrollDelta.y < -0.1f) step = 3;
                if (step != 0)
                {
                    if (_tool == Tool.Paste) _pasteTurns = (_pasteTurns + step) % 4;
                    else _rotation = (_rotation + 90 * step) % 360;
                }
            }
            if (_hover == null) return;
            var (x, y) = _hover.Value;

            if (_tool == Tool.Select)
            {
                if (Input.GetMouseButtonDown(0) && !MouseOverWindow()) _dragStart = (x, y);
                if (_dragStart != null)
                {
                    _selection = new GridRect(_dragStart.Value.x, _dragStart.Value.y, x, y);
                    if (Input.GetMouseButtonUp(0)) { _dragStart = null; _tab = Tab.Selection; }
                }
                return;
            }

            if (!Input.GetMouseButtonDown(0) || MouseOverWindow()) return;
            switch (_tool)
            {
                case Tool.Place: PlaceSelected(x, y); break;
                case Tool.Delete: Send(new LayoutCommand { Kind = CommandKind.Delete, Region = new GridRect(x, y, x, y) }); break;
                case Tool.Rotate: Send(new LayoutCommand { Kind = CommandKind.Rotate, Region = new GridRect(x, y, x, y), QuarterTurns = 1 }); break;
                case Tool.Paste: PasteAt(x, y); break;
                case Tool.PickGrabber: PickGrabber(x, y); break;
                case Tool.PasteSettings:
                    if (_copiedSettings != null)
                        Send(new LayoutCommand { Kind = CommandKind.Configure, Region = new GridRect(x, y, x, y), Settings = _copiedSettings.Clone(), ClearFilter = CopiedFilterIsEmpty() });
                    break;
                case Tool.MoveTarget:
                    if (_selection != null)
                    {
                        MoveSelection(x - _selection.MinX, y - _selection.MinY, 0);
                        SetTool(Tool.None);
                    }
                    break;
            }
        }

        private void SetTool(Tool tool)
        {
            _tool = tool;
            _dragStart = null;
        }

        private void DrawOverlay()
        {
            if (_overlay == null) _overlay = new TileOverlay();
            var rects = new List<(GridRect rect, Color color)>();
            if (_selection != null) rects.Add((_selection, new Color(1f, 0.8f, 0.2f)));
            if (_hover != null && _tool != Tool.None && _tool != Tool.Select)
            {
                var (x, y) = _hover.Value;
                if ((_tool == Tool.Paste || _tool == Tool.MoveTarget) && FootprintSize(out int w, out int h))
                    rects.Add((new GridRect(x, y, x + w - 1, y + h - 1), new Color(0.3f, 0.9f, 1f)));
                else
                    rects.Add((new GridRect(x, y, x, y), _tool == Tool.Delete ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 1f, 0.4f)));
            }
            _overlay.Show(rects);
        }

        private bool FootprintSize(out int w, out int h)
        {
            w = h = 1;
            if (_tool == Tool.MoveTarget && _selection != null) { w = _selection.Width; h = _selection.Height; return true; }
            if (_clipboard == null) return false;
            var bounds = _clipboard.Bounds ?? new GridRect(0, 0, 0, 0);
            bool swap = _pasteTurns % 2 == 1;
            w = swap ? bounds.Height : bounds.Width;
            h = swap ? bounds.Width : bounds.Height;
            return true;
        }

        // ----- Actions -----

        private void PlaceSelected(int x, int y)
        {
            if (_selectedAppliance == 0) { Log("Pick an appliance in the list first"); return; }
            var doc = new LayoutDocument { Kind = LayoutKinds.Module, Name = "spawn" };
            doc.Appliances.Add(new ApplianceRecord { Id = "a1", ApplianceId = _selectedAppliance, X = x, Y = y, Rotation = _rotation });
            Send(new LayoutCommand { Kind = CommandKind.Apply, LayoutJson = LayoutJson.Serialize(doc) });
        }

        private void PasteAt(int x, int y)
        {
            if (_clipboard == null) { Log("Clipboard is empty"); return; }
            Send(new LayoutCommand
            {
                Kind = CommandKind.Apply, AsModule = true, LayoutJson = _clipboardJson,
                OriginX = x, OriginY = y, QuarterTurns = _pasteTurns
            });
        }

        private void MoveSelection(int dx, int dy, int turns)
        {
            if (_selection == null) return;
            var region = _selection;
            Send(new LayoutCommand { Kind = CommandKind.Move, Region = region, Dx = dx, Dy = dy, QuarterTurns = turns },
                r => { if (r.Ok) _selection = MovedSelection(region, dx, dy, turns); });
        }

        private static GridRect MovedSelection(GridRect region, int dx, int dy, int turns)
        {
            bool swap = turns % 2 == 1;
            int w = swap ? region.Height : region.Width, h = swap ? region.Width : region.Height;
            return new GridRect(region.MinX + dx, region.MinY + dy, region.MinX + dx + w - 1, region.MinY + dy + h - 1);
        }

        private void CopySelection(bool saveToFile)
        {
            if (_selection == null) return;
            var name = _saveName;
            Send(new LayoutCommand { Kind = CommandKind.Export, Region = _selection, Name = name }, r =>
            {
                if (!r.Ok || r.LayoutJson == null) return;
                SetClipboard(r.LayoutJson);
                if (saveToFile) Log("Saved module " + LayoutFiles.Save(name, r.LayoutJson));
            });
        }

        private void SetClipboard(string json)
        {
            try
            {
                _clipboard = LayoutJson.Parse(json);
                if (_clipboard.Bounds == null && _clipboard.Appliances.Count > 0)
                    _clipboard.Bounds = new GridRect(0, 0, _clipboard.Appliances.Max(a => a.X), _clipboard.Appliances.Max(a => a.Y));
                _clipboardJson = LayoutJson.Serialize(_clipboard);
                _pasteTurns = 0;
                Log("Clipboard: " + _clipboard.Appliances.Count + " appliances. Use Paste and click a tile.");
            }
            catch (LayoutFormatException e) { Log("Invalid module: " + e.Message); }
        }

        private void ExportRestaurant()
        {
            var name = _saveName;
            Send(new LayoutCommand { Kind = CommandKind.Export, Name = name }, r =>
            {
                if (r.Ok && r.LayoutJson != null) { Log("Saved " + LayoutFiles.Save(name, r.LayoutJson)); RefreshFiles(); }
            });
        }

        private void ApplyFile(string file, bool replaceAll)
        {
            string json;
            try { json = LayoutFiles.Read(file); LayoutJson.Parse(json); }
            catch (Exception e) { Log("Cannot read " + file + ": " + e.Message); return; }
            Send(new LayoutCommand
            {
                Kind = CommandKind.Apply, LayoutJson = json,
                Clear = replaceAll ? ClearScope.Everything : ClearScope.None
            });
        }

        private void LoadFileToClipboard(string file)
        {
            try { SetClipboard(LayoutFiles.Read(file)); SetTool(Tool.Paste); }
            catch (Exception e) { Log("Cannot read " + file + ": " + e.Message); }
        }

        private void RefreshFiles()
        {
            try { _files = LayoutFiles.List(); }
            catch (Exception e) { Log("Cannot list files: " + e.Message); }
        }

        // ----- Networking -----

        private void Send(LayoutCommand command, Action<LayoutResult>? callback = null)
        {
            var settings = EditorSettings.Current;
            command.RequestId = ++_nextRequest;
            command.AllowDuringDay = settings.AllowDuringDay;
            if (command.Kind == CommandKind.Apply)
            {
                command.Conflicts = settings.Conflicts;
                command.RestoreState = settings.RestoreState;
            }
            if (callback != null) _callbacks[command.RequestId] = callback;
            if (!EditorChannel.Send(command))
            {
                _callbacks.Remove(command.RequestId);
                Log(Main.NetworkingAvailable ? "Not connected to the host's editor yet" : "Client editing needs the Harmony Workshop mod");
            }
        }

        private void PollResults()
        {
            var status = EditorChannel.Status;
            if (status == null) return;
            var results = status.Results;
            if (results.Count == 0)
            {
                if (_lastSequence < 0) _lastSequence = 0;
                return;
            }
            int newest = results.Max(r => r.Sequence);
            if (_lastSequence < 0 || newest < _lastSequence)
            {
                // First contact, or the host restarted its counter: ignore stale results.
                _lastSequence = newest;
                return;
            }
            int me = EditorChannel.LocalInput;
            foreach (var result in results.Where(r => r.Sequence > _lastSequence).OrderBy(r => r.Sequence))
            {
                if (result.TargetInput != me) continue;
                Log((result.Ok ? "" : "[!] ") + result.Message);
                if (_callbacks.TryGetValue(result.RequestId, out var callback))
                {
                    _callbacks.Remove(result.RequestId);
                    try { callback(result); }
                    catch (Exception e) { Log("Error: " + e.Message); }
                }
            }
            _lastSequence = newest;
        }

        private void ReportProgress(string message)
        {
            // Progress lines replace each other instead of filling the log.
            if (_log.Count > 0 && _log[_log.Count - 1].StartsWith("Rendering thumbnails")) _log.RemoveAt(_log.Count - 1);
            Log(message);
        }

        private void Log(string message)
        {
            _log.Add(message);
            while (_log.Count > 6) _log.RemoveAt(0);
        }

        // ----- GUI -----

        private void OnGUI()
        {
            if (!_visible) return;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Layout Editor (" + EditorSettings.Current.ToggleKey + ")");
            if (_hover != null && _tool != Tool.None)
            {
                var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                GUI.Label(new Rect(mouse.x + 18, mouse.y + 6, 260, 22), ToolHint());
            }
        }

        private string ToolHint()
        {
            var (x, y) = _hover!.Value;
            var at = " (" + x + "," + y + ")";
            switch (_tool)
            {
                case Tool.Place: return ApplianceCatalog.DisplayName(_selectedAppliance) + " " + _rotation + "°" + at + "  R/scroll rotate";
                case Tool.Paste: return "Paste x" + (_pasteTurns * 90) + "°" + at + "  R/scroll rotate";
                case Tool.MoveTarget: return "Move selection to" + at;
                case Tool.PickGrabber: return "Click a grabber" + at;
                case Tool.PasteSettings: return "Paste grabber settings" + at;
                default: return _tool + at;
            }
        }

        private void DrawWindow(int id)
        {
            DrawHeader();
            GUILayout.BeginHorizontal();
            foreach (Tab tab in Enum.GetValues(typeof(Tab)))
                if (GUILayout.Toggle(_tab == tab, tab.ToString(), "Button")) _tab = tab;
            GUILayout.EndHorizontal();

            switch (_tab)
            {
                case Tab.Place: DrawPlace(); break;
                case Tab.Selection: DrawSelection(); break;
                case Tab.Files: DrawFiles(); break;
                case Tab.Grabbers: DrawGrabbers(); break;
                case Tab.Options: DrawOptions(); break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Tool: " + _tool + (_tool != Tool.None ? "  (right click / Esc to stop)" : ""));
            foreach (var line in _log) GUILayout.Label(line);
            GUI.DragWindow();
        }

        private void DrawHeader()
        {
            var status = EditorChannel.Status;
            string role = EditorChannel.IsClient ? "Client" : "Host";
            string text = role;
            if (status == null) text += " | waiting for restaurant";
            else
            {
                text += status.InKitchen ? (status.IsPreparation ? " | preparation" : " | DAY (locked)") : " | not in a restaurant";
                if (role == "Client" && !status.ClientsAllowed) text += " | host disabled client editing";
            }
            GUILayout.Label(text);
        }

        private void DrawPlace()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            _search = GUILayout.TextField(_search);
            GUILayout.EndHorizontal();

            _catalogScroll = GUILayout.BeginScrollView(_catalogScroll, GUILayout.Height(220));
            var filter = _search.Trim();
            foreach (var entry in ApplianceCatalog.Entries)
            {
                if (filter.Length > 0 && entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.Code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var label = entry.Name + (entry.Purchasable ? "" : "  *");
                if (GUILayout.Toggle(_selectedAppliance == entry.Id, label, "Button"))
                {
                    if (_selectedAppliance != entry.Id) { _selectedAppliance = entry.Id; SetTool(Tool.Place); }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Label("* = not normally sold in the shop");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Facing", GUILayout.Width(50));
            foreach (var r in new[] { 0, 90, 180, 270 })
                if (GUILayout.Toggle(_rotation == r, r + "°", "Button")) _rotation = r;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_tool == Tool.Place, "Place", "Button") && _tool != Tool.Place) SetTool(Tool.Place);
            if (GUILayout.Toggle(_tool == Tool.Delete, "Delete", "Button") && _tool != Tool.Delete) SetTool(Tool.Delete);
            if (GUILayout.Toggle(_tool == Tool.Rotate, "Rotate", "Button") && _tool != Tool.Rotate) SetTool(Tool.Rotate);
            if (GUILayout.Toggle(_tool == Tool.Select, "Select", "Button") && _tool != Tool.Select) SetTool(Tool.Select);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Undo last change")) Send(new LayoutCommand { Kind = CommandKind.Undo });
        }

        private void DrawSelection()
        {
            if (GUILayout.Button(_tool == Tool.Select ? "Drag on the floor to select..." : "Select area with mouse")) SetTool(Tool.Select);
            if (_selection == null) { GUILayout.Label("No selection."); DrawClipboard(); return; }
            GUILayout.Label("Selection: (" + _selection.MinX + "," + _selection.MinY + ") - (" + _selection.MaxX + "," + _selection.MaxY + ")  " + _selection.Width + "x" + _selection.Height);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy")) CopySelection(false);
            if (GUILayout.Button("Delete")) Send(new LayoutCommand { Kind = CommandKind.Delete, Region = _selection });
            if (GUILayout.Button("Clear selection")) _selection = null;
            GUILayout.EndHorizontal();
            if (_selection == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate each +90")) Send(new LayoutCommand { Kind = CommandKind.Rotate, Region = _selection, QuarterTurns = 1 });
            if (GUILayout.Button("Rotate block +90")) MoveSelection(0, 0, 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Move dx", GUILayout.Width(55)); _dx = GUILayout.TextField(_dx, GUILayout.Width(40));
            GUILayout.Label("dy", GUILayout.Width(20)); _dy = GUILayout.TextField(_dy, GUILayout.Width(40));
            if (GUILayout.Button("Move") && int.TryParse(_dx, out int dx) && int.TryParse(_dy, out int dy)) MoveSelection(dx, dy, 0);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Move by clicking the new corner")) SetTool(Tool.MoveTarget);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(40));
            _saveName = GUILayout.TextField(_saveName);
            if (GUILayout.Button("Save as module", GUILayout.Width(120))) CopySelection(true);
            GUILayout.EndHorizontal();
            DrawClipboard();
        }

        private void DrawClipboard()
        {
            if (_clipboard == null) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Clipboard: " + _clipboard.Appliances.Count + " appliances");
            if (GUILayout.Toggle(_tool == Tool.Paste, "Paste", "Button") && _tool != Tool.Paste) SetTool(Tool.Paste);
            GUILayout.EndHorizontal();
        }

        private void DrawFiles()
        {
            GUILayout.Label("Folder: " + LayoutFiles.Folder);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh")) RefreshFiles();
            if (GUILayout.Button("Open folder")) Application.OpenURL("file://" + LayoutFiles.Folder);
            if (GUILayout.Button("Write catalog.json")) { LayoutFiles.WriteCatalog(); Log("Wrote catalog.json for the browser editor"); }
            GUILayout.EndHorizontal();
            GUI.enabled = !ThumbnailExporter.Running;
            if (GUILayout.Button("Export thumbnails for the browser editor"))
                StartCoroutine(ThumbnailExporter.Export(ReportProgress));
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(40));
            _saveName = GUILayout.TextField(_saveName);
            if (GUILayout.Button("Export restaurant", GUILayout.Width(130))) ExportRestaurant();
            GUILayout.EndHorizontal();

            _filesScroll = GUILayout.BeginScrollView(_filesScroll, GUILayout.Height(180));
            if (_files.Count == 0) GUILayout.Label("No layouts yet. Export one, or copy .json files from the browser editor here.");
            foreach (var file in _files)
                if (GUILayout.Toggle(_selectedFile == file, file, "Button")) _selectedFile = file;
            GUILayout.EndScrollView();

            if (_selectedFile == null) return;
            if (GUILayout.Button("Apply as whole restaurant (removes everything else)")) ApplyFile(_selectedFile, true);
            if (GUILayout.Button("Apply at saved positions (merge)")) ApplyFile(_selectedFile, false);
            if (GUILayout.Button("Paste as module (click a tile, R rotates)")) LoadFileToClipboard(_selectedFile);
        }

        // ----- Grabber settings (no practice mode needed) -----

        private void PickGrabber(int x, int y)
        {
            _grabberTile = (x, y);
            _grabberInfo = null;
            _grabberUseSelection = false;
            _tab = Tab.Grabbers;
            SetTool(Tool.None);
            ReadGrabber();
        }

        /// <summary>Reads the current settings of the target tile through a one-tile export.</summary>
        private void ReadGrabber()
        {
            if (_grabberTile == null) return;
            var (x, y) = _grabberTile.Value;
            Send(new LayoutCommand { Kind = CommandKind.Export, Region = new GridRect(x, y, x, y) }, r =>
            {
                if (!r.Ok || r.LayoutJson == null) return;
                try { _grabberInfo = LayoutJson.Parse(r.LayoutJson).Appliances.FirstOrDefault(a => a.Layer == null); }
                catch (LayoutFormatException) { _grabberInfo = null; }
            });
        }

        private GridRect? GrabberTarget()
        {
            if (_grabberUseSelection) return _selection;
            if (_grabberTile == null) return null;
            var (x, y) = _grabberTile.Value;
            return new GridRect(x, y, x, y);
        }

        private void Configure(Action<LayoutCommand> fill)
        {
            var region = GrabberTarget();
            if (region == null) { Log("Click a grabber first (or select an area)"); return; }
            var command = new LayoutCommand { Kind = CommandKind.Configure, Region = region };
            fill(command);
            Send(command, r => { if (r.Ok && !_grabberUseSelection) ReadGrabber(); });
        }

        private void DrawGrabbers()
        {
            GUILayout.Label("Set grabbers during preparation - no practice mode needed.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_tool == Tool.PickGrabber, "Click a grabber", "Button") && _tool != Tool.PickGrabber) SetTool(Tool.PickGrabber);
            GUI.enabled = _selection != null;
            _grabberUseSelection = GUILayout.Toggle(_grabberUseSelection && _selection != null, "All in selection", "Button");
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_grabberUseSelection) GUILayout.Label("Target: every grabber in the selection");
            else if (_grabberTile == null) { GUILayout.Label("Target: none"); }
            else
            {
                var (x, y) = _grabberTile.Value;
                string what = _grabberInfo == null ? "(empty or reading...)" : ApplianceCatalog.DisplayName(_grabberInfo.ApplianceId);
                GUILayout.Label("Target: " + what + " at " + x + "," + y);
                var st = _grabberInfo?.State;
                if (st?.PushDirection != null) GUILayout.Label("Pushes to: " + DirectionLabel(st.PushDirection));
                if (st?.GrabSpecificType == true)
                    GUILayout.Label("Filter: " + (st.SpecificType is int f ? ItemCatalog.DisplayName(f) : "none (takes the first item it sees)"));
            }

            GUILayout.Label("Rotating grabber - pushes to (tip: Ping key on it also turns it):");
            GUILayout.BeginHorizontal();
            foreach (var dir in new[] { "Left", "Up", "Right" })
                if (GUILayout.Button(DirectionLabel(dir))) Configure(c => c.Settings = new ApplianceState { PushDirection = dir });
            GUILayout.EndHorizontal();

            GUILayout.Label("Smart grabber filter:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use the item I'm holding")) Configure(c => c.FilterFromHeldItem = true);
            if (GUILayout.Button("Clear filter")) Configure(c => c.ClearFilter = true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Item", GUILayout.Width(35));
            _itemSearch = GUILayout.TextField(_itemSearch);
            GUILayout.EndHorizontal();
            if (_itemSearch.Trim().Length >= 2)
            {
                _itemScroll = GUILayout.BeginScrollView(_itemScroll, GUILayout.Height(110));
                var words = _itemSearch.Trim().Split(' ');
                int shown = 0;
                foreach (var item in ItemCatalog.Entries)
                {
                    if (!words.All(w => item.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    if (GUILayout.Button(item.Name)) Configure(c => c.Settings = new ApplianceState { SpecificType = item.Id });
                    if (++shown >= 40) break;
                }
                GUILayout.EndScrollView();
                GUILayout.Label("For finished dishes (several ingredients) prefer 'Use the item I'm holding'.");
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = _grabberInfo?.State != null;
            if (GUILayout.Button("Copy settings")) { _copiedSettings = _grabberInfo!.State!.Clone(); Log("Copied grabber settings"); }
            GUI.enabled = _copiedSettings != null;
            if (GUILayout.Toggle(_tool == Tool.PasteSettings, "Paste on click", "Button") && _tool != Tool.PasteSettings) SetTool(Tool.PasteSettings);
            if (GUILayout.Button("Paste to selection") && _selection != null)
                Send(new LayoutCommand { Kind = CommandKind.Configure, Region = _selection, Settings = _copiedSettings!.Clone(), ClearFilter = CopiedFilterIsEmpty() });
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>Copied from a smart grabber that has no filter: pasting clears filters too.</summary>
        private bool CopiedFilterIsEmpty() => _copiedSettings != null && _copiedSettings.GrabSpecificType == true && _copiedSettings.SpecificType == null;

        private static string DirectionLabel(string direction)
        {
            switch (direction)
            {
                case "Left": return "Left";
                case "Right": return "Right";
                case "Up": return "Straight";
                default: return direction;
            }
        }

        private void DrawOptions()
        {
            var s = EditorSettings.Current;
            bool changed = false;
            GUILayout.Label("When a target tile is occupied:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(s.Conflicts == ConflictPolicy.Replace, "Replace it", "Button") && s.Conflicts != ConflictPolicy.Replace) { s.Conflicts = ConflictPolicy.Replace; changed = true; }
            if (GUILayout.Toggle(s.Conflicts == ConflictPolicy.Skip, "Keep it", "Button") && s.Conflicts != ConflictPolicy.Skip) { s.Conflicts = ConflictPolicy.Skip; changed = true; }
            GUILayout.EndHorizontal();
            changed |= Toggle(ref s.RestoreState, "Restore grabber directions / smart filters");
            changed |= Toggle(ref s.AllowDuringDay, "Allow changes during the day (may confuse customers)");
            changed |= Toggle(ref s.AllowClients, "Host: let other players use the editor");
            changed |= Toggle(ref s.PingRotatesGrabbers, "Ping on a rotating grabber during preparation turns it");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Toggle key", GUILayout.Width(80));
            var key = GUILayout.TextField(s.ToggleKey);
            if (key != s.ToggleKey) { s.ToggleKey = key; changed = true; }
            GUILayout.EndHorizontal();
            if (changed) EditorSettings.Save();
        }

        private static bool Toggle(ref bool value, string label)
        {
            var next = GUILayout.Toggle(value, label);
            if (next == value) return false;
            value = next;
            return true;
        }

        private void OnDestroy() => _overlay?.Dispose();
    }
}
