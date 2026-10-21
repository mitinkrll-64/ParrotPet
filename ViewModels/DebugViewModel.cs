using System;
using System.Windows.Input;
using System.Windows.Threading;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.ViewModels;

/// <summary>ViewModel окна разработчика: ручной запуск анимаций, правка профиля, тестовые действия.</summary>
public class DebugViewModel : ViewModelBase, IDisposable
{
    private readonly MainViewModel _main;
    private readonly PetProfile _profile;
    private readonly DispatcherTimer _stateTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public DebugViewModel(MainViewModel main, SaveService save)
    {
        _main = main;
        _profile = main.Profile;

        // Анимации: каждая команда безопасно прерывает текущее действие FSM
        PlayIdle = new RelayCommand(main.PlayIdle);
        PlayWalkLeft = new RelayCommand(() => main.PlayWalk(-1));
        PlayWalkRight = new RelayCommand(() => main.PlayWalk(1));
        PlayFly = new RelayCommand(main.PlayFly);
        PlayFall = new RelayCommand(main.PlayFall);
        PlayEat = new RelayCommand(main.PlayEat);
        PlayPreen = new RelayCommand(main.PlayPreen);
        PlayHeart = new RelayCommand(main.PlayHeart);

        // Состояние
        RandomName = new RelayCommand(() => Name = PetProfile.RandomName());
        AgeMinus = new RelayCommand(() => AgeDays--);
        AgePlus = new RelayCommand(() => AgeDays++);

        // Тесты
        DropSeed = new RelayCommand(main.Feed);
        ResetSave = new RelayCommand(save.Reset);
        Teleport = new RelayCommand(main.TeleportToCenter);

        _profile.Changed += OnProfileChanged;
        _stateTimer.Tick += (_, _) => Raise(nameof(CurrentState));
        _stateTimer.Start();
    }

    public ICommand PlayIdle { get; }
    public ICommand PlayWalkLeft { get; }
    public ICommand PlayWalkRight { get; }
    public ICommand PlayFly { get; }
    public ICommand PlayFall { get; }
    public ICommand PlayEat { get; }
    public ICommand PlayPreen { get; }
    public ICommand PlayHeart { get; }
    public ICommand RandomName { get; }
    public ICommand AgeMinus { get; }
    public ICommand AgePlus { get; }
    public ICommand DropSeed { get; }
    public ICommand ResetSave { get; }
    public ICommand Teleport { get; }

    public string Name
    {
        get => _profile.Name;
        set
        {
            _profile.Name = value;
            Raise(); // показываем нормализованное значение (например, обрезанное)
        }
    }

    public bool IsMale
    {
        get => _profile.Gender == PetGender.Male;
        set { if (value) _profile.Gender = PetGender.Male; }
    }

    public bool IsFemale
    {
        get => _profile.Gender == PetGender.Female;
        set { if (value) _profile.Gender = PetGender.Female; }
    }

    public double Hunger
    {
        get => _profile.Hunger;
        set => _profile.Hunger = value;
    }

    public int AgeDays
    {
        get => _profile.AgeDays;
        set
        {
            _profile.AgeDays = value;
            Raise();
        }
    }

    /// <summary>Включено ли случайное поведение (раз в 7–15 секунд).</summary>
    public bool AutoBehavior
    {
        get => _main.Behavior.Enabled;
        set
        {
            _main.Behavior.Enabled = value;
            Raise();
        }
    }

    public string CurrentState => $"Состояние: {_main.Movement.State}"
        + (_main.Movement.State == PetBehaviorState.Fly ? $" / {_main.Movement.Phase}" : "");

    /// <summary>Профиль изменился (в том числе из другого места) — обновляем все поля.</summary>
    private void OnProfileChanged()
    {
        Raise(nameof(Name));
        Raise(nameof(IsMale));
        Raise(nameof(IsFemale));
        Raise(nameof(Hunger));
        Raise(nameof(AgeDays));
    }

    public void Dispose()
    {
        _stateTimer.Stop();
        _profile.Changed -= OnProfileChanged;
    }
}