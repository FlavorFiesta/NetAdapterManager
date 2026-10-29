using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetAdapterManager.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    protected ObservableObject() => System.ComponentModel.PropertyChangedEventManager.AddHandler(L.Instance, LanguageChanged, string.Empty);
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(string.Empty);
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
