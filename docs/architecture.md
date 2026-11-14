# Архітектура CafeOrders 1.0

## 1. Шари та правило залежностей

```
CafeOrders.App  ──────►  CafeOrders.Core  ◄──────  CafeOrders.Tests
(консоль, JSON,          (домен, правила,           (xUnit, фейки,
 файловий журнал,         сервіс сценаріїв,          стаби, шпигуни)
 композиційний корінь)    інтерфейси сховищ)
```

- **Core** — бібліотека без залежностей від консолі, файлів і конкретного журналу.
  Знає лише `Microsoft.Extensions.Logging.Abstractions`.
- **App** — зовнішній шар: читає ввід, реалізує інтерфейси Core (`IMenuCatalog`,
  `IOrderRepository`) через JSON-файли, налаштовує журнал і збирає об’єкти в `Program.cs`.
- **Tests** — посилається на обидва проєкти; App видно для тестів через `InternalsVisibleTo`.

Правило «Core не залежить від App» перевіряє тест
`ArchitectureAndInfrastructureTests.Core_DoesNotReferenceAppOrConsoleLogging`: він
читає список збірок, на які посилається Core, і падає, якщо там з’явиться `CafeOrders.App`
або консольний журнал.

## 2. Модулі Core

| Простір імен | Відповідальність | Ключові типи |
|---|---|---|
| `Domain` | Сутності й інваріанти, переходи станів | `Order`, `OrderLine`, `MenuItem`, `ComboMeal`, `OrderStatus` |
| `Abstractions` | Порти до зовнішнього світу | `IOrderRepository`, `IMenuCatalog`, `IServiceChargePolicy`, `IPricingPolicy` |
| `Pricing` | Профільне правило та знижки | `ServiceChargePolicy`, `RegularPricingPolicy`, `VipPricingPolicy` |
| `Services` | Сценарії UC-01…UC-07, рахунок | `CafeDesk`, `BillCalculator`, `BillSummary`, `OrderService` (ЛР4–ЛР7) |
| `Reporting` | Звіти без вводу-виводу | `PopularityReport`, `PopularityRow`, `OrderCsvReport` |
| `Errors` | Порушення правил і результати | `DomainRuleException`, `Result<T>` |
| `Documents`, `Billing`, `Storage` | Код лабораторних 3–7, збережено для тестів і демо-режимів | `DocumentTotalCalculator`, `Bill`, `InMemoryOrderRepository` |

## 3. Модулі App

| Тип | Роль |
|---|---|
| `Program.cs` | Композиційний корінь і таблиця режимів (`без аргументу` — каса; `add-line`, `logging`, `demo` — режими лабораторних) |
| `Ui/CafeConsole` | Меню команд 1–9; перехоплює `DomainRuleException` і показує «Відхилено: …» |
| `Ui/ConsoleIo` | Барикада вводу: повторює запит, доки значення неправильне |
| `Infrastructure/JsonMenuCatalog` | Читає `menu.json` (позиції, комплекси, стоп-лист) |
| `Infrastructure/JsonOrderRepository` | Зберігає знімки замовлень; при читанні відновлює стан повторенням переходів, тож інваріанти `Order` не обходяться |
| `Infrastructure/FileLoggerProvider` | Журнал у файл: `час;рівень;область;повідомлення` |

## 4. Потік одного сценарію (UC-04, закриття рахунку)

```
Касир → CafeConsole.Pay
          ├─ ConsoleIo.ReadPositiveInt   (барикада: номер)
          ├─ CafeDesk.BillOf → BillCalculator → ServiceChargePolicy
          ├─ ConsoleIo.ReadMoney         (барикада: сума)
          └─ CafeDesk.CloseBill
                ├─ Require(orderId)      → order.exists
                ├─ EnsureStatus(Served)  → order.payable
                ├─ paid < Total          → order.amount + Warning у журнал
                ├─ Order.Pay()           (інваріант переходу)
                └─ IOrderRepository.Save → JsonOrderRepository → orders.local.json
```

## 5. Обробка помилок

| Рівень | Що перевіряється | Механізм |
|---|---|---|
| Барикада (`ConsoleIo`) | Формат: ціле > 0, гроші з крапкою, так/ні, назва стану | Повторний запит, до Core нічого не доходить |
| Сервіс (`CafeDesk`) | Бізнес-правила: стан, стоп-лист, сума оплати | `DomainRuleException(rule, message)` + `Warning` у журнал |
| Домен (`Order`, `ComboMeal`) | Інваріанти, які не мають порушуватися взагалі | `InvalidOperationException`, `ArgumentException` — це вже дефект програми |
| `Program.cs` | Правило, не оброблене меню, або неочікуваний збій | Єдиний верхній обробник: `Warning` і код виходу 1 для правила, `Critical` і код 2 для збою |

## 6. Композиційний корінь

`RunCafe()` у `Program.cs` — єдине місце, де створюються конкретні класи:
`JsonMenuCatalog`, `JsonOrderRepository`, `ServiceChargePolicy`, `BillCalculator`,
`CafeDesk`, `ConsoleIo`, `CafeConsole`, `TimeProvider.System`, фабрика журналів із
консольним і файловим провайдерами. `CafeDesk` отримує лише інтерфейси й `ILogger<T>`,
тому в тестах його збирають із `FakeOrderRepository`, `FakeMenuCatalog` і `NullLogger`.

## 7. Як додати нове правило

Приклад: знижка 10 % гостям-студентам.

1. Додати рядки в таблицю рішень `docs/spec.md` і відповідні `InlineData` у тест — тест червоний.
2. Реалізувати нову політику за інтерфейсом у `Core/Pricing` або розширити `ServiceChargePolicy`.
3. Підключити її в композиційному корені; `CafeDesk` і консоль не змінюються.
