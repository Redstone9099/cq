using Client;
using Client.MirGraphics;
using Client.MirObjects;

internal static class LootEffectsRegression
{
    private static int checks;
    private static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
    private static ItemObject Item(uint id,ItemGrade grade,Point cell)
    {
        var item=new ItemObject(id);
        item.Load(new ServerPackets.ObjectItem {ObjectID=id,grade=grade,Location=cell,DropAge=10000,Name="Loot"});
        item.Process();
        item.LootEffectAnchor=new(150+cell.X,250+cell.Y);
        return item;
    }
    public static void Main()
    {
        var zero=LootEffectTimeline.At(-1,0,1);
        Check(zero.Opacity==0 && zero.Flash==0,"Future birth must not show a burst");
        foreach(var key in LootEffectTimeline.Arrival)
        {
            var s=LootEffectTimeline.At(key.Time,0,42);
            Check(Math.Abs(s.Height-key.Height)<0.0001 && Math.Abs(s.Flash-key.Flash)<0.0001,
                "Authored arrival keyframe was not reached");
        }
        foreach(int hz in new[]{24,30,60,120,144,240,360})
        {
            for(int frame=0;frame<hz*8;frame++)
            {
                var s=LootEffectTimeline.At(frame*1000L/hz,0,42);
                Check(float.IsFinite(s.Opacity) && s.Opacity>=0 && s.Opacity<=1 && s.Height>0,
                    "Timeline exceeded material bounds");
            }
            Check(LootEffectTimeline.At(5000,0,42)==LootEffectTimeline.At(7400,0,42),"Loop drifted across frame rates");
        }
        Check(LootEffectTimeline.At(2000,0,1).Phase!=LootEffectTimeline.At(2000,0,2).Phase,
            "Different drops blink in lockstep");
        var before=LootEffectTimeline.At(999,0,9);var after=LootEffectTimeline.At(1000,0,9);
        Check(Math.Abs(before.Opacity-after.Opacity)<0.001,"Arrival-to-idle transition pops");
        Check(LootEffectTimeline.At(1_000_000,0,9).Flash==0,"Long pause replays arrival flash");

        var budget=new LootEffectBudget();var objects=new List<MapObject>();
        for(uint i=0;i<150;i++)objects.Add(Item(i+1,(ItemGrade)(1+i%5),new((int)i%20,(int)i/20)));
        var top=Item(1001,ItemGrade.Heroic,new(0,0));objects.Add(top);
        var sameCell=Item(1002,ItemGrade.Common,new(0,0));objects.Add(sameCell);
        var none=Item(1003,ItemGrade.None,new(30,30));objects.Add(none);
        var offscreen=Item(1004,ItemGrade.Heroic,new(40,40));offscreen.LootEffectAnchor=new(-500,-500);objects.Add(offscreen);
        Check(budget.Update(objects,Point.Empty,new(800,600),180,true),"Visible loot did not invalidate cache");
        var selected=objects.Cast<ItemObject>().Where(i=>i.LootEffectDetail>0).ToArray();
        Check(selected.Length==LootEffectBudget.MaximumEffects && selected.Count(i=>i.LootEffectDetail==2)==LootEffectBudget.MaximumDetailedEffects,"Draw budget exceeded");
        Check(selected.Select(i=>i.CurrentLocation).Distinct().Count()==selected.Length,"Same-cell effects stacked");
        Check(top.LootEffectDetail==2 && sameCell.LootEffectDetail==0 && none.LootEffectDetail==0 && offscreen.LootEffectDetail==0,
            "Priority, ungraded-item or viewport filtering failed");
        Check(!budget.Update(objects,Point.Empty,new(800,600),181,true),"High-Hz update needlessly redrew loot");
        MLibrary.Calls.Clear();foreach(var item in selected)LootEffectRenderer.Draw(item);
        Check(MLibrary.Calls.Count<=64,"Per-frame submissions exceeded fixed budget");
        Check(MLibrary.Calls.All(c=>c.Blend && c.Opacity==1 && c.Colour.A>0),"Material intensity not encoded in alpha");
        Check(DXManager.Changes==selected.Length*2,"Blend state changed for each particle instead of each item");
        Check(!DXManager.Blending && DXManager.BlendingRate==1,"Draw leaked additive state into item rendering");
        DXManager.SetBlend(true,0.4F,BlendMode.INVLIGHT);MLibrary.ThrowOnDraw=true;
        try{LootEffectRenderer.Draw(top);throw new Exception("Missing simulated draw failure");}
        catch(InvalidOperationException) { }
        finally{MLibrary.ThrowOnDraw=false;}
        Check(DXManager.Blending && DXManager.BlendingRate==0.4F && DXManager.BlendingMode==BlendMode.INVLIGHT,
            "Exception failed to restore incoming blend state");
        Check(budget.Update(objects,Point.Empty,new(800,600),181,false) && selected.All(i=>i.LootEffectDetail==0),
            "Switching effects off left cached pixels");
        Check(!budget.Update(objects,Point.Empty,new(800,600),220,false),"Disabled effects still redraw");
        budget.Update(objects,Point.Empty,new(800,600),250,true);budget.Clear();
        Check(objects.Cast<ItemObject>().All(i=>i.LootEffectDetail==0),"Map unload retained selected effects");
        MLibrary.Present=false;
        Check(!budget.Update(objects,Point.Empty,new(800,600),300,true),"Missing atlas triggers perpetual redraw");
        MLibrary.Present=true;budget.Update(objects,Point.Empty,new(800,600),350,true);objects.Clear();
        Check(budget.Update(objects,Point.Empty,new(800,600),400,true),"Removing final drop did not clear effects");
        Check(!budget.Update(objects,Point.Empty,new(800,600),450,true),"Empty scene still redraws");
        // Execute the actual ItemObject Load/Process/Draw path with padded sprites.
        CMain.Time=0;
        var live=new ItemObject(8001);
        live.Load(new ServerPackets.ObjectItem {Location=new(5,5),DropOrigin=new(3,4),DropAge=0,
            grade=ItemGrade.Common,NameColour=Color.Cyan,Name="Added white item"});
        live.Process();
        Check(live.NameColour==ItemQualityPresentation.Colour(ItemGrade.Common),"Legacy cyan override leaked into white grade");
        Check(live.Size.Width<=40 && live.Size.Height<=26,"Oversized ground sprite was not fitted");
        var start=live.DisplayRectangle;
        CMain.Time=200;live.Process();
        Check(live.DisplayRectangle!=start && live.LootHeight>0,"Actual ground object did not follow its drop arc");
        Check(!Client.MirScenes.GameScene.Scene.MapControl.TextureValid,"Flight did not invalidate cached map");
        Client.MirScenes.GameScene.Scene.MapControl.TextureValid=true;
        CMain.Time=2000;live.Process();var final=live.DisplayRectangle;
        Check(!Client.MirScenes.GameScene.Scene.MapControl.TextureValid,"Landing left the previous airborne sprite cached");
        CMain.Time=9000;live.Process();Check(live.DisplayRectangle==final && live.LootHeight==0,"Settled item keeps floating or changing jitter");
        MLibrary.Calls.Clear();MLibrary.FloorCalls.Clear();live.Draw();
        Check(MLibrary.FloorCalls.Single().Source==MLibrary.VisibleBounds,"Transparent padding was not cropped");
        Check(MLibrary.FloorCalls.Single().Destination==live.DisplayRectangle,"Sprite and selection bounds disagree");
        Check(MLibrary.Calls.Count==1 && !MLibrary.Calls[0].Blend && MLibrary.Calls[0].Colour.R==0,"Common ground item needs a non-additive contact shadow only");
        var gold=new ItemObject(8002);gold.Load(new ServerPackets.ObjectGold {Gold=1000,Location=new(4,4),DropOrigin=new(3,3),DropAge=0});
        CMain.Time+=200;gold.Process();Check(gold.LootHeight>0,"Gold bypassed flight animation");
        var snapshot=new ItemObject(8003);snapshot.Load(new ServerPackets.ObjectItem {Location=new(5,5),DropOrigin=new(3,3),DropAge=10000});
        snapshot.Process();Check(snapshot.LootHeight==0 && !snapshot.LootInArrival,"Old drop replayed arrival on re-entering view");
        foreach(var grade in Enum.GetValues<ItemGrade>())
        {
            var item=Item(9000+(uint)grade,grade,new(0,0));
            Check(item.NameColour==ItemQualityPresentation.Colour(grade),"Name and quality material disagree");
            item.LootEffectDetail=2;item.LootEffectSample=LootEffectTimeline.At(9000,0,item.ObjectID);
            MLibrary.Calls.Clear();LootEffectRenderer.Draw(item);
            Check(MLibrary.Calls.All(c=>c.Frame>=5 && c.Size.Height<=32 && c.Size.Width>c.Size.Height),"Tall vertical decal re-entered ground pass");
            Check(grade>=ItemGrade.Rare || MLibrary.Calls.Count==0,"White quality received permanent magical effects");
            Check(MLibrary.Calls.All(c=>(c.Colour.ToArgb()&0xFFFFFF)==(item.NameColour.ToArgb()&0xFFFFFF)),"Effect colour differs from item name");
        }
        Check(LootPresentation.Fit(new(48,48))==new Size(20,20),"Compact bottle/shield art should have a small ground footprint");
        Check(LootPresentation.Fit(new(8,12))==new Size(8,12),"Tiny original art must not be enlarged");
        var savedBounds=MLibrary.VisibleBounds;MLibrary.VisibleBounds=Rectangle.Empty;
        var missing=Item(9999,ItemGrade.Heroic,Point.Empty);missing.LootEffectDetail=2;
        missing.LootEffectSample=LootEffectTimeline.At(9000,0,9999);
        MLibrary.Calls.Clear();LootEffectRenderer.DrawShadow(missing);LootEffectRenderer.Draw(missing);
        Check(MLibrary.Calls.Count==0,"Missing item art still draws a floating effect");
        MLibrary.VisibleBounds=savedBounds;
        var random=new Random(68);
        for(int i=0;i<300;i++)
        {
            var size=LootPresentation.Fit(new(random.Next(1,300),random.Next(1,300)));
            Check(size.Width<=40 && size.Height<=26 && size.Width>0 && size.Height>0,"Footprint fitting failed");
            uint id=(uint)i;
            var settled=LootPresentation.At(LootPresentation.Duration(id),id,new(2,2),new(4,5));
            Check(!settled.Moving && settled.Offset==PointF.Empty && settled.Height==0,"Landing endpoint is not exact");
        }
        var layout=new LootLabelLayout();var bounds=new Rectangle(0,0,800,600);layout.Begin(bounds);
        var labels=new List<Rectangle>();
        for(int i=0;i<200;i++)
        {
            var size=new Size(100,16);
            if(layout.TryPlace(new Point(300+i%8*30,300+i/8*8),size,i==0,out var at))
            {
                var rect=new Rectangle(at,size);
                Check(bounds.Contains(rect) && !labels.Any(r=>r.IntersectsWith(rect)),"Dense loot names overlap or escape screen");
                labels.Add(rect);
            }
        }
        Check(labels.Count>0 && labels.Count<=LootLabelLayout.MaximumLabels,"Label budget failed");
        layout.Begin(bounds);
        Check(layout.TryPlace(new(5,5),new(100,16),true,out var edge) && edge.X==0 && edge.Y==0,"Hovered edge label clipped offscreen");
        CheckEnhancedFeedback();
        CheckDenseSceneWork();
        CaptureFrames();
        Console.WriteLine($"Loot effect regression: {checks} assertions; production timeline, budget and renderer with graphics adapters.");
    }

