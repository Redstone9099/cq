using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using System.Drawing;
using System.Windows.Forms;
using Font = System.Drawing.Font;

namespace Client.MirScenes.Dialogs
{
    public sealed partial class MainDialog
    {
        private MirImageControl originalDayNight;
        private MirLabel[] hudV5Legends;
        private static int P(int value) => OriginalUILayout.HudPx(value);
        private void ApplyOriginalUI()
        {
            AutoSize = false; OriginalDraw = OriginalUI.Hud;
            OriginalHitTest = p => Controls.Any(c=>c.Visible && !c.NotControl && c.DisplayRectangle.Contains(p)) || OriginalUI.HudPixel(this,p);
            LeftCap.Visible = RightCap.Visible = false;
            HealthLabel.SizeChanged -= Label_SizeChanged; ManaLabel.SizeChanged -= Label_SizeChanged;
            HealthOrb.Location = new Point(P(38),P(90)); HealthOrb.Size = new Size(P(94),P(92));
            HealthOrb.NotControl = false;
            HealthLabel.Parent = HealthOrb; ManaLabel.Parent = HealthOrb;
            OriginalUI.Text(HealthLabel,new Rectangle(P(2),P(1),P(45),P(90)),15);
            OriginalUI.Text(ManaLabel,new Rectangle(P(49),P(1),P(44),P(90)),15);
            HealthLabel.NotControl = ManaLabel.NotControl = false;
            TopLabel.Parent = BottomLabel.Parent = this;
            OriginalUI.Text(TopLabel,new Rectangle(P(26),P(212),P(59),P(16)),14);
            OriginalUI.Text(BottomLabel,new Rectangle(P(89),P(212),P(59),P(16)),14);
            foreach(var label in new[]{HealthLabel,ManaLabel,TopLabel,BottomLabel})
                label.DrawFormat |= TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            TopLabel.Visible = BottomLabel.Visible = true;
            TopLabel.ForeColour = BottomLabel.ForeColour = Color.White;
            TopLabel.DrawFormat |= TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            BottomLabel.DrawFormat |= TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            TopLabel.NotControl = BottomLabel.NotControl = false;
            OriginalUI.Text(CharacterName,new Rectangle(P(22),P(216),P(128),P(17)),19);
            CharacterName.Visible = false;
            CharacterName.DrawFormat |= TextFormatFlags.HorizontalCenter;
            OriginalUI.Text(LevelLabel,new Rectangle(0,0,P(35),P(17)),19);
            OriginalUI.Text(GoldLabel,new Rectangle(0,0,P(118),P(17)),18);
            OriginalUI.Text(WeightLabel,new Rectangle(0,0,P(74),P(17)),18);
            OriginalUI.Text(SpaceLabel,new Rectangle(0,0,P(24),P(17)),18);
            OriginalUI.Text(ExperienceLabel,new Rectangle(-2,-2,P(74),P(17)),16);
            foreach(var label in new[]{LevelLabel,ExperienceLabel,WeightLabel,SpaceLabel})
                label.DrawFormat |= TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine;
            ExperienceBar.AutoSize = false; ExperienceBar.Size = new Size(P(76),P(13));
            WeightBar.AutoSize = false; WeightBar.Size = new Size(P(76),P(13));
            foreach(var label in new[]{AModeLabel,PModeLabel,SModeLabel})
                OriginalUI.Text(label,new Rectangle(0,0,P(220),P(18)),19);
            originalDayNight=new MirImageControl{Parent=this};
            OriginalUI.HudDayNight(originalDayNight);
            hudV5Legends=new[]{
                OriginalUI.Label(this,"等级",new Rectangle(0,0,P(22),P(17)),12),
                OriginalUI.Label(this,"经验",new Rectangle(0,0,P(22),P(17)),12),
                OriginalUI.Label(this,"负重",new Rectangle(0,0,P(22),P(17)),12)};
            LayoutOriginalHud();
            BeforeDraw+=(_,_)=>UpdateOriginalHeroEntrances();
        }
        internal void LayoutOriginalHud()
        {
            Size = new Size(OriginalUILayout.HudWidth(UIViewport.Width),OriginalUILayout.HudHeight);
            Location = OriginalUILayout.Hud(UIViewport.Size);
            int right = Size.Width - P(800);
            HeroInfoPanel.Location=new Point(System.Math.Max(-Location.X,P(176)-HeroInfoPanel.Size.Width),-HeroInfoPanel.Size.Height-P(42));
            OriginalUI.HudButton(CharacterButton,new Rectangle(634,52,42,42),right,()=>GameScene.Scene.CharacterDialog.Visible && GameScene.Scene.CharacterDialog.CharacterPage.Visible);
            OriginalUI.HudButton(InventoryButton,new Rectangle(673,31,42,42),right,()=>GameScene.Scene.InventoryDialog.Visible);
            OriginalUI.HudButton(SkillButton,new Rectangle(713,11,42,42),right,()=>GameScene.Scene.CharacterDialog.Visible && GameScene.Scene.CharacterDialog.SkillPage.Visible);
            OriginalUI.HudButton(OptionButton,new Rectangle(755,1,42,42),right,()=>GameScene.Scene.OptionDialog.Visible);
            OriginalUI.HudButton(GameShopButton,new Rectangle(749,202,36,32),right,()=>GameScene.Scene.GameShopDialog.Visible);
            OriginalUI.Icon(QuestButton,OriginalIcon.Quest,new Rectangle(right+P(638),P(104),P(32),P(32)),"任务",()=>GameScene.Scene.QuestLogDialog.Visible);
            OriginalUI.Icon(HeroMenuButton,OriginalIcon.Hero,new Rectangle(right+P(674),P(104),P(32),P(32)),"英雄",()=>GameScene.Scene.HeroMenuPanel.Visible);
            OriginalUI.Icon(HeroSummonButton,OriginalIcon.Summon,new Rectangle(right+P(710),P(104),P(32),P(32)),"召唤 / 收回英雄",()=>GameScene.Scene.HeroSpawnState>HeroSpawnState.Unsummoned);
            if(MenuButton!=null)OriginalUI.Icon(MenuButton,OriginalIcon.Help,new Rectangle(right+P(746),P(104),P(32),P(32)),"主菜单",()=>GameScene.Scene.MenuDialog.Visible);
            originalDayNight.Location=new Point(right+P(748),P(79));
            QuestButton.BringToFront();HeroMenuButton.BringToFront();HeroSummonButton.BringToFront();MenuButton?.BringToFront();
            for(int i=0;i<hudV5Legends.Length;i++)
            {
                hudV5Legends[i].Visible=OriginalUI.V5SkinAvailable;
                hudV5Legends[i].Location=new Point(right+P(645),P(new[]{140,166,192}[i]));
            }
            LevelLabel.Location = new Point(right+P(692),P(140));
            ExperienceBar.Location = new Point(right+P(671),P(166));
            WeightBar.Location = new Point(right+P(671),P(192));
            GoldLabel.Location = new Point(right+P(645),P(216));
            WeightLabel.Location = new Point(right+P(671),P(190));
            SpaceLabel.Location = new Point(right+P(755),P(166));
            SpaceLabel.Hint="背包空格数";
            for(int i=0;i<3;i++) new[]{AModeLabel,PModeLabel,SModeLabel}[i].Location = new Point(Size.Width-P(232),-P(62)+i*P(20));
        }
    }


