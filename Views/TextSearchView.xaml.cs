using MZResourceManager.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace MZResourceManager.Views;

public partial class TextSearchView : UserControl
{
    public TextSearchView() => InitializeComponent();

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TextSearchViewModel vm)
            vm.TriggerSearch();
    }
}
