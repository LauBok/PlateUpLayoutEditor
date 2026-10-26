using Controllers;
using Kitchen;

namespace KitchenLayoutEditor
{
    /// <summary>
    /// Routes editor requests: the host (or a solo player) executes them directly; clients send them
    /// to the host through <see cref="LayoutEditorView"/>.
    /// </summary>
    internal static class EditorChannel
    {
        public static bool IsClient => Session.CurrentGameNetworkMode == GameNetworkMode.Client;

        public static int LocalInput => InputSourceIdentifier.Identifier.Value;

        /// <summary>Returns false when a client has no channel to the host yet.</summary>
        public static bool Send(LayoutCommand command)
        {
            if (!IsClient)
            {
                LayoutEditorSystem.Enqueue(LocalInput, command);
                return true;
            }
            var view = LayoutEditorView.Active;
            if (view == null) return false;
            view.Send(command);
            return true;
        }

        public static HostStatus? Status => IsClient ? LayoutEditorView.Active?.Status : LocalHostStatus();

        public static HostStatus LocalHostStatus()
        {
            var status = new HostStatus
            {
                ClientsAllowed = Main.ClientsAllowed,
                InKitchen = GameInfo.CurrentScene == SceneType.Kitchen,
                IsPreparation = GameInfo.IsPreparationTime
            };
            status.Results.AddRange(LayoutEditorSystem.RecentResults);
            return status;
        }
    }
}
