using System.Drawing;
using System.Runtime.CompilerServices;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using Server.MirNetwork;
using Server.Rewards;

internal static class RewardsRegression
{
    private static int checks;
    private static void Check(bool condition,string message)
    { checks++;if(!condition)throw new Exception(message); }
    private static void Time(long value)=>typeof(Envir).GetProperty(nameof(Envir.Time)).SetValue(Envir.Main,value);

    private sealed class TestPlayer:PlayerObject
    {
        public readonly List<Packet> Packets=new();
        public override void Enqueue(Packet packet)=>Packets.Add(packet);
        public override void ReceiveChat(string text,ChatType type) { }
    }
    private sealed class TestMonster:MonsterObject
    {
        public TestMonster(Map map,int hp=1000):base(new MonsterInfo { Index=201,Name="Reward test" })
        {
            CurrentMap=map;CurrentLocation=new Point(8,8);Stats[Stat.HP]=hp;HP=hp;
        }
        public override void Broadcast(Packet packet) { }
        public override void BroadcastHealthChange() { }
        // Keep this headless: actual attributed ChangeHP and settlement are production code.
        public override void Die(){if(Dead)return;PrepareDeathRewards();Dead=true;}
        public void Settle(){PrepareDeathRewards();}
        public void TickPoison(){ProcessPoison();}
        public void SettleQuests(){PrepareDeathRewards();AwardDeathQuestKills();}
        public bool DropTestGold(uint amount){PrepareDeathRewards();return DropGold(amount);}
        public readonly List<UserItem> PhysicalDrops=new();
        protected override bool DropItem(UserItem item){PhysicalDrops.Add(item);return true;}
        public void RollDrops(){Drop();}
    }
    private static TestPlayer Player(Map map,int index)
    {
        var player=new TestPlayer { Info=new CharacterInfo { Index=index,Name="P"+index,Level=10 },
            Stats=new Stats() };
        player.CurrentMap=map;player.CurrentLocation=new Point(8,8);
        player.Node=new LinkedListNode<MapObject>(player);
        // Metadata-only connection: no socket is opened or network loop started.
        player.Connection=(MirConnection)RuntimeHelpers.GetUninitializedObject(typeof(MirConnection));
        player.Connection.Observers=new();player.Connection.SentItemInfo=new();
        player.Connection.SentHeroInfo=new();player.Report=new Reporting(player);
        return player;
    }
    private static QuestProgressInfo Quest(TestPlayer player,ItemInfo item,MonsterInfo monster=null)
    {
        int index=1000+Envir.Main.QuestInfoList.Count;
        var info=new QuestInfo { Index=index,Name="Reward regression" };
        if(item!=null)info.ItemTasks.Add(new QuestItemTask { Item=item,Count=5 });
        if(monster!=null)info.KillTasks.Add(new QuestKillTask { Monster=monster,Count=5 });
        Envir.Main.QuestInfoList.Add(info);
        var progress=new QuestProgressInfo(index);progress.Init(player);
        player.CurrentQuests.Add(progress);return progress;
    }

