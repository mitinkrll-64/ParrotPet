# ParrotPet Guidelines

## Стек и технологии
- C#, .NET 8, WPF (Windows). Без игровых движков.
- Оконный режим: Transparent Overlay (Topmost, AllowsTransparency).
- Иконка в трее: NotifyIcon с контекстным меню (WindowsForms подключён только ради неё).

## Правила архитектуры
- Модульная структура: папки Models, ViewModels, Views, Services.
- Код разбит на небольшие файлы с комментариями на русском языке.
- Все файлы проекта хранятся строго внутри этой рабочей папки.
- Физика и логика не знают про окна: `MovementService`, `FoodService`, `AnimationService` только считают, окна (`MainWindow`, `FoodWindow`) применяют результат каждый кадр через `CompositionTarget.Rendering`.
- Координаты везде в DIP. Логический хитбокс попугая — квадрат 64×64 (`MovementService.BaseSize`), холст рисования 96×96 с полями по краям, чтобы клюв и хвост не обрезались.
- Анимации и скины развязаны: анимации крутят только `RenderTransform` контейнеров частей тела в `MainWindow.xaml`; цвета и картинки частей берутся из ресурсов `Skin.*` (`SkinService` + `PixelSpriteBuilder`). Анимация не должна знать, какой скин надет.
- Спрайт — процедурный пиксель-арт в стиле референса `Assets/Skins/skins.jpg` (силуэты многоугольниками, автоконтур). Скины — данные в `SkinService.All`.
- Любое ручное действие из Debug-окна должно безопасно прерывать текущее состояние FSM (`MovementService.Interrupt`) и откладывать случайное поведение (`BehaviorController.Reschedule`).

## Структура
- Models: `PetProfile` (имя, пол, день рождения, сытость, `ActiveSkinId`), `PetSkin`, `SettingsModel`, `PetBehaviorState`, `FlightPhase`, `LandingKind`, `IdleAction`, `AnimationPose`.
- Services: `MovementService` (FSM и физика), `BehaviorController` (случайные действия), `AnimationService` (позы), `FoodService`, `SkinService`, `PixelSpriteBuilder`, `ScreenService` (мониторы), `WindowSurfaceService` (окна, на которые садится попугай), `SettingsService` и `SaveService` (JSON), `TrayService`, `HotkeyService` (Ctrl+Shift+D), `ThemeHelper`.
- ViewModels: `MainViewModel`, `DebugViewModel`. Views: главное окно, окно зерна, настройки, Debug.
- Данные пользователя рядом с exe: `settings.json`, `save_data.json` (в .gitignore). Сборка в `publish/` тоже в .gitignore.

## Сборка и проверка
- SDK .NET 8 лежит в `%USERPROFILE%\.dotnet` (в PATH не добавлен): `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.
- После каждого этапа пересобирать exe и сообщать путь: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish`.
- Без запуска GUI проверять так: консольный проект в scratchpad с `ProjectReference` на `ParrotPet.csproj`, `new App().InitializeComponent()`, `new MainWindow()`, рендер `Content` в `RenderTargetBitmap`; логику прогонять симуляцией `MovementService` с шагом 1/60 с.
- Сборка должна проходить с 0 ошибок и 0 предупреждений.

## Git
- Ветка `main`, remote `origin` — https://github.com/mitinkrll-64/ParrotPet.git. Сообщения коммитов в стиле `feat: ...` / `docs: ...`.
- Перед пушем смотреть `git status`: в рабочей папке могут лежать изменения из других сессий, их нужно прочитать до коммита.

## Управление памятью
- Перед завершением любой задачи обновлять файл MEMORY.md.