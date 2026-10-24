using System.Collections.Generic;
using System.Drawing;
using Client.MirGraphics;
using Client.MirObjects;

namespace Client
{
    internal sealed class LootEffectBudget : IComparer<ItemObject>
    {
        internal const int MaximumEffects=24, MaximumDetailedEffects=8;
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
                if(objects[i] is not ItemObject item || item.Size.IsEmpty || ((int)item.Grade<2 && !item.LootInArrival) || (int)item.Grade>5)continue;
                Point p=item.LootEffectAnchor;
                // Ground footprints and short motes; no offscreen 192px beam budget.
                if(!screen.IntersectsWith(new Rectangle(p.X-42,p.Y-32,84,56)))continue;
                candidates.Add(item);
            }
            if(candidates.Count==0 || !LootEffectRenderer.Available)return dirty;
            candidates.Sort(this);
            foreach(var item in candidates)
            {
                if(!cells.Add(item.CurrentLocation))continue;
                item.LootEffectDetail=selected.Count<MaximumDetailedEffects?2:1;
                item.LootEffectSample=LootEffectTimeline.At(now,item.LootAppearedAt,item.ObjectID);
                selected.Add(item);
                if(selected.Count==MaximumEffects)break;
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
