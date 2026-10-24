using Client.MirObjects;

namespace Client.MirGraphics
{
    internal static class LootEffectRenderer
    {
        // Only the neutral ring / star / soft spot are used. The old vertical
        // beam pictures (frames 0-4) deliberately have no world render path.
        private static readonly MLibrary Atlas=new(Settings.DataPath+@"UI\LootEffectsR65");
        internal static bool Available=>Atlas.GetSize(7)==new Size(32,32);
        private static void Sprite(int frame,Point anchor,float width,float height,
            Color colour,float opacity)
        {
            if(opacity<=0 || width<=0 || height<=0)return;
            int alpha=(int)(255*Math.Clamp(opacity,0,1));
            if(alpha==0)return;
            var size=new Size(Math.Max(1,(int)MathF.Round(width)),Math.Max(1,(int)MathF.Round(height)));
            var at=new Point(anchor.X-size.Width/2,anchor.Y-size.Height/2);
            Atlas.DrawScaled(frame,at,size,Color.FromArgb(alpha,colour),1,false);
        }
        internal static void DrawShadow(ItemObject item)
        {
            if(item.Size.IsEmpty || !Available)return;
            bool oldBlend=DXManager.Blending;
            float oldRate=DXManager.BlendingRate;
            BlendMode oldMode=DXManager.BlendingMode;
            DXManager.SetBlend(false);
            try
            {
                float height=Math.Clamp(item.LootHeight/40F,0,1);
                Sprite(7,item.LootEffectAnchor,Math.Clamp(item.Size.Width+10,16,48)*(1-.2F*height),
                    Math.Clamp(item.Size.Height*.35F+5,7,14),Color.Black,.48F-.24F*height);
            }
            finally {DXManager.SetBlend(oldBlend,oldRate,oldMode);}
        }
        internal static void Draw(ItemObject item)
        {
            int grade=(int)ItemQualityPresentation.Normalize(item.Grade);
            if(!Settings.Effect || item.LootEffectDetail==0 || item.Size.IsEmpty || item.LootHeight>6)return;
            var s=item.LootEffectSample;
            Point anchor=item.LootEffectAnchor;
            Color colour=ItemQualityPresentation.Colour(item.Grade);
            float width=Math.Clamp(item.Size.Width+12,22,52);
            float depth=Math.Clamp(item.Size.Height*.4F+8,10,18);
            bool oldBlend=DXManager.Blending;
            float oldRate=DXManager.BlendingRate;
            BlendMode oldMode=DXManager.BlendingMode;
            DXManager.SetBlend(true,1F);
            try
            {
                // A brief, flat impact expands from the actual item's footpoint.
                Sprite(5,anchor,width*s.RingScale,depth*s.RingScale,colour,s.RingOpacity*.42F);
                if(grade<2) return; // white/common drops do not emit idle magic
                Sprite(7,anchor,width,depth,colour,s.Opacity*(.12F+.025F*grade));
                if(item.LootEffectDetail<2)return;
                int count=grade>=4?2:1;
                for(int i=0;i<count;i++)
                {
                    float phase=(s.Phase+i/(float)count)%1;
                    float angle=(phase+i*.37F)*MathF.Tau;
                    var at=new Point(anchor.X+(int)(MathF.Cos(angle)*width*.25F),
                        anchor.Y-(int)(phase*(6+grade*2))+(int)(MathF.Sin(angle)*2));
                    float alpha=MathF.Pow(MathF.Sin(phase*MathF.PI),3)*s.Opacity*.45F;
                    Sprite(6,at,3+grade*.3F,3+grade*.5F,colour,alpha);
                }
            }
            finally {DXManager.SetBlend(oldBlend,oldRate,oldMode);}
        }
    }
}
