using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed partial class InventoryDialog : MirImageControl
    {
        public MirImageControl WeightBar;
        public MirImageControl[] LockBar = new MirImageControl[10];
        public MirItemCell[] Grid;
        public MirItemCell[] QuestGrid;

        public MirButton CloseButton, ItemButton, ItemButton2, QuestButton, AddButton, DelItemButton;
        public MirLabel GoldLabel, WeightLabel;

        public InventoryDialog()
        {
            Index = 196;
            Library = Libraries.Title;
            Movable = true;
            Sort = true;
            Visible = false;

            WeightBar = new MirImageControl
            {
                Index = 24,
                Library = Libraries.Prguse,
                Location = new Point(182, 217),
                Parent = this,
                DrawImage = false,
                NotControl = true,
            };

            ItemButton = new MirButton
            {
                Index = 197,
                Library = Libraries.Title,
                Location = new Point(6, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
            };
            ItemButton.Click += Button_Click;

            ItemButton2 = new MirButton
            {
                Index = 738,
                Library = Libraries.Title,
                Location = new Point(76, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
            };
            ItemButton2.Click += Button_Click;

            QuestButton = new MirButton
            {
                Index = 739,
                Library = Libraries.Title,
                Location = new Point(146, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
            };
            QuestButton.Click += Button_Click;

            AddButton = new MirButton
            {
                Index = 483,
                HoverIndex = 484,
                PressedIndex = 485,
                Library = Libraries.Title,
                Location = new Point(235, 5),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            AddButton.Click += (o1, e) =>
            {
                int openLevel = (GameScene.User.Inventory.Length - 46) / 4;
                int openGold = (1000000 + openLevel * 1000000);
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ExtraSlots4), openGold), MirMessageBoxButtons.OKCancel);

                messageBox.OKButton.Click += (o, a) =>
                {
                    Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
                };
                messageBox.Show();
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(289, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            DelItemButton = new MirButton
            {
                AutoSize = false,
                Location = InventoryActionLayout.Delete.Location,
                Size = InventoryActionLayout.Delete.Size,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            DelItemButton.Click += DelItemButton_Click;

            GoldLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(40, 212),
                Size = new Size(111, 14),
                Sound = SoundList.Gold,
            };
            GoldLabel.Click += (o, e) =>
            {
                if (GameScene.SelectedCell == null)
                    GameScene.PickedUpGold = !GameScene.PickedUpGold && GameScene.Gold > 0;
            };


            Grid = new MirItemCell[8 * 10];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    int idx = 8 * y + x;
                    Grid[idx] = new MirItemCell
                    {
                        ItemSlot = 6 + idx,
                        GridType = MirGridType.Inventory,
                        Library = Libraries.Items,
                        Parent = this,
                        Location = new Point(x * 36 + 9 + x, y % 5 * 32 + 37 + y % 5),
                    };

                    if (idx >= 40)
                        Grid[idx].Visible = false;
                }
            }

            QuestGrid = new MirItemCell[8 * 5];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    QuestGrid[8 * y + x] = new MirItemCell
                    {
                        ItemSlot = 8 * y + x,
                        GridType = MirGridType.QuestInventory,
                        Library = Libraries.Items,
                        Parent = this,
                        Location = new Point(x * 36 + 9 + x, y * 32 + 37 + y),
                        Visible = false
                    };
                }
            }

            WeightLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(268, 212),
                Size = new Size(26, 14)
            };
            WeightBar.BeforeDraw += WeightBar_BeforeDraw;

            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i] = new MirImageControl
                {
                    Index = 307,
                    Library = Libraries.Prguse2,
                    Location = new Point(9 + i % 2 * 148, 37 + i / 2 * 33),
                    Parent = this,
                    DrawImage = true,
                    NotControl = true,
                    Visible = false,
                };
            }

        
            ApplyOriginalUI();
        }

        void Button_Click(object sender, EventArgs e)
        {
            // Ctrl + Left-click: move selected item to the tab's bag without switching tabs.
            if (GameScene.SelectedCell != null &&
                e is MouseEventArgs me &&
                me.Button == MouseButtons.Left &&
                (Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (sender == ItemButton)
                {
                    TryMoveSelectedInventoryItem(false);
                }
                else if (sender == ItemButton2)
                {
                    TryMoveSelectedInventoryItem(true);
                }
                return;
            }

            if (GameScene.User.Inventory.Length == 46 && sender == ItemButton2)
            {
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ExtraSlots8), MirMessageBoxButtons.OKCancel);

                messageBox.OKButton.Click += (o, a) =>
                {
                    Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
                };
                messageBox.Show();
            }
            else
            {
                if (sender == ItemButton)
                {
                    RefreshInventory();
                }
                else if (sender == ItemButton2)
                {
                    RefreshInventory2();
                }
                else if (sender == QuestButton)
                {
                    Reset();

                    ItemButton.Index = 737;
                    ItemButton2.Index = 738;
                    QuestButton.Index = 198;

                    if (GameScene.User.Inventory.Length == 46)
                    {
                        ItemButton2.Index = 169;
                    }

                    foreach (var grid in QuestGrid)
                    {
                        grid.Visible = true;
                    }
                }
            }
        }

        private bool TryMoveSelectedInventoryItem(bool toSecondBag)
        {
            // Allow dragging an item (inventory or belt) onto a bag tab to move it to that bag page.
            var selected = GameScene.SelectedCell;
            if (selected == null || selected.Item == null || selected.Locked || selected.GridType != MirGridType.Inventory)
                return false;

            const int firstBagVisibleSlots = 40; // 40 slots per bag page, belt is handled separately.
            int firstBagStart = GameScene.User.BeltIdx;
            int secondBagStart = firstBagStart + firstBagVisibleSlots;
            int inventoryLength = GameScene.User.Inventory.Length;

            bool targetIsSecond = toSecondBag;
            if (targetIsSecond && inventoryLength <= secondBagStart) return false;          // no expanded bag
            if (targetIsSecond && selected.ItemSlot >= secondBagStart) return false;        // already there
            if (!targetIsSecond && selected.ItemSlot >= firstBagStart && selected.ItemSlot < secondBagStart) return false; // already in first bag

            int start = targetIsSecond ? secondBagStart : firstBagStart;
            int end = targetIsSecond ? inventoryLength : secondBagStart;

            int? targetSlot = FindFirstEmptySlot(start, end, GameScene.User.Inventory);
            if (targetSlot == null) return false; // no space

            Network.Enqueue(new C.MoveItem { Grid = MirGridType.Inventory, From = selected.ItemSlot, To = targetSlot.Value });

            selected.Locked = true;
            MirItemCell targetCell = Grid.FirstOrDefault(c => c.ItemSlot == targetSlot.Value);
            if (targetCell != null)
                targetCell.Locked = true;

            GameScene.SelectedCell = null;
            return true;
        }

        internal bool IsBagTab(MirControl control) => control==ItemButton || control==ItemButton2;
        internal void ShowBagDuringDrag(MirControl control)
        {
            if(control==ItemButton)RefreshInventory();
            else if(control==ItemButton2 && GameScene.User.Inventory.Length>46)RefreshInventory2();
        }
        internal bool AcceptBagDrop(MirControl control)
        {
            if(!IsBagTab(control))return false;
            bool moved=TryMoveSelectedInventoryItem(control==ItemButton2);
            ShowBagDuringDrag(control);
            return moved;
        }

        private static int? FindFirstEmptySlot(int start, int end, UserItem[] items)
        {
            for (int i = start; i < end; i++)
            {
                if (items[i] == null)
                    return i;
            }
            return null;
        }

        void Reset()
        {
            foreach (MirItemCell grid in QuestGrid)
            {
                grid.Visible = false;
            }

            foreach (MirItemCell grid in Grid)
            {
                grid.Visible = false;
            }

            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i].Visible = false;
            }

            AddButton.Visible = false;
        }



        public void RefreshInventory()
        {
            Reset();

            ItemButton.Index = 197;
            ItemButton2.Index = 738;
            QuestButton.Index = 739;

            if (GameScene.User.Inventory.Length == 46)
            {
                ItemButton2.Index = 169;
            }

            foreach (var grid in Grid)
            {
                if (grid.ItemSlot < 46)
                    grid.Visible = true;
                else
                    grid.Visible = false;
            }
        }

        public void RefreshInventory2()
        {
            Reset();

            ItemButton.Index = 737;
            ItemButton2.Index = 168;
            QuestButton.Index = 739;

            foreach (var grid in Grid)
            {
                if (grid.ItemSlot < 46 || grid.ItemSlot >= GameScene.User.Inventory.Length)
                    grid.Visible = false;
                else
                    grid.Visible = true;
            }

            int openLevel = (GameScene.User.Inventory.Length - 46) / 4;
            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i].Visible = (i < openLevel) ? false : true;
            }

            AddButton.Visible = openLevel >= 10 ? false : true;
        }

        public void Process()
        {
            if(Visible)OriginalUI.ReportInventoryArtStatus();
            WeightLabel.Text = $"空位 {GameScene.User.Inventory.Count(t => t == null)}";
            WeightBar.Hint = $"负重：{GameScene.User.CurrentBagWeight} / {GameScene.User.Stats[Stat.BagWeight]}";
            GoldLabel.Hint = $"金币：{GameScene.Gold:#,##0}";
            RefreshOriginalWeightBar();
            GoldLabel.Text = GameScene.Gold.ToString("###,###,##0");

            DelItemButton.Hint = GameScene.SelectedCell?.GridType==MirGridType.Inventory && GameScene.SelectedCell.Item!=null
                ? $"永久删除：{GameScene.SelectedCell.Item.FriendlyName}（需确认）"
                : "永久删除：将背包物品拖到这里，或拿起物品后点击";

        }


        private void WeightBar_BeforeDraw(object sender, EventArgs e)
        {
            if (WeightBar.Library == null) return;

            double percent = OriginalUILayout.Fraction(MapObject.User.CurrentBagWeight, MapObject.User.Stats[Stat.BagWeight]);
            if (percent > 1) percent = 1;
            if (percent <= 0) return;

            // Weight bar art based on fill
            if (percent <= 0.50)
            {
                WeightBar.Library = Libraries.Prguse;
                WeightBar.Index = 24;
            }
            else if (percent <= 0.75)
            {
                WeightBar.Library = Libraries.UI_32bit;
                WeightBar.Index = 471;
            }
            else
            {
                WeightBar.Library = Libraries.UI_32bit;
                WeightBar.Index = 470;
            }

            Rectangle section = new Rectangle
            {
                Size = new Size((int)((WeightBar.Size.Width - 3) * percent), WeightBar.Size.Height)
            };

            WeightBar.Library.Draw(WeightBar.Index, section, WeightBar.DisplayLocation, Color.White, false);
        }


        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }

        public MirItemCell GetQuestCell(ulong id)
        {
            return QuestGrid.FirstOrDefault(t => t.Item != null && t.Item.UniqueID == id);
        }

        public void DisplayItemGridEffect(ulong id, int type = 0)
        {
            MirItemCell cell = GetCell(id);

            if (cell.Item == null) return;

            MirAnimatedControl animEffect = null;

            switch (type)
            {
                case 0:
                    animEffect = new MirAnimatedControl
                    {
                        Animated = true,
                        AnimationCount = 9,
                        AnimationDelay = 150,
                        Index = 410,
                        Library = Libraries.Prguse,
                        Location = cell.Location,
                        Parent = this,
                        Loop = false,
                        NotControl = true,
                        UseOffSet = true,
                        Blending = true,
                        BlendingRate = 1F
                    };
                    animEffect.AfterAnimation += (o, e) => animEffect.Dispose();
                    SoundManager.PlaySound(20000 + (ushort)Spell.MagicShield * 10);
                    break;
            }
        }

        private void DelItemButton_Click(object sender, EventArgs e)
        {
            if(GameScene.SelectedCell?.GridType==MirGridType.Inventory && GameScene.SelectedCell.Item!=null)
                PromptDelete(GameScene.SelectedCell);
            else GameScene.Scene.ChatDialog.ReceiveChat("将背包物品拖到垃圾桶，确认后永久删除。丢到地上请拖出背包。",ChatType.System);
        }

        internal bool AcceptDeleteDrop(MirControl target)
        {
            if(!IsDeleteDropTarget(target))return false;
            if(GameScene.SelectedCell?.GridType==MirGridType.Inventory)PromptDelete(GameScene.SelectedCell);
            return true;
        }
        internal bool IsDeleteDropTarget(MirControl target)=>Visible && DelItemButton.Enabled &&
            (target==DelItemButton || target?.Parent==DelItemButton);

        public void PromptDelete(MirItemCell cell) => MirItemActionDialog.Open(cell,InventoryItemAction.Delete);

    }
    public sealed partial class BeltDialog : MirImageControl
    {
        public MirLabel[] Key = new MirLabel[6];
        public MirButton CloseButton;
        public MirItemCell[] Grid;

        public BeltDialog()
        {
            Index = 1932;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;
            Visible = true;
            Location = new Point(GameScene.Scene.MainDialog.Location.X + 230, UIViewport.Height - 150);

            BeforeDraw += BeltPanel_BeforeDraw;

            for (int i = 0; i < Key.Length; i++)
            {
                Key[i] = new MirLabel
                {
                    Parent = this,
                    Size = new Size(26, 14),
                    Location = new Point(8 + i * 35, 2),
                    Text = (i + 1).ToString()
                };
            }

            CloseButton = new MirButton
            {
                HoverIndex = 1924,
                Index = 1923,
                Location = new Point(222, 19),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 1925,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CloseKey), CMain.InputKeys.GetKey(KeybindOptions.Belt))
            };
            CloseButton.Click += (o, e) => Hide();

            Grid = new MirItemCell[6];

            for (int x = 0; x < 6; x++)
            {
                Grid[x] = new MirItemCell
                {
                    ItemSlot = x,
                    Size = new Size(32, 32),
                    GridType = MirGridType.Inventory,
                    Library = Libraries.Items,
                    Parent = this,
                    Location = new Point(x * 35 + 12, 3),
                };
            }

        
            ApplyOriginalUI();
        }

        private void BeltPanel_BeforeDraw(object sender, EventArgs e)
        {
            //if Transparent return

            if (Libraries.Prguse != null)
                Libraries.Prguse.Draw(Index + 1, DisplayLocation, Color.White, false, 0.5F);
        }

        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }
    }
}
