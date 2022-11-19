using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore;
using SpaceShared.UI;
using StardewValley;
using StardewValley.Menus;

namespace MajesticArcana
{
    internal class AlchemyMenu : IClickableMenu
    {
        private RootElement ui;
        private ItemSlot[] ingreds;
        private ItemSlot output;

        private InventoryMenu inventory;

        private class Pixel
        {
            public float x;
            public float y;
            public Color color;
            public float scale;
            public Vector2 velocity;
        }
        private List<Pixel> pixels = new();

        private Item held;
        private float? animStart;

        public AlchemyMenu()
        :   base( ( Game1.viewport.Width - 64 * 12 - 32 ) / 2, ( Game1.viewport.Height - 480 - 250) / 2, 64 * 12 + 32, 480 + 250 )
        {
            ui = new RootElement();
            ui.LocalPosition = new Vector2(xPositionOnScreen, yPositionOnScreen);

            Vector2 basePoint = new(width / 2, (height - 200) / 2);

            output = new ItemSlot()
            {
                LocalPosition = basePoint,
                TransparentItemDisplay = true,
                Callback = (e) => DoCraftingIfPossible(),
            };
            output.LocalPosition -= new Vector2(output.Width / 2, output.Height / 2);
            ui.AddChild(output);

            ingreds = new ItemSlot[ 6 ];
            for (int i = 0; i < 6; ++i)
            {
                ingreds[i] = new ItemSlot()
                {
                    LocalPosition = basePoint +
                                    new Vector2( MathF.Cos( 3.14f * 2 / 6 * i ) * 200,
                                                 MathF.Sin( 3.14f * 2 / 6 * i ) * 200 ) +
                                    -new Vector2( output.Width / 2, output.Height / 2 ),
                    Callback = (e) => CheckRecipe(),
                };
                ui.AddChild(ingreds[i]);
            }

            inventory = new InventoryMenu(xPositionOnScreen + 16, yPositionOnScreen + height - 64 * 3 - 16, true, Game1.player.Items);
        }

        private void Pixelize(ItemSlot slot)
        {
            var obj = slot.Item as StardewValley.Object;
            if (obj == null)
                return;

            var rect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, obj.ParentSheetIndex, 16, 16);
            var target = TileSheetExtensions.GetAdjustedTileSheetTarget(Game1.objectSpriteSheet, rect);
            rect.Y = target.Y;
            var tex = TileSheetExtensions.GetTileSheet(Game1.objectSpriteSheet, target.TileSheet);

            var cols = new Color[16 * 16];
            tex.GetData(0, rect, cols, 0, cols.Length);

            for (int i = 0; i < cols.Length; ++i)
            {
                int ix = i % 16;
                int iy = i / 16;

                float velDir = (float)Game1.random.NextDouble() * 3.14f * 2;
                Vector2 vel = new Vector2(MathF.Cos(velDir), MathF.Sin(velDir)) * (60 + Game1.random.Next( 70 ) );

                pixels.Add(new Pixel()
                {
                    x = slot.Bounds.Location.X + 16 + ix * Game1.pixelZoom,
                    y = slot.Bounds.Location.Y + 16 + iy * Game1.pixelZoom,
                    color = cols[i],
                    scale = 3 + (float) Game1.random.NextDouble() * 3,
                    velocity = vel,
                });
            }
        }

        private void DoCraftingIfPossible()
        {
            if (output.Item == null && output.ItemDisplay != null && pixels.Count == 0)
            {
                foreach (var ingred in ingreds)
                {
                    Pixelize(ingred);
                }
                animStart = (float)Game1.currentGameTime.TotalGameTime.TotalSeconds;
                foreach (var ingred in ingreds)
                    ingred.Item = null;
            }
        }

        private void CheckRecipe()
        {
            var recipe = new Tuple< int, bool >[ 6 ];
            int output = 74;

            recipe[0] = new(768, false);
            recipe[1] = new(769, false);
            recipe[2] = new(771, false);
            recipe[3] = new(766, false);
            recipe[4] = new(82, false);
            recipe[5] = new(444, false);

            for (int i = 0; i < ingreds.Length; ++i)
            {
                for (int j = 0; j < recipe.Length; ++j)
                {
                    if (ingreds[i].Item is StardewValley.Object obj && obj.ParentSheetIndex == recipe[j].Item1 && !recipe[j].Item2)
                        recipe[j] = new(recipe[j].Item1, true);
                }
            }

            bool okay = true;
            for (int i = 0; i < recipe.Length; ++i)
            {
                if (!recipe[i].Item2)
                {
                    okay = false;
                    break;
                }
            }

            if (okay)
            {
                this.output.ItemDisplay = new StardewValley.Object(output, 1);
            }
        }

