using System.Collections.Generic;
using System.Linq;

namespace SchiffeVersenken.Models
{
    public class Ship
    {
        public int Size { get; }
        public List<Cell> Cells { get; } = new();
        public bool IsSunk => Cells.All(c => c.State == CellState.Hit || c.State == CellState.Sunk);
        public Ship(int size) { Size = size; }
    }
}
