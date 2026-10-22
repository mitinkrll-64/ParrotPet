using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.ViewModels;

/// <summary>Кнопка Debug-окна для запуска одного действия в покое.</summary>
public record IdleActionItem(string Title, ICommand Command);

/// <summary>ViewModel окна разработчика: ручной запуск анимаций, правка профиля, тестовые действия.</summary>
public class DebugViewModel : ViewModelBase, IDisposable
{
    private readonly MainViewModel _main;
    private readonly PetProfile _profile;
    private readonly DispatcherTimer _stateTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    private readonly SkinService _skins;

    public DebugViewModel(MainViewModel main, SkinService skins, SaveService save)
    {
        _main = main;
        _skins = skins;
        _profile = main.Profile;

        // Анимации: каждая команда безопасно прерывает текущее действие FSM
        PlayIdle = new RelayCommand(main.PlayIdle);
        IdleActions = new[]
        {
            Item("Чистка крыла", IdleAction.PreenWing), Item("Чистка груди", IdleAction.PreenChest),
            Item("Чистка хвоста", IdleAction.PreenTail), Item("Встряхнуться", IdleAction.Fluff),
            Item("Потянуться", IdleAction.Stretch), Item("Зевок", IdleAction.Yawn),
            Item("Скрип клювом", IdleAction.BeakGrind), Item("Дрёма", IdleAction.Doze),
            Item("Чириканье", IdleAction.Chirp), Item("Вильнуть хвостом", IdleAction.TailWag),
            Item("Осмотреться", IdleAction.LookAround)
        };
        PlayWalkLeft = new RelayCommand(() => main.PlayWalk(-1));
        PlayWalkRight = new RelayCommand(() => main.PlayWalk(1));
        PlayFly = new RelayCommand(main.PlayFly);
        PlayFlyTop = new RelayCommand(main.PlayFlyToWindowTop);
        PlayCling = new RelayCommand(main.PlayFlyToWindowSide);
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

    /// <summary>Кнопки запуска отдельных действий в покое.</summary>
    public IReadOnlyList<IdleActionItem> IdleActions { get; }

    public ICommand PlayIdle { get; }
    public ICommand PlayWalkLeft { get; }
    public ICommand PlayWalkRight { get; }
    public ICommand PlayFly { get; }
    private IdleActionItem Item(string title, IdleAction action) =>
        new(title, new RelayCommand(() => _main.PlayIdleAction(action)));

    public ICommand PlayFlyTop { get; }
    public ICommand PlayCling { get; }
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

    /// <summary>Доступные скины для списка.</summary>
    public System.Collections.Generic.IReadOnlyList<PetSkin> Skins => SkinService.All;

    /// <summary>Id активного скина; смена применяется мгновенно, анимация продолжается.</summary>
    public string ActiveSkinId
    {
        get => _skins.Active.Id;
        set
        {
            if (value is not null) _skins.ApplySkin(value);
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
        + (_main.Movement.State == PetBehaviorState.Fly ? $" / {_main.Movement.Phase} → {_main.Movement.FlyTarget}" : "")
        + (_main.Movement.IsPerched ? " (на окне)" : "")
        + (_main.Animator.CurrentIdleAction != IdleAction.None ? $" · {_main.Animator.CurrentIdleAction}" : "");

    /// <summary>Профиль изменился (в том числе из другого места) — обновляем все поля.</summary>
    private void OnProfileChanged()
    {
        Raise(nameof(Name));
        Raise(nameof(IsMale));
        Raise(nameof(IsFemale));
        Raise(nameof(Hunger));
        Raise(nameof(AgeDays));
        Raise(nameof(ActiveSkinId));
    }

    public void Dispose()
    {
        _stateTimer.Stop();
        _profile.Changed -= OnProfileChanged;
    }
}