        protected override void cleanupBeforeExit()
        {
            if (output.Item != null)
                Game1.createItemDebris(output.Item, Game1.player.Position, 0, Game1.player.currentLocation);
            foreach (var ingred in ingreds)
            {
                if (ingred.Item != null)
                    Game1.createItemDebris(ingred.Item, Game1.player.Position, 0, Game1.player.currentLocation);
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            held = inventory.leftClick(x, y, held, playSound);

            if (ItemWithBorder.HoveredElement is ItemSlot slot)
            {
                if (slot != output)
                {
                    if (slot.Item == null && held != null && held is StardewValley.Object)
                    {
                        if (held.Stack > 1)
                        {
                            slot.Item = held.getOne();
                            slot.Item.Stack = 1;
                            held.Stack -= 1;
                        }
                        else
                        {
                            slot.Item = held;
                            held = null;
                        }
                    }
                    else if (slot.Item != null && held == null)
                    {
                        held = slot.Item;
                        slot.Item = null;
                    }
                    else if (slot.Item != null && held != null && held is StardewValley.Object)
                    {
                        int left = slot.Item.addToStack(held);
                        if (slot.Item.Stack > 1)
                        {
                            left += slot.Item.Stack - 1;
                            slot.Item.Stack = 1;
                        }
                        held.Stack = left;
                        if (left <= 0)
                            held = null;
                    }

                    CheckRecipe();
                }
                else if ( output.Item != null )
                {
                    if (held == null || held.canStackWith(output.Item))
                    {
                        held = output.Item;
                        output.Item = null;
                    }
                }
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            base.receiveRightClick(x, y, playSound);
            held = inventory.rightClick(x, y, held, playSound);
        }

        public override void update(GameTime time)
        {
            base.update(time);
            ui.Update();
            inventory.update(time);

            if ( animStart != null && pixels.Count == 0 && output.ItemDisplay != null && output.Item == null )
            {
                animStart = null;
                output.Item = output.ItemDisplay;
                output.ItemDisplay = null;
            }
        }

        public override void draw(SpriteBatch b)
        {
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            ui.Draw(b);
            inventory.draw(b);

            float delta = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            float ts = (float)(Game1.currentGameTime.TotalGameTime.TotalSeconds - animStart ?? 0);
            if (ts < 0) ts = 0;
            Vector2 center = new(xPositionOnScreen + width / 2, yPositionOnScreen + ( height - 200 )  / 2);
            float velMult = ts * ts;
            List<Pixel> toRemove = new();
            for (int i = 0; i < pixels.Count; ++i)
            {
                Pixel pixel = pixels[i];
                float actualScale = (pixel.scale + MathF.Sin(ts * 3) - 3)%3 + 3;

                Vector2 ppos = new Vector2(pixel.x, pixel.y) + pixel.velocity * delta;
                pixel.x = ppos.X;
                pixel.y = ppos.Y;
                Vector2 toCenter = center - ppos;
                float dist = Vector2.Distance(center, ppos);
                pixel.velocity = pixel.velocity * 0.99f + toCenter / dist * velMult;

                b.Draw(Game1.staminaRect, new Vector2(pixel.x, pixel.y), null, pixel.color, 0, Vector2.Zero, actualScale, SpriteEffects.None, 1);

                if (float.IsNaN(dist))
                {
                    //Console.WriteLine("wat");
                }

                if (dist < 24 || float.IsNaN(dist))
                {
                    toRemove.Add(pixel);
                }
            }
            pixels.RemoveAll((p) => toRemove.Contains(p));

            held?.drawInMenu(b, Game1.getMousePosition().ToVector2(), 1);

            if (ItemWithBorder.HoveredElement != null)
            {
                if (ItemWithBorder.HoveredElement is ItemSlot slot && slot.Item != null)
                {
                    drawToolTip(b, slot.Item.getDescription(), slot.Item.DisplayName, slot.Item);
                }
                else if (ItemWithBorder.HoveredElement.ItemDisplay != null)
                {
                    drawToolTip(b, ItemWithBorder.HoveredElement.ItemDisplay.getDescription(), ItemWithBorder.HoveredElement.ItemDisplay.DisplayName, ItemWithBorder.HoveredElement.ItemDisplay);
                }
            }
            else
            {
                var hover = inventory.hover(Game1.getMouseX(), Game1.getMouseY(), null);
                if (hover != null)
                {
                    drawToolTip(b, inventory.hoverText, inventory.hoverTitle, hover);
                }
            }

            drawMouse(b);
        }
    }
}