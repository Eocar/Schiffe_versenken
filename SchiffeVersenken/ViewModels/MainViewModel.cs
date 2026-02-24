using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SchiffeVersenken.Models;
using SchiffeVersenken.Network;

namespace SchiffeVersenken.ViewModels
{
    public enum GamePhase { Menu, Setup, WaitingForOpponent, Playing, GameOver }

    public class MainViewModel : INotifyPropertyChanged
    {
        private GamePhase _phase = GamePhase.Menu;
        private string _statusText = "Willkommen bei Schiffe Versenken!";
        private bool _isMyTurn;
        private bool _isHost;
        private bool _isHorizontal = true;
        private int _currentShipIndex;
        private string _hostPort = "12345";
        private string _connectHost = "localhost";
        private string _connectPort = "12345";
        private string _gameOverMessage = "";
        private int _enemyHitCount;
        private bool _opponentReady;

        public GameBoard OwnBoard { get; } = new();
        public GameBoard EnemyBoard { get; } = new();

        public ObservableCollection<Cell> OwnCells { get; } = new();
        public ObservableCollection<Cell> EnemyCells { get; } = new();

        private NetworkManager? _network;

        public GamePhase Phase
        {
            get => _phase;
            set { _phase = value; OnPropertyChanged(nameof(Phase)); UpdatePhaseProperties(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public bool IsMyTurn
        {
            get => _isMyTurn;
            set { _isMyTurn = value; OnPropertyChanged(nameof(IsMyTurn)); }
        }

        public bool IsHorizontal
        {
            get => _isHorizontal;
            set { _isHorizontal = value; OnPropertyChanged(nameof(IsHorizontal)); OnPropertyChanged(nameof(OrientationText)); }
        }

        public string OrientationText => IsHorizontal ? "Horizontal" : "Vertikal";

        public string HostPort
        {
            get => _hostPort;
            set { _hostPort = value; OnPropertyChanged(nameof(HostPort)); }
        }

        public string ConnectHost
        {
            get => _connectHost;
            set { _connectHost = value; OnPropertyChanged(nameof(ConnectHost)); }
        }

        public string ConnectPort
        {
            get => _connectPort;
            set { _connectPort = value; OnPropertyChanged(nameof(ConnectPort)); }
        }

        public string GameOverMessage
        {
            get => _gameOverMessage;
            set { _gameOverMessage = value; OnPropertyChanged(nameof(GameOverMessage)); }
        }

        public int CurrentShipSize => _currentShipIndex < GameBoard.ShipSizes.Length
            ? GameBoard.ShipSizes[_currentShipIndex] : 0;

        public int ShipsRemaining => GameBoard.ShipSizes.Length - _currentShipIndex;

        public bool IsMenuPhase => Phase == GamePhase.Menu;
        public bool IsSetupPhase => Phase == GamePhase.Setup;
        public bool IsWaitingPhase => Phase == GamePhase.WaitingForOpponent;
        public bool IsPlayingPhase => Phase == GamePhase.Playing;
        public bool IsGameOverPhase => Phase == GamePhase.GameOver;

        public ICommand HostGameCommand { get; }
        public ICommand ConnectGameCommand { get; }
        public ICommand ToggleOrientationCommand { get; }
        public ICommand UndoShipCommand { get; }
        public ICommand PlaceCellCommand { get; }
        public ICommand AttackCellCommand { get; }
        public ICommand BackToMenuCommand { get; }

        public MainViewModel()
        {
            HostGameCommand = new RelayCommand(_ => HostGame());
            ConnectGameCommand = new RelayCommand(_ => ConnectGame());
            ToggleOrientationCommand = new RelayCommand(_ => IsHorizontal = !IsHorizontal);
            UndoShipCommand = new RelayCommand(_ => UndoShip(), _ => IsSetupPhase && OwnBoard.Ships.Count > 0);
            PlaceCellCommand = new RelayCommand(p => PlaceShipAt(p!), p => IsSetupPhase);
            AttackCellCommand = new RelayCommand(p => AttackCell(p!), p => IsPlayingPhase && IsMyTurn);
            BackToMenuCommand = new RelayCommand(_ => BackToMenu());

            InitializeCells();
        }

        private void InitializeCells()
        {
            OwnCells.Clear();
            EnemyCells.Clear();
            for (int y = 0; y < GameBoard.Size; y++)
                for (int x = 0; x < GameBoard.Size; x++)
                {
                    OwnCells.Add(OwnBoard.Cells[x, y]);
                    EnemyCells.Add(EnemyBoard.Cells[x, y]);
                }
        }

        private void UpdatePhaseProperties()
        {
            OnPropertyChanged(nameof(IsMenuPhase));
            OnPropertyChanged(nameof(IsSetupPhase));
            OnPropertyChanged(nameof(IsWaitingPhase));
            OnPropertyChanged(nameof(IsPlayingPhase));
            OnPropertyChanged(nameof(IsGameOverPhase));
        }

        private void HostGame()
        {
            if (!int.TryParse(HostPort, out int port)) { StatusText = "Ungültiger Port."; return; }
            _isHost = true;
            _opponentReady = false;
            Phase = GamePhase.Setup;
            StatusText = $"Platzieren Sie Ihre Schiffe. Warte auf Verbindung auf Port {port}...";

            _network = new NetworkManager();
            _network.MessageReceived += OnMessageReceived;
            _network.Connected += OnConnected;
            _network.Disconnected += OnDisconnected;

            _ = Task.Run(async () =>
            {
                try { await _network.HostAsync(port); }
                catch (Exception ex) { Application.Current.Dispatcher.Invoke(() => StatusText = $"Fehler: {ex.Message}"); }
            });
        }

        private void ConnectGame()
        {
            if (!int.TryParse(ConnectPort, out int port)) { StatusText = "Ungültiger Port."; return; }
            _isHost = false;
            _opponentReady = false;
            Phase = GamePhase.Setup;
            StatusText = $"Platzieren Sie Ihre Schiffe. Verbinde mit {ConnectHost}:{port}...";

            _network = new NetworkManager();
            _network.MessageReceived += OnMessageReceived;
            _network.Connected += OnConnected;
            _network.Disconnected += OnDisconnected;

            _ = Task.Run(async () =>
            {
                try { await _network.ConnectAsync(ConnectHost, port); }
                catch (Exception ex) { Application.Current.Dispatcher.Invoke(() => { StatusText = $"Verbindungsfehler: {ex.Message}"; Phase = GamePhase.Menu; }); }
            });
        }

        private void OnConnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Phase == GamePhase.WaitingForOpponent)
                    StartGame();
                else
                    StatusText = _isHost
                        ? "Gegner verbunden! Platzieren Sie Ihre Schiffe."
                        : "Verbunden! Platzieren Sie Ihre Schiffe.";
            });
        }

