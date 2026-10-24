using Client.MirControls;
using System.Drawing;
using System.Windows.Forms;

namespace Client.MirScenes.Dialogs
{
    public sealed partial class HeroInfoPanel
    {
        private void ApplyOriginalHeroUI()
        {
            // Keep the supplied health-container artwork and coordinates.
            // HP/MP handlers refresh numeric labels even when the bar is empty.
            int width=System.Math.Max(164,HealthContainer.Location.X+HealthContainer.Size.Width+8);
            int nameY=System.Math.Max(74,HealthContainer.Location.Y+HealthContainer.Size.Height+4);
            OriginalUI.Panel(this,380,new Size(width,nameY+28));
            NameContainer.AutoSize=false;NameContainer.Size=new Size(width-16,22);
            NameContainer.Location=new Point(8,nameY);
            NameContainer.OriginalDraw=c=>OriginalUI.Frame(OriginalUI.P3(245),c.DisplayRectangle,3);
            OriginalUI.Text(LevelLabel,new Rectangle(3,1,24,20),11);
            OriginalUI.Text(NameLabel,new Rectangle(29,1,width-48,20),12);
            NameLabel.DrawFormat|=TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
            NameLabel.NotControl=false;
            NameLabel.BeforeDraw+=(_,_)=>NameLabel.Hint=NameLabel.Text;
        }
    }
    public sealed partial class HeroInventoryDialog
    {
        private void ApplyOriginalHeroUI()
        {
            OriginalUI.Panel(this,380,new Size(520,400));
            OriginalUI.Icon(CloseButton,OriginalIcon.CloseSmall,new Rectangle(480,8,28,28),"关闭英雄行囊");
            OriginalUI.Label(this,"英雄行囊",new Rectangle(24,10,400,28),19);
            for(int i=0;i<Grid.Length;i++)OriginalUI.NativeCell(Grid[i],OriginalUILayout.InventorySlot(i));
            for(int i=0;i<LockBar.Length;i++)
            {
                var cover=LockBar[i];cover.AutoSize=false;cover.Location=new Point(24+i%2*232,46+i/2*50);
                cover.Size=new Size(230,48);cover.OriginalDraw=OriginalUI.LockMask;
            }
            OriginalUI.Chip(HPButton,new Rectangle(24,310,108,34),"生命药水");
            OriginalUI.Chip(MPButton,new Rectangle(274,310,108,34),"魔法药水");
            OriginalUI.NativeCell(HPItem,new Point(148,306));OriginalUI.NativeCell(MPItem,new Point(400,306));
            OriginalUI.Text(AutoHPPercentLabel,new Rectangle(24,352,180,28),16);
            OriginalUI.Text(AutoMPPercentLabel,new Rectangle(274,352,180,28),16);
            foreach(var cover in new[]{HPLockBar,MPLockBar})
            {
                cover.AutoSize=false;cover.Size=new Size(212,78);cover.OriginalDraw=OriginalUI.LockMask;
            }
            HPLockBar.Location=new Point(20,306);MPLockBar.Location=new Point(270,306);
        }
    }
    public sealed partial class HeroBeltDialog
    {
        private void ApplyOriginalHeroUI()
        {
            BeforeDraw-=BeltPanel_BeforeDraw;
            AutoSize=false;
            OriginalDraw=c=>{
                foreach(var cell in Grid)OriginalUI.Frame(1603,new Rectangle(cell.DisplayLocation.X-4,cell.DisplayLocation.Y-4,40,40),3);
            };
            foreach(var cell in Grid)OriginalUI.ObserveSlot(cell);
            LayoutOriginalHeroBelt();
            Location=OriginalUILayout.DefaultHeroBelt(UIViewport.Size);
        }
        private void LayoutOriginalHeroBelt()
        {
            Size=new Size(108,40);
            for(int i=0;i<Grid.Length;i++)
            {
                Grid[i].Location=new Point(4+i*40,4);
                Key[i].Location=new Point(3+i*40,1);
                Key[i].NotControl=true;
            }
            OriginalUI.Icon(CloseButton,OriginalIcon.CloseSmall,new Rectangle(88,22,16,16),"隐藏英雄快捷栏");
            Location=OriginalUILayout.Clamp(Location,Size,UIViewport.Size);
        }
    }
    public sealed partial class HeroMenuPanel
    {
        private void ApplyOriginalHeroUI()
        {

            OriginalUI.Panel(this,380,new Size(156,52));
            OriginalUI.NativeIcon(HeroMagicsButton,OriginalIcon.HeroSkill,new Rectangle(6,4,44,44),"英雄技能");
            OriginalUI.NativeIcon(HeroInventoryButton,OriginalIcon.HeroBag,new Rectangle(56,4,44,44),"英雄背包");
            OriginalUI.NativeIcon(HeroEquipmentButton,OriginalIcon.HeroEquipment,new Rectangle(106,4,44,44),"英雄装备");
        }
    }
    public sealed partial class HeroBehaviourPanel
    {
        private void ApplyOriginalHeroUI()
        {
            OriginalUI.Panel(this,380,new Size(112,28));
            var icons=new[]{OriginalIcon.Attack,OriginalIcon.Defend,OriginalIcon.Follow,OriginalIcon.Tactics};
            var hints=new[]{"英雄：主动攻击","英雄：反击","英雄：跟随","英雄：自定义行为"};
            for(int i=0;i<BehaviourButtons.Length;i++)
            {
                var button=BehaviourButtons[i];
                // Existing code disables the active mode. Keep that input rule,
                // with a separate selected-and-disabled visual state.
                OriginalUI.Icon(button,icons[i],new Rectangle(i*28,2,28,24),hints[i],()=>!button.Enabled);
            }
        }
    }
    public sealed partial class HeroManageDialog
    {
        private void ApplyOriginalHeroUI()
        {

            OriginalUI.Panel(this,380,new Size(560,272));
            OriginalUI.NativeClose(CloseButton,new Point(524,8));
            OriginalUI.Label(this,"英雄管理",new Rectangle(24,10,460,32),19);
            OriginalUI.Label(this,"当前英雄",new Rectangle(24,58,104,28),17);
            OriginalUI.Label(this,"可选英雄",new Rectangle(156,58,360,28),17);
            CurrentAvatar.Location=new Point(30,108);
            for(int i=0;i<Avatars.Length;i++)Avatars[i].Location=new Point(156+i%4*94,102+i/4*76);
        }
    }
    public sealed partial class HeroManageAvatar
    {
        private void ApplyOriginalHeroUI()
        {

            AutoSize=false;Size=new Size(64,60);
            OriginalDraw=c=>{
                OriginalUI.SlotSurface(c.DisplayRectangle);
                if(Index!=DefaultIndex)
                {
                    var native=Library.GetSize(Index);var size=OriginalUILayout.ItemArtSize(native,new Size(58,54));
                    Library.DrawScaled(Index,new Point(c.DisplayLocation.X+(64-size.Width)/2,c.DisplayLocation.Y+(60-size.Height)/2),size,Color.White);
                }
                if(MirControl.MouseControl==c)OriginalUI.SlotRim(c.DisplayRectangle,Color.Gold,2);
            };
        }
    }
    public sealed partial class HeroAutoPotPreview
    {
        private void ApplyOriginalHeroUI()
        {
            OriginalDraw=c=>OriginalUI.Frame(1603,c.DisplayRectangle,3);
        }
    }
}
