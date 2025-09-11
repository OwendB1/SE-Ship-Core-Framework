#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using VRage.Game.GUI.TextPanel;
using VRageMath;
using IngameCubeBlock = VRage.Game.ModAPI.Ingame.IMyCubeBlock;
using IngameIMyEntity = VRage.Game.ModAPI.Ingame.IMyEntity;

#endregion

namespace ShipCoreFramework
{
    [MyTextSurfaceScript("GridStatusLCDScript", "Grid class status")]
    internal class GridStatusLCDScript : MyTSSCommon
    {
        private static readonly float ScrollSpeed = 3; //pixels per update
        private static readonly int ScrollPauseUpdates = 15; //how many updates to say paused at the start and end when scrolling

        private readonly Table _appliedModifiersTable = new Table
        {
            Columns = new List<Column>
            {
                new Column { Name = "ModifierName" },
                new Column { Name = "Value" }
            }
        };

        private readonly Table _gridResultsTable = new Table
        {
            Columns = new List<Column>
            {
                new Column { Name = "Property" },
                new Column { Name = "Value", Alignment = TextAlignment.RIGHT },
                new Column { Name = "Separator" },
                new Column { Name = "Max" },
                new Column { Name = "Success" }
            }
        };

        private readonly Table _headerTable = new Table
        {
            Columns = new List<Column>
            {
                new Column { Name = "Label" },
                new Column { Name = "Name" },
                new Column { Name = "Success" }
            }
        };

        private readonly IMyTerminalBlock _terminalBlock;
        private int _scrollTime;

        public GridStatusLCDScript(IMyTextSurface surface, IngameCubeBlock block, Vector2 size) : base(surface, block, size)
        {
            _terminalBlock = (IMyTerminalBlock)block; // internal stored m_block is the ingame interface which has no events, so can't unhook later on, therefore this field is required.
            _terminalBlock.OnMarkForClose += BlockMarkedForClose; // required if you're going to make use of Dispose() as it won't get called when block is removed or grid is cut/unloaded.

            // Called when script is created.
            // This class is instanced per LCD that uses it, which means the same block can have multiple instances of this script aswell (e.g. a cockpit with all its screens set to use this script).
        }

        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update10; // frequency that Run() is called.

        private GridLogic GridLogic => _terminalBlock?.GetMainGridLogic();
        private MyCubeGrid Grid => _terminalBlock?.CubeGrid as MyCubeGrid;
        private ShipCore ShipCore => GridLogic.ShipCore;

        public override void Dispose()
        {
            base.Dispose();
            _terminalBlock.OnMarkForClose -= BlockMarkedForClose;
        }

        private void BlockMarkedForClose(IngameIMyEntity ent)
        {
            Dispose();
        }
        
        public override void Run()
        {
            if (ModSessionManager.Config.SelectedNoCore == null) return;
            try
            {
                base.Run(); // do not remove
                _gridResultsTable.Clear();
                if (!Constants.IsClient) return;

                Draw();
            }
            catch (Exception e)
            {
                DrawError(e);
            }
        }

