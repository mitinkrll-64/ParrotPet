# Память проекта ParrotPet

## Текущий статус
Этап 2 готов: физика экрана, посадка на панель задач и автономное поведение. `dotnet build` — 0 ошибок, 0 предупреждений; приложение запускается (4 с без падения). Поведение вживую (полёт, падение, перетаскивание) глазами не проверялось.

## Сделано
- Этап 1: WPF (.NET 8), прозрачное оверлей-окно, трей (меню «Показать/Скрыть», «Выход»), Git/GitHub (origin/main).
- Models: ParrotState, PetBehaviorState (Idle, Walk, Fly, Fall, Dragged).
- Services/MovementService: физика без привязки к UI. «Земля» = SystemParameters.WorkArea.Bottom - высота спрайта (читается каждый кадр, следует за панелью задач). Walk 70 px/с с разворотом ScaleX 1/-1; Fly — квадратичная Безье со smoothstep 1.6 с + парение 3–6 с, затем Fall; Fall — гравитация 1800 px/с², отскок с затуханием 0.35; Dragged.
- Services/BehaviorController: DispatcherTimer, раз в 7–15 с случайно: сидеть / пройти 100–200 px / взлететь.
- Views/MainWindow: кадры через CompositionTarget.Rendering (dt ограничен 50 мс), ручное перетаскивание с захватом мыши (вместо DragMove, чтобы физика владела позицией). Трей не менялся.

## Окружение
- SDK 8.0.426 в `%USERPROFILE%\.dotnet` (не в PATH). Сборка: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.
- Экзешник: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish` (нужен .NET 8 Desktop Runtime; папка publish в .gitignore).

## Дальше
- Проверить вручную поведение и подобрать константы (скорость, высота дуги, гравитация).
- Работа только с основным монитором (SystemParameters.WorkArea); мультимонитор не поддержан.
- Заменить тестовую фигуру на спрайт, добавить анимации.