#nullable enable
using System.Collections;
using System.IO;
using NUnit.Framework;
using Statecraft.Game;
using Statecraft.Game.Map;
using Statecraft.Game.UI;
using Statecraft.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>The Settings screen (#40): opened from the left column's gear like the other screens, its tilt-shift option
/// applied at once and kept in the SettingsStore across a reload, and laid out at 1280x720 in both languages. A reload
/// here is what a page reload leaves: the statics start over (TiltShift.Enabled back on) and only the file remains.</summary>
public class SettingsScreenTests
{
    static UiText En => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text);
    static UiText He => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.StringsHebrew).text, "strings.he.yaml");

    string dir = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "sc-settings-" + System.Guid.NewGuid().ToString("N"));
        TiltShift.Enabled = true;
    }

    [TearDown]
    public void TearDown()
    {
        TiltShift.Enabled = true;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Test]
    public void TheGearInTheLeftColumnOpensAndClosesTheSettingsScreen()
    {
        using var ui = new UiTestKit();
        using var nav = new ScreenNav(ui.Root);
        using var settings = new SettingsPresenter(ui.Root, En, new SettingsStore(dir));
        var screen = ui.Q<VisualElement>("screen-settings");
        Assert.IsFalse(ScreenNav.IsOpen(screen), "hidden at start");
        ui.Press("nav-settings");
        Assert.IsTrue(ScreenNav.IsOpen(screen), "the gear opens it");
        Assert.AreEqual("settings", nav.Current);
        ui.Press("nav-settings");
        Assert.IsFalse(ScreenNav.IsOpen(screen), "pressed again, it closes");
    }

    [Test]
    public void TheTiltShiftOptionTakesEffectAtOnceAndSurvivesAReload([Values(false, true)] bool hebrew)
    {
        var text = hebrew ? He : En;
        using (var ui = new UiTestKit())
        using (new SettingsPresenter(ui.Root, text, new SettingsStore(dir)))
        {
            var button = ui.Q<Button>("settings-tiltshift");
            Assert.IsTrue(TiltShift.Enabled, "on by default, with no file");
            Assert.AreEqual(text["settings.on"], button.text);
            UiTestKit.Press(button);
            Assert.IsFalse(TiltShift.Enabled, "off at once, with no apply step");
            Assert.AreEqual(text["settings.off"], button.text, "the button says what is set");
        }
        TiltShift.Enabled = true;   // the reload
        using (var ui = new UiTestKit())
        using (new SettingsPresenter(ui.Root, text, new SettingsStore(dir)))
        {
            Assert.IsFalse(TiltShift.Enabled, "still off after the reload");
            var button = ui.Q<Button>("settings-tiltshift");
            Assert.AreEqual(text["settings.off"], button.text);
            UiTestKit.Press(button);
            Assert.IsTrue(TiltShift.Enabled, "and on again at once");
            Assert.AreEqual(text["settings.on"], button.text);
        }
        TiltShift.Enabled = true;   // the reload
        using (var ui = new UiTestKit())
        using (new SettingsPresenter(ui.Root, text, new SettingsStore(dir)))
            Assert.IsTrue(TiltShift.Enabled, "on survives a reload too");
    }

    /// <summary>?notiltshift (TiltShift.OffRequested) boots with the blur off; a stored "on" does not turn it back on.</summary>
    [Test]
    public void AStoredOnDoesNotOverrideNoTiltShift()
    {
        new SettingsStore(dir).Write(SettingsPresenter.TiltShiftKey, "on");
        TiltShift.Enabled = false;   // what CameraRig.Init does for ?notiltshift
        using var ui = new UiTestKit();
        using var settings = new SettingsPresenter(ui.Root, En, new SettingsStore(dir));
        Assert.IsFalse(TiltShift.Enabled);
        Assert.AreEqual(En["settings.off"], ui.Q<Button>("settings-tiltshift").text);
    }

    /// <summary>A file the game cannot use (another key, a value it does not know, a line without "=") is no reason to fail
    /// a start: the option keeps its default.</summary>
    [Test]
    public void AnUnusableFileLeavesTheDefault()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(new SettingsStore(dir).FilePath, "junk\nscale=2\ntiltshift=maybe\n");
        using var ui = new UiTestKit();
        using var settings = new SettingsPresenter(ui.Root, En, new SettingsStore(dir));
        Assert.IsTrue(TiltShift.Enabled);
        UiTestKit.Press(ui.Q<Button>("settings-tiltshift"));
        Assert.AreEqual("off", new SettingsStore(dir).Read(SettingsPresenter.TiltShiftKey));
        Assert.AreEqual("2", new SettingsStore(dir).Read("scale"), "another key's line is kept");
    }

    /// <summary>On the web the file sits in a fixed /idbfs folder, as the autosave does (SaveStore), not under
    /// persistentDataPath, which hashes the page's URL: itch.io serves every upload from a new URL. It is not the save's
    /// folder: the settings never touch the save.</summary>
    [Test]
    public void OnTheWebTheFolderIsFixedAndIsNotTheSaves()
    {
        Assert.AreEqual("/idbfs/statecraft-settings", SettingsStore.DirectoryFor(RuntimePlatform.WebGLPlayer, "/idbfs/0123456789abcdef"));
        Assert.AreNotEqual(SaveStore.WebDirectory, SettingsStore.WebDirectory);
        Assert.AreEqual(Path.Combine("/home/p", "settings"), SettingsStore.DirectoryFor(RuntimePlatform.LinuxPlayer, "/home/p"));
    }

    /// <summary>The open screen at 1280x720 (the issue's size), in English and Hebrew: the title, the option's name and its
    /// button inside the screen, below the top bar and inside the window, each text whole (on one line or wrapped).</summary>
    [UnityTest]
    public IEnumerator TheOpenScreenFitsAt1280x720InBothLanguages()
    {
        var c = ContentLoader.Load(Paths.Content);
        foreach (var hebrew in new[] { false, true })
        {
            var text = hebrew ? He : En;
            string at = hebrew ? "he" : "en";
            var rt = new RenderTexture(1280, 720, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var go = new GameObject("SettingsLayoutTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            root.Q("root").RemoveFromClassList("loading");
            if (hebrew) root.Q("root").AddToClassList(UiLanguage.HebrewClass);
            var hud = new HudPresenter(root, new SimBridge(GameSession.New(c, GoldenRun.Seed, Mode.Historical), text), text);
            var nav = new ScreenNav(root);
            var settings = new SettingsPresenter(root, text, new SettingsStore(dir));
            try
            {
                nav.Open("settings");
                for (int i = 0; i < 30 && !(root.Q("screen-settings").layout.width > 0); i++) yield return null;
                for (int i = 0; i < 3; i++) yield return null;
                var screen = root.Q("screen-settings").worldBound;
                Assert.IsTrue(Inside(screen, root.worldBound), $"{at}: the screen {screen} leaves the window {root.worldBound}");
                Assert.GreaterOrEqual(screen.yMin, root.Q("top-bar").worldBound.yMax - 0.5f, at + ": the screen starts below the top bar");
                foreach (var name in new[] { "settings-screen-title", "settings-tiltshift-label", "settings-tiltshift" })
                {
                    var e = root.Q<TextElement>(name);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(e.text), $"{at}: {name} has its text");
                    Assert.IsTrue(Inside(e.worldBound, screen), $"{at}: {name} {e.worldBound} leaves the screen {screen}");
                    Assert.IsTrue(UiLayoutTests.TextFits(e), $"{at}: {name} '{e.text}' is cut ({e.contentRect})");
                }
            }
            finally
            {
                settings.Dispose(); nav.Dispose(); hud.Dispose();
                Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }

    static bool Inside(Rect inner, Rect outer) =>
        inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;
}
