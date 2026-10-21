# Память проекта ParrotPet

## Текущий статус
Этап 6 готов: универсальная система скинов (6 пресетов), отвязанная от анимаций. `dotnet build` — 0 ошибок, 0 предупреждений; приложение запускается и пишет ActiveSkinId в save_data.json. Проверено рендером настоящего MainWindow в PNG: все 6 скинов × 3 позы (покой, полёт, клевание/самка) выглядят корректно, при смене скина значения трансформаций анимаций не меняются (0 расхождений). Вживую смену скина на летящем попугае и окна НЕ проверяли.

## Сделано
- Этапы 1–5: оверлей, трей, FSM, физика, настройки, мультимонитор, масштаб, волнистый попугайчик, полёт, зерно, анимации, PetProfile + save_data.json, Debug-окно (F12 / Ctrl+Shift+D / трей).
- Этап 6:
  - Models/PetSkin: Id, DisplayName, IsUnlocked, цвета (hex): Body, BodyShade, Head, Stripes, Wing, WingStripes, Beak, Cheeks, Cheeks2, Tail1–3, Feet. Цвет восковицы зависит от пола, не от скина.
  - Services/SkinService: реестр из 6 скинов по референсу Assets/Skins/skins.jpg (в референсе 8 птиц; взяты: Классический зелёный (id classic-green, по умолчанию), Небесно-голубой sky-blue, Лютино lutino, Фиолетовый опалин violet-opaline, Бирюзово-серый teal-grey, Закатный оранжевый sunset-orange; не вошли оранжевоголовый зелёный и коралловый — добавляются одной записью). ApplySkin(id) подменяет кисти в Application.Resources (Skin.*), запоминает выбор в PetProfile.ActiveSkinId. Неизвестный/закрытый скин → стандартный; изменение ActiveSkinId из профиля (Reset Save) подтягивает внешний вид.
  - Архитектура: в MainWindow.xaml цвета частей — DynamicResource Skin.*; анимации крутят только RenderTransform контейнеров BodyContainer/HeadContainer/WingContainer/TailContainer и скина не знают. Смена скина не сбрасывает ни позу, ни позицию, ни состояние FSM.
  - UI: ComboBox скинов с мини-иконкой (общий шаблон SkinItemTemplate и HexBrushConverter в App.xaml) в SettingsWindow (применяется кнопками «Применить/Сохранить») и в DebugWindow (применяется сразу).
  - PetProfile.ActiveSkinId сохраняется в save_data.json; старые сохранения без поля получают classic-green.

## Окружение
- SDK 8.0.426 в `%USERPROFILE%\.dotnet` (не в PATH). Сборка: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.
- Правило пользователя: после каждого этапа пересобирать exe: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish` (нужен .NET 8 Desktop Runtime; publish/ в .gitignore).
- Приём для проверки без запуска GUI: консольный проект с ProjectReference на ParrotPet.csproj, new App().InitializeComponent(), new MainWindow(), рендер Content в RenderTargetBitmap.

## Дальше
- Посмотреть вживую смену скина во время полёта/ходьбы; при желании добавить ещё скины (запись в SkinService.All) и магазин (IsUnlocked).