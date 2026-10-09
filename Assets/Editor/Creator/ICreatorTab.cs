using System;
using UnityEngine;

namespace SBGames.Editor.Creator
{
    public interface ICreatorTab
    {
        string Title { get; }
        void Initialize(SBGamesCreatorWindow window);
        void OnEnable();
        void OnGUI();
        void Generate();
    }
}