    public static void Main()
    {
        Time(0);
        var ledger=new DamageContributionLedger();
        ledger.Record(1,10,25,1);ledger.Record(1,11,25,2);ledger.Record(2,20,49,3);
        Check(ledger.Winner(_=>true).Key==1,"Party damage was not combined");
        ledger.Record(2,20,2,4);
        Check(ledger.Winner(_=>true).Key==2,"Higher total damage did not win");
        Check(ledger.Sides.All(s=>ledger.Qualifies(s,10)),"Both contributing sides should get quest eligibility");
        ledger.Clear();ledger.Record(1,1,10,1);ledger.Record(2,2,90,2);
        Check(ledger.Qualifies(ledger.Sides.Single(s=>s.Key==1),10),"Exact 10 percent boundary failed");
        ledger.Record(2,2,1,3);
        Check(!ledger.Qualifies(ledger.Sides.Single(s=>s.Key==1),10),"Below threshold got quest credit");
        ledger.Clear();ledger.Record(1,1,50,1);ledger.Record(2,2,50,2);
        Check(ledger.Winner(_=>true).Key==1,"Equal damage tie should use first effective hit");
        Check(ledger.Winner(k=>k!=1).Key==2,"Ineligible side retained loot ownership");
        ledger.Record(3,3,0,3);Check(ledger.Sides.Count()==2,"Zero damage created a claim");

        var map=new Map(new MapInfo { Index=77 }) { Width=32,Height=32,Cells=new Cell[32,32] };
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)map.Cells[x,y]=new Cell { Attribute=CellAttribute.Walk };
        var a=Player(map,1);var support=Player(map,2);var b=Player(map,3);var newcomer=Player(map,4);
        // Exercise the actual LeaveGroup path: it clears the final member's GroupMembers.
        var leaver=Player(map,20);var remaining=Player(map,21);var opponent=Player(map,22);
        leaver.GroupMembers=remaining.GroupMembers=new List<PlayerObject>{leaver,remaining};
        var dissolved=new TestMonster(map);
        var remainingQuest=Quest(remaining,null,dissolved.Info);
        dissolved.ChangeHPAttributed(-700,leaver);leaver.LeaveGroup();
        Check(remaining.GroupMembers==null,"Test did not dissolve the two-player group");
        dissolved.ChangeHPAttributed(-300,opponent);dissolved.SettleQuests();
        Check(dissolved.EXPOwner==remaining && remainingQuest.KillTaskCount[0].Count==1,
            "Remaining member lost historical party loot or quest credit when UI group dissolved");
        leaver.GroupMembers=remaining.GroupMembers=new List<PlayerObject>{leaver,remaining};
        dissolved=new TestMonster(map);dissolved.ChangeHPAttributed(-700,leaver);leaver.LeaveGroup();
        remaining.GroupMembers=new List<PlayerObject>{remaining,newcomer};
        dissolved.ChangeHPAttributed(-300,opponent);
        Check(dissolved.EXPOwner==opponent,"Dissolved party damage migrated into a new party");
        a.GroupMembers=support.GroupMembers=new List<PlayerObject>{a,support};
        var monster=new TestMonster(map);
        monster.ChangeHPAttributed(-400,a);monster.ChangeHPAttributed(-500,b);monster.ChangeHPAttributed(-10000,a);
        Check(monster.EXPOwner==a,"Overkill was not clipped or equal damage tie changed");

        monster=new TestMonster(map);
        monster.ChangeHPAttributed(-400,a);monster.ChangeHPAttributed(-600,b);
        Check(monster.EXPOwner==b,"Outsider's higher effective damage did not win");
        uint oldGoldLimit=Settings.MaxDropGold;Settings.MaxDropGold=100;
        var beforeGold=Envir.Main.Objects.OfType<ItemObject>().Count();
        Check(monster.DropTestGold(200),"Gold could not be placed on an empty map");
        var goldPiles=Envir.Main.Objects.OfType<ItemObject>().Skip(beforeGold).ToArray();
        Check(goldPiles.Length==2 && goldPiles.All(p=>p.Gold==100),"Exact gold multiple created a zero or extra pile");
        Check(goldPiles.All(p=>p.CanBePickedUpBy(b) && !p.CanBePickedUpBy(a)),"Gold ownership differs from equipment ownership");
        Settings.MaxDropGold=oldGoldLimit;
        var qa=Quest(a,null,monster.Info);var qs=Quest(support,null,monster.Info);var qb=Quest(b,null,monster.Info);
        // Previous settlement snapshot predates these quests; no retroactive acceptance.
        monster.SettleQuests();
        Check(qa.KillTaskCount[0].Count==0 && qb.KillTaskCount[0].Count==0,"Quest accepted after death got retroactive credit");
        monster=new TestMonster(map);monster.ChangeHPAttributed(-400,a);monster.ChangeHPAttributed(-600,b);
        monster.SettleQuests();monster.SettleQuests();
        Check(qa.KillTaskCount[0].Count==1 && qs.KillTaskCount[0].Count==1 && qb.KillTaskCount[0].Count==1,
            "Eligible parties/support did not independently progress exactly once");