    public sealed partial class ChatDialog
    {
        private static int P(int value) => OriginalUILayout.HudPx(value);
        private void ApplyOriginalUI()
        {
            AutoSize=false;
            ChatFont.Dispose(); ChatFont = new Font(Settings.FontName,(int)(14*OriginalUILayout.HudScale),FontStyle.Regular,GraphicsUnit.Pixel);
            ChatTextBox.Font=ChatFont; ChatTextBox.ForeColour=Color.White; ChatTextBox.BackColour=Color.FromArgb(8,8,10);
            ChatTextBox.CanLoseFocus=true;
            OriginalDraw = c => {
                if (!Transparent) OriginalUI.Library.DrawScaled(1601,c.DisplayLocation,c.Size,Color.White);
                if(WindowSize>0) OriginalUI.RefinedHudSurface(c.DisplayRectangle);
            };
            CountBar.DrawImage=false;
            OriginalUI.Button(HomeButton,228,228,228,new Rectangle(0,0,P(12),P(11)));
            OriginalUI.Button(UpButton,228,228,228,new Rectangle(0,0,P(12),P(11)));
            OriginalUI.Button(DownButton,227,227,227,new Rectangle(0,0,P(12),P(11)));
            OriginalUI.Button(EndButton,227,227,227,new Rectangle(0,0,P(12),P(11)));
            OriginalUI.Thumb(PositionBar,new Rectangle(0,0,P(10),P(20)));
            LayoutOriginalChat();
        }
        internal void LayoutOriginalChat()
        {
            LineCount = WindowSize==0 ? 5 : WindowSize==1 ? 8 : 11;
            Size = new Size(OriginalUILayout.ChatWidth(UIViewport.Width),P(36+LineCount*18));
            Location = new Point(OriginalUILayout.Hud(UIViewport.Size).X+OriginalUILayout.ChatInset,UIViewport.Height-P(6)-Size.Height);
            ChatTextBox.Location=new Point(P(3),Size.Height-P(29)); ChatTextBox.Size=new Size(Size.Width-P(22),P(21));
            int x=Size.Width-P(13);
            HomeButton.Location=new Point(x,P(3)); UpButton.Location=new Point(x,P(16));
            EndButton.Location=new Point(x,Size.Height-P(15)); DownButton.Location=new Point(x,Size.Height-P(28));
            CountBar.Location=new Point(x,P(30)); CountBar.AutoSize=false; CountBar.Size=new Size(P(10),Math.Max(P(20),Size.Height-P(62)));
            PositionBar.Location=new Point(x,P(30));
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if(disposing) ChatFont?.Dispose();
        }
    }