        private void Draw()
        {
            if (!Constants.IsClient) return;

            var screenSize = Surface.SurfaceSize;
            var screenTopLeft = (Surface.TextureSize - screenSize) * 0.5f;
            var padding = new Vector2(10, 10);
            var cellGap = new Vector2(12, 5);
            var screenInnerWidth = Surface.SurfaceSize.X - padding.X * 2;
            var successColor = Color.Green;
            var failColor = Color.Red;
            const float baseScale = 1;
            var bodyScale = baseScale * 13 / TextUtils.CharWidth;

            var frame = Surface.DrawFrame();

            // https://github.com/malware-dev/MDK-SE/wiki/Text-Panels-and-Drawing-Sprites

            AddBackground(frame, Color.Black.Alpha(0f));
            Surface.ScriptBackgroundColor = Color.Black;
            Surface.ScriptForegroundColor = Color.Black;
            // the colors in the terminal are Surface.ScriptBackgroundColor and Surface.ScriptForegroundColor, the other ones without Script in name are for text/image mode.
            _gridResultsTable.Clear();

            Vector2 currentPosition;
            var spritesToRender = new List<MySprite>();

            //Render the header
            _headerTable.Clear();

            _headerTable.Rows.Add(new Row
            {
                new Cell("Type:"),
                new Cell(ShipCore.UniqueName)
            });

            _headerTable.RenderToSprites(spritesToRender, screenTopLeft + padding, screenInnerWidth, new Vector2(15, 0), out currentPosition);

            //Render the results checklist
            /*
            if (ShipCore.MaxBlocks > 1 || ShipCore.MinBlocks > 1)
            {
                var passed = (ShipCore.MaxBlocks < 1 || Grid.BlocksCount <= ShipCore.MaxBlocks) &&
                             (ShipCore.MinBlocks < 1 || Grid.BlocksCount <= ShipCore.MinBlocks);
                var target = ShipCore.MaxBlocks > 1 && ShipCore.MinBlocks > 1
                    ? $"{ShipCore.MinBlocks} - {ShipCore.MaxBlocks}"
                    : ShipCore.MaxBlocks > 1
                        ? $"<= {ShipCore.MaxBlocks}"
                        : $">= {ShipCore.MinBlocks}";

                _gridResultsTable.Rows.Add(new Row
                {
                    new Cell("Blocks: "),
                    new Cell(Grid.BlocksCount.ToString()),
                    new Cell("/"),
                    new Cell(target, passed ? successColor : failColor),
                    passed ? new Cell() : new Cell("X", failColor)
                });
            }*/

            if (ShipCore.MaxMass > 1)
                _gridResultsTable.Rows.Add(new Row
                {
                    new Cell("Mass: "),
                    new Cell(Grid.Mass.ToString(CultureInfo.InvariantCulture)),
                    new Cell("/"),
                    new Cell(ShipCore.MaxMass.ToString(CultureInfo.InvariantCulture),
                        Grid.Mass <= ShipCore.MaxMass ? successColor : failColor),
                    Grid.Mass <= ShipCore.MaxMass ? new Cell() : new Cell("X", failColor)
                });

            if (ShipCore.MaxPCU > 1)
                _gridResultsTable.Rows.Add(new Row
                {
                    new Cell("PCU: "),
                    new Cell(Grid.BlocksPCU.ToString()),
                    new Cell("/"),
                    new Cell(ShipCore.MaxPCU.ToString(),
                        Grid.BlocksPCU <= ShipCore.MaxPCU ? successColor : failColor),
                    Grid.BlocksPCU <= ShipCore.MaxPCU ? new Cell() : new Cell("X", failColor)
                });

            if (ShipCore.BlockLimits != null)
                foreach (var blockLimit in GridLogic.BlocksPerLimit)
                {
                    var countWeight = blockLimit.Value.Sum(l => l.Value);
                    _gridResultsTable.Rows.Add(new Row
                    {
                        new Cell($"{blockLimit.Key.Name}:"),
                        new Cell(countWeight.ToString(CultureInfo.InvariantCulture)),
                        new Cell("/"),
                        new Cell(blockLimit.Key.MaxCount.ToString(CultureInfo.InvariantCulture),
                            countWeight <= blockLimit.Key.MaxCount ? successColor : failColor),
                        countWeight <= blockLimit.Key.MaxCount ? new Cell() : new Cell("X", failColor)
                    });
                }

            var gridResultsTableTopLeft = currentPosition + new Vector2(0, 5);

            _gridResultsTable.RenderToSprites(spritesToRender, gridResultsTableTopLeft, screenInnerWidth, cellGap,
                out currentPosition, bodyScale);

            //Applied modifiers
            spritesToRender.Add(CreateLine("Applied modifiers", currentPosition + new Vector2(0, 5),
                out currentPosition));

            _appliedModifiersTable.Clear();

            var appliedModifiersTableTopLeft = currentPosition + new Vector2(0, 5);

            foreach (var modifierValue in GridLogic.Modifiers.GetModifierValues())
                _appliedModifiersTable.Rows.Add(new Row
                {
                    new Cell($"{modifierValue.Name}:"),
                    new Cell(modifierValue.Value.ToString(CultureInfo.InvariantCulture))
                });

            _appliedModifiersTable.RenderToSprites(spritesToRender, appliedModifiersTableTopLeft, screenInnerWidth,
                cellGap, out currentPosition, bodyScale);
            var scrollPosition = GetScrollPosition(currentPosition + padding);

            foreach (var t in spritesToRender)
            {
                var sprite = t;
                if (scrollPosition.Y != 0) sprite.Position -= scrollPosition;
                frame.Add(sprite);
            }

            frame.Dispose(); // send sprites to the screen
        }

