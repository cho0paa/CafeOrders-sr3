# CafeOrders

![CI](https://github.com/cho0paa/CafeOrders-sr3/actions/workflows/ci.yml/badge.svg)

Консольна каса кав’ярні: замовлення з меню й комплексів, кухня, рахунок із сервісним
збором, оплата, пошук і звіт популярності позицій.

## Автор і варіант

Варіант № 12 «Замовлення та меню кав’ярні». Новіков Артем, група 471.
Дисципліна «Конструювання програмного забезпечення», самостійна робота 3.

## Швидкий старт

Потрібен .NET SDK 8.0 (версію зафіксовано в `global.json`) і Git.

    git clone https://github.com/cho0paa/CafeOrders-sr3.git && cd CafeOrders-sr3
    dotnet build
    dotnet run --project CafeOrders.App

Замовлення зберігаються в `data/orders.local.json`, журнал — у `data/cafe.local.log`
(обидва файли не потрапляють у Git). Як працювати з касою — [docs/user-guide.md](docs/user-guide.md).

## Структура рішення

- `CafeOrders.Core` — бібліотека логіки: домен, правило сервісного збору, сценарії каси, звіти.
- `CafeOrders.App` — консольний застосунок: меню, барикада вводу, JSON-сховище, журнал у файл.
- `CafeOrders.Tests` — модульні тести xUnit бібліотеки й інфраструктури.
- `Legacy`, `Legacy.Tests` — успадкований модуль і його характеризаційні тести (ЛР6).
- `data/menu.json` — умовне меню: 57 позицій, 4 комплекси, стоп-лист.
- `docs/` — [специфікація](docs/spec.md), [архітектура](docs/architecture.md),
  [журнал рішень](docs/decisions.md), [самооцінка](docs/self-check.md).

## Можливості

Сценарії UC-01…UC-07 з [docs/spec.md](docs/spec.md):

1. UC-01 — відкрити замовлення гостя (за столиком чи навинос).
2. UC-02 — додати позицію або комплекс; стоп-лист і невідомі коди відхиляються.
3. UC-03 — передати на кухню й позначити подачу страв.
4. UC-04 — рахунок із сервісним збором (5 % за столиком, +10 % від 1000 грн, не більше 150 грн) і оплата.
5. UC-05 — скасування до подачі страв.
6. UC-06 — пошук за станом, гостем, датою.
7. UC-07 — звіт «популярність позицій» за оплаченими замовленнями.

## Тести й покриття

    dotnet test
    dotnet test CafeOrders.Tests --collect:"XPlat Code Coverage"
    reportgenerator -reports:"CafeOrders.Tests/TestResults/*/coverage.cobertura.xml" -targetdir:coverage "-assemblyfilters:+CafeOrders.Core"

Тестів 119 (106 основних і 13 характеризаційних), усі зелені. Покриття бібліотеки
`CafeOrders.Core`: **92 % рядків, 84,7 % гілок**.

Конвеєр `.github/workflows/ci.yml` на кожен push і pull request перевіряє форматування,
складання Release без попереджень, тести й поріг покриття 70 % рядків бібліотеки.
Те саме локально:

    dotnet format --verify-no-changes && dotnet build -c Release -warnaserror && dotnet test CafeOrders.Tests -c Release /p:CollectCoverage=true "/p:Include=[CafeOrders.Core]*" /p:Threshold=70

## Технології

.NET 8, C# 12, xUnit, coverlet, ReportGenerator, аналізатори Roslyn і StyleCop
(стандарт — [CODING_STANDARD.md](CODING_STANDARD.md), `.editorconfig`),
Microsoft.Extensions.Logging, System.Text.Json, GitHub Actions.

## Обмеження й відомий борг

- Одна каса: файл замовлень не захищено від одночасного запису кількома процесами.
- Оплата лише готівкою; фіскального чека й оплати карткою немає.
- Меню змінюється тільки редагуванням `data/menu.json`, з каси — ні.
- Консольний шар (`Program.cs`, `CafeConsole`) перевірено ручним сценарним запуском, а не автотестами.
- У рішенні лишається код лабораторних 3–7 (`Documents`, `Billing`, `OrderService`), який каса не використовує.