    public sealed partial class ChatControlBar
    {
        private static int P(int value) => OriginalUILayout.HudPx(value);
        private void ApplyOriginalUI()
        {
            AutoSize=false; OriginalDraw=_=>{};OriginalUI.MarkRefined(this);
            var buttons=new[]{NormalButton,ShoutButton,WhisperButton,LoverButton,MentorButton,GroupButton,GuildButton,TradeButton};
            var captions=new[]{"附近","喊话","私聊","伴侣","师徒","队伍","行会","交易"};
            int[] selected={2038,2041,2044,2047,2050,2053,2056,-1};
            for(int i=0;i<buttons.Length;i++)
            {
                var button=buttons[i]; int state=selected[i];
                OriginalUI.Tab(button,new Rectangle(i*P(58),P(2),P(56)-2,P(20)),captions[i],()=>state>=0 && button.NormalIndex==state);
            }
            OriginalUI.Icon(ReportButton,OriginalIcon.Report,new Rectangle(P(470),0,P(24),P(24)),"举报");
            OriginalUI.Icon(SizeButton,OriginalIcon.ChatSize,new Rectangle(P(502),0,P(24),P(24)),"调整聊天框高度");
            OriginalUI.Icon(SettingsButton,OriginalIcon.ChatSettings,new Rectangle(P(534),0,P(24),P(24)),"频道与聊天设置",()=>GameScene.Scene.ChatOptionDialog.Visible);
            Size=new Size(OriginalUILayout.ChatWidth(UIViewport.Width),P(24));
            LayoutOriginalChatBar();
        }
        internal void LayoutOriginalChatBar()
        {
            var buttons=new[]{NormalButton,ShoutButton,WhisperButton,LoverButton,MentorButton,GroupButton,GuildButton,TradeButton};
            int width=(Size.Width-P(90))/8;
            for(int i=0;i<buttons.Length;i++)
            {buttons[i].Location=new Point(i*width,P(2));buttons[i].Size=new Size(width-3,P(20));}
            var actions=new[]{ReportButton,SizeButton,SettingsButton};
            for(int i=0;i<actions.Length;i++)actions[i].Location=new Point(Size.Width-P(88)+i*P(30),0);
        }
    }