    private static void CheckEnhancedFeedback()
    {
        DXManager.SetBlend(false);Settings.Effect=true;
        CMain.Time=20000;
        var rare=Item(20001,ItemGrade.Rare,Point.Empty);
        var objects=new List<MapObject>{rare};var budget=new LootEffectBudget();
        // Reduced-detail quality rims must remain visible throughout a full loop.
        // This catches the original regression where the entire effect sat under the body.
        for(int t=0;t<2400;t+=33)
        {
            CMain.Time=20000+t;rare.Process();
            budget.Update(objects,Point.Empty,new(800,600),CMain.Time,true);
            rare.LootEffectDetail=1;MLibrary.Calls.Clear();LootEffectRenderer.Draw(rare);
            var rim=MLibrary.Calls.Single(c=>c.Frame==5);
            Check(rim.Colour.A>=85 && rim.Size.Width>=rare.Size.Width+20,
                "Reduced-detail rare rim became too dim or hidden underneath the item");
            Check(rim.At.Y+rim.Size.Height>=rare.DisplayRectangle.Bottom+6,
                "Near rim does not extend beyond the opaque item's foot");
            Check(MLibrary.Calls.Count==2,"Idle reduced detail exceeds two ground submissions");
        }
        rare.LootEffectDetail=2;rare.LootEffectSample=LootEffectTimeline.At(21000,0,rare.ObjectID);
        MLibrary.Calls.Clear();MLibrary.Order.Clear();rare.Draw();
        int body=MLibrary.Order.IndexOf("body");
        Check(body>=2 && MLibrary.Order.Take(body).Contains("effect-5") &&
            MLibrary.Order.Skip(body+1).Contains("effect-6"),"Highlights remain hidden underneath the body");
        Check(!DXManager.Blending,"Two-pass loot drawing leaked additive blending");
        MLibrary.Calls.Clear();LootEffectRenderer.DrawLight(rare,Color.White);
        var light=MLibrary.Calls.Single();
        Check(light.Frame==7 && light.Size.Width<=134 && light.Size.Height==88 && light.Blend,
            "Quality lighting is missing or grew into an unbounded light source");
        MLibrary.Calls.Clear();LootEffectRenderer.DrawLight(rare,Color.FromArgb(20,20,20));
        Check(MLibrary.Calls.Single().Colour.R==20,"Loot light bypasses blindness attenuation");
        MLibrary.Calls.Clear();LootEffectRenderer.DrawLight(rare,Color.Black);
        Check(MLibrary.Calls.Count==0,"Black light tint still submits invisible geometry");
        Settings.Effect=false;MLibrary.Calls.Clear();
        LootEffectRenderer.Draw(rare);LootEffectRenderer.DrawHighlights(rare);LootEffectRenderer.DrawLight(rare,Color.White);
        Check(MLibrary.Calls.Count==0,"Effects-off left highlights or light-mask illumination");
        Settings.Effect=true;MLibrary.Present=false;
        LootEffectRenderer.Draw(rare);LootEffectRenderer.DrawHighlights(rare);LootEffectRenderer.DrawLight(rare,Color.White);
        Check(MLibrary.Calls.Count==0,"Missing atlas still submits quality effects");
        MLibrary.Present=true;
        DXManager.SetBlend(true,.4F,BlendMode.INVLIGHT);MLibrary.ThrowOnDraw=true;
        foreach(var draw in new Action[]{()=>LootEffectRenderer.DrawHighlights(rare),()=>LootEffectRenderer.DrawLight(rare,Color.White)})
        {
            try {draw();throw new Exception("Missing simulated graphics failure");}catch(InvalidOperationException) { }
            Check(DXManager.Blending && DXManager.BlendingRate==.4F && DXManager.BlendingMode==BlendMode.INVLIGHT,
                "New loot pass failed to restore blend state after an exception");
        }
        MLibrary.ThrowOnDraw=false;DXManager.SetBlend(false);

        // Saturate the screen with persistent rare drops, then drop ordinary loot.
        // Four arrival slots must survive without exceeding the shared 24/8 budget.
        CMain.Time=30000;objects.Clear();budget.Clear();
        for(uint i=0;i<40;i++)objects.Add(Item(21000+i,ItemGrade.Heroic,new((int)i+1,0)));
        var arrivals=new List<ItemObject>();
        for(uint i=0;i<6;i++)
        {
            var item=new ItemObject(22000+i);
            item.Load(new ServerPackets.ObjectItem{Location=new((int)i+1,2),DropOrigin=new(0,2),DropAge=0,grade=ItemGrade.Common});
            arrivals.Add(item);objects.Add(item);
        }
        CMain.Time=30200;foreach(var item in arrivals)item.Process();
        budget.Update(objects,Point.Empty,new(2000,900),CMain.Time,true);
        Check(arrivals.All(i=>i.LootEffectDetail==0),"Arrival burst played before ground contact");
        CMain.Time=30600;foreach(var item in arrivals)item.Process();
        budget.Update(objects,Point.Empty,new(2000,900),CMain.Time,true);
        var selected=objects.Cast<ItemObject>().Where(i=>i.LootEffectDetail>0).ToArray();
        Check(arrivals.Count(i=>i.LootEffectDetail>0)==LootEffectBudget.ReservedArrivals,
            "Persistent rare drops starved every new ordinary landing");
        Check(selected.Length==24 && selected.Count(i=>i.LootEffectDetail==2)==8,
            "Reserved arrivals exceeded the total/detail limits");
        MLibrary.Calls.Clear();
        foreach(var item in arrivals.Where(i=>i.LootEffectDetail>0))LootEffectRenderer.Draw(item);
        Check(MLibrary.Calls.Any(c=>c.Frame==5 && c.Colour.A>=120),"Ordinary impact is still imperceptibly faint");
        Check(MLibrary.Calls.All(c=>(c.Colour.ToArgb()&0xFFFFFF)==(ItemQualityPresentation.Colour(ItemGrade.Common).ToArgb()&0xFFFFFF)),
            "Common landing was falsely promoted to a quality colour");
        MLibrary.Calls.Clear();
        foreach(var item in selected)
        {LootEffectRenderer.Draw(item);LootEffectRenderer.DrawHighlights(item);LootEffectRenderer.DrawLight(item,Color.White);}
        Check(MLibrary.Calls.Count<=144,"Enhanced loot exceeded the hard draw-call envelope");
        // Same-cell quality wins even when a fresh white item has a reserved slot.
        var stacked=Item(23000,ItemGrade.Heroic,arrivals[0].CurrentLocation);objects.Add(stacked);
        CMain.Time+=33;budget.Update(objects,Point.Empty,new(2000,900),CMain.Time,true);
        Check(stacked.LootEffectDetail>0 && arrivals[0].LootEffectDetail==0,"Fresh common item hid a stacked rare effect");
        CMain.Time=33000;foreach(var item in arrivals)item.Process();
        budget.Update(objects,Point.Empty,new(2000,900),CMain.Time,true);
        Check(arrivals.All(i=>i.LootEffectDetail==0),"Ordinary landing turned into permanent magical effects");
        foreach(var item in arrivals)
        {
            item.LootEffectDetail=2;item.LootEffectSample=LootEffectTimeline.At(CMain.Time,item.LootAppearedAt,item.ObjectID);
            MLibrary.Calls.Clear();LootEffectRenderer.Draw(item);LootEffectRenderer.DrawHighlights(item);LootEffectRenderer.DrawLight(item,Color.White);
            Check(MLibrary.Calls.Count==0,"White quality retained idle magic or illumination");
        }
        budget.Clear();
    }

