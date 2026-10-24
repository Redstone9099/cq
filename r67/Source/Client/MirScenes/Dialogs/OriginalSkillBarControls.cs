using Client.MirControls;
using System.Drawing;

namespace Client.MirScenes.Dialogs
{
    public sealed partial class SkillBarDialog
    {
        private readonly Spell[] originalSkillSpells = new Spell[8];
        private bool originalBarCollapsed;
        private MirButton originalBarToggle;
        private Size originalExpandedSize;
        private long originalKeyNamesRefreshTime;

        private void ApplyOriginalBarToggle()
        {
            originalExpandedSize = Size;
            originalBarToggle = new MirButton { Parent = this, Sound = _switchBindsButton.Sound };
            originalBarToggle.Click += (sender, args) =>
            {
                originalBarCollapsed = !originalBarCollapsed;
                Size = originalBarCollapsed ? new Size(20, 20) : originalExpandedSize;
                RefreshOriginalBarVisibility();
                if (!originalBarCollapsed) Update();
            };
            OriginalUI.DynamicIcon(originalBarToggle,
                () => originalBarCollapsed ? OriginalIcon.Expand : OriginalIcon.Collapse,
                new Rectangle(1, 0, 18, 18), string.Empty);
            RefreshOriginalBarVisibility();
        }

        private void ResetOriginalBarCollapse()
        {
            originalBarCollapsed = false;
            Size = originalExpandedSize;
        }

        private void RefreshOriginalKeyBindings()
        {
            if (!Visible || CMain.Time < originalKeyNamesRefreshTime) return;
            originalKeyNamesRefreshTime = CMain.Time + 200;
            for (int i = 0; i < KeyNameLabels.Length; i++)
            {
                if (KeyNameLabels[i].Hint == GetKey(BarIndex, i + 1)) continue;
                Update();
                return;
            }
        }

        private void RefreshOriginalBarVisibility()
        {
            foreach (var control in Controls)
            {
                if (control == originalBarToggle) continue;
                // Expanded cooldown visibility belongs to ProcessSkillDelay.
                if (CoolDowns.Contains(control))
                {
                    if (originalBarCollapsed) control.Visible = false;
                }
                else control.Visible = !originalBarCollapsed;
            }
            originalBarToggle.Hint = (originalBarCollapsed ? "显示快捷栏 " : "隐藏快捷栏 ") + (BarIndex + 1);
        }
    }
}