    public sealed partial class InventoryDialog
    {
        private void ApplyOriginalUI()
        {
            OriginalUI.RefinedPanel(this,RefinedPanelStyle.Inventory,OriginalUILayout.InventorySize);
            OriginalDraw=c=>{
                OriginalUI.RefinedPanelSurface(RefinedPanelStyle.Inventory,c.DisplayRectangle);
                OriginalUI.InventoryFooter(c);
            };
            OriginalUI.Label(this,"行 囊",new Rectangle(24,10,300,28),19);
            OriginalUI.Icon(CloseButton,OriginalIcon.CloseSmall,new Rectangle(480,8,28,28),"关闭行囊");
            OriginalUI.NativeTab(ItemButton,new Rectangle(24,352,142,34),"行囊一",()=>ItemButton.NormalIndex==197);
            OriginalUI.NativeTab(ItemButton2,new Rectangle(174,352,142,34),"行囊二",()=>ItemButton2.NormalIndex==168);
            OriginalUI.NativeTab(QuestButton,new Rectangle(324,352,168,34),"任务物品",()=>QuestButton.NormalIndex==198);
            OriginalUI.Icon(AddButton,OriginalIcon.Expand,new Rectangle(438,8,32,28),"扩展行囊");
            OriginalUI.NativeIcon(DelItemButton,OriginalIcon.Discard,
                InventoryActionLayout.Delete,"永久删除：将背包物品拖到这里");
            for(int i=0;i<Grid.Length;i++)OriginalUI.NativeCell(Grid[i],OriginalUILayout.InventorySlot(i));
            for(int i=0;i<QuestGrid.Length;i++)OriginalUI.NativeCell(QuestGrid[i],OriginalUILayout.InventorySlot(i));
            for(int i=0;i<LockBar.Length;i++)
            {
                LockBar[i].Location=new Point(24+i%2*232,46+i/2*50);
                LockBar[i].AutoSize=false;LockBar[i].Size=new Size(230,48);
                LockBar[i].OriginalDraw=OriginalUI.LockMask;
            }
            OriginalUI.InventoryText(GoldLabel,InventoryActionLayout.Gold,17);
            GoldLabel.DrawFormat|=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
            OriginalUI.InventoryText(WeightLabel,InventoryActionLayout.Space,13);
            WeightLabel.DrawFormat|=TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine;
            GoldLabel.ForeColour=WeightLabel.ForeColour=Color.White;
            var goldHit=new MirImageControl{Parent=this,AutoSize=false};
            var goldArea=InventoryActionLayout.GoldHit;
            goldHit.Location=goldArea.Location; goldHit.Size=goldArea.Size;
            goldHit.OriginalDraw=_=>{};
            goldHit.Hint="金币（点击拾取）";
            goldHit.Click+=(o,e)=>{ if(GameScene.SelectedCell==null) GameScene.PickedUpGold=!GameScene.PickedUpGold && GameScene.Gold>0; };
            WeightLabel.Hint="背包空格数";
            WeightBar.BeforeDraw-=WeightBar_BeforeDraw;
            var weightBounds=InventoryActionLayout.Weight;
            var weightHit=new MirImageControl {Parent=this,AutoSize=false,
                Location=InventoryActionLayout.WeightHit.Location,Size=InventoryActionLayout.WeightHit.Size};
            weightHit.OriginalDraw=_=>{};
            weightHit.BeforeDraw+=(_,_)=>weightHit.Hint=WeightBar.Hint;
            WeightBar.AutoSize=false;WeightBar.UseOffSet=false;
            WeightBar.Location=weightBounds.Location;WeightBar.Size=weightBounds.Size;
            WeightBar.Visible=true;WeightBar.OriginalDraw=DrawOriginalWeightBar;
            Location=OriginalUILayout.Clamp(new Point(UIViewport.Width-Size.Width-32,180),Size,UIViewport.Size);
        }
    }

