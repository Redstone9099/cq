using System;

namespace Client
{
    // Absolute presentation time only. Sampling never changes drop/pickup rules.
    internal static class LootEffectTimeline
    {
        internal const int SampleMilliseconds = 33;
        internal const int ArrivalMilliseconds = 1000;
        internal const int LoopMilliseconds = 2400;
        internal readonly record struct Keyframe(int Time, float Width, float Height,
            float Opacity, float RingScale, float RingOpacity, float Flash);
        internal readonly record struct Sample(float Width, float Height, float Opacity,
            float RingScale, float RingOpacity, float Flash, float Phase);

        // Authoring source; the offline preview reads these exact keyframes.
        internal static readonly Keyframe[] Arrival =
        {
            new(0,    1F, 1F, 0.00F, 0.35F, 0.00F, 0.00F),
            new(80,   1F, 1F, 0.85F, 0.65F, 0.75F, 0.30F),
            new(180,  1F, 1F, 0.90F, 1.00F, 0.42F, 0.00F),
            new(340,  1F, 1F, 0.82F, 1.30F, 0.12F, 0.00F),
            new(600,  1F, 1F, 0.82F, 1.50F, 0.00F, 0.00F),
            new(1000, 1F, 1F, 0.82F, 1.50F, 0.00F, 0.00F),
        };

        private static float Ease(float t) { t=Math.Clamp(t,0,1);return t*t*(3-2*t); }
        private static float Mix(float a,float b,float t)=>a+(b-a)*t;

        internal static Sample At(long now,long appeared,uint id)
        {
            long elapsed=Math.Max(0,now-appeared);
            uint seed=unchecked(id*2654435761U);
            float phase=((elapsed%LoopMilliseconds)+(seed%LoopMilliseconds))%LoopMilliseconds/(float)LoopMilliseconds;
            if(elapsed<ArrivalMilliseconds)
            {
                for(int i=1;i<Arrival.Length;i++)
                {
                    var a=Arrival[i-1];var b=Arrival[i];
                    if(elapsed>b.Time)continue;
                    float t=Ease((elapsed-a.Time)/(float)(b.Time-a.Time));
                    return new(Mix(a.Width,b.Width,t),Mix(a.Height,b.Height,t),
                        Mix(a.Opacity,b.Opacity,t),Mix(a.RingScale,b.RingScale,t),
                        Mix(a.RingOpacity,b.RingOpacity,t),Mix(a.Flash,b.Flash,t),phase);
                }
            }
            float settle=Ease((elapsed-ArrivalMilliseconds)/400F);
            float wave=(1-MathF.Cos(phase*MathF.Tau))*0.5F;
            return new(Mix(1,0.97F+0.06F*wave,settle),Mix(1,0.98F+0.05F*wave,settle),
                Mix(0.82F,0.72F+0.18F*wave,settle),Mix(1.60F,0.85F+0.35F*wave,settle),
                0,0,phase);
        }
    }
}
