using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SchiffeVersenken.Models
{
    public class GameBoard
    {
        public const int Size = 10;
        public Cell[,] Cells { get; } = new Cell[Size, Size];
        public ObservableCollection<Ship> Ships { get; } = new();

        // Ships to place: 1x5, 2x4, 3x3, 4x2
        public static readonly int[] ShipSizes = { 5, 4, 4, 3, 3, 3, 2, 2, 2, 2 };

        public GameBoard()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    Cells[x, y] = new Cell(x, y);
        }

        public bool CanPlaceShip(int x, int y, int size, bool horizontal)
        {
            if (horizontal && x + size > Size) return false;
            if (!horizontal && y + size > Size) return false;

            for (int i = 0; i < size; i++)
            {
                int cx = horizontal ? x + i : x;
                int cy = horizontal ? y : y + i;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && nx < Size && ny >= 0 && ny < Size)
                            if (Cells[nx, ny].State == CellState.Ship)
                                return false;
                    }
            }
            return true;
        }

        public Ship? PlaceShip(int x, int y, int size, bool horizontal)
        {
            if (!CanPlaceShip(x, y, size, horizontal)) return null;
            var ship = new Ship(size);
            for (int i = 0; i < size; i++)
            {
                int cx = horizontal ? x + i : x;
                int cy = horizontal ? y : y + i;
                Cells[cx, cy].State = CellState.Ship;
                ship.Cells.Add(Cells[cx, cy]);
            }
            Ships.Add(ship);
            return ship;
        }

        public bool RemoveLastShip()
        {
            if (Ships.Count == 0) return false;
            var ship = Ships[^1];
            foreach (var cell in ship.Cells)
                cell.State = CellState.Empty;
            Ships.RemoveAt(Ships.Count - 1);
            return true;
        }

        // Returns: "hit", "miss", "sunk", "already_shot"
        public string ReceiveAttack(int x, int y)
        {
            var cell = Cells[x, y];
            if (cell.State == CellState.Hit || cell.State == CellState.Miss || cell.State == CellState.Sunk)
                return "already_shot";

            if (cell.State == CellState.Ship)
            {
                cell.State = CellState.Hit;
                var ship = Ships.FirstOrDefault(s => s.Cells.Contains(cell));
                if (ship != null && ship.IsSunk)
                {
                    foreach (var c in ship.Cells) c.State = CellState.Sunk;
                    return "sunk";
                }
                return "hit";
            }

            cell.State = CellState.Miss;
            return "miss";
        }

        public bool AllShipsSunk => Ships.Count > 0 && Ships.All(s => s.IsSunk);
    }
}