        private Vector2 GetScrollPosition(Vector2 contentBottomRight)
        {
            var screenSize = Surface.SurfaceSize;
            var screenTopLeft = (Surface.TextureSize - screenSize) * 0.5f;
            var contentHeight = contentBottomRight.Y - screenTopLeft.Y;

            if (!(contentHeight > screenSize.Y)) return new Vector2();
            var scrollRange = contentHeight - screenSize.Y;
            var numUpdatesToScroll = (int)Math.Ceiling(scrollRange / ScrollSpeed);
            var fullScrollCycleTime = (ScrollPauseUpdates + numUpdatesToScroll) * 2;
            Vector2 scrollPosition;

            if (_scrollTime < ScrollPauseUpdates)
                scrollPosition = new Vector2();
            else if (_scrollTime < ScrollPauseUpdates + numUpdatesToScroll)
                scrollPosition = new Vector2(0, (_scrollTime - ScrollPauseUpdates) * ScrollSpeed);
            else if (_scrollTime < ScrollPauseUpdates * 2 + numUpdatesToScroll)
                scrollPosition = new Vector2(0, scrollRange);
            else
                scrollPosition = new Vector2(0,
                    scrollRange - (_scrollTime - (ScrollPauseUpdates * 2 + numUpdatesToScroll)) * ScrollSpeed);

            _scrollTime++;

            if (_scrollTime > fullScrollCycleTime) _scrollTime = 0;

            return scrollPosition;
        }

        private static MySprite CreateLine(string text, Vector2 position, out Vector2 positionAfter, float scale = 1)
        {
            var sprite = MySprite.CreateText(text, "Monospace", Color.White, scale, TextAlignment.LEFT);
            sprite.Position =
                position; // screenCorner + padding + new Vector2(0, y); // 16px from top left corner of the visible surface

            positionAfter = position + new Vector2(0, TextUtils.GetTextHeight(text, scale));

            return sprite;
        }

        private void DrawError(Exception e)
        {
            Utils.Log($"Failed to draw LCD: {e.Message}\n{e.StackTrace}");

            try // first try printing the error on the LCD
            {
                var screenSize = Surface.SurfaceSize;
                var screenCorner = (Surface.TextureSize - screenSize) * 0.5f;

                var frame = Surface.DrawFrame();

                var bg = new MySprite(SpriteType.TEXTURE, "SquareSimple", null, null, Color.Black);
                frame.Add(bg);

                var text = MySprite.CreateText(
                    $"ERROR: {e.Message}\n{e.StackTrace}\n\nPlease send screenshot of this to mod author.\n{MyAPIGateway.Utilities.GamePaths.ModScopeName}",
                    "White", Color.Red, 0.7f, TextAlignment.LEFT);
                text.Position = screenCorner + new Vector2(16, 16);
                frame.Add(text);

                frame.Dispose();
            }
            catch (Exception e2)
            {
                Utils.Log($"Also failed to draw error on screen: {e2.Message}\n{e2.StackTrace}");
            }
        }
    }
}