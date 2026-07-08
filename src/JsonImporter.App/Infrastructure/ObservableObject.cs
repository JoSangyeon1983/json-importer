using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JsonImporter.App.Infrastructure;

/// <summary>INotifyPropertyChanged 기본 구현. 뷰모델의 베이스 클래스.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>값이 실제로 바뀐 경우에만 필드를 갱신하고 알림을 발생시킵니다.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