    public sealed partial class BeltDialog
    {
        private static int P(int value) => OriginalUILayout.HudPx(value);
        private void ApplyOriginalUI()
        {
            BeforeDraw-=BeltPanel_BeforeDraw;
            AutoSize=false;
            OriginalDraw=c=>{
                if(!OriginalUI.V5Draw(2,c.DisplayRectangle))
                    OriginalUI.RefinedHudSurface(c.DisplayRectangle);
                for(int i=0;i<6;i++)
                {
                    var slot=OriginalQuickbarLayout.Slot(i,false);slot.Offset(c.DisplayLocation);
                    OriginalUI.RefinedItemSlot(slot);
                }
            };
            OriginalHitTest=p=>Controls.Any(c=>c.Visible && !c.NotControl && c.DisplayRectangle.Contains(p)) ||
                (OriginalUI.V5SkinAvailable?OriginalUI.V5Pixel(2,DisplayRectangle,p):DisplayRectangle.Contains(p));
            LayoutOriginalBelt();
            foreach(var cell in Grid)OriginalUI.ObserveSlot(cell);
        }
        internal void LayoutOriginalBelt()
        {
            Size=OriginalQuickbarLayout.Size(false);
            for(int i=0;i<6;i++)
            {
                Grid[i].AutoSize=false; Grid[i].Size=new Size(P(32),P(32));
                Grid[i].Location=OriginalQuickbarLayout.Cell(i,false);
                Key[i].Location=new Point(P(62+i*40),P(38));
                Key[i].NotControl=true;
            }
            OriginalUI.Button(CloseButton,227,227,227,OriginalQuickbarLayout.Action(true,false));
            Location=OriginalUILayout.Clamp(Location,Size,UIViewport.Size);
        }
    }


    public sealed partial class SkillBarDialog
    {
        private static int P(int value) => OriginalUILayout.HudPx(value);
        private void ApplyOriginalUI()
        {
            BeforeDraw-=MagicKeyDialog_BeforeDraw;
            AutoSize=false; Size=new Size(P(368),P(56));
            OriginalDraw=c=>{
                if(originalBarCollapsed)return;
                for(int i=0;i<8;i++) OriginalUI.RefinedItemSlot(new Rectangle(c.DisplayLocation.X+P(20)+i*P(43),c.DisplayLocation.Y+P(15),P(43),P(40)));
            };
            OriginalUI.Chip(_switchBindsButton,new Rectangle(0,P(15),P(18),P(40)),"");
            _switchBindsButton.NotControl=true;
            OriginalUI.Text(BindNumberLabel,new Rectangle(P(3),P(27),P(12),P(18)),15);
            for(int i=0;i<8;i++)
            {
                Cells[i].AutoSize=false; Cells[i].Size=new Size(P(32),P(32));
                Cells[i].Location=new Point(P(25)+i*P(43),P(19));
                int slot = i;
                OriginalSkillArtwork.Bind(Cells[i], () => originalSkillSpells[slot]);
                OriginalSkillArtwork.BindCooldown(CoolDowns[i], Cells[i]);
                OriginalUI.Text(KeyNameLabels[i],new Rectangle(P(20)+i*P(43),0,P(42),P(15)),15);
                KeyNameLabels[i].DrawFormat|=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
                KeyNameLabels[i].NotControl=true;
            }
            ApplyOriginalBarToggle();
        }
    }


