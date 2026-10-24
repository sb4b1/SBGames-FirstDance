using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    public interface IDevToolPanel
    {
        string PanelId { get; }
        string PanelTitle { get; }
        int Order { get; }

        void OnPanelRegistered(DevTool devTool);
        void DrawPanelGUI();
        void OnPanelUpdate();
    }
}
