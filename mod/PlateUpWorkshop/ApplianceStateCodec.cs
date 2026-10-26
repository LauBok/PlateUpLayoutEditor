using System;
using System.Collections.Generic;
using Kitchen;
using KitchenData;
using PlateUpLayoutEditor.Core;
using Unity.Entities;

namespace KitchenLayoutEditor
{
    /// <summary>
    /// Reads and writes the appliance configuration that matters for automation.
    /// Field names follow the game components as used by KitchenLib (KitchenPropertiesUtils) and
    /// UrFriendKen's AutomationPlus / SmartGrabber fixes.
    /// </summary>
    internal static class ApplianceStateCodec
    {
        public static ApplianceState? Capture(EntityManager em, Entity entity)
        {
            var state = new ApplianceState();
            if (em.HasComponent<CConveyPushRotatable>(entity))
            {
                var rotatable = em.GetComponentData<CConveyPushRotatable>(entity);
                state.GrabDirection = rotatable.Target.ToString();
            }
            if (em.HasComponent<CConveyPushItems>(entity))
            {
                var push = em.GetComponentData<CConveyPushItems>(entity);
                if (push.GrabSpecificType)
                {
                    state.GrabSpecificType = true;
                    if (push.SpecificType != 0)
                    {
                        state.SpecificType = push.SpecificType;
                        var components = new List<int>();
                        for (int i = 0; i < push.SpecificComponents.Count; i++) components.Add(push.SpecificComponents[i]);
                        state.SpecificComponents = components;
                    }
                }
            }
            return state.IsEmpty ? null : state;
        }

        /// <summary>Applies the saved state where the live appliance supports it. Never upgrades an appliance
        /// (for example a plain grabber is not turned into a smart one).</summary>
        public static void Restore(EntityManager em, Entity entity, ApplianceState? state)
        {
            if (state == null) return;
            if (state.GrabDirection != null && em.HasComponent<CConveyPushRotatable>(entity) &&
                Enum.TryParse(state.GrabDirection, out Orientation orientation))
            {
                var rotatable = em.GetComponentData<CConveyPushRotatable>(entity);
                rotatable.Target = orientation;
                em.SetComponentData(entity, rotatable);
            }
            if (state.SpecificType != null && em.HasComponent<CConveyPushItems>(entity))
            {
                var push = em.GetComponentData<CConveyPushItems>(entity);
                if (push.GrabSpecificType)
                {
                    push.SpecificType = state.SpecificType.Value;
                    var components = new ItemList();
                    if (state.SpecificComponents != null)
                        foreach (var id in state.SpecificComponents) components.Add(id);
                    push.SpecificComponents = components;
                    em.SetComponentData(entity, push);
                }
            }
        }
    }
}