        var hero=(HeroObject)RuntimeHelpers.GetUninitializedObject(typeof(HeroObject));hero.Owner=a;
        var pet=new TestMonster(map){Master=hero};
        Check(MonsterObject.RewardPlayer(pet)==a && MonsterObject.RewardPlayer(hero)==a,"Hero/pet damage failed player normalization");
        monster=new TestMonster(map);monster.ChangeHPAttributed(-600,pet);monster.ChangeHPAttributed(-400,b);
        Check(monster.EXPOwner==a,"Pet contribution was lost");
        // Check actual HP, not nominal input damage. This must work BEFORE the
        // first ProcessPoison tick, as well as during an established encounter.
        foreach(bool afterTick in new[]{false,true})
        {
            monster=new TestMonster(map);
            Check(monster.DamageRate==1F && monster.ArmourRate==1F,"Fresh monster has non-neutral incoming multipliers");
            if(afterTick)monster.TickPoison();
            int dealt=monster.StruckAttributed(600,a,DefenceType.MAC);
            Check(dealt==600 && monster.HP==400 && !monster.Dead,"Area hit did not actually remove 600 HP");
            monster.PoisonDamage(-400,b);
            Check(monster.Dead && monster.HP==0 && monster.EXPOwner==a,
                "Owned area spell damage or poison lost attribution");
            var before=Envir.Main.Objects.OfType<ItemObject>().ToHashSet();
            Check(monster.DropTestGold(1),"Attribution test could not place actual loot");
            var prize=Envir.Main.Objects.OfType<ItemObject>().Single(x=>!before.Contains(x));
            Check(prize.CanBePickedUpBy(a) && !prize.CanBePickedUpBy(b),"Lower poison damage stole actual loot protection");
            Console.WriteLine($"Area/poison (after tick={afterTick}): A={dealt}, B=400, HP={monster.HP}, owner={monster.EXPOwner.Name}");
        }
        foreach(var poisonType in new[]{PoisonType.Green,PoisonType.Bleeding})
        {
            monster=new TestMonster(map);
            Check(monster.StruckAttributed(600,a,DefenceType.MAC)==600 && monster.HP==400,
                "Poison tick setup did not remove actual area damage");
            monster.PoisonList.Add(new Poison { Owner=b,PType=poisonType,Value=400,Duration=2,TickTime=-1,TickSpeed=1000 });
            monster.TickPoison();
            Check(monster.Dead && monster.HP==0 && monster.EXPOwner==a,
                "Real poison processing retained its provisional owner over the damage winner");
            monster.TickPoison();
            Check(monster.EXPOwner==a,"Post-death poison processing changed settled ownership");
        }
        monster=new TestMonster(map);monster.PoisonDamage(-600,a);
        Check(monster.HP==400,"Leading poison damage was not applied");
        Check(monster.StruckAttributed(400,b,DefenceType.MAC)==400 && monster.Dead && monster.EXPOwner==a,
            "Lower area last hit stole the leading poison contribution");
        monster=new TestMonster(map);monster.Stats[Stat.MinMAC]=monster.Stats[Stat.MaxMAC]=200;
        Check(monster.StruckAttributed(800,a,DefenceType.MAC)==600 && monster.HP==400,"Armour was not applied before attribution");
        monster.PoisonDamage(-10000,b);
        Check(monster.Dead && monster.EXPOwner==a,"Poison overkill was not clipped to remaining HP");
        monster=new TestMonster(map);monster.DamageRate=1.5F;
        Check(monster.StruckAttributed(400,a,DefenceType.MAC)==600 && monster.HP==400,"Incoming amplification was not applied");
        monster.PoisonDamage(-400,b);
        Check(monster.EXPOwner==a,"Amplified effective damage was misattributed");
        monster=new TestMonster(map);monster.DamageRate=0F;
        Check(monster.StruckAttributed(600,a,DefenceType.MAC)==0 && monster.HP==1000,"Explicit zero multiplier stopped working");
        monster.PoisonDamage(-1000,b);
        Check(monster.Dead && monster.EXPOwner==b,"Zero effective damage created a loot claim");
        monster=new TestMonster(map);monster.ChangeHPAttributed(-50,a);monster.CreditInstantKill(b);monster.Die();
        Check(monster.EXPOwner==b,"Instant kill failed to credit remaining HP");

