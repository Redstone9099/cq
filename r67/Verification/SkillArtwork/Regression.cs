using Client.MirControls;
using Client.MirGraphics;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks++;
    }
    private static void Main()
    {
        Check(OriginalSkillArtwork.Renderer != null, "Module registers the renderer before first draw");
        var native = new MLibrary("MagIcon");
        var cell = new MirImageControl { Library = native, Index = 6,
            Location = new Point(134, 226), Size = new Size(43, 43) };
        Spell spell = Spell.FireBall;
        OriginalSkillArtwork.Bind(cell, () => spell);
        Check(!cell.AutoSize && !cell.UseOffSet, "128px source cannot change slot geometry or introduce sprite offsets");
        cell.OriginalDraw(cell);
        var call = MLibrary.Calls.Last();
        Check(call.Frame == 31 && call.Library.Contains("SkillIconsV6"), "Uses Spell ID 31, not native icon index 6");
        Check(call.Size == cell.Size && call.At == cell.Location, "Full icon occupies the clickable 43px square");
        spell = Spell.Poisoning;
        cell.OriginalDraw(cell);
        Check(MLibrary.Calls.Last().Frame == 63, "Rebinding is evaluated on draw instead of capturing a stale spell");
        int before = MLibrary.Calls.Count;
        cell.Index = -1;
        cell.OriginalDraw(cell);
        Check(MLibrary.Calls.Count == before, "Empty slot draws nothing even before spell state clears");
        cell.Index = 6;
        cell.DrawImage = false;
        cell.OriginalDraw(cell);
        Check(MLibrary.Calls.Count == before, "DrawImage suppression still works");
        cell.DrawImage = true;
        foreach (var size in new[] { new Size(43,43), new Size(44,44), new Size(48,48) })
        {
            cell.Size = size;
            cell.OriginalDraw(cell);
            Check(MLibrary.Calls.Last().Size == size, "Hotbar, book and assignment use their destination bounds: " + size);
        }
        cell.Size = new Size(43,43);
        MLibrary.SkillAssetPresent = false;
        cell.OriginalDraw(cell);
        call = MLibrary.Calls.Last();
        Check(call.Library == "MagIcon" && call.Frame == 6 && call.Size == cell.Size,
            "Missing optional skill library keeps native artwork enlarged to the slot");
        MLibrary.SkillAssetPresent = true;
        MLibrary.MissingSkillFrame = (int)spell;
        cell.OriginalDraw(cell);
        Check(MLibrary.Calls.Last().Library == "MagIcon", "Empty 1px frame falls back");
        MLibrary.MissingSkillFrame = -1;
        spell = Spell.MapLightning;
        cell.OriginalDraw(cell);
        Check(MLibrary.Calls.Last().Library == "MagIcon", "Uncovered event ID falls back");
        spell = Spell.None;
        cell.Index = 8;
        cell.OriginalDraw(cell);
        call = MLibrary.Calls.Last();
        Check(call.Size == new Size(43,29) && call.At == new Point(134,233), "Native fallback preserves aspect ratio and centers");
        spell = Spell.FireBall;
        cell.ForeColour = Color.FromArgb(220,200,100,50);
        cell.Opacity = .45F;
        cell.Enabled = false;
        cell.GrayScale = true;
        cell.OriginalDraw(cell);
        call = MLibrary.Calls.Last();
        Check(call.Colour == Color.FromArgb(220,100,50,25) && call.Opacity == .45F && call.Gray,
            "Disabled tint, source tint, opacity and grayscale survive the custom renderer");
        Check(!DXManager.GrayScale, "Grayscale restored after drawing");
        DXManager.GrayScale = true;
        cell.OriginalDraw(cell);
        Check(DXManager.GrayScale, "An already-active grayscale state is preserved");
        DXManager.GrayScale = false;
        MLibrary.ThrowOnDraw = true;
        bool threw = false;
        try { cell.OriginalDraw(cell); } catch (InvalidOperationException) { threw = true; }
        finally { MLibrary.ThrowOnDraw = false; }
        Check(threw && !DXManager.GrayScale, "Grayscale restored on failed draw");

        var button = new MirButton { Library = native, Index = 6, Size = new Size(44,44) };
        OriginalSkillArtwork.Bind(button, () => Spell.FireBall);
        MirControl.MouseControl = MirControl.ActiveControl = button;
        button.OriginalDraw(button);
        Check(MLibrary.Calls.Last().Colour == Color.FromArgb(255,204,204,204), "Single-frame artwork retains pressed feedback");
        MirControl.ActiveControl = null;
        button.OriginalDraw(button);
        Check(MLibrary.Calls.Last().Colour == Color.White, "Releasing restores normal artwork");
        var mask = new MirImageControl();
        OriginalSkillArtwork.BindCooldown(mask, cell);
        Check(mask.Location == cell.Location && mask.Size == cell.Size && !mask.AutoSize && !mask.UseOffSet && mask.NotControl,
            "Cooldown matches icon bounds and cannot intercept clicks");
        mask.OriginalDraw(mask);
        Check(OriginalUI.ClassicCalls == 1, "Cooldown delegates to the existing scaled mask renderer");
        Check(OriginalUILayout.CooldownFrame(1000,0,22) == -1 && OriginalUILayout.CooldownFrame(0,1,22) == -1,
            "Expired and zero-duration cooldowns clear");
        before = MLibrary.Calls.Count;
        Check(!OriginalSkillArtwork.DrawFrame(native,6,Rectangle.Empty,Color.White,1F) && MLibrary.Calls.Count == before,
            "Invalid bounds never reach graphics division");
        foreach (var screen in new[] { new Size(1280,720), new Size(1366,768), new Size(1920,1080), new Size(2560,1440) })
            foreach (int percent in new[] {50,100,115,150})
            {
                float scale = OriginalUILayout.Scale(screen.Width,screen.Height,percent);
                var logicalSize = button.Size;
                button.OriginalDraw(button);
                Check(MLibrary.Calls.Last().Size == logicalSize && scale > 0,
                    "Screen scale remains outside the icon draw: " + screen + "/" + percent);
            }
        var bar = new Client.MirScenes.Dialogs.SkillBarDialog();
        var otherBar = new Client.MirScenes.Dialogs.SkillBarDialog { BarIndex=1 };
        var expandedSize = bar.Size;
        var toggle = bar.Controls.OfType<MirButton>().Single();
        toggle.ClickForTest();
        Check(bar.Size == new Size(20,20) && bar.Controls.Where(c=>c!=toggle).All(c=>!c.Visible),
            "Collapse hides the entire bar, not only key labels, and releases the original hitbox");
        Check(toggle.Visible && toggle.Glyph()==OriginalIcon.Expand, "Collapsed bar retains an actionable restore control");
        Check(otherBar.Size == expandedSize && otherBar.KeyNameLabels.All(c=>c.Visible), "Collapsing one bar leaves the other intact");
        bar.Update();
        Check(bar.Controls.Where(c=>c!=toggle).All(c=>!c.Visible), "Binding/cooldown refresh cannot reveal a collapsed bar");
        bar.TestCooling = false;
        toggle.ClickForTest();
        Check(bar.Size == expandedSize && bar.KeyNameLabels.All(c=>c.Visible) && bar.CoolDowns.All(c=>!c.Visible),
            "Restore returns the full bar and clears masks which expired while collapsed");
        Check(toggle.Glyph()==OriginalIcon.Collapse, "Restored button offers collapse again");
        toggle.ClickForTest();
        bar.GlobalShowForTest();
        Check(bar.Size == expandedSize && bar.KeyNameLabels.All(c=>c.Visible), "Global show also restores the bar body");
        Console.WriteLine($"PASS: {checks} actual artwork-router/bar-collapse checks with graphics doubles; no Direct3D/game execution.");
    }
}
