using System.Windows;
using MZResourceManager.ViewModels;

namespace MZResourceManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        Loaded += async (_, _) => await vm.TryAutoLoadAsync();
    }
}
