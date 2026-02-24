using System.Windows;
using SchiffeVersenken.ViewModels;

namespace SchiffeVersenken;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}