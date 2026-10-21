using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParrotPet.ViewModels;

/// <summary>Базовый класс для ViewModel с уведомлением об изменении свойств.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}