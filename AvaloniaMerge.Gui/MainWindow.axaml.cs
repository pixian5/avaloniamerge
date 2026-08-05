using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
            if (!await ConfirmDeleteAsync(item))
            {
                return;
            }

            await ViewModel.DeleteRightAsync(item);
        }
    }

    private async Task<bool> ConfirmDeleteAsync(DirectoryItemViewModel item)
    {
        var dialog = new Window
        {
            Title = "确认删除",
            Width = 440,
            Height = 190,
            MinWidth = 360,
            MinHeight = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = item.IsDirectory
            ? $"确定删除右侧目录及其全部内容吗？\n{item.DisplayPath}"
            : $"确定删除右侧文件吗？\n{item.DisplayPath}";
        var cancel = new Button { Content = "取消", HorizontalContentAlignment = HorizontalAlignment.Center };
        var confirm = new Button { Content = "删除", HorizontalContentAlignment = HorizontalAlignment.Center };
        var result = false;

        cancel.Click += (_, _) => dialog.Close();
        confirm.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancel, confirm }
                }
            }
        };

        await dialog.ShowDialog(this);
        return result;
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
