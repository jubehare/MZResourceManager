using System.Windows;
using MZResourceManager.ViewModels;

namespace MZResourceManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
