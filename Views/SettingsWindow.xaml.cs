using System.Windows;
using ParrotPet.Models;
using ParrotPet.Services;

namespace ParrotPet.Views;

/// <summary>Окно настроек: кличка, монитор, размер и скорость. «Применить» — на лету, «Сохранить» — ещё и в файл.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly PetProfile _profile;
    private readonly SettingsModel _draft;

    public SettingsWindow(SettingsService settings, ScreenService screens, PetProfile profile)
    {
        _settings = settings;
        _profile = profile;
        _draft = settings.Current.Clone();

        ThemeHelper.Apply(this);
        InitializeComponent();

        NameBox.MaxLength = PetProfile.MaxNameLength;
        NameBox.Text = profile.Name;
        ScreenCombo.ItemsSource = screens.GetScreens();
        DataContext = _draft;
    }

    private void Apply()
    {
        _profile.Name = NameBox.Text; // пустое имя игнорируется профилем
        NameBox.Text = _profile.Name;
        _settings.Apply(_draft);
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => Apply();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Apply();
        _settings.Save();
        Close();
    }
}