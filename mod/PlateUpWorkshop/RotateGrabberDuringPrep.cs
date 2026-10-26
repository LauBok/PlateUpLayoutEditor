using Kitchen;
using KitchenMods;
using Unity.Entities;

namespace KitchenLayoutEditor
{
    /// <summary>
    /// Lets players turn a rotating grabber's output (Left -> Straight -> Right) with the Ping/Notify
    /// button during preparation, so its front can be set without practice mode. Same approach as
    /// UrFriendKen's AutomationPlus "InteractRotatePushDuringNight": reuse the game's own
    /// InteractRotatePush, but in appliance (night) interaction mode.
    /// </summary>
    [UpdateInGroup(typeof(InteractionGroup))]
    [UpdateBefore(typeof(ShowPingedApplianceInfo))]
    public class RotateGrabberDuringPrep : InteractRotatePush, IModSystem
    {
        protected override InteractionMode RequiredMode => InteractionMode.Appliances;

        protected override InteractionType RequiredType => InteractionType.Notify;

        protected override bool IsPossible(ref InteractionData data)
        {
            if (!EditorSettings.Current.PingRotatesGrabbers || !Has<SIsNightTime>()) return false;
            return base.IsPossible(ref data);
        }
    }
}
