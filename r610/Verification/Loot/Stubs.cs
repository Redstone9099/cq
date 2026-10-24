global using System.Drawing;
namespace Client
{
    internal static class Settings {internal static string DataPath="";internal static bool Effect=true,DropView;}
    internal static class CMain {internal static long Time;}
}
namespace Client.MirControls
{
    public class MirLabel
    {
        public Size Size=new(100,15);public Point Location;public bool Border;public string Text;
        internal static readonly List<(string Text,Rectangle Bounds)> Drawn=new();
        public void Draw() {Drawn.Add((Text,new Rectangle(Location,Size)));}
    }
    internal static class WorldLabelCache
    {
        internal static int Requests;
        internal static MirLabel Get(string name,Color colour,Color back,bool border,bool outline)
        {Requests++;return new(){Border=border,Text=name};}
    }
}
namespace Client.MirScenes
{
    public sealed partial class MapControl
    {
        public const int CellWidth=48,CellHeight=32;
        public static int OffSetX,OffSetY;
        public static Point MapLocation;
        public static Point MouseLocation;
        public static Client.MirObjects.MapObject User=>Client.MirObjects.MapObject.User;
        public static List<Client.MirObjects.MapObject> ObjectsList=new();
        public Size Size=new(800,600);
        public bool TextureValid=true;
        public void AddObject(Client.MirObjects.MapObject item) { }
        internal void DrawLootNamesForTest()=>DrawLootNames();
    }
    public class GameScene
    {
        public static GameScene Scene=new();
        public static long DropViewTime;
        public MapControl MapControl=new();
    }
}
namespace Client.MirObjects
{
    public class MapObject
    {
        public MapObject(uint id) {ObjectID=id;}
        public static MapObject User=new(0);
        public uint ObjectID;
        public bool Dead;
        public Point CurrentLocation,MapLocation,Movement,OffSetMove,DrawLocation,FinalDrawLocation;
        protected Point GlobalDisplayLocationOffset=>Point.Empty;
        public int DrawFrame,DrawY;
        public string Name;
        public Color NameColour,DrawColour=Color.White;
        public Rectangle DisplayRectangle;
        public Client.MirControls.MirLabel NameLabel;
        public Client.MirGraphics.MLibrary BodyLibrary;
        public virtual ObjectType Race=>ObjectType.Item;
        public virtual bool Blocking=>false;
        public virtual void Draw() { }
        public virtual void Process() { }
        public virtual bool MouseOver(Point p)=>false;
        public virtual void DrawName() { }
        public virtual void DrawBehindEffects(bool effects) { }
        public virtual void DrawEffects(bool effects) { }
    }
    public class MonsterObject : MapObject
    {
        public MonsterObject(uint id):base(id) { }
        public Monster BaseImage;
        public override ObjectType Race=>ObjectType.Monster;
    }
}
namespace Client.MirGraphics
{
    internal static class Libraries {internal static MLibrary FloorItems=new("floor");}
    internal static class DXManager
    {
        internal static bool Blending;
        internal static float BlendingRate=1;
        internal static BlendMode BlendingMode;
        internal static int Changes;
        internal static void SetBlend(bool value,float rate=1,BlendMode mode=BlendMode.NORMAL)
        {Changes++;Blending=value;BlendingRate=rate;BlendingMode=mode;}
    }
    public class MLibrary
    {
        internal readonly record struct DrawCall(int Frame,Point At,Size Size,Color Colour,float Opacity,bool Blend);
        internal static readonly List<DrawCall> Calls=new();
        internal static readonly List<string> Order=new();
        internal static readonly List<(Rectangle Source,Rectangle Destination)> FloorCalls=new();
        internal static bool Present=true;
        internal static int SizeRequests;
        internal static bool ThrowOnDraw;
        internal static Rectangle VisibleBounds=new(19,13,64,48);
        internal MLibrary(string path) { }
        internal Size GetSize(int frame)
        {SizeRequests++;return !Present ? Size.Empty : frame switch
        {5=>new(96,32),6=>new(16,16),7=>new(32,32),_=>new(96,192)};}
        public Rectangle GetVisibleBounds(int frame)=>VisibleBounds;
        public void DrawSectionScaled(int frame,Rectangle source,Rectangle destination,Color colour,float opacity=1)
        {FloorCalls.Add((source,destination));Order.Add("body");}
        internal void DrawScaled(int f,Point p,Size s,Color c,float opacity,bool blend)
        {
            if(ThrowOnDraw)throw new InvalidOperationException("Simulated graphics failure");
            Calls.Add(new(f,p,s,c,opacity,DXManager.Blending));
            Order.Add("effect-"+f);
        }
    }
}
