namespace Client.MirObjects
{
    public class CellInfo
    {
        public short BackIndex;
        public int BackImage;
        public short MiddleIndex;
        public int MiddleImage;
        public short FrontIndex;
        public int FrontImage;

        public byte DoorIndex;
        public byte DoorOffset;

        public byte FrontAnimationFrame;
        public byte FrontAnimationTick;

        public byte MiddleAnimationFrame;
        public byte MiddleAnimationTick;

        public short TileAnimationImage;
        public short TileAnimationOffset;
        public byte  TileAnimationFrames;

        public byte Light;
        public byte Unknown;
        public List<MapObject> CellObjects;

        public bool FishingCell;

        public void AddObject(MapObject ob)
        {
            if (CellObjects == null) CellObjects = new List<MapObject>();

            CellObjects.Insert(0, ob);
            Sort();
        }
        public void RemoveObject(MapObject ob)
        {
            if (CellObjects == null) return;

            CellObjects.Remove(ob);

            if (CellObjects.Count == 0) CellObjects = null;
            else Sort();
        }
        public MapObject FindObject(uint ObjectID)
        {
            return CellObjects.Find(
                delegate(MapObject mo)
            {
                return mo.ObjectID == ObjectID;
            });
        }
        public void DrawObjects()
        {
            if (CellObjects == null) return;

            for (int i = 0; i < CellObjects.Count; i++)
            {
                if (!CellObjects[i].Dead)
                {
                    CellObjects[i].Draw();
                    continue;
                }

                if(CellObjects[i].Race == ObjectType.Monster)
                {
                    switch(((MonsterObject)CellObjects[i]).BaseImage)
                    {
                        case Monster.PalaceWallLeft:
                        case Monster.PalaceWall1:
                        case Monster.PalaceWall2:
                        case Monster.SSabukWall1:
                        case Monster.SSabukWall2:
                        case Monster.SSabukWall3:
                        case Monster.HellLord:
                            CellObjects[i].Draw();
                            break;
                        default:
                            continue;
                    }
                }
            }
        }

        public void DrawDeadObjects()
        {
            if (CellObjects == null) return;
            for (int i = 0; i < CellObjects.Count; i++)
            {
                if (!CellObjects[i].Dead) continue;

                if (CellObjects[i].Race == ObjectType.Monster)
                {
                    switch (((MonsterObject)CellObjects[i]).BaseImage)
                    {
                        case Monster.PalaceWallLeft:
                        case Monster.PalaceWall1:
                        case Monster.PalaceWall2:
                        case Monster.SSabukWall1:
                        case Monster.SSabukWall2:
                        case Monster.SSabukWall3:
                        case Monster.HellLord:
                            continue;
                    }
                }

                CellObjects[i].Draw();
            }
        }

        public void Sort()
        {
            CellObjects.Sort(delegate(MapObject ob1, MapObject ob2)
            {
                if (ob1.Race == ObjectType.Item && ob2.Race != ObjectType.Item)
                    return -1;
                if (ob2.Race == ObjectType.Item && ob1.Race != ObjectType.Item)
                    return 1;
                if(ob1 is ItemObject a && ob2 is ItemObject b)
                {
                    // Low quality first; the effect budget's representative
                    // (highest grade, then lowest ID) is the last item drawn.
                    int grade=a.Grade.CompareTo(b.Grade);
                    return grade!=0 ? grade : b.ObjectID.CompareTo(a.ObjectID);
                }
                if (ob1.Race == ObjectType.Spell && ob2.Race != ObjectType.Spell)
                    return -1;
                if (ob2.Race == ObjectType.Spell && ob1.Race != ObjectType.Spell)
                    return 1;

                int i = ob2.Dead.CompareTo(ob1.Dead);
                return i == 0 ? ob1.ObjectID.CompareTo(ob2.ObjectID) : i;
            });
        }
    }

}
