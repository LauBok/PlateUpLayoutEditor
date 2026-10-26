// PlateUp! game API subset (Kitchen.*, KitchenData, KitchenMods, Controllers).
// Every member here is used by at least one open-source mod that builds against the real game;
// see Evidence.md for the file each signature was taken from.
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KitchenMods
{
    public class Mod { public string Name => throw null; }
    public interface IModInitializer
    {
        void PostActivate(Mod mod);
        void PreInject();
        void PostInject();
    }
    public interface IModSystem { }
    public interface IModComponent { }
}

namespace KitchenData
{
    public interface IApplianceProperty { }
    public interface IItemProperty { }

    public abstract class GameDataObject : ScriptableObject
    {
        public int ID;
    }

    public class Appliance : GameDataObject
    {
        public string Name;
        public string Description;
        public OccupancyLayer Layer;
        public bool IsPurchasable;
        public bool IsNonInteractive;
        public List<IApplianceProperty> Properties = new List<IApplianceProperty>();
        public GameObject Prefab;
        public bool GetProperty<T>(out T result) where T : IApplianceProperty => throw null;
    }

    public class GameData
    {
        public static GameData Main => throw null;
        public IEnumerable<T> Get<T>() where T : GameDataObject => throw null;
        public T Get<T>(int id) where T : GameDataObject => throw null;
        public bool TryGet<T>(int id, out T result, bool warn_if_fail = false) where T : GameDataObject => throw null;
    }

    public struct ItemList
    {
        public int Count => throw null;
        public int this[int index] => throw null;
        public void Add(int item) => throw null;
        public bool Contains(int item) => throw null;
        public bool IsEquivalent(ItemList other) => throw null;
    }

    public enum OccupancyLayer { Default, Wall, Floor, Ceiling }
}

namespace Kitchen.Layouts
{
    public enum RoomType { NoRoom, Kitchen, Dining, Front, Garden, Workshop }
}

namespace Controllers
{
    public struct InputIdentifier { public int Value; }
    public static class InputSourceIdentifier
    {
        public static InputIdentifier Identifier => throw null;
    }
}

namespace Kitchen
{
    using KitchenData;
    using Kitchen.Layouts;

    public enum Orientation { Null, Up, Down, Left, Right }
    public enum SceneType { Null, Main, Kitchen, Franchise, Postgame }
    public enum GameNetworkMode { None, Host, Client }
    public enum ViewMode { World, Screen }
    public enum ViewType { Appliance, HeldAppliance, Player, Item, ApplianceInfo }

    public static class GameInfo
    {
        public static SceneType CurrentScene => throw null;
        public static bool IsPreparationTime => throw null;
        public static int CurrentDay => throw null;
    }

    public static class Session
    {
        public static GameNetworkMode CurrentGameNetworkMode => throw null;
    }

    public static class VectorExtensions
    {
        public static bool IsSameTile(this Vector3 a, Vector3 b) => throw null;
        public static Vector3 Rounded(this Vector3 a) => throw null;
    }

    public class QueryHelper
    {
        public QueryHelper All(params ComponentType[] types) => this;
        public QueryHelper Any(params ComponentType[] types) => this;
        public QueryHelper None(params ComponentType[] types) => this;
        public static implicit operator EntityQueryDesc(QueryHelper helper) => throw null;
    }

    // ---- Components ----
    public struct CPosition : IComponentData
    {
        public Vector3 Position;
        public quaternion Rotation;
        public CPosition(Vector3 position) { Position = position; Rotation = quaternion.identity; }
        public static CPosition Hidden => throw null;
        public Vector3 ForwardPosition => throw null;
        public static implicit operator Vector3(CPosition p) => p.Position;
        public static implicit operator CPosition(Vector3 v) => new CPosition(v);
    }

    public struct CAppliance : IComponentData { public int ID; public OccupancyLayer Layer; }
    public struct CCreateAppliance : IComponentData { public int ID; }
    public struct CItemHolder : IComponentData { public Entity HeldItem; }
    public struct CHeldBy : IComponentData { public Entity Holder; }
    public struct CHeldAppliance : IComponentData { }
    public struct CApplianceBlueprint : IComponentData { }
    public struct CLetter : IComponentData { }
    public struct CApplianceGhostChair : IComponentData { public Entity Table; }
    public struct CApplianceChair : IComponentData, IApplianceProperty { public bool IsInUse; public Entity Occupant; }
    public struct CDestroyApplianceAtDay : IComponentData, IApplianceProperty { }
    public struct CImmovable : IComponentData, IApplianceProperty { }

