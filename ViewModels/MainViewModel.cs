using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MZResourceManager.Services;
using System.Windows;

namespace MZResourceManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [RelayCommand]
    private void OpenProject()
    {
        var dlg = new OpenFolderDialog { Title = "Select RPG Maker MZ Game Folder" };
        if (dlg.ShowDialog() != true) return;

        var error = ProjectValidator.GetValidationError(dlg.FolderName);
        if (error != null)
        {
            MessageBox.Show($"{error}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // TODO: Load project
    }
}
