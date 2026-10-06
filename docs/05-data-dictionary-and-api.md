# 5. Словарь данных и сводка API

Полный контракт — [`src/AggStudentDiscounts.Api/openapi.yaml`](../src/AggStudentDiscounts.Api/openapi.yaml)
(OpenAPI 3.0); интерактивно — Swagger UI на `/swagger` в Development.

## 5.1 Сводка эндпоинтов

| Метод и путь | Доступ | Назначение | Требование |
|---|---|---|---|
| `POST /api/auth/register` | Гость | Регистрация, выдача JWT | FR-01 |
| `POST /api/auth/login` | Гость | Вход, выдача JWT | FR-01 |
| `GET /api/auth/me` | Авториз. | Профиль (с пересчётом курса) | FR-01, BR-06 |
| `GET /api/establishments` | Гость | Каталог: `search`, `district`, `lat/lng/radius`, `sortBy`, `sortOrder`, `page`, `pageSize` | FR-03 |
| `GET /api/establishments/{id}` | Гость | Карточка заведения | FR-03 |
| `GET /api/establishments/{id}/votes` | Гость | Статистика голосов | FR-07 |
| `POST /api/establishments/{id}/vote` | Авториз. | Голос `yes`/`no` | FR-07 |
| `POST /api/applications` | Авториз. | Подать заявку (multipart/form-data) | FR-04 |
| `GET /api/applications` | Авториз. | Мои заявки, фильтр `status` | FR-05 |
| `GET /api/applications/{id}` | Автор / модератор | Детали заявки | FR-05 |
| `DELETE /api/applications/{id}` | Автор | Отменить заявку в статусе «на проверке» | FR-05 |
| `GET /api/admin/applications` | Модератор | Очередь (по умолч. `OnModeration`) | FR-06 |
| `PUT /api/admin/applications/{id}/approve` | Модератор | Одобрить | FR-06 |
| `PUT /api/admin/applications/{id}/reject` | Модератор | Отклонить с причиной | FR-06 |
| `PUT /api/admin/applications/{id}` | Модератор | Править заявку | FR-06 |
| `PUT /api/admin/places/{id}` | Модератор | Править опубликованное заведение | FR-06 |
| `GET /api/feedback/contacts` | Гость | Контакты поддержки | FR-08 |
| `GET /health` | Гость | Проверка работоспособности | NFR-05 |

> Идентификатор пользователя не передаётся параметром — он берётся из `sub` в JWT.
> Это исключает подмену чужого `userId`.

## 5.2 Словарь данных: `POST /api/auth/register`

| Поле | Тип | Обяз. | Ограничения |
|---|---|:---:|---|
| `email` | string | ✅ | email-формат, ≤ 256, уникален без учёта регистра |
| `password` | string | ✅ | 8–100 символов |
| `name` | string | ✅ | ≤ 100 |
| `university` | string | ✅ | ≤ 200 |
| `department` | string | ✅ | ≤ 200 |
| `course` | int | ✅ | 1–6 |
| `telegram` | string | — | ≤ 64 |

## 5.3 Словарь данных: `POST /api/applications` (multipart)

| Поле | Тип | Обяз. | Ограничения |
|---|---|:---:|---|
| `placeName` | string | ✅ | ≤ 300 |
| `address` | string | ✅ | ≤ 500 |
| `discount` | string | ✅ | ≤ 1000 |
| `conditions` | string | ✅ | ≤ 1000 |
| `latitude` / `longitude` | number | — | −90…90 / −180…180 |
| `validityPeriod` | string | — | свободный текст |
| `sourceUrl` | string | — | абсолютный http(s) URL |
| `photos` | file[] | ✅ | ≥ 1; `.jpg .jpeg .png .pdf .heic`; запрос ≤ 20 МБ |

## 5.4 Пример сквозного сценария

```bash
# 1. Регистрация → токен
curl -s -X POST localhost:5075/api/auth/register -H 'Content-Type: application/json' \
  -d '{"email":"a@x.com","password":"Password123","name":"Анна","university":"МГУ","department":"ВМК","course":2}'

# 2. Подача заявки
curl -s -X POST localhost:5075/api/applications -H "Authorization: Bearer $TOKEN" \
  -F placeName="Кофейня Зерно" -F address="Москва, Тверская, 10" \
  -F discount="-15% на напитки" -F conditions="По студенческому" -F photos=@proof.jpg

# 3. Модератор одобряет (токен модератора)
curl -s -X PUT localhost:5075/api/admin/applications/$ID/approve -H "Authorization: Bearer $MOD_TOKEN"

# 4. Каталог (без токена)
curl -s "localhost:5075/api/establishments?search=зерно"
```
