using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Linq;
using AvaloniaMerge.Gui.ViewModels;

namespace AvaloniaMerge.Gui;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private async void OnBrowseLeftFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择左侧文件"
        });

        if (files.Any())
        {
            ViewModel.LeftPath = files[0].Path.LocalPath;
        }
    }

    private async void OnBrowseRightFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择右侧文件"
        });

        if (files.Any())
        {
            ViewModel.RightPath = files[0].Path.LocalPath;
        }
    }

    private async void OnBrowseLeftFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择左侧目录"
        });

        if (folders.Any())
        {
            ViewModel.LeftPath = folders[0].Path.LocalPath;
        }
    }

    private async void OnBrowseRightFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择右侧目录"
        });

        if (folders.Any())
        {
            ViewModel.RightPath = folders[0].Path.LocalPath;
        }
    }

    private async void OnCompare(object? sender, RoutedEventArgs e)
    {
        await ViewModel.CompareAsync();
    }

    private void OnSwap(object? sender, RoutedEventArgs e)
    {
        ViewModel.SwapPaths();
    }

    private async void OnCopyToLeft(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menu && menu.Tag is DirectoryItemViewModel item)
        {
            await ViewModel.CopyRightToLeftAsync(item);
        }
    }

    private async void OnDeleteRight(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menu && menu.Tag is DirectoryItemViewModel item)
        {
            await ViewModel.DeleteRightAsync(item);
        }
    }

    private void OnOpenParentFolders(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menu && menu.Tag is DirectoryItemViewModel item)
        {
            ViewModel.OpenParentFolders(item);
        }
    }

    private void OnDirectoryItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is DirectoryItemViewModel item)
        {
            ViewModel.OpenSingleSideItem(item);
        }
    }
}