    public sealed partial class CharacterDialog
    {
        private void ApplyOriginalUI()
        {
            OriginalUI.Text(NameLabel,ClassicCharacterLayout.Name(false),18);
            NameLabel.DrawFormat|=TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding;
            NameLabel.ForeColour=Color.FromArgb(246,229,192);
            OriginalUI.Text(GuildLabel,ClassicCharacterLayout.Guild,13);
            GuildLabel.DrawFormat|=TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding;
            GuildLabel.ForeColour=Color.FromArgb(191,183,163);
            OriginalUI.ClassSymbol(ClassImage,()=>Actor==null?(MirClass?)null:Actor.Class,ClassicCharacterLayout.ClassBadge);
            foreach(var page in new[]{CharacterPage,StatusPage,StatePage,SkillPage})
            {
                page.Location=ClassicCharacterLayout.Page.Location;
                page.AutoSize=false;page.Size=ClassicCharacterLayout.Page.Size;
            }
            CharacterPage.OriginalDraw=c=>{
                DrawOriginalPortrait();
            };
            StatusPage.OriginalDraw=c=>OriginalUI.CharacterPageMaterial(c,1);
            StatePage.OriginalDraw=c=>OriginalUI.CharacterPageMaterial(c,2);
            SkillPage.OriginalDraw=c=>OriginalUI.CharacterPageMaterial(c,3);
            foreach(var cell in Grid)
                if(cell!=null)OriginalUI.CharacterCell(cell,new Point((int)Math.Round(cell.Location.X*1.5),(int)Math.Round(cell.Location.Y*1.5)),new Size(54,48));
            var status=new[]{HealthLabel,ManaLabel,ACLabel,MACLabel,DCLabel,MCLabel,SCLabel,CritRLabel,CritDLabel,AttkSpdLabel,AccLabel,AgilLabel,LuckLabel};
            var statusNames=new[]{"生命","魔法","防御","魔御","攻击","魔法攻击","道术","暴击几率","暴击伤害","攻击速度","准确","敏捷","幸运"};
            for(int i=0;i<status.Length;i++)
            {
                OriginalUI.Label(StatusPage,statusNames[i],new Rectangle(22,18+i*29,154,26),16);
                OriginalUI.Text(status[i],new Rectangle(180,18+i*29,168,26),16);
                status[i].DrawFormat|=TextFormatFlags.Right|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
            }
            var state=new[]{ExpPLabel,BagWLabel,WearWLabel,HandWLabel,MagicRLabel,PoisonResLabel,HealthRLabel,ManaRLabel,PoisonRecLabel,HolyTLabel,FreezeLabel,PoisonAtkLabel};
            var stateNames=new[]{"经验比例","背包负重","穿戴负重","手持负重","魔法躲避","中毒躲避","生命恢复","魔法恢复","中毒恢复","神圣","冰冻","毒物攻击"};
            for(int i=0;i<state.Length;i++)
            {
                OriginalUI.Label(StatePage,stateNames[i],new Rectangle(22,18+i*29,154,26),16);
                OriginalUI.Text(state[i],new Rectangle(180,18+i*29,168,26),16);
                state[i].DrawFormat|=TextFormatFlags.Right|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
            }
            OriginalUI.Icon(BackButton,OriginalIcon.Left,new Rectangle(124,382,40,30),"上一页技能");
            OriginalUI.Icon(NextButton,OriginalIcon.Right,new Rectangle(206,382,40,30),"下一页技能");
            EmptySkillsLabel=OriginalUI.Label(SkillPage,"暂无已学技能\n使用符合职业、等级要求的技能书学习\n学会后点击技能图标设置快捷键",new Rectangle(22,126,328,130),16);
            EmptySkillsLabel.DrawFormat|=TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter;
            for(int i=0;i<Magics.Length;i++)
            {
                var magic=Magics[i];magic.Location=new Point(12,12+i*52);magic.Size=new Size(348,48);
                magic.BeforeDraw+=(_,_)=>OriginalUI.CharacterSkillRow(magic.DisplayRectangle);
                magic.LevelImage.Visible=magic.ExpImage.Visible=false;
                magic.SkillButton.AutoSize=false;magic.SkillButton.Location=new Point(60,0);magic.SkillButton.Size=new Size(44,44);
                OriginalSkillArtwork.Bind(magic.SkillButton, () => magic.Magic?.Spell ?? Spell.None);
                OriginalSkillArtwork.BindCooldown(magic.CoolDown, magic.SkillButton);
                OriginalUI.Text(magic.KeyLabel,new Rectangle(0,0,54,44),12);
                magic.KeyLabel.DrawFormat|=TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPadding;
                magic.KeyLabel.NotControl=true;
                OriginalUI.Text(magic.NameLabel,new Rectangle(112,0,230,24),17);
                OriginalUI.Label(magic,"级",new Rectangle(112,26,22,20),12);
                OriginalUI.Text(magic.LevelLabel,new Rectangle(136,26,26,20),14);
                OriginalUI.Label(magic,"熟练",new Rectangle(170,26,36,20),12);
                OriginalUI.Text(magic.ExpLabel,new Rectangle(210,26,136,20),13);
            }
            InitializeCompleteCharacterFrame();
            Location=OriginalUILayout.Clamp(new Point(UIViewport.Width-Size.Width-600,20),Size,UIViewport.Size);
        }
    }

