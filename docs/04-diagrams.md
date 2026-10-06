# 4. Диаграммы

Все диаграммы написаны в Mermaid и отображаются прямо в GitHub.

## 4.1 Процесс «Жизненный цикл скидки» (to-be, BPMN-подобная схема)

```mermaid
flowchart TD
    A([Студент нашёл скидку]) --> B[Заполнить заявку и приложить подтверждение]
    B --> C{Данные валидны?}
    C -- нет --> B
    C -- да --> D{Лимит 4/сутки исчерпан?}
    D -- да --> X1([Отказ 429])
    D -- нет --> E[Заявка: OnModeration]
    E --> F{Автор отменил?}
    F -- да --> X2([Заявка удалена])
    F -- нет --> G[Модератор проверяет заявку]
    G --> H{Решение}
    H -- Отклонить --> I[Указать причину]
    I --> J([Rejected — автор видит причину])
    H -- Одобрить --> K([Published — в каталоге])
    K --> L[Студенты голосуют за актуальность]
    L --> M{Много голосов «нет»?}
    M -- да --> N[Модератор перепроверяет и правит заведение]
    M -- нет --> L
    N --> L
```

## 4.2 Диаграмма состояний заявки

```mermaid
stateDiagram-v2
    [*] --> OnModeration: POST /applications
    OnModeration --> Published: approve (Moderator)
    OnModeration --> Rejected: reject + причина (Moderator)
    OnModeration --> Cancelled: DELETE (автор)
    Published --> Published: правка модератором
    Published --> [*]
    Rejected --> [*]
    Cancelled --> [*]
    note right of Published
        Финальные статусы (BR-08):
        повторное approve/reject → 409
        Cancelled скрыта от пользователя,
        но учитывается в лимите 4/сутки
    end note
```

## 4.3 Диаграмма последовательности: подача и модерация

```mermaid
sequenceDiagram
    actor S as Студент
    actor M as Модератор
    participant API as API (ASP.NET Core)
    participant DB as PostgreSQL

    S->>API: POST /api/auth/login
    API->>DB: SELECT user by email
    API-->>S: 200 JWT (sub, role)

    S->>API: POST /api/applications (Bearer, multipart)
    API->>API: Валидация (FluentValidation)
    API->>DB: COUNT заявок за сутки
    alt лимит исчерпан
        API-->>S: 429
    else
        API->>DB: INSERT Application(OnModeration)
        API-->>S: 201
    end

    M->>API: GET /api/admin/applications (Bearer, role=Moderator)
    API->>DB: SELECT ... WHERE Status = OnModeration ORDER BY CreatedAt
    API-->>M: 200 очередь
    M->>API: PUT /api/admin/applications/{id}/approve
    API->>DB: UPDATE Status=Published, ModeratedAt
    API-->>M: 200 заведение
```

## 4.4 Диаграмма последовательности: голосование

```mermaid
sequenceDiagram
    actor S as Студент
    participant API
    participant DB

    S->>API: POST /api/establishments/{id}/vote {vote: "yes"}
    API->>DB: заведение опубликовано?
    alt нет
        API-->>S: 404
    end
    API->>DB: последний голос пользователя за заведение
    alt прошло < 15 дней и роль не Moderator
        API-->>S: 429
    else
        API->>DB: INSERT Vote
        API->>DB: SELECT все голоса заведения
        API-->>S: 200 {yes, no, canVoteAgain:false}
    end
```

## 4.5 ER-диаграмма

```mermaid
erDiagram
    USERS ||--o{ APPLICATIONS : "подаёт"
    USERS ||--o{ VOTES : "голосует"
    APPLICATIONS ||--o{ VOTES : "получает"
    APPLICATIONS ||--o{ APPLICATION_PHOTOS : "содержит"

    USERS {
        uuid Id PK
        string Email UK "lower-case, ≤256"
        string PasswordHash "BCrypt"
        string Nickname
        string University
        string Department
        int Course
        string Telegram "nullable"
        string Role "User | Moderator"
        datetime DateRegistered
        datetime LastProfileUpdate
    }
    APPLICATIONS {
        uuid Id PK
        uuid UserId FK
        string EstablishmentName "≤300"
        string Address "≤500"
        decimal Latitude "18,6"
        decimal Longitude "18,6"
        string DiscountDescription "≤1000"
        string Conditions "≤1000"
        string ValidityPeriod "nullable"
        string SourceLink "nullable"
        string Category "nullable, из геосервиса или вручную"
        string Phone "nullable"
        string Website "nullable"
        string WorkingHours "nullable"
        enum Status "OnModeration | Published | Rejected | Cancelled"
        string RejectionReason "nullable, ≤500"
        datetime CreatedAt
        datetime ModeratedAt "nullable"
    }
    APPLICATION_PHOTOS {
        uuid Id PK
        uuid ApplicationId FK
        string FileName
        string StoredName "имя на диске"
        string ContentType
        long Size
    }
    GEOCODE_CACHE {
        string Key PK "тип запроса + параметры"
        text ResponseJson
        datetime ExpiresAt ">= 24 ч"
    }
    VOTES {
        uuid Id PK
        uuid ApplicationId FK
        uuid UserId FK
        bool IsRelevant "true=yes, false=no"
        datetime CreatedAt
    }
```

## 4.6 Архитектура (C4, уровень контейнеров)

```mermaid
flowchart LR
    subgraph Клиенты
        W[Web / Mobile клиент]
    end
    subgraph Backend["AggStudentDiscounts.Api (.NET 9)"]
        C[Controllers] --> V[FluentValidation]
        C --> D[Domain: Application, User, Vote]
        C --> E[EF Core DbContext]
        A[JWT Bearer + Role policy] --> C
    end
    DB[(PostgreSQL)]
    FS[(Файловое хранилище)]
    GEO[Nominatim / OpenStreetMap]
    W -- HTTPS + JWT --> A
    E --> DB
    C --> FS
    C -- кэш 24 ч --> GEO
```
