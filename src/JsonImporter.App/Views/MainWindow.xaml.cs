using System.Windows;
using System.Windows.Controls;
using JsonImporter.App.ViewModels;
using JsonImporter.Core.Services;

namespace JsonImporter.App.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// Core 서비스를 조립해 <see cref="MainViewModel"/>을 구성합니다(간단한 수동 DI).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settingsStore = new SettingsStore(SettingsStore.DefaultPath());
        var importService = new ImportService(new TextDownloader(), new JsonFileSaver());
        DataContext = new MainViewModel(settingsStore, importService);
    }

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 새 로그가 추가되면 항상 마지막 줄로 스크롤합니다.
        LogBox.ScrollToEnd();
    }
}
