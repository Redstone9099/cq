using System.Drawing;
﻿using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects
{
    public sealed partial class ItemObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Item; }
        }

        public override string Name
        {
            get { return Item == null ? string.Empty : Item.Info.FriendlyName; }
            set { throw new NotSupportedException(); }
        }

        public override int CurrentMapIndex { get; set; }
        public override Point CurrentLocation { get; set; }
        public override MirDirection Direction
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
        public override ushort Level
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override bool Blocking
        {
            get { return false; }
        }

        public Point DropOrigin { get; private set; }
        private long dropStartedAt = -1;
        public override void Spawned()
        {
            dropStartedAt = Envir.Time;
            base.Spawned();
        }
        private int DropAge => dropStartedAt < 0 ? 10000 : (int)Math.Clamp(Envir.Time - dropStartedAt, 0L, 10000L);

        public uint Gold;
        public UserItem Item;


        public override int Health
        {
            get { throw new NotSupportedException(); }
        }

        public override int MaxHealth
        {
            get { throw new NotSupportedException(); }
        }

        public ItemObject(MapObject dropper, UserItem item, bool deathDrop = false)
        {
            if (deathDrop)//player dropped it when he died: allow for time to run back and pickup his drops
                ExpireTime = Envir.Time + Settings.PlayerDiedItemTimeOut * Settings.Minute;
            else
                ExpireTime = Envir.Time + Settings.ItemTimeOut * Settings.Minute;

            Item = item;
            NameColour = ItemQualityPresentation.Colour(item.Info.Grade);

			DropOrigin = dropper.CurrentLocation;
            CurrentMap = dropper.CurrentMap;
            CurrentLocation = dropper.CurrentLocation;
        }
        public ItemObject(MapObject dropper, UserItem item, Point manualpoint)
        {
            ExpireTime = Envir.Time + Settings.ItemTimeOut * Settings.Minute;

            Item = item;
            NameColour = ItemQualityPresentation.Colour(item.Info.Grade);

            DropOrigin = dropper.CurrentLocation;
            CurrentMap = dropper.CurrentMap;
            CurrentLocation = manualpoint;
        }
        public ItemObject(MapObject dropper, uint gold)
        {
            ExpireTime = Envir.Time + Settings.ItemTimeOut * Settings.Minute;

            Gold = gold;

            DropOrigin = dropper.CurrentLocation;
            CurrentMap = dropper.CurrentMap;
            CurrentLocation = dropper.CurrentLocation;
        }
        public ItemObject(MapObject dropper, uint gold, Point manualLocation)
        {
            ExpireTime = Envir.Time + Settings.ItemTimeOut * Settings.Minute;

            Gold = gold;

            DropOrigin = dropper.CurrentLocation;
            CurrentMap = dropper.CurrentMap;
            CurrentLocation = manualLocation;
        }
         
        public override void Process()
        {
            if (Envir.Time > ExpireTime)
            {
                CurrentMap.RemoveObject(this);
                Despawn();
                return;
            }

            if (Owner != null && Envir.Time > OwnerTime)
                Owner = null;

            base.Process();
        }

        public override void SetOperateTime()
        {
            long time = Envir.Time + 2000;

            if (OwnerTime < time && OwnerTime > Envir.Time)
                time = OwnerTime;

            if (ExpireTime < time && ExpireTime > Envir.Time)
                time = ExpireTime;

            if (PKPointTime < time && PKPointTime > Envir.Time)
                time = PKPointTime;

            if (LastHitTime < time && LastHitTime > Envir.Time)
                time = LastHitTime;

            if (EXPOwnerTime < time && EXPOwnerTime > Envir.Time)
                time = EXPOwnerTime;

            if (BrownTime < time && BrownTime > Envir.Time)
                time = BrownTime;

            for (int i = 0; i < ActionList.Count; i++)
            {
                if (ActionList[i].Time >= time && ActionList[i].Time > Envir.Time) continue;
                time = ActionList[i].Time;
            }

            for (int i = 0; i < PoisonList.Count; i++)
            {
                if (PoisonList[i].TickTime >= time && PoisonList[i].TickTime > Envir.Time) continue;
                time = PoisonList[i].TickTime;
            }

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].NextTime >= time && Buffs[i].NextTime > Envir.Time) continue;
                time = Buffs[i].NextTime;
            }


            if (OperateTime <= Envir.Time || time < OperateTime)
                OperateTime = time;
        }


        public bool Drop(int distance)
        {
            if (CurrentMap == null || distance < 0) return false;
            Point origin = CurrentLocation, best = Point.Empty;
            int bestCount = int.MaxValue, ties = 0;
            bool found = false;
            // Search nearest rings first, but choose uniformly within each ring.
            // Keep the existing walkability, warp, blocking and stack-limit checks.
            for (int d = 0; d <= distance; d++)
            {
                Point empty = Point.Empty;
                int emptyCount = 0;
                for (int y = Math.Max(0,origin.Y-d); y <= Math.Min(CurrentMap.Height-1,origin.Y+d); y++)
                for (int x = Math.Max(0,origin.X-d); x <= Math.Min(CurrentMap.Width-1,origin.X+d); x++)
                {
                    if (Math.Max(Math.Abs(x-origin.X),Math.Abs(y-origin.Y)) != d) continue;
                    if (!CurrentMap.ValidPoint(x,y)) continue;
                    var at = new Point(x,y);
                    if (CurrentMap.Info.Movements.Any(m=>m.Source==at)) continue;
                    var cell = CurrentMap.GetCell(x,y);
                    int count = 0;
                    bool blocking = false;
                    if (cell.Objects != null)
                        foreach (var ob in cell.Objects)
                        {
                            if (ob.Blocking) { blocking = true; break; }
                            if (ob.Race == ObjectType.Item) count++;
                        }
                    if (blocking || count >= Settings.DropStackSize) continue;
                    if (count == 0)
                    {
                        if (Envir.Random.Next(++emptyCount) == 0) empty = at;
                        continue;
                    }
                    if (count < bestCount) { bestCount=count; ties=0; }
                    if (count == bestCount && Envir.Random.Next(++ties) == 0)
                    { best=at; found=true; }
                }
                if (emptyCount > 0) { best=empty; found=true; break; }
            }
            if (!found) return false;
            CurrentLocation=best;
            CurrentMap.AddObject(this);
            Spawned();
            return true;
        }

        public bool DragonDrop(int distance)
        {
            if (CurrentMap == null) return false;

            Cell best = null;
            int bestCount = 0;
            Point bestLocation = Point.Empty;

            for (int d = 0; d <= distance; d++)
            {
                Point empty = Point.Empty;
                int emptyCount = 0;
                for (int y = CurrentLocation.Y + 3; y <= CurrentLocation.Y + (d * 2); y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = CurrentLocation.X - d; x <= CurrentLocation.X + d; x += Math.Abs(y - CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;
                        if (!CurrentMap.ValidPoint(x, y)) continue;

                        bool movement = false;
                        for (int i = 0; i < CurrentMap.Info.Movements.Count; i++)
                        {
                            MovementInfo info = CurrentMap.Info.Movements[i];
                            if (info.Source != new Point(x, y)) continue;
                            movement = true;
                            break;
                        }

                        if (movement) continue;

                        Cell cell = CurrentMap.GetCell(x, y);

                        if (cell.Objects == null)
                        {
                            if (Envir.Random.Next(++emptyCount) == 0) empty = new Point(x,y);
                            continue;
                        }

                        int count = 0;
                        bool blocking = false;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Blocking)
                            {
                                blocking = true;
                                break;
                            }
                            if (ob.Race == ObjectType.Item)
                                count++;
                        }

                        if (blocking || count >= Settings.DropStackSize) continue;

                        if (count == 0)
                        {
                            if (Envir.Random.Next(++emptyCount) == 0) empty = new Point(x,y);
                            continue;
                        }

                        if (best == null || count < bestCount)
                        {
                            best = cell;
                            bestCount = count;
                            bestLocation = new Point(x, y);
                        }
                    }
                }
                if (emptyCount > 0)
                {
                    CurrentLocation=empty;
                    CurrentMap.AddObject(this);
                    Spawned();
                    return true;
                }
            }

            if (best == null)

                return false;

            CurrentLocation = bestLocation;
            CurrentMap.AddObject(this);
            Spawned();
            return true;
        }


        public override Packet GetInfo()
        {
            if (Item != null)
                return new S.ObjectItem
                    {
                        ObjectID = ObjectID,
                        Name = Item.Count > 1 ? string.Format("{0} ({1})", Name, Item.Count) : Name,
                        NameColour = ItemQualityPresentation.Colour(Item.Info.Grade),
                        Location = CurrentLocation,
                        Image = Item.Image,
                        grade = ItemQualityPresentation.Normalize(Item.Info.Grade),
                        DropOrigin = DropOrigin,
                        DropAge = DropAge
                    };

            return new S.ObjectGold
                {
                    ObjectID =  ObjectID,
                    Gold = Gold,
                    DropOrigin = DropOrigin,
                    DropAge = DropAge,
                    Location = CurrentLocation,
                };
        }



        public override void Process(DelayedAction action)
        {
            throw new NotSupportedException();
        }
        public override bool IsAttackTarget(HumanObject attacker)
        {
            throw new NotSupportedException();
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {
            throw new NotSupportedException();
        }
        public override int Attacked(HumanObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            throw new NotSupportedException();
        }
        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            throw new NotSupportedException();
        }
        public override int Struck(int damage, DefenceType type = DefenceType.ACAgility)
        {
            throw new NotSupportedException();
        }

        public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false, bool ignoreDefence = true)
        {
            throw new NotSupportedException();
        }

        public override Buff AddBuff(BuffType type, MapObject owner, int duration, Stats stats, bool refreshStats = true, bool updateOnly = false, params int[] values)
        {
            throw new NotSupportedException();
        }

        public override bool IsFriendlyTarget(HumanObject ally)
        {
            throw new NotSupportedException();
        }

        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            throw new NotSupportedException();
        }

        public override void Die()
        {
            throw new NotSupportedException();
        }

        public override void SendHealth(HumanObject player)
        {
            throw new NotSupportedException();
        }

        public override int Pushed(MapObject pusher, MirDirection dir, int distance)
        {
            throw new NotSupportedException();
        }

        public override void ReceiveChat(string text, ChatType type)
        {
            throw new NotSupportedException();
        }
    }
}