        monster=new TestMonster(map);monster.ChangeHPAttributed(-900,a);Time(31_000);monster.ChangeHPAttributed(-100,b);
        Check(monster.EXPOwner==b,"Stale encounter damage survived combat timeout");Time(0);
        monster=new TestMonster(map);monster.ChangeHPAttributed(-900,a);monster.SetHP(1000);
        monster.ChangeHPAttributed(-1000,b);Check(monster.EXPOwner==b,"Reset-to-full retained old damage");
        monster=new TestMonster(map);monster.ChangeHPAttributed(-700,a);a.GroupMembers.Remove(a);a.GroupMembers=null;
        monster.ChangeHPAttributed(-300,b);Check(monster.EXPOwner==support,"Leaving a party transferred historical damage to solo play");
        a.GroupMembers=support.GroupMembers;
        a.GroupMembers.Add(a);

        var template=new ItemInfo { Index=501,Name="Quest tooth",Type=ItemType.Quest,StackSize=10 };
        var pa=Quest(a,template);var ps=Quest(support,template);
        var drop=new UserItem(template){Count=10};
        var ia=a.CreatePersonalQuestReward(drop,new[]{pa});var ib=support.CreatePersonalQuestReward(drop,new[]{ps});
        Check(ia.Count==5 && ib.Count==5,"Quest reward exceeded outstanding objective count");
        Check(ia.UniqueID!=ib.UniqueID && !ReferenceEquals(ia,ib),"Quest rewards share identity or object");
        ia.Count=1;Check(ib.Count==5 && drop.Count==10,"Recipient mutated another recipient/template");
        Check(newcomer.CreatePersonalQuestReward(drop,newcomer.CurrentQuests)==null,"No quest still produced personal reward");
        var ground=new ItemObject(monster,ib);ground.SetPersonalQuestReward(support,new[]{ps});
        Check(ground.CanBePickedUpBy(support) && !ground.CanBePickedUpBy(a),"Private quest item could be stolen by teammate");
        Time(1_000_000);ground.Owner=null;
        Check(!ground.CanBePickedUpBy(a),"Private quest reward became public after protection time");
        support.CurrentQuests.Remove(ps);var replacement=new QuestProgressInfo(ps.Index);
        replacement.Init(support);replacement.StartDateTime=ps.StartDateTime;support.CurrentQuests.Add(replacement);
        Check(!ground.CanBePickedUpBy(support),"Abandon/reaccept authorized an old private drop");Time(0);

        var fresh=a.CreatePersonalQuestReward(drop,new[]{pa});
        Check(a.TryGainPersonalQuestReward(fresh,new[]{pa}) && a.Info.QuestInventory.Where(i=>i?.Info==template).Sum(i=>i.Count)==5,
            "Eligible personal quest item was not granted to the quest bag");
        Check(a.NeededQuestItemCount(template,new[]{pa})==0,"Completed objective still requested excess drops");
        var filler=new ItemInfo { Index=503,Name="Full bag filler",Type=ItemType.Quest,StackSize=1 };
        for(int slot=0;slot<support.Info.QuestInventory.Length;slot++)support.Info.QuestInventory[slot]=new UserItem(filler);
        var pending=support.CreatePersonalQuestReward(drop,new[]{replacement});
        Check(!support.TryGainPersonalQuestReward(pending,new[]{replacement}),"Full quest bag incorrectly consumed a reward");
        ground=new ItemObject(monster,pending);ground.SetPersonalQuestReward(support,new[]{replacement});
        support.Info.QuestInventory[0]=null;
        Check(ground.TryGrantPersonalQuestReward(support),"Private ground fallback could not be claimed after making space");

