using Client.MirGraphics;
using Client.MirControls;
using Client.MirScenes;
using S = ServerPackets;


namespace Client.MirObjects
{
    public class ItemObject : MapObject
    {
        public override ObjectType Race{
            get { return ObjectType.Item; }
        }

        public override bool Blocking
        {
            get { return false; }
        }

        public Size Size;
        public ItemGrade Grade { get; private set; }
        internal long LootAppearedAt;
        internal int LootEffectDetail;
        internal LootEffectTimeline.Sample LootEffectSample;
        internal Point LootEffectAnchor;
        internal float LootHeight;
        private Rectangle sourceBounds;
        private Point dropOrigin;
        private long dropStartedAt;
        private bool wasMoving;
        internal bool LootInArrival => CMain.Time >= LootAppearedAt &&
            CMain.Time < LootAppearedAt + LootEffectTimeline.ArrivalMilliseconds;

        private void PrepareAppearance(Point origin,int age)
        {
            dropOrigin=origin;
            dropStartedAt=CMain.Time-Math.Clamp(age,0,10000);
            LootAppearedAt=dropStartedAt+LootPresentation.FlightMilliseconds(ObjectID);
            sourceBounds=BodyLibrary.GetVisibleBounds(DrawFrame);
            Size=LootPresentation.Fit(sourceBounds.Size);
        }


        public ItemObject(uint objectID) : base(objectID)
        {
        }


        public void Load(S.ObjectItem info)
        {
            Grade = ItemQualityPresentation.Normalize(info.grade);
            LootAppearedAt = CMain.Time;
            LootEffectDetail = 0;
            Name = info.Name;
            NameColour = ItemQualityPresentation.Colour(Grade);

            BodyLibrary = Libraries.FloorItems;

            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);
            DrawFrame = info.Image;

            PrepareAppearance(info.DropOrigin,info.DropAge);

            DrawY = CurrentLocation.Y;
            
        }
        public void Load(S.ObjectGold info)
        {
            Grade = ItemGrade.None;
            NameColour = ItemQualityPresentation.Colour(Grade);
            LootEffectDetail = 0;
            Name = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.GoldAmount), info.Gold);


            BodyLibrary = Libraries.FloorItems;

            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);

            if (info.Gold < 100)
                DrawFrame = 112;
            else if (info.Gold < 200)
                DrawFrame = 113;
            else if (info.Gold < 500)
                DrawFrame = 114;
            else if (info.Gold < 1000)
                DrawFrame = 115;
            else
                DrawFrame = 116;

            PrepareAppearance(info.DropOrigin,info.DropAge);

            DrawY = CurrentLocation.Y;
        }
        public override void Draw()
        {
            // Same depth pass as the physical item: later map rows and actors occlude it.
            LootEffectRenderer.DrawShadow(this);
            LootEffectRenderer.Draw(this);
            if (BodyLibrary != null)
                BodyLibrary.DrawSectionScaled(DrawFrame,sourceBounds,DisplayRectangle,DrawColour);
            LootEffectRenderer.DrawHighlights(this);
        }

        public override void Process()
        {
            var previousRectangle=DisplayRectangle;
            var cell = new Point((CurrentLocation.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                (CurrentLocation.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            cell.Offset(User.OffSetMove);
            cell.Offset(GlobalDisplayLocationOffset);
            var scatter=LootPresentation.Scatter(ObjectID,Size);
            var motion=LootPresentation.At(CMain.Time-dropStartedAt,ObjectID,dropOrigin,CurrentLocation);
            LootHeight=motion.Height;
            LootEffectAnchor=new Point(cell.X+24+scatter.X+(int)MathF.Round(motion.Offset.X),
                cell.Y+20+scatter.Y+(int)MathF.Round(motion.Offset.Y));
            DrawLocation=new Point(LootEffectAnchor.X-Size.Width/2,
                LootEffectAnchor.Y-Size.Height+4-(int)MathF.Round(motion.Height));
            FinalDrawLocation=DrawLocation;
            DisplayRectangle=new Rectangle(DrawLocation,Size);
            if(motion.Moving || wasMoving)
            {
                var map=GameScene.Scene.MapControl;
                var viewport=new Rectangle(Point.Empty,map.Size);
                // Retain the previous footprint to erase a departing/final
                // airborne sprite. Include the shadow margin at screen edges.
                bool visible=viewport.IntersectsWith(Rectangle.Inflate(DisplayRectangle,24,16));
                bool wasVisible=!previousRectangle.IsEmpty &&
                    viewport.IntersectsWith(Rectangle.Inflate(previousRectangle,24,16));
                if(visible || wasVisible)map.TextureValid=false;
            }
            wasMoving=motion.Moving;
        }

        public override bool MouseOver(Point p)
        {
            return MapControl.MapLocation == CurrentLocation;
            // return DisplayRectangle.Contains(p);
        }

        public override void DrawName()
        {
            CreateLabel(Color.Transparent, false, true);

            if (NameLabel == null) return;
            NameLabel.Location = new Point(
                DisplayRectangle.X + (DisplayRectangle.Width - NameLabel.Size.Width) / 2,
                DisplayRectangle.Y + (DisplayRectangle.Height - NameLabel.Size.Height) / 2 - 20);
            NameLabel.Draw();
        }

        public override void DrawBehindEffects(bool effectsEnabled)
        {
        }

        public override void DrawEffects(bool effectsEnabled)
        {
            

        }

        public void DrawName(int y)
        {
            CreateLabel(Color.FromArgb(100, 0, 24, 48), true, false);

            NameLabel.Location = new Point(
                DisplayRectangle.X + (DisplayRectangle.Width - NameLabel.Size.Width) / 2,
                DisplayRectangle.Y + y + (DisplayRectangle.Height - NameLabel.Size.Height) / 2 - 20);
            NameLabel.Draw();
        }

        internal Size LootNameSize(bool hovered)
        {
            CreateLabel(hovered ? Color.FromArgb(155,8,13,18) : Color.Transparent,hovered,!hovered);
            return NameLabel.Size;
        }
        internal void DrawLootName(Point location)
        {
            // The packing pass has just resolved this exact label and size.
            NameLabel.Location=location;
            NameLabel.Draw();
        }

        private void CreateLabel(Color backColour, bool border, bool outline)
        {
            NameLabel = WorldLabelCache.Get(Name, NameColour, backColour, border, outline);
        }


    }
}