        private void OnDisconnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Phase != GamePhase.GameOver && Phase != GamePhase.Menu)
                {
                    Phase = GamePhase.GameOver;
                    GameOverMessage = "Verbindung getrennt.";
                    StatusText = "Verbindung getrennt.";
                }
            });
        }

        private void OnMessageReceived(string message)
        {
            Application.Current.Dispatcher.Invoke(() => HandleMessage(message));
        }

        private void HandleMessage(string message)
        {
            var parts = message.Split(':');
            switch (parts[0])
            {
                case "READY":
                    _opponentReady = true;
                    if (Phase == GamePhase.WaitingForOpponent)
                        StartGame();
                    break;
                case "ATTACK":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int ax) && int.TryParse(parts[2], out int ay))
                        HandleIncomingAttack(ax, ay);
                    break;
                case "RESULT":
                    if (parts.Length >= 4)
                        HandleAttackResult(parts[1], parts[2], parts[3]);
                    break;
            }
        }

        private void StartGame()
        {
            Phase = GamePhase.Playing;
            IsMyTurn = _isHost;
            StatusText = IsMyTurn ? "Sie sind dran! Wählen Sie ein Feld zum Angreifen." : "Gegner ist dran. Bitte warten...";
        }

        private void PlaceShipAt(object param)
        {
            if (param is not Cell cell) return;
            if (_currentShipIndex >= GameBoard.ShipSizes.Length) return;

            int size = GameBoard.ShipSizes[_currentShipIndex];
            var placed = OwnBoard.PlaceShip(cell.X, cell.Y, size, IsHorizontal);
            if (placed == null)
            {
                StatusText = "Ungültige Position! Versuchen Sie es erneut.";
                return;
            }

            _currentShipIndex++;
            OnPropertyChanged(nameof(CurrentShipSize));
            OnPropertyChanged(nameof(ShipsRemaining));

            if (_currentShipIndex >= GameBoard.ShipSizes.Length)
            {
                Phase = GamePhase.WaitingForOpponent;
                StatusText = "Alle Schiffe gesetzt! Warte auf Gegner...";
                _ = _network?.SendAsync("READY");
                if (_opponentReady)
                    StartGame();
            }
            else
            {
                int next = GameBoard.ShipSizes[_currentShipIndex];
                StatusText = $"Schiff ({size} Felder) gesetzt. Nächstes: {next} Felder. Noch {ShipsRemaining} Schiffe.";
            }
        }

        private void UndoShip()
        {
            if (OwnBoard.RemoveLastShip())
            {
                _currentShipIndex--;
                OnPropertyChanged(nameof(CurrentShipSize));
                OnPropertyChanged(nameof(ShipsRemaining));
                StatusText = $"Schiff entfernt. Platzieren Sie ein {CurrentShipSize}-Felder Schiff.";
            }
        }

        private async void AttackCell(object param)
        {
            if (param is not Cell cell) return;
            if (!IsMyTurn) return;
            if (cell.State != CellState.Empty) return;
            if (_network == null) return;

            IsMyTurn = false;
            StatusText = "Warte auf Ergebnis...";
            await _network.SendAsync($"ATTACK:{cell.X}:{cell.Y}");
        }

        private async void HandleIncomingAttack(int x, int y)
        {
            if (_network == null) return;
            string result = OwnBoard.ReceiveAttack(x, y);
            await _network.SendAsync($"RESULT:{x}:{y}:{result}");

            if (OwnBoard.AllShipsSunk)
            {
                Phase = GamePhase.GameOver;
                GameOverMessage = "Sie haben verloren! Alle Ihre Schiffe wurden versenkt.";
            }
            else if (result == "hit" || result == "sunk")
            {
                StatusText = "Gegner hat getroffen! Gegner ist wieder dran.";
            }
            else
            {
                IsMyTurn = true;
                StatusText = "Gegner hat verfehlt! Sie sind dran.";
            }
        }

        private void HandleAttackResult(string xs, string ys, string result)
        {
            if (!int.TryParse(xs, out int x) || !int.TryParse(ys, out int y)) return;

            var cell = EnemyBoard.Cells[x, y];
            switch (result)
            {
                case "hit":
                    cell.State = CellState.Hit;
                    _enemyHitCount++;
                    IsMyTurn = true;
                    StatusText = "Treffer! Nochmal schießen!";
                    break;
                case "sunk":
                    cell.State = CellState.Sunk;
                    _enemyHitCount++;
                    IsMyTurn = true;
                    StatusText = "Versenkt! Nochmal schießen!";
                    break;
                case "miss":
                    cell.State = CellState.Miss;
                    IsMyTurn = false;
                    StatusText = "Daneben! Gegner ist dran.";
                    break;
            }

            // Win: all 30 enemy ship cells hit/sunk
            if (_enemyHitCount >= GameBoard.ShipSizes.Sum())
            {
                Phase = GamePhase.GameOver;
                GameOverMessage = "Sie haben gewonnen! Alle feindlichen Schiffe versenkt!";
            }
        }

        private void BackToMenu()
        {
            _network?.Dispose();
            _network = null;
            _currentShipIndex = 0;
            _isHost = false;
            _opponentReady = false;
            _enemyHitCount = 0;
            IsMyTurn = false;
            IsHorizontal = true;

            for (int x = 0; x < GameBoard.Size; x++)
                for (int y = 0; y < GameBoard.Size; y++)
                {
                    OwnBoard.Cells[x, y].State = CellState.Empty;
                    EnemyBoard.Cells[x, y].State = CellState.Empty;
                }
            OwnBoard.Ships.Clear();
            EnemyBoard.Ships.Clear();
            OwnCells.Clear();
            EnemyCells.Clear();
            InitializeCells();

            Phase = GamePhase.Menu;
            StatusText = "Willkommen bei Schiffe Versenken!";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
