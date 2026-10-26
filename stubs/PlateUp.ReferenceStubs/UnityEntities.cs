// Unity.Entities (DOTS 0.x) subset. Public Unity API; signatures as documented for Entities 0.17-0.51.
using System;
using Unity.Collections;

namespace Unity.Entities
{
    public interface IComponentData { }
    public interface IBufferElementData { }
    public interface ISystemStateComponentData : IComponentData { }

    public struct Entity : IEquatable<Entity>
    {
        public int Index;
        public int Version;
        public static Entity Null => default;
        public bool Equals(Entity other) => Index == other.Index && Version == other.Version;
        public override bool Equals(object obj) => obj is Entity e && Equals(e);
        public override int GetHashCode() => Index;
        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
    }

    public struct ComponentType
    {
        public static implicit operator ComponentType(Type type) => default;
        public static ComponentType ReadOnly<T>() => default;
    }

    public class EntityQueryDesc
    {
        public ComponentType[] All = new ComponentType[0];
        public ComponentType[] Any = new ComponentType[0];
        public ComponentType[] None = new ComponentType[0];
    }

    public struct EntityQuery
    {
        public NativeArray<Entity> ToEntityArray(Allocator allocator) => throw null;
        public NativeArray<T> ToComponentDataArray<T>(Allocator allocator) where T : struct, IComponentData => throw null;
        public int CalculateEntityCount() => throw null;
        public bool IsEmpty => throw null;
    }

    public struct DynamicBuffer<T> where T : struct
    {
        public int Length => throw null;
        public T this[int index] { get => throw null; set => throw null; }
    }

    public struct EntityManager
    {
        public Entity CreateEntity() => throw null;
        public Entity CreateEntity(params ComponentType[] types) => throw null;
        public bool Exists(Entity entity) => throw null;
        public void DestroyEntity(Entity entity) => throw null;
        public void DestroyEntity(EntityQuery query) => throw null;
        public bool HasComponent<T>(Entity entity) => throw null;
        public bool AddComponent<T>(Entity entity) => throw null;
        public bool AddComponentData<T>(Entity entity, T data) where T : struct, IComponentData => throw null;
        public bool RemoveComponent<T>(Entity entity) => throw null;
        public T GetComponentData<T>(Entity entity) where T : struct, IComponentData => throw null;
        public void SetComponentData<T>(Entity entity, T data) where T : struct, IComponentData => throw null;
        public EntityQuery CreateEntityQuery(params ComponentType[] types) => throw null;
    }

    public class World
    {
        public static World DefaultGameObjectInjectionWorld => throw null;
        public EntityManager EntityManager => throw null;
    }

    public abstract class ComponentSystemBase
    {
        public EntityManager EntityManager => throw null;
        public World World => throw null;
        protected virtual void OnCreate() { }
        protected EntityQuery GetEntityQuery(params ComponentType[] componentTypes) => throw null;
        protected EntityQuery GetEntityQuery(params EntityQueryDesc[] queryDesc) => throw null;
        protected void RequireSingletonForUpdate<T>() { }
    }

    public abstract class SystemBase : ComponentSystemBase
    {
        protected abstract void OnUpdate();
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateBeforeAttribute : Attribute { public UpdateBeforeAttribute(Type systemType) { } }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateAfterAttribute : Attribute { public UpdateAfterAttribute(Type systemType) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UpdateInGroupAttribute : Attribute { public UpdateInGroupAttribute(Type groupType) { } }
}
