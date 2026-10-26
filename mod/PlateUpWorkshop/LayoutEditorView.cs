using System.Collections.Generic;
using System.Runtime.InteropServices;
using Controllers;
using HarmonyLib;
using Kitchen;
using KitchenMods;
using MessagePack;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KitchenLayoutEditor
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct SLayoutEditorView : IComponentData, IModComponent { }

    /// <summary>
    /// Screen-space singleton view used as the editor's network channel, following the pattern of
    /// IcedMilo's "Stuff on Demand" spawn request view: every player's editor sends requests as view
    /// responses (routed to the host by the game), and the host broadcasts results as view data.
    /// </summary>
    public class LayoutEditorView : ResponsiveObjectView<LayoutEditorView.ViewData, LayoutEditorView.ResponseData>
    {
        public class UpdateView : ResponsiveViewSystemBase<ViewData, ResponseData>, IModSystem
        {
            private EntityQuery _views;
            private string? _payload;
            private (int, bool, bool, bool) _payloadKey;

            protected override void Initialise()
            {
                base.Initialise();
                _views = GetEntityQuery(typeof(CLinkedView), typeof(SLayoutEditorView));
            }

            protected override void OnUpdate()
            {
                // Without the Harmony prefab patch the game could not instantiate this view type.
                if (!Main.NetworkingAvailable) return;
                EnsureView();
                var status = EditorChannel.LocalHostStatus();
                int newest = status.Results.Count == 0 ? 0 : status.Results[status.Results.Count - 1].Sequence;
                var key = (newest, status.ClientsAllowed, status.InKitchen, status.IsPreparation);
                if (_payload == null || !key.Equals(_payloadKey))
                {
                    // Results can carry a whole exported layout, so only serialise when something changed.
                    _payload = Wire.Write(status);
                    _payloadKey = key;
                }
                var payload = _payload;
                using (var views = _views.ToComponentDataArray<CLinkedView>(Allocator.Temp))
                {
                    foreach (var view in views)
                    {
                        SendUpdate(view, new ViewData { Payload = payload });
                        ApplyUpdates(view.Identifier, HandleResponse, only_final_update: false);
                    }
                }
            }

            private void HandleResponse(ResponseData data)
            {
                var command = Wire.Read<LayoutCommand>(data.Payload);
                if (command == null) return;
                bool fromHost = data.InputIdentifier == InputSourceIdentifier.Identifier.Value;
                if (!fromHost && !Main.ClientsAllowed)
                {
                    Main.LogInfo("Ignored editor request from a client (client editing is disabled by the host)");
                    return;
                }
                LayoutEditorSystem.Enqueue(data.InputIdentifier, command);
            }

            private void EnsureView()
            {
                if (TryGetSingletonEntity<SLayoutEditorView>(out _)) return;
                var entity = EntityManager.CreateEntity();
                Set<SLayoutEditorView>(entity);
                Set(entity, new CPersistThroughSceneChanges());
                Set(entity, new CDoNotPersist());
                Set(entity, new CRequiresView
                {
                    ViewMode = ViewMode.Screen,
                    Type = Main.EditorViewType,
                    PhysicsDriven = false
                });
            }
        }

        [MessagePackObject(false)]
        public class ViewData : IViewData, IViewResponseData, IViewData.ICheckForChanges<ViewData>
        {
            [Key(0)] public string Payload = "";

            public IUpdatableObject GetRelevantSubview(IObjectView view) => view.GetSubView<LayoutEditorView>();

            public bool IsChangedFrom(ViewData check) => check == null || Payload != check.Payload;
        }

        [MessagePackObject(false)]
        public class ResponseData : IResponseData, IViewResponseData
        {
            [Key(0)] public int InputIdentifier;
            [Key(1)] public string Payload = "";
        }

        /// <summary>The view instance on this machine, if the host has created it.</summary>
        public static LayoutEditorView? Active { get; private set; }

        public HostStatus Status { get; private set; } = new HostStatus();

        private readonly Queue<string> _outgoing = new Queue<string>();

        public void Send(LayoutCommand command) => _outgoing.Enqueue(Wire.Write(command));

        protected override void UpdateData(ViewData data)
        {
            Active = this;
            var status = Wire.Read<HostStatus>(data.Payload);
            if (status != null) Status = status;
        }

        public override bool HasStateUpdate(out IResponseData state)
        {
            Active = this;
            if (_outgoing.Count > 0)
            {
                state = new ResponseData
                {
                    InputIdentifier = InputSourceIdentifier.Identifier.Value,
                    Payload = _outgoing.Dequeue()
                };
                return true;
            }
            state = default!;
            return false;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }

    [HarmonyPatch(typeof(LocalViewRouter), "GetPrefab")]
    internal static class LocalViewRouterPatch
    {
        private static GameObject? _prefab;

        [HarmonyPrefix]
        private static bool Prefix(ViewType view_type, ref GameObject __result)
        {
            if (view_type != Main.EditorViewType) return true;
            if (_prefab == null)
            {
                // Inactive parent keeps the template itself from running.
                var hider = new GameObject("LayoutEditorViewHider");
                hider.SetActive(false);
                Object.DontDestroyOnLoad(hider);
                _prefab = new GameObject("LayoutEditorView");
                _prefab.AddComponent<LayoutEditorView>();
                _prefab.transform.SetParent(hider.transform);
            }
            __result = _prefab;
            return false;
        }
    }
}