    public struct CConveyPushItems : IComponentData, IApplianceProperty
    {
        public enum ConveyState { None, Grab, Push }
        public float Delay;
        public bool Push;
        public bool Grab;
        public bool Reversed;
        public bool GrabSpecificType;
        public int SpecificType;
        public ItemList SpecificComponents;
        public bool IgnoreProcessingItems;
        public float Progress;
        public ConveyState State;
    }

    public struct CConveyPushRotatable : IComponentData, IApplianceProperty { public Orientation Target; }

    public struct CLayoutRoomTile : IComponentData
    {
        public int RoomID;
        public RoomType Type;
    }

    public struct ViewIdentifier { }
    public struct CLinkedView : IComponentData { public ViewIdentifier Identifier; }
    public struct CRequiresView : IComponentData { public ViewType Type; public ViewMode ViewMode; public bool PhysicsDriven; }
    public struct CRemoveView : IComponentData { }
    public struct CPersistThroughSceneChanges : IComponentData { }
    public struct CDoNotPersist : IComponentData { }

    // ---- Systems ----
    public abstract class GenericSystemBase : SystemBase
    {
        protected virtual void Initialise() { }
        protected bool Require<T>(Entity e, out T comp) where T : struct, IComponentData => throw null;
        protected bool Has<T>(Entity e) where T : struct, IComponentData => throw null;
        protected bool Has<T>() where T : struct, IComponentData => throw null;
        protected void Set<T>(Entity e, T value) where T : struct, IComponentData => throw null;
        protected void Set<T>(Entity e) where T : struct, IComponentData => throw null;
        protected void Set<T>() where T : struct, IComponentData => throw null;
        protected bool TryGetSingletonEntity<T>(out Entity e) where T : struct, IComponentData => throw null;
        protected Entity GetOccupant(Vector3 position, OccupancyLayer layer = OccupancyLayer.Default) => throw null;
        protected Entity GetPrimaryOccupant(Vector3 position) => throw null;
        protected void SetOccupant(Vector3 position, Entity occupant, OccupancyLayer layer = OccupancyLayer.Default) => throw null;
        protected CLayoutRoomTile GetTile(Vector3 position) => throw null;
        protected int GetRoom(Vector3 position) => throw null;
        protected bool CanReach(Vector3 from, Vector3 to, bool do_not_swap = false) => throw null;
        protected Vector3 GetFrontDoor(bool get_external_tile = false) => throw null;
        protected Vector3 GetFallbackTile() => throw null;
    }

    public abstract class GameSystemBase : GenericSystemBase
    {
        protected Bounds Bounds => throw null;
    }

    // ---- Views ----
    public interface IUpdatableObject { }
    public interface IObjectView { T GetSubView<T>() where T : IUpdatableObject; }
    public interface IViewData
    {
        IUpdatableObject GetRelevantSubview(IObjectView view);
        public interface ICheckForChanges<T> { bool IsChangedFrom(T check); }
    }
    public interface IViewResponseData { }
    public interface IResponseData { }

    public abstract class UpdatableObjectView<T> : MonoBehaviour, IUpdatableObject where T : IViewData
    {
        protected abstract void UpdateData(T data);
    }

    public abstract class ResponsiveObjectView<T, TResp> : UpdatableObjectView<T>
        where T : IViewData where TResp : IResponseData
    {
        public abstract bool HasStateUpdate(out IResponseData state);
    }

    public abstract class IncrementalViewSystemBase<T> : GenericSystemBase where T : IViewData
    {
        protected void SendUpdate(CLinkedView view, T data) => throw null;
        protected void SendUpdate(ViewIdentifier identifier, T data) => throw null;
    }

    public abstract class ResponsiveViewSystemBase<T, TResp> : IncrementalViewSystemBase<T>
        where T : IViewData where TResp : IResponseData
    {
        protected void ApplyUpdates(ViewIdentifier identifier, Action<TResp> act, bool only_final_update = false) => throw null;
        protected void ApplyUpdates(CLinkedView view, Action<TResp> act, bool only_final_update = false) => throw null;
    }

    public class LocalViewRouter
    {
        protected virtual GameObject GetPrefab(ViewType view_type) => throw null;
    }
}