    private static void CheckDenseSceneWork()
    {
        var map=Client.MirScenes.GameScene.Scene.MapControl;
        var savedSize=map.Size;map.Size=new(1280,720);
        var objects=Client.MirScenes.MapControl.ObjectsList;objects.Clear();
        Client.MirScenes.MapControl.MapLocation=new(-999,-999);
        Settings.DropView=true;CMain.Time=40000;
        for(uint i=0;i<1024;i++)
        {
            Point cell=i<64 ? new(1+(int)(i%8)*3,2+(int)(i/8)*2) : new(2,18);
            var item=Item(40000+i,i<64 ? ItemGrade.Heroic : ItemGrade.Common,cell);
            item.Name="Unique loot "+i;item.Process();objects.Add(item);
        }
        Client.MirControls.WorldLabelCache.Requests=0;Client.MirControls.MirLabel.Drawn.Clear();
        map.DrawLootNamesForTest();
        var drawn=Client.MirControls.MirLabel.Drawn;
        Check(drawn.Count==64 && Client.MirControls.WorldLabelCache.Requests==64,
            "Full name budget still resolves hidden labels or resolves visible labels twice");
        Check(drawn.All(d=>d.Text.StartsWith("Unique loot ")) &&
            drawn.SelectMany((a,i)=>drawn.Skip(i+1).Select(b=>!a.Bounds.IntersectsWith(b.Bounds))).All(x=>x),
            "Optimized name pass lost names or overlap avoidance");
        var hovered=Item(50000,ItemGrade.Common,new(24,18));hovered.Name="Hovered low grade";hovered.Process();objects.Add(hovered);
        Client.MirScenes.MapControl.MapLocation=hovered.CurrentLocation;
        Client.MirControls.MirLabel.Drawn.Clear();map.DrawLootNamesForTest();
        Check(Client.MirControls.MirLabel.Drawn[0].Text==hovered.Name && Client.MirControls.MirLabel.Drawn.Count==64,
            "Early label stop suppressed the hovered low-grade name");
        objects.Clear();Settings.DropView=false;Client.MirScenes.MapControl.MapLocation=new(-999,-999);
        Client.MirControls.WorldLabelCache.Requests=0;map.DrawLootNamesForTest();
        Check(Client.MirControls.WorldLabelCache.Requests==0,"Empty scene continues to resolve names");
        map.Size=savedSize;

        // Execute the real cell drawing pass, not a copy of its comparator.
        CMain.Time=50000;
        var stack=new CellInfo();var stackItems=new List<RecordingItem>();
        foreach(var pair in new[]{(200U,ItemGrade.Common),(100U,ItemGrade.Common),(30U,ItemGrade.Rare),
            (20U,ItemGrade.Legendary),(12U,ItemGrade.Heroic),(10U,ItemGrade.Heroic)})
        {
            var item=new RecordingItem(pair.Item1);item.Load(new ServerPackets.ObjectItem{Location=new(2,2),DropAge=10000,grade=pair.Item2});
            item.Process();stackItems.Add(item);
        }
        var actor=new RecordingActor(999);var random=new Random(610);
        for(int trial=0;trial<50;trial++)
        {
            stack=new CellInfo();foreach(var item in stackItems.OrderBy(_=>random.Next()))stack.AddObject(item);stack.AddObject(actor);
            drawOrder.Clear();stack.DrawObjects();
            Check(drawOrder.SequenceEqual(new uint[]{200,100,30,20,12,10,999}),
                "Same-cell quality order is unstable, or items cover an actor");
            var budget=new LootEffectBudget();budget.Update(stack.CellObjects,Point.Empty,new(800,600),CMain.Time,true);
            Check(stackItems.Single(i=>i.LootEffectDetail>0).ObjectID==10,
                "Top physical item differs from the quality-effect representative");
            budget.Clear();
        }
        stack.RemoveObject(stackItems.Single(i=>i.ObjectID==10));drawOrder.Clear();stack.DrawObjects();
        Check(drawOrder.SequenceEqual(new uint[]{200,100,30,20,12,999}),"Removing top loot left a stale physical stack entry");

        int offscreenInvalidations=0;
        for(uint i=0;i<256;i++)
        {
            CMain.Time=60000;var item=new ItemObject(60000+i);
            item.Load(new ServerPackets.ObjectItem{Location=new(1000+(int)i,1000),DropOrigin=new(999+(int)i,1000),DropAge=0});
            foreach(int age in new[]{0,200,500,800})
            {
                CMain.Time=60000+age;map.TextureValid=true;item.Process();
                if(!map.TextureValid)offscreenInvalidations++;
            }
        }
        Check(offscreenInvalidations==0,"Wholly offscreen flight still invalidates the visible map texture");
        CMain.Time=70000;var edge=new ItemObject(70000);
        edge.Load(new ServerPackets.ObjectItem{Location=new(-1,4),DropOrigin=new(-1,4),DropAge=0});
        CMain.Time+=200;map.TextureValid=true;edge.Process();
        Check(!map.TextureValid,"Screen-edge shadow margin was culled too aggressively");
        CMain.Time=71000;var departing=new ItemObject(71000);
        departing.Load(new ServerPackets.ObjectItem{Location=new(5,5),DropOrigin=new(4,5),DropAge=0});
        CMain.Time+=200;departing.Process();
        MapObject.User.Movement=new(-100,-100);map.TextureValid=true;departing.Process();
        Check(!map.TextureValid,"Leaving the viewport failed to erase the last visible flight footprint");
        CMain.Time++;map.TextureValid=true;departing.Process();
        Check(map.TextureValid,"Fully departed flight keeps invalidating the viewport");
        MapObject.User.Movement=Point.Empty;map.TextureValid=true;departing.Process();
        Check(!map.TextureValid,"Re-entering the viewport did not draw the moving item");
        CMain.Time=73000;map.TextureValid=true;departing.Process();
        Check(!map.TextureValid && departing.LootHeight==0,"Final visible landing was lost by culling");
        var unselected=Item(75000,ItemGrade.Heroic,new(2,2));unselected.LootEffectDetail=0;
        MLibrary.SizeRequests=0;MLibrary.Calls.Clear();
        for(int i=0;i<1024;i++)
        {LootEffectRenderer.Draw(unselected);LootEffectRenderer.DrawHighlights(unselected);LootEffectRenderer.DrawLight(unselected,Color.White);}
        Check(MLibrary.SizeRequests==0 && MLibrary.Calls.Count==0,"Unselected loot still probes the effect atlas");
        var report=new {scene_loot=1024,visible_labels=64,label_requests=64,legacy_equivalent_label_requests=1088,
            offscreen_flights=256,flight_samples=1024,offscreen_cache_invalidations=offscreenInvalidations,
            unselected_effect_attempts=3072,unselected_atlas_lookups=MLibrary.SizeRequests,gameplay_executed=false,fps_measured=false};
        Console.WriteLine("LOOT_WORK_CHECK: "+System.Text.Json.JsonSerializer.Serialize(report));
    }
    private static readonly List<uint> drawOrder=new();
    private sealed class RecordingItem : ItemObject
    {
        internal RecordingItem(uint id):base(id) { }
        public override void Draw(){drawOrder.Add(ObjectID);base.Draw();}
    }
    private sealed class RecordingActor : MapObject
    {
        internal RecordingActor(uint id):base(id) { }
        public override ObjectType Race=>ObjectType.Player;
        public override void Draw()=>drawOrder.Add(ObjectID);
    }

