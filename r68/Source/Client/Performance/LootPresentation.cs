using System;
using System.Drawing;

namespace Client
{
    internal static class LootPresentation
    {
        internal readonly record struct Motion(PointF Offset, float Height, bool Moving);
        internal static uint Seed(uint id) => unchecked(id*2654435761U);
        internal static int FlightMilliseconds(uint id) => 420+(int)(Seed(id)%121);
        internal static int Duration(uint id) => FlightMilliseconds(id)+150;
        internal static Size Fit(Size visible)
        {
            if (visible.Width<=0 || visible.Height<=0) return Size.Empty;
            float scale=Math.Min(1F,Math.Min(40F/visible.Width,26F/visible.Height));
            return new(Math.Max(1,(int)MathF.Round(visible.Width*scale)),Math.Max(1,(int)MathF.Round(visible.Height*scale)));
        }
        internal static Point Scatter(uint id, Size size)
        {
            uint seed=Seed(id);
            int spread=Math.Min(4,Math.Max(0,(48-size.Width)/2-1));
            return new((int)(seed%(uint)(spread*2+1))-spread,(int)((seed>>8)%5)-2);
        }
        internal static Motion At(long elapsed,uint id,Point from,Point to)
        {
            int flight=FlightMilliseconds(id);
            // Legacy snapshots or distant manual placement settle immediately.
            if (elapsed>=Duration(id) || Math.Abs((long)from.X-to.X)>8 || Math.Abs((long)from.Y-to.Y)>8)
                return new(PointF.Empty,0,false);
            float t=Math.Clamp(elapsed/(float)flight,0,1);
            float remaining=1-t;
            var offset=new PointF((from.X-to.X)*48*remaining,(from.Y-to.Y)*32*remaining);
            float height=elapsed<flight ? (24+Seed(id)%17)*4*t*(1-t)
                : 5*MathF.Sin(Math.Clamp((elapsed-flight)/150F,0,1)*MathF.PI);
            return new(offset,height,true);
        }
    }
}