        // Ground stack is five, but the owner only needs one; a full bag can fit that one.
        var pickupPlayer=Player(map,23);
        var oneQuest=Quest(pickupPlayer,template);
        for(int slot=0;slot<pickupPlayer.Info.QuestInventory.Length;slot++)
            pickupPlayer.Info.QuestInventory[slot]=new UserItem(filler);
        oneQuest.Info.ItemTasks[0].Count=10;
        pickupPlayer.Info.QuestInventory[0]=new UserItem(template){Count=9};
        var extra=new UserItem(template){Count=5};
        var privateStack=new ItemObject(monster,extra);privateStack.SetPersonalQuestReward(pickupPlayer,new[]{oneQuest});
        Check(privateStack.CanAcceptPickupFor(pickupPlayer) && extra.Count==5,
            "Pet capacity probe rejected an outstanding partial stack or mutated ground loot");
        Check(privateStack.TryGrantPersonalQuestReward(pickupPlayer) && pickupPlayer.Info.QuestInventory[0].Count==10,
            "Pet/manual partial pickup did not share the same capacity rule");
        oneQuest.Info.ItemTasks[0].Count=12;
        var blocked=new UserItem(template){Count=5};
        Check(!pickupPlayer.TryGainPersonalQuestReward(blocked,new[]{oneQuest}) && blocked.Count==5,
            "Failed pickup shrank the ground stack");

        var loot=new ItemObject(monster,new UserItem(template));loot.SetRewardOwners(new[]{a,support},60_000);
        loot.Owner=null;a.GroupMembers.Add(newcomer);newcomer.GroupMembers=a.GroupMembers;
        Check(loot.CanBePickedUpBy(a) && !loot.CanBePickedUpBy(newcomer),"Late party join bypassed frozen loot owners");
        Time(60_000);Check(loot.CanBePickedUpBy(newcomer),"Ordinary loot never became public after protection expired");Time(0);

