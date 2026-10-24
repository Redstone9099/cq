using System.Collections.Generic;
using System.Drawing;

namespace Client
{
    // Bounded label packing. Hidden labels remain available by hovering a cell.
    internal sealed class LootLabelLayout
    {
        internal const int MaximumLabels=64;
        private readonly List<Rectangle> placed=new(MaximumLabels);
        private Rectangle viewport;
        internal void Begin(Rectangle viewport) { this.viewport=viewport; placed.Clear(); }
        internal bool TryPlace(Point anchor,Size size,bool hovered,out Point location)
        {
            location=Point.Empty;
            if(size.Width<=0 || size.Height<=0 || size.Width>viewport.Width || size.Height>viewport.Height || placed.Count>=MaximumLabels)return false;
            int rows=hovered?16:3;
            for(int row=0;row<rows;row++)
            {
                int x=Math.Clamp(anchor.X-size.Width/2,viewport.Left,viewport.Right-size.Width);
                int y=anchor.Y-14-size.Height-row*(size.Height+3);
                if(y<viewport.Top) { if(row==0)y=viewport.Top;else break; }
                var rect=new Rectangle(x,y,size.Width,size.Height);
                if(!viewport.Contains(rect))continue;
                var padded=Rectangle.Inflate(rect,3,2);
                bool overlap=false;
                foreach(var other in placed)if(padded.IntersectsWith(other)){overlap=true;break;}
                if(overlap)continue;
                placed.Add(rect);location=rect.Location;return true;
            }
            return false;
        }
    }
}
