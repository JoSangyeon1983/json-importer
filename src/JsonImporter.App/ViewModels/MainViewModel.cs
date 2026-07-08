using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using JsonImporter.App.Infrastructure;
using JsonImporter.Core.Models;
using JsonImporter.Core.Services;

namespace JsonImporter.App.ViewModels;

/// <summary>
/// 메인 화면 뷰모델. 테이블 목록의 동적 추가/삭제, import 실행, 로그, 설정 영구 저장을 담당합니다.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly SettingsStore _settingsStore;
    private readonly ImportService _importService;
    private bool _isLoading;

    public ObservableCollection<TableEntryViewModel> Tables { get; } = new();

    /// <summary>어셈블리 버전에서 읽어온 표시용 버전 문자열(예: "v1.0.0"). csproj의 &lt;Version&gt;이 원본.</summary>
    public string AppVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "v1.0.0" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    private string _outputRoot = "output";
    public string OutputRoot
    {
        get => _outputRoot;
        set { if (SetProperty(ref _outputRoot, value)) Persist(); }
    }

    private string _log = string.Empty;
    public string Log
    {
        get => _log;
        private set => SetProperty(ref _log, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand AddTableCommand { get; }
    public ICommand RemoveTableCommand { get; }
    public ICommand ImportAllCommand { get; }
    public ICommand ImportOneCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }

    public MainViewModel(SettingsStore settingsStore, ImportService importService)
    {
        _settingsStore = settingsStore;
        _importService = importService;

        AddTableCommand = new RelayCommand(_ => AddTable());
        RemoveTableCommand = new RelayCommand(p => RemoveTable(p as TableEntryViewModel));
        ImportAllCommand = new AsyncRelayCommand(_ => ImportAllAsync(), _ => !IsBusy && Tables.Count > 0);
        ImportOneCommand = new AsyncRelayCommand(p => ImportOneAsync(p as TableEntryViewModel), _ => !IsBusy);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder());

        Tables.CollectionChanged += OnTablesChanged;
        LoadSettings();
    }

    // ---------- 설정 로드/저장 ----------

    private void LoadSettings()
    {
        _isLoading = true;
        try
        {
            var settings = _settingsStore.Load();
            _outputRoot = string.IsNullOrWhiteSpace(settings.OutputRoot) ? "output" : settings.OutputRoot;
            OnPropertyChanged(nameof(OutputRoot));

            Tables.Clear();
            foreach (var table in settings.Tables)
                Tables.Add(new TableEntryViewModel(table));
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Persist()
    {
        if (_isLoading)
            return;

        var settings = new AppSettings
        {
            OutputRoot = OutputRoot,
            Tables = Tables.Select(t => t.ToModel()).ToList(),
        };
        _settingsStore.Save(settings);
    }

    private void OnTablesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (TableEntryViewModel vm in e.OldItems)
                vm.PropertyChanged -= OnEntryChanged;

        if (e.NewItems != null)
            foreach (TableEntryViewModel vm in e.NewItems)
                vm.PropertyChanged += OnEntryChanged;

        Persist();
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Status는 런타임 표시용이므로 저장할 필요가 없습니다.
        if (e.PropertyName == nameof(TableEntryViewModel.Status))
            return;

        Persist();
    }

    // ---------- 테이블 추가/삭제 ----------

    private void AddTable()
    {
        Tables.Add(new TableEntryViewModel(new DataTableEntry($"Table{Tables.Count + 1}", string.Empty)));
    }

    private void RemoveTable(TableEntryViewModel? vm)
    {
        if (vm != null)
            Tables.Remove(vm);
    }

    // ---------- Import ----------

    private async Task ImportAllAsync()
    {
        IsBusy = true;
        try
        {
            var targets = Tables.ToList();
            AppendLog($"=== 전체 Import 시작 ({targets.Count} tables) ===");

            var success = 0;
            foreach (var vm in targets)
            {
                if (await ImportEntryAsync(vm))
                    success++;
            }

            AppendLog($"=== 완료: {success}/{targets.Count} tables ===");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ImportOneAsync(TableEntryViewModel? vm)
    {
        if (vm == null)
            return;

        IsBusy = true;
        try
        {
            await ImportEntryAsync(vm);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ImportEntryAsync(TableEntryViewModel vm)
    {
        vm.Status = "진행 중…";
        var result = await _importService.ImportAsync(vm.ToModel(), OutputRoot, AppendLog);
        vm.Status = result.Success ? "성공" : "실패";

        // 성공 시, 스프레드시트의 시트(탭) 이름을 테이블 이름 칸에 자동으로 채웁니다.
        if (result.Success && !string.IsNullOrWhiteSpace(result.TableName))
            vm.Name = result.TableName!;

        AppendLog(result.Message);
        return result.Success;
    }

    // ---------- 출력 경로 ----------

    private void BrowseOutput()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "출력 폴더 선택" };

        try
        {
            if (!string.IsNullOrWhiteSpace(OutputRoot) && Directory.Exists(OutputRoot))
                dialog.InitialDirectory = Path.GetFullPath(OutputRoot);
        }
        catch
        {
            // 잘못된 경로는 무시하고 기본 위치에서 시작합니다.
        }

        if (dialog.ShowDialog() == true)
            OutputRoot = dialog.FolderName;
    }

    private void OpenOutputFolder()
    {
        try
        {
            var full = Path.GetFullPath(OutputRoot);
            Directory.CreateDirectory(full);
            Process.Start(new ProcessStartInfo { FileName = full, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog($"폴더 열기 실패: {ex.Message}");
        }
    }

    // ---------- 로그 ----------

    private void AppendLog(string message)
    {
        Log += (Log.Length == 0 ? string.Empty : Environment.NewLine) + message;
    }
}