        var material=new ItemInfo { Index=502,Name="Ordinary material",Type=ItemType.CraftingMaterial,StackSize=10 };
        var grouped=new DropInfo { Chance=1,GroupedDrop=new GroupDropInfo {
            new DropInfo { Chance=1,Item=material },
            new DropInfo { Chance=1,Item=material,QuestRequired=true }
        }};
        var rolled=grouped.AttemptDrop();
        Check(rolled.Items.Count==2 && !rolled.QuestRequiredItems[0] && rolled.QuestRequiredItems[1],
            "Nested group lost per-instance quest flags for identical item templates");
        grouped=new DropInfo { Chance=1,QuestRequired=true,GroupedDrop=new GroupDropInfo { Random=true }};
        grouped.GroupedDrop.Add(new DropInfo { Chance=1,Item=material });
        Check(grouped.AttemptDrop().QuestRequiredItems.Single(),"Parent quest flag was lost by random group selection");
        var materialQuest=Quest(a,material);a.Info.Inventory[0]=new UserItem(material){Count=5};
        materialQuest.ProcessItem(a.Info.QuestInventory);
        Check(materialQuest.ItemTaskCount[0].Count==5,"Pre-collected ordinary materials were ignored");
        materialQuest.CheckCompleted();a.Info.Inventory[0]=null;materialQuest.ProcessItem(a.Info.QuestInventory);
        Check(!materialQuest.CheckCompleted() && !materialQuest.Completed,"Removed materials left quest permanently completed");
        var dropper=new TestMonster(map);
        Quest(support,material);
        dropper.Info.Drops.Add(new DropInfo { Chance=1,Item=material,QuestRequired=true });
        dropper.ChangeHPAttributed(-1000,a);dropper.RollDrops();dropper.RollDrops();
        Check(dropper.PhysicalDrops.Count==1 && dropper.PhysicalDrops[0].Info==material,
            "Q-marked tradable material was cloned or duplicate death rolled twice");
        Check(!a.Info.QuestInventory.Any(i=>i?.Info==material) && !support.Info.QuestInventory.Any(i=>i?.Info==material),
            "Ordinary Q-marked material went into personal quest inventory");
        var equipment=new ItemInfo {Index=504,Name="Ordinary quest blade",Type=ItemType.Weapon,StackSize=1};
        Quest(a,equipment);Quest(support,equipment);
        dropper=new TestMonster(map);dropper.Info.Drops.Add(new DropInfo {Chance=1,Item=equipment,QuestRequired=true});
        dropper.ChangeHPAttributed(-1000,a);dropper.RollDrops();
        Check(dropper.PhysicalDrops.Count==1,"Q-marked equipment did not remain a single physical drop");
        dropper=new TestMonster(map);dropper.Info.Drops.Add(new DropInfo {Chance=1,Item=equipment,QuestRequired=true});
        dropper.ChangeHPAttributed(-1000,b);dropper.RollDrops();
        Check(dropper.PhysicalDrops.Count==0,"Ordinary Q gate ignored absence of an eligible quest");
        var reserved=Quest(newcomer,template);reserved.Info.CarryItems.Add(new QuestItemTask { Item=template,Count=2 });
        newcomer.Info.QuestInventory[0]=new UserItem(template){Count=5};reserved.ProcessItem(newcomer.Info.QuestInventory);
        Check(reserved.ItemTaskCount[0].Count==3 && newcomer.NeededQuestItemCount(template,new[]{reserved})==2,
            "Carry item reservation caused duplicate consumption or a collection deadlock");
        newcomer.Info.QuestInventory[0].Count=7;reserved.ProcessItem(newcomer.Info.QuestInventory);
        Check(reserved.CheckCompleted() && newcomer.NeededQuestItemCount(template,new[]{reserved})==0,"Reserved carry and task quantities never completed");
        var timed=Quest(a,material);timed.Info.TimeLimitInSeconds=60;timed.SetTimer();timed.SetTimer();
        Check(a.ActionList.Count(x=>x.Type==DelayedType.Quest && !x.FlaggedToRemove && ReferenceEquals(x.Params[0],timed))==1,
            "Restarting timer duplicated live quest expiry callbacks");
        var stale=new DelayedAction(DelayedType.Quest,0,timed,QuestAction.TimeExpired,true);
        a.AbandonQuest(timed.Index);
        var retaken=new QuestProgressInfo(timed.Index);retaken.Init(a);a.CurrentQuests.Add(retaken);
        a.Process(stale);
        Check(a.CurrentQuests.Contains(retaken),"Old expiry callback canceled a new attempt");
        long action=a.ActionTime;a.Harvest(MirDirection.Up);
        Check(a.ActionTime==action,"Removed harvesting still locks player actions");
        Time(0);
        foreach(var grade in Enum.GetValues<ItemGrade>())
        {
            var gear=new UserItem(new ItemInfo {Name="Quality regression",Grade=grade});
            gear.AddedStats[Stat.MaxDC]=1;
            Check(gear.IsAdded,"Added-stat fixture is not actually enhanced");
            var physical=new ItemObject(monster,gear);
            var info=(ServerPackets.ObjectItem)physical.GetInfo();
            Check(info.grade==grade && info.NameColour==ItemQualityPresentation.Colour(grade),"Added stats overrode authoritative grade colour");
            Check(physical.NameColour==info.NameColour,"Constructor and packet quality disagree");
            var manual=new ItemObject(monster,gear,new Point(7,7));
            Check(((ServerPackets.ObjectItem)manual.GetInfo()).NameColour==info.NameColour,"Manual item uses a different palette");
            Packet.IsServer=false;
            byte[] raw=info.GetPacketBytes().ToArray();
            var round=(ServerPackets.ObjectItem)Packet.ReceivePacket(raw,out var rest);
            Check(rest.Length==0 && round.grade==grade && round.DropAge==10000 && round.DropOrigin==monster.CurrentLocation,"Item packet presentation fields failed roundtrip");
            var legacy=raw[..^12];BitConverter.GetBytes((ushort)legacy.Length).CopyTo(legacy,0);
            round=(ServerPackets.ObjectItem)Packet.ReceivePacket(legacy,out rest);
            Check(round.DropAge==10000 && round.DropOrigin==round.Location,"Legacy packet should settle with no false burst");
        }
        var scatterMap=new Map(new MapInfo {Index=78}) {Width=9,Height=9,Cells=new Cell[9,9]};
        for(int y=0;y<9;y++)for(int x=0;x<9;x++)scatterMap.Cells[x,y]=new Cell {Attribute=CellAttribute.Walk};
        var origin=Player(scatterMap,80);origin.CurrentLocation=new Point(4,4);scatterMap.AddObject(origin);
        scatterMap.Cells[3,3].Attribute=CellAttribute.HighWall;
        scatterMap.Info.Movements.Add(new MovementInfo {Source=new Point(5,3)});
        var locations=new HashSet<Point>();
        for(int i=0;i<120;i++)
        {
            var physical=new ItemObject(origin,100U);
            Check(physical.Drop(1),"Legal neighbour drop failed");
            var at=physical.CurrentLocation;locations.Add(at);
            Check(at!=origin.CurrentLocation && at!=new Point(3,3) && at!=new Point(5,3) && scatterMap.ValidPoint(at),"Drop chose a blocked, occupied or warp cell");
            var packet=(ServerPackets.ObjectGold)physical.GetInfo();
            Check(packet.DropOrigin==origin.CurrentLocation && packet.DropAge==0,"Fresh drop lost origin or creation age");
            var copy=(ServerPackets.ObjectGold)Packet.ReceivePacket(packet.GetPacketBytes().ToArray(),out _);
            Check(copy.DropOrigin==packet.DropOrigin && copy.DropAge==0,"Gold presentation packet failed roundtrip");
            Time(5000);Check(((ServerPackets.ObjectGold)physical.GetInfo()).DropAge==5000,"Range snapshot reset drop age");Time(0);
            scatterMap.RemoveObject(physical);physical.Despawn();
        }
        Check(locations.Count>=5,"Drops are still biased to a fixed scan direction");
        var dragonLocations=new HashSet<Point>();
        for(int i=0;i<80;i++)
        {
            var physical=new ItemObject(origin,1U);
            Check(physical.DragonDrop(2),"Dragon drop rectangle has no usable placement");
            Check(physical.CurrentLocation.Y>=7 && physical.CurrentLocation.Y<=8,"Dragon drop left its existing region");
            dragonLocations.Add(physical.CurrentLocation);
            scatterMap.RemoveObject(physical);physical.Despawn();
        }
        Check(dragonLocations.Count>=5,"Dragon drops retain fixed scan placement");
        Check(!new ItemObject(origin,1U).Drop(0),"Drop ignored a blocking actor");
        // Stack fallback remains legal only below its configured cap.
        for(int y=3;y<=5;y++)for(int x=3;x<=5;x++)scatterMap.Cells[x,y].Attribute=CellAttribute.HighWall;
        scatterMap.Cells[3,4].Attribute=CellAttribute.Walk;
        var stack=new List<ItemObject>();
        for(int i=0;i<Server.Settings.DropStackSize;i++)
        {
            var physical=new ItemObject(origin,1U);Check(physical.Drop(1),"Stack fallback rejected capacity");stack.Add(physical);
        }
        Check(!new ItemObject(origin,1U).Drop(1),"Drop stack limit exceeded");
        foreach(var physical in stack){scatterMap.RemoveObject(physical);physical.Despawn();}
        Console.WriteLine($"Rewards regression passed: {checks} assertions. Real server classes; no live server or renderer started.");
    }
}
