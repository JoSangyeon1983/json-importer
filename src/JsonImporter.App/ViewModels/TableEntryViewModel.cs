using JsonImporter.App.Infrastructure;
using JsonImporter.Core.Models;

namespace JsonImporter.App.ViewModels;

/// <summary>
/// UI에서 편집 가능한 테이블 한 행. <see cref="DataTableEntry"/>를 관찰 가능(observable)하게 감쌉니다.
/// </summary>
public sealed class TableEntryViewModel : ObservableObject
{
    private string _name;
    private string _url;
    private string _status = string.Empty;

    public TableEntryViewModel(DataTableEntry model)
    {
        _name = model.Name;
        _url = model.Url;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    /// <summary>마지막 import 결과 표시용(성공/실패/진행 중). 설정에는 저장되지 않습니다.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DataTableEntry ToModel() => new(Name?.Trim() ?? string.Empty, Url?.Trim() ?? string.Empty);
}
