# Память проекта ParrotPet

## Текущий статус
MVP-каркас готов: WPF (.NET 8), прозрачное оверлей-окно и иконка в трее. `dotnet build` — 0 ошибок, 0 предупреждений. Коммит создан локально; пуш в GitHub НЕ выполнен: 403, сохранённые учётные данные Git принадлежат аккаунту fl265e-lang без доступа к mitinkrll-64/ParrotPet.

## Сделано
- Рабочая директория и файлы конфигурации (CLAUDE.md, MEMORY.md).
- Git: ветка main, origin = https://github.com/mitinkrll-64/ParrotPet.git, .gitignore для .NET/VS.
- Проект ParrotPet.csproj (net8.0-windows, UseWPF + UseWindowsForms для NotifyIcon).
- Структура: Models (ParrotState), ViewModels (MainViewModel), Views (MainWindow), Services (TrayService).
- MainWindow: без рамок, Topmost, AllowsTransparency, не в панели задач; тестовый попугай из примитивов; перетаскивание мышью через DragMove.
- Трей: меню «Показать/Скрыть» и «Выход», двойной клик переключает видимость; иконка рисуется в коде.

## Окружение
- На машине был только рантайм .NET; SDK 8.0.426 установлен в `%USERPROFILE%\.dotnet` (не в PATH). Сборка: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build`.

## Дальше
- Запустить приложение и проверить вручную drag и трей (запуск GUI не проверялся, только сборка).
- Заменить тестовую фигуру на спрайт, добавить анимации и поведение питомца.