    private static void CaptureFrames()
    {
        string path=Environment.GetEnvironmentVariable("LOOT_RENDER_CAPTURE");
        if(string.IsNullOrEmpty(path))return;
        var snapshots=new List<object>();
        Settings.Effect=true;DXManager.SetBlend(false);
        for(int t=0;t<=3600;t+=50)
        {
            for(int grade=1;grade<=5;grade++)
            {
                CMain.Time=0;
                var item=new ItemObject((uint)(30000+grade));
                item.Load(new ServerPackets.ObjectItem{Location=new(2,2),DropOrigin=new(1,2),DropAge=0,grade=(ItemGrade)grade});
                CMain.Time=t;item.Process();
                var budget=new LootEffectBudget();budget.Update(new List<MapObject>{item},Point.Empty,new(800,600),CMain.Time,true);
                MLibrary.Calls.Clear();LootEffectRenderer.DrawShadow(item);LootEffectRenderer.Draw(item);
                var ground=MLibrary.Calls.ToArray();MLibrary.Calls.Clear();LootEffectRenderer.DrawHighlights(item);
                var highlights=MLibrary.Calls.ToArray();MLibrary.Calls.Clear();LootEffectRenderer.DrawLight(item,Color.White);
                object[] Pack(IEnumerable<MLibrary.DrawCall> calls)=>calls.Select(c=>(object)new object[]{c.Frame,c.At.X,c.At.Y,c.Size.Width,c.Size.Height,c.Colour.R,c.Colour.G,c.Colour.B,c.Colour.A,c.Blend}).ToArray();
                snapshots.Add(new{time=t,grade,body=new[]{item.DisplayRectangle.X,item.DisplayRectangle.Y,item.Size.Width,item.Size.Height},
                    ground=Pack(ground),highlights=Pack(highlights),light=Pack(MLibrary.Calls)});
                budget.Clear();
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(snapshots));
        Console.WriteLine($"Captured {snapshots.Count} production-renderer samples for offline pixel review (not Direct3D).");
    }
}
