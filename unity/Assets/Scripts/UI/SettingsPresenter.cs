#nullable enable
using System;
using System.IO;
using Statecraft.Game.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Statecraft.Game.UI
{
    /// <summary>The Settings screen (#40), opened from the left column's gear like the other screens (ScreenNav). Its one
    /// option so far turns the tilt-shift blur off and on: applied at once (TiltShift.Enabled, what the "tiltshift" toggle
    /// sets) and kept in the SettingsStore, so it holds after a reload. A stored "off" only ever turns the blur off, so
    /// ?notiltshift still boots without it. The button says what is set ("On" / "Off"), not only a colour: the player's
    /// choice (Enabled), not the web tier's automatic last fallback step (TiltShift.FallbackOff), which it never changes.</summary>
    public sealed class SettingsPresenter : IDisposable
    {
        public const string TiltShiftKey = "tiltshift";
        readonly SettingsStore? store; readonly UiText text; readonly VisualElement screen; readonly Button tiltShift;

        /// <param name="store">Where the choice is kept; null keeps nothing (the option still works until the page closes).</param>
        public SettingsPresenter(VisualElement root, UiText text, SettingsStore? store)
        {
            this.store = store; this.text = text;
            screen = root.Q<VisualElement>("screen-settings");
            screen.Q<Label>("settings-screen-title").text = text["settings.title"];
            screen.Q<Label>("settings-tiltshift-label").text = text["settings.tiltshift"];
            tiltShift = screen.Q<Button>("settings-tiltshift");
            if (store?.Read(TiltShiftKey) == "off") TiltShift.Enabled = false;
            tiltShift.clicked += Flip;
            ScreenNav.OnOpened(screen, Show);   // a dev toggle may have changed it while the screen was shut
            Show();
        }

        public void Dispose()
        {
            tiltShift.clicked -= Flip;
            if (screen.userData is Action) screen.userData = null;
        }

        void Flip()
        {
            TiltShift.Enabled = !TiltShift.Enabled;
            Show();
            try { store?.Write(TiltShiftKey, TiltShift.Enabled ? "on" : "off"); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Settings: could not keep the tilt-shift choice ({e.Message}); it holds until the page is closed");
            }
        }

        void Show() => tiltShift.text = text[TiltShift.Enabled ? "settings.on" : "settings.off"];
    }
}