    public sealed partial class OptionDialog
    {
        private void ApplyOriginalUI()
        {

            OriginalUI.Panel(this,380,new Size(540,648));
            OriginalUI.NativeClose(CloseButton,new Point(504,8));
            OriginalUI.Label(this,"游 戏 设 置",new Rectangle(24,12,448,34),19);
            var on=new[]{SkillModeOn,SkillBarOn,EffectOn,DropViewOn,NameViewOn,HPViewOn,ObserveOn,NewMoveOn};
            var off=new[]{SkillModeOff,SkillBarOff,EffectOff,DropViewOff,NameViewOff,HPViewOff,ObserveOff,NewMoveOff};
            var names=new[]{"技能组合键","快捷技能栏","技能特效","地面物品名称","人物名称","血蓝提示格式","允许观察","移动方式"};
            Func<bool>[] active={() => Settings.SkillMode,()=>Settings.SkillBar,()=>Settings.Effect,()=>Settings.DropView,
                ()=>Settings.NameView,()=>Settings.HPView,()=>GameScene.AllowObserve,()=>Settings.NewMove};
            for(int i=0;i<on.Length;i++)
            {
                int y=62+i*44;var test=active[i];
                OriginalUI.Label(this,names[i],new Rectangle(24,y,176,36),17);
                OriginalUI.NativeChip(on[i],new Rectangle(224,y,132,36),i==0?"~ + 技能":i==5?"数值":i==7?"新版":"开启",test);
                OriginalUI.NativeChip(off[i],new Rectangle(372,y,144,36),i==0?"Ctrl + 技能":i==5?"比例":i==7?"经典":"关闭",()=>!test());
            }
            OriginalUI.Label(this,"音效",new Rectangle(24,430,92,28),17);
            OriginalUI.Label(this,"音乐",new Rectangle(24,472,92,28),17);
            SoundBar.AutoSize=MusicSoundBar.AutoSize=false;
            SoundBar.Location=new Point(132,430);MusicSoundBar.Location=new Point(132,472);
            SoundBar.Size=MusicSoundBar.Size=new Size(384,28);
            SoundBar.OriginalDraw=MusicSoundBar.OriginalDraw=_=>{};
            VolumeBar.Visible=MusicVolumeBar.Visible=false;
            var caption=OriginalUI.Label(this,"",new Rectangle(24,520,492,28),17);
            var minus=new MirButton{Parent=this};var reset=new MirButton{Parent=this};var plus=new MirButton{Parent=this};
            OriginalUI.NativeIcon(minus,OriginalIcon.Collapse,new Rectangle(24,562,44,36),"缩小界面");
            OriginalUI.NativeChip(reset,new Rectangle(84,562,372,36),"默认 115%");
            OriginalUI.NativeIcon(plus,OriginalIcon.Expand,new Rectangle(472,562,44,36),"放大界面");
            void SetScale(int value){Settings.ScaleGameUI=true;Settings.GameUIScalePercent=Math.Clamp(value,50,150);Settings.Save();GameScene.Scene.ReflowOriginalUI(true);}
            minus.Click+=(_,_)=>SetScale(Settings.GameUIScalePercent-5);reset.Click+=(_,_)=>SetScale(115);plus.Click+=(_,_)=>SetScale(Settings.GameUIScalePercent+5);
            BeforeDraw+=(_,_)=>{caption.Text=$"界面大小  {Settings.GameUIScalePercent}%";minus.Enabled=Settings.GameUIScalePercent>50;plus.Enabled=Settings.GameUIScalePercent<150;};
            OriginalUI.Label(this,"1920×1080 原生布局 · 可调整界面比例",new Rectangle(24,614,492,22),13);
            Location=OriginalUILayout.Clamp(new Point((UIViewport.Width-Size.Width)/2,(UIViewport.Height-Size.Height)/2),Size,UIViewport.Size);
        }
    }
}
