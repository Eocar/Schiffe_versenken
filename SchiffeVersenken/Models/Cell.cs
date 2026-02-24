using System.ComponentModel;

namespace SchiffeVersenken.Models
{
    public enum CellState { Empty, Ship, Hit, Miss, Sunk }

    public class Cell : INotifyPropertyChanged
    {
        private CellState _state = CellState.Empty;
        public int X { get; }
        public int Y { get; }
        public CellState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(nameof(State)); }
        }
        public Cell(int x, int y) { X = x; Y = y; }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
