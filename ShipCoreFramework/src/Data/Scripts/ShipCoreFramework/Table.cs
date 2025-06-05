using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace ShipCoreFramework
{
    public class Table
    {
        public List<Column> Columns = new List<Column>();

        public List<Row> Rows = new List<Row>();

        public void Clear()
        {
            Rows.Clear();
        }

        public void RenderToSprites(List<MySprite> sprites, Vector2 topLeft, float width, Vector2 cellGap,
            out Vector2 positionAfter, float scale = 1)
        {
            //Calculate column widths & row heights
            var columnContentWidths = new float[Columns.Count];
            var columnWidths = new float[Columns.Count];
            var rowHeights = new float[Rows.Count];
            float totalFreeSpaceWeight = 0;
            float minWidthRequired = 0;
            for (var colNum = 0; colNum < Columns.Count; colNum++)
            {
                totalFreeSpaceWeight += Columns[colNum].FreeSpace;
                for (var rowNum = 0; rowNum < Rows.Count; rowNum++)
                {
                    var row = Rows[rowNum];
                    var cell = row[colNum];

                    if (cell.IsEmpty) continue;
                    columnContentWidths[colNum] = Math.Max(columnContentWidths[colNum],
                        TextUtils.GetTextWidth(cell.Value, scale));
                    rowHeights[rowNum] = Math.Max(rowHeights[rowNum], TextUtils.GetTextHeight(cell.Value, scale));
                }

                minWidthRequired += columnContentWidths[colNum] + (colNum > 0 ? cellGap.X : 0);
                columnWidths[colNum] = columnContentWidths[colNum];
            }

            //distribute free space
            if (minWidthRequired < width && totalFreeSpaceWeight > 0)
            {
                var freeSpace = width - minWidthRequired;

                for (var i = 0; i < Columns.Count; i++)
                    if (Columns[i].FreeSpace > 0)
                        columnWidths[i] += freeSpace * (Columns[i].FreeSpace / totalFreeSpaceWeight);
            }

            var rowTopLeft = topLeft;
            var tableHeight = 0f;
            //render rows
            for (var rowNum = 0; rowNum < Rows.Count; rowNum++)
            {
                var row = Rows[rowNum];

                float rowX = 0;
                for (var colNum = 0; colNum < Columns.Count; colNum++)
                {
                    var cell = row[colNum];
                    var column = Columns[colNum];

                    if (!cell.IsEmpty)
                    {
                        var sprite = MySprite.CreateText(cell.Value, "Monospace", cell.Color, scale, column.Alignment);

                        switch (column.Alignment)
                        {
                            case TextAlignment.LEFT:
                                sprite.Position = rowTopLeft + new Vector2(rowX, 0);
                                break;
                            case TextAlignment.RIGHT:
                                sprite.Position = rowTopLeft + new Vector2(rowX + columnWidths[colNum], 0);
                                break;
                            case TextAlignment.CENTER:
                                sprite.Position = rowTopLeft + new Vector2(rowX + columnWidths[colNum] / 2, 0);

                                break;
                        }

                        sprites.Add(sprite);
                    }

                    rowX += columnWidths[colNum] + cellGap.X;
                }

                var rowTotalHeight = rowHeights[rowNum] + (rowNum > 0 ? cellGap.Y : 0);
                rowTopLeft += new Vector2(0, rowTotalHeight);
                tableHeight += rowTotalHeight;
            }

            positionAfter = topLeft + new Vector2(0, tableHeight);
        }
    }

    public class Column
    {
        public TextAlignment Alignment = TextAlignment.LEFT;
        public float FreeSpace = 0;
        public string Name;
    }

    public class Row : List<Cell>
    {
    }

    public struct Cell
    {
        public string Value;
        public Color Color;

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public Cell(string value, Color color)
        {
            Value = value;
            Color = color;
        }

        public Cell(string value)
        {
            Value = value;
            Color = Color.White;
        }
    }
}