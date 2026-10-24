using System.Collections.Generic;
using System.Drawing;
using Client.MirGraphics;
using Client.MirObjects;

namespace Client
{
    internal sealed class LootEffectBudget : IComparer<ItemObject>
    {
        internal const int MaximumEffects=24, MaximumDetailedEffects=8;
        internal const int ReservedArrivals=4;
        private readonly List<ItemObject> candidates=new();
        private readonly List<ItemObject> selected=new();
        private readonly HashSet<Point> cells=new();
        private long sampleTick=long.MinValue;
        private bool enabled;
        private Point viewer;

        internal void Clear()
        {
            foreach(var item in selected)item.LootEffectDetail=0;
            selected.Clear();candidates.Clear();cells.Clear();sampleTick=long.MinValue;
        }

        internal bool Update(IReadOnlyList<MapObject> objects,Point player,Size viewport,long now,bool effects)
        {
            long tick=now/LootEffectTimeline.SampleMilliseconds;
            if(tick==sampleTick && enabled==effects)return false;
            sampleTick=tick;enabled=effects;viewer=player;
            bool dirty=selected.Count>0;
            foreach(var item in selected)item.LootEffectDetail=0;
            selected.Clear();candidates.Clear();cells.Clear();
            if(!effects)return dirty;
            var screen=new Rectangle(Point.Empty,viewport);
            for(int i=0;i<objects.Count;i++)
            {
                if(objects[i] is not ItemObject item || item.Size.IsEmpty || item.LootHeight>6 ||
                    CMain.Time<item.LootAppearedAt || ((int)item.Grade<2 && !item.LootInArrival) || (int)item.Grade>5)continue;
                Point p=item.LootEffectAnchor;
                // Ground footprints and short motes; no offscreen 192px beam budget.
                if(!screen.IntersectsWith(new Rectangle(p.X-76,p.Y-48,152,96)))continue;
                candidates.Add(item);
            }
            if(candidates.Count==0 || !LootEffectRenderer.Available)return dirty;
            candidates.Sort(this);
            // Collapse a stack to its highest-grade representative before
            // reserving arrivals: a white drop must not suppress a rare rim.
            int unique=0;
            for(int i=0;i<candidates.Count;i++)
                if(cells.Add(candidates[i].CurrentLocation))candidates[unique++]=candidates[i];
            candidates.RemoveRange(unique,candidates.Count-unique);
            cells.Clear();
            foreach(var item in candidates)
            {
                if(!item.LootInArrival)continue;
                cells.Add(item.CurrentLocation);selected.Add(item);
                if(selected.Count==ReservedArrivals)break;
            }
            foreach(var item in candidates)
            {
                if(!cells.Add(item.CurrentLocation))continue;
                selected.Add(item);
                if(selected.Count==MaximumEffects)break;
            }
            selected.Sort(this);
            for(int i=0;i<selected.Count;i++)
            {
                var item=selected[i];
                item.LootEffectDetail=i<MaximumDetailedEffects?2:1;
                item.LootEffectSample=LootEffectTimeline.At(now,item.LootAppearedAt,item.ObjectID);
            }
            return dirty || selected.Count>0;
        }

        public int Compare(ItemObject a,ItemObject b)
        {
            int grade=b.Grade.CompareTo(a.Grade);
            if(grade!=0)return grade;
            int ax=a.CurrentLocation.X-viewer.X,ay=a.CurrentLocation.Y-viewer.Y;
            int bx=b.CurrentLocation.X-viewer.X,by=b.CurrentLocation.Y-viewer.Y;
            int distance=(ax*ax+ay*ay).CompareTo(bx*bx+by*by);
            return distance!=0?distance:a.ObjectID.CompareTo(b.ObjectID);
        }
    }
}
