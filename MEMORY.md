# Память проекта ParrotPet

## Текущий статус
Этап 3 готов: квадратный спрайт 64×64, масштаб, настройки, мультимонитор, процедурные анимации. `dotnet build` — 0 ошибок, 0 предупреждений; запуск с тестовым settings.json (All, Scale 1.5) 6 с без падения. Визуально анимации, переходы между мониторами и окно настроек НЕ проверялись.

## Сделано
- Этап 1: WPF (.NET 8), прозрачное оверлей-окно, трей, Git/GitHub (origin/main).
- Этап 2: FSM (Idle/Walk/Fly/Fall/Dragged), MovementService, BehaviorController (раз в 7–15 с), кадры через CompositionTarget.Rendering, ручной drag.
- Этап 3:
  - Models: SettingsModel (SelectedScreen "Primary"/"Screen N"/"All", Scale 0.5–2.0, BehaviorSpeed 0.5–2.0; Normalize), AnimationPose, PetBehaviorState + Eat.
  - Services/SettingsService: settings.json рядом с exe (в .gitignore); Apply (на лету, без записи), Save, автосохранение при выходе; событие Changed.
  - Services/ScreenService: список мониторов (имя + разрешение), границы в DIP (px / GetDpiForSystem), кэш 1 с. Один монитор — его WorkingArea; «Все экраны» — SystemParameters.VirtualScreen*, «земля» = низ рабочей области монитора под центром попугая. Отключённый монитор → откат на основной.
  - MovementService: размер = 64 × Scale (квадрат), BehaviorSpeed влияет на ходьбу и полёт; OnSettingsChanged возвращает попугая в границы (при смене монитора «роняет» в его центр); состояние Eat; событие Landed.
  - Services/AnimationService: Walk — качание ±5°, Fly — пульсация Y/X 6 Гц, Land — затухающая пружина squash&stretch, Eat — 3 наклона за 1.8 с; плавное сглаживание поз.
  - MainWindow: окно 64×Scale; ScaleTransform (LayoutTransform) для масштаба, TransformGroup (squash → rotate → flip) вокруг нижнего центра.
  - Views/SettingsWindow: монитор (ComboBox), размер и скорость (Slider), «Применить» / «Сохранить»; тема по настройке Windows (фон и текст; сами контролы системные).
  - Трей: «Показать/Скрыть», «Покормить», «Настройки...», «Выход».

## Окружение
- SDK 8.0.426 в `%USERPROFILE%\.dotnet` (не в PATH). Сборка: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.
- Экзешник: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish` (нужен .NET 8 Desktop Runtime; publish/ в .gitignore). После этапа 3 exe ещё не пересобирался.

## Дальше
- Проверить вручную: смену монитора, режим «Все экраны» при разной высоте таскбаров/разрешениях, DPI-масштаб на втором мониторе.
- Заменить тестовую фигуру на спрайт.