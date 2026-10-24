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
        CMain.Time=2000;live.Process();var final=live.DisplayRectangle;
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
            Check(MLibrary.Calls.All(c=>c.Frame>=5 && c.Size.Height<=18),"Tall vertical decal re-entered ground pass");
            Check(grade>=ItemGrade.Rare || MLibrary.Calls.Count==0,"White quality received permanent magical effects");
            Check(MLibrary.Calls.All(c=>(c.Colour.ToArgb()&0xFFFFFF)==(item.NameColour.ToArgb()&0xFFFFFF)),"Effect colour differs from item name");
        }
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
        Console.WriteLine($"Loot effect regression: {checks} assertions; production timeline, budget and renderer with graphics adapters.");
    }
}
