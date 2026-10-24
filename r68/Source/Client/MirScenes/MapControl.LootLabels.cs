using Client.MirObjects;

namespace Client.MirScenes
{
    public sealed partial class MapControl
    {
        private readonly LootLabelLayout lootLabels=new();
        private readonly List<ItemObject> lootNameCandidates=new();
        private void DrawLootNames()
        {
            bool all=Settings.DropView || GameScene.DropViewTime>CMain.Time;
            lootNameCandidates.Clear();
            var viewport=new Rectangle(4,4,Math.Max(1,Size.Width-8),Math.Max(1,Size.Height-8));
            foreach(var ob in ObjectsList)
            {
                if(ob is not ItemObject item || (!all && !item.MouseOver(MouseLocation)))continue;
                if(!viewport.IntersectsWith(Rectangle.Inflate(item.DisplayRectangle,0,16)))continue;
                lootNameCandidates.Add(item);
            }
            lootNameCandidates.Sort(CompareLootNames);
            lootLabels.Begin(viewport);
            foreach(var item in lootNameCandidates)
            {
                bool hovered=item.MouseOver(MouseLocation);
                var size=item.LootNameSize(hovered);
                if(lootLabels.TryPlace(item.LootEffectAnchor,size,hovered,out var location))
                    item.DrawLootName(location,hovered);
            }
        }
        private int CompareLootNames(ItemObject a,ItemObject b)
        {
            int hover=b.MouseOver(MouseLocation).CompareTo(a.MouseOver(MouseLocation));
            if(hover!=0)return hover;
            int grade=b.Grade.CompareTo(a.Grade);
            if(grade!=0)return grade;
            int ax=a.CurrentLocation.X-User.CurrentLocation.X,ay=a.CurrentLocation.Y-User.CurrentLocation.Y;
            int bx=b.CurrentLocation.X-User.CurrentLocation.X,by=b.CurrentLocation.Y-User.CurrentLocation.Y;
            int distance=(ax*ax+ay*ay).CompareTo(bx*bx+by*by);
            return distance!=0?distance:a.ObjectID.CompareTo(b.ObjectID);
        }
    }
}
