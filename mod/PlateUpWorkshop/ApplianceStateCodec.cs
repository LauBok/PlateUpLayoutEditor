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
                state.PushDirection = rotatable.Target.ToString();
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

        /// <summary>Sets the smart grabber filter; returns false when the appliance is not a smart grabber.</summary>
        public static bool SetFilter(EntityManager em, Entity entity, int itemId, IEnumerable<int> components)
        {
            if (!em.HasComponent<CConveyPushItems>(entity)) return false;
            var push = em.GetComponentData<CConveyPushItems>(entity);
            if (!push.GrabSpecificType) return false;
            push.SpecificType = itemId;
            var list = new ItemList();
            foreach (var id in components) list.Add(id);
            push.SpecificComponents = list;
            em.SetComponentData(entity, push);
            return true;
        }

        /// <summary>Sets a rotating grabber's grab direction; returns false when not applicable.</summary>
        public static bool SetPushDirection(EntityManager em, Entity entity, string direction)
        {
            if (!em.HasComponent<CConveyPushRotatable>(entity) || !Enum.TryParse(direction, out Orientation orientation)) return false;
            var rotatable = em.GetComponentData<CConveyPushRotatable>(entity);
            rotatable.Target = orientation;
            em.SetComponentData(entity, rotatable);
            return true;
        }

        /// <summary>Applies the saved state where the live appliance supports it. Never upgrades an appliance
        /// (for example a plain grabber is not turned into a smart one).</summary>
        public static void Restore(EntityManager em, Entity entity, ApplianceState? state)
        {
            if (state == null) return;
            if (state.PushDirection != null && em.HasComponent<CConveyPushRotatable>(entity) &&
                Enum.TryParse(state.PushDirection, out Orientation orientation))
            {
                var rotatable = em.GetComponentData<CConveyPushRotatable>(entity);
                rotatable.Target = orientation;
                em.SetComponentData(entity, rotatable);
            }
            if (state.SpecificType != null)
                SetFilter(em, entity, state.SpecificType.Value, state.SpecificComponents ?? new List<int>());
            else if (state.GrabSpecificType == true)
                SetFilter(em, entity, 0, new int[0]);   // saved as "smart grabber without a filter"
        }
    }
}
