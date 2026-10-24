global using System.Drawing;

namespace Client
{
    internal static class Settings { internal static string DataPath = "Data/"; }
    internal static class CMain { internal static long Time; }
}
namespace Client.MirGraphics
{
    internal static class DXManager
    {
        internal static bool GrayScale;
        internal static void SetGrayscale(bool value) => GrayScale = value;
    }
    internal sealed class MLibrary
    {
        internal readonly record struct DrawCall(string Library, int Frame, Point At, Size Size,
            Color Colour, float Opacity, bool Gray);
        internal static readonly List<DrawCall> Calls = new();
        internal static bool SkillAssetPresent = true, ThrowOnDraw;
        internal static int MissingSkillFrame = -1;
        private readonly string path;
        internal MLibrary(string filename) => path = filename;
        internal Size GetSize(int frame) => path.Contains("SkillIconsV6")
            ? !SkillAssetPresent ? Size.Empty : frame == MissingSkillFrame || frame == 0 || frame >= 200
                ? new Size(1, 1) : new Size(128, 128)
            : frame < 0 ? Size.Empty : frame == 8 ? new Size(24, 16) : new Size(24, 24);
        internal void DrawScaled(int frame, Point at, Size size, Color colour, float opacity = 1F, bool blend = false)
        {
            if (ThrowOnDraw) throw new InvalidOperationException("Simulated draw failure");
            Calls.Add(new(path, frame, at, size, colour, opacity, DXManager.GrayScale));
        }
    }
}
namespace Client.MirControls
{
    public class MirControl
    {
        internal static MirControl ActiveControl, MouseControl;
    }
    public class MirImageControl : MirControl
    {
        internal readonly List<MirImageControl> Controls = new();
        internal MirImageControl Parent { set { value.Controls.Add(this); } }
        internal bool Visible = true;
        internal string Hint = "";
        internal bool AutoSize = true, UseOffSet = true, NotControl, GrayScale;
        internal bool DrawImage = true, Enabled = true;
        internal int Index;
        internal Point Location;
        internal Size Size;
        internal Rectangle DisplayRectangle => new(Location, Size);
        internal Color ForeColour = Color.White;
        internal float Opacity = 1F;
        internal Client.MirGraphics.MLibrary Library;
        internal Action<MirImageControl> OriginalDraw;
    }
    public sealed class MirButton : MirImageControl
    {
        internal int Sound;
        internal Func<OriginalIcon> Glyph;
        internal event EventHandler Click;
        internal void ClickForTest() => Click?.Invoke(this, EventArgs.Empty);
    }
    public sealed class MirLabel : MirImageControl { }
    internal enum OriginalIcon { Collapse, Expand }
    internal static class OriginalUI
    {
        internal static void DynamicIcon(MirButton button, Func<OriginalIcon> glyph, Rectangle bounds, string hint)
        { button.Glyph = glyph; button.Location = bounds.Location; button.Size = bounds.Size; button.Hint = hint; }
        // Only records delegation. Actual ClassicImage is covered separately
        // by source inspection; this test does not emulate Direct3D masks.
        internal static int ClassicCalls;
        internal static void ClassicImage(MirImageControl control) => ClassicCalls++;
    }
}
namespace Client.MirScenes.Dialogs
{
    // The production partial owns collapse/restore and child visibility. This
    // fixture supplies only the existing dialog fields and cooldown producer.
    public sealed partial class SkillBarDialog : Client.MirControls.MirImageControl
    {
        private readonly Client.MirControls.MirButton _switchBindsButton = new();
        internal readonly Client.MirControls.MirImageControl[] CoolDowns = new Client.MirControls.MirImageControl[8];
        internal readonly Client.MirControls.MirLabel[] KeyNameLabels = new Client.MirControls.MirLabel[8];
        internal byte BarIndex;
        internal bool TestCooling = true;
        internal SkillBarDialog()
        {
            Size = new Size(497,76);
            for(int i=0;i<8;i++)
            {
                _ = new Client.MirControls.MirImageControl { Parent=this };
                KeyNameLabels[i] = new Client.MirControls.MirLabel { Parent=this };
                CoolDowns[i] = new Client.MirControls.MirImageControl { Parent=this };
            }
            _ = new Client.MirControls.MirLabel { Parent=this };
            ApplyOriginalBarToggle();
        }
        private string GetKey(int bar, int slot) => $"{bar}:{slot}";
        internal void Update()
        {
            foreach(var mask in CoolDowns) mask.Visible = TestCooling;
            RefreshOriginalBarVisibility();
        }
        internal void GlobalShowForTest() { ResetOriginalBarCollapse(); Update(); }
    }
}
