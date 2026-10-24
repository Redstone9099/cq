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
            if(!CanDraw(item))return;
            var s=item.LootEffectSample;
            Point anchor=item.LootEffectAnchor;
            Color colour=ItemQualityPresentation.Colour(item.Grade);
            // The near edge must extend past the sprite's foot (+4 pixels).
            // A tiny glow hidden underneath an opaque item is effectively invisible.
            float width=Math.Clamp(item.Size.Width+30,42,72);
            float depth=Math.Clamp(item.Size.Height*.5F+16,22,30);
            bool oldBlend=DXManager.Blending;
            float oldRate=DXManager.BlendingRate;
            BlendMode oldMode=DXManager.BlendingMode;
            DXManager.SetBlend(true,1F);
            try
            {
                // A readable impact starts at first contact, not at monster death.
                float impactWidth=grade<2 ? width*.8F : width;
                Sprite(5,anchor,impactWidth*s.RingScale,depth*s.RingScale,colour,s.RingOpacity*.72F);
                Sprite(7,anchor,impactWidth,depth,colour,s.Flash*.55F);
                if(grade<2) return; // white/common drops do not emit idle magic
                Sprite(7,anchor,width*.86F,depth*.85F,colour,s.Opacity*(.26F+.035F*grade));
                // Even reduced-detail drops retain a visible, gently breathing rim.
                Sprite(5,anchor,width*s.Width,depth*s.Height,colour,s.Opacity*(.42F+.035F*grade));
            }
            finally {DXManager.SetBlend(oldBlend,oldRate,oldMode);}
        }

        private static bool CanDraw(ItemObject item) => Settings.Effect && Available &&
            item.LootEffectDetail>0 && !item.Size.IsEmpty && item.LootHeight<=6;

        // Called only while composing the world's light mask, not as an overlay.
        // A bounded pool keeps emissive rims readable in dark maps while normal
        // world depth still hides items behind walls. Respect the blindness tint.
        internal static void DrawLight(ItemObject item,Color illumination)
        {
            int grade=(int)ItemQualityPresentation.Normalize(item.Grade);
            if(grade<2 || !CanDraw(item) || (illumination.R|illumination.G|illumination.B)==0)return;
            bool oldBlend=DXManager.Blending;
            float oldRate=DXManager.BlendingRate;
            BlendMode oldMode=DXManager.BlendingMode;
            DXManager.SetBlend(true,1F);
            try
            {
                Sprite(7,item.LootEffectAnchor,Math.Clamp(item.Size.Width+94,110,134),88,
                    illumination,item.LootEffectSample.Opacity*(.65F+.025F*grade));
            }
            finally {DXManager.SetBlend(oldBlend,oldRate,oldMode);}
        }

        internal static void DrawHighlights(ItemObject item)
        {
            int grade=(int)ItemQualityPresentation.Normalize(item.Grade);
            if(grade<2 || !CanDraw(item) || item.LootEffectDetail<2)return;
            var s=item.LootEffectSample;
            Point anchor=item.LootEffectAnchor;
            Color colour=ItemQualityPresentation.Colour(item.Grade);
            float radius=Math.Clamp(item.Size.Width*.5F+5,12,25);
            bool oldBlend=DXManager.Blending;
            float oldRate=DXManager.BlendingRate;
            BlendMode oldMode=DXManager.BlendingMode;
            DXManager.SetBlend(true,1F);
            try
            {
                // Draw after the body, within this item's depth pass. Small rim
                // highlights can be seen without painting a beam over the world.
                int count=Math.Min(3,grade-1);
                for(int i=0;i<count;i++)
                {
                    float phase=(s.Phase+i/(float)count)%1;
                    float angle=(phase+i*.37F)*MathF.Tau;
                    var at=new Point(anchor.X+(int)(MathF.Cos(angle)*radius),
                        anchor.Y-(int)(phase*(10+grade*3))+(int)(MathF.Sin(angle)*3));
                    float alpha=MathF.Pow(MathF.Sin(phase*MathF.PI),2)*s.Opacity*(.65F+.04F*grade);
                    Sprite(6,at,5+grade*.4F,5+grade*.4F,colour,alpha);
                }
            }
            finally {DXManager.SetBlend(oldBlend,oldRate,oldMode);}
        }
    }
}
