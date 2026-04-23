# Admin HTTP API

All routes are served under the Azure Functions prefix **`/api`**. Unless noted, responses use **`application/json; charset=utf-8`** with camelCase property names. Enum values in JSON use **PascalCase** strings (same as `System.Text.Json` + `JsonStringEnumConverter` in `Program.cs`).

## Authentication

Every admin endpoint requires:

`Authorization: Bearer <Clerk JWT>`

The token is validated as a **Clerk** JWT (`ValidateClerkJwtAsync`). The principal must include role **`admin`** (claim `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`, compared case-insensitively to `"admin"`). A matching **app database user is not required** for authorization: valid Clerk JWT + admin role is enough to call admin APIs.

There is no separate admin API key: access is **Clerk admin role** (plus successful JWT validation and a non-empty user id claim).

**`GET /v1/admin/profile`** still prefers the app `UserDto` when that Clerk user id exists in the database; otherwise it returns a synthetic `UserDto` built from JWT claims (`ClerkPrincipalUserDtoMapper`): `subscriptionType` **Free**, `status` **Active**, `createdAt` **UTC now** (placeholder), `aiUsage` omitted—treat as display-only, not billing truth.

Failures here return **`401`** with an **`ApiException`** JSON body (see below), not `ApiResponse<T>`.

---

## Standard JSON envelope: `ApiResponse<T>`

Successful and error JSON bodies (except the CSV export below) follow:

| Field       | Type    | Description |
|------------|---------|-------------|
| `success`  | boolean | `true` on success, `false` on logical errors returned as JSON |
| `data`     | T       | Payload when `success` is true; shape depends on the endpoint |
| `message`  | string  | Human-readable message (default success text: `"Request successful"`) |
| `meta`     | object  | Optional; used for **paged list** responses |

**Paged list (`GET /v1/admin/users`)** — `data` is an array of users; `meta` contains:

| Field         | Type   |
|---------------|--------|
| `totalItems`  | number |
| `pageNumber`  | number |
| `pageSize`    | number |
| `totalPages`  | number |

**Error example** (explicit JSON from the handler, e.g. `GET .../users/{id}` when missing):

```json
{
  "success": false,
  "data": null,
  "message": "User not found",
  "meta": null
}
```

### Errors from thrown exceptions (`ApiException`)

Many failures (invalid Clerk token, missing `admin` role, missing user, `ResourceNotFoundException`, unexpected errors, etc.) are handled by `FunctionExecutionHelper` and return a different JSON shape **`ApiException`** (not `ApiResponse<T>`):

| Field            | Type           | Description |
|------------------|----------------|-------------|
| `statusCode`     | number         | HTTP status (e.g. 401, 404, 500) |
| `message`        | string         | Client-safe message |
| `details`        | string \| null | Stack trace in **Development** only; otherwise null |
| `correlationId`  | string \| null | Functions invocation id for support |

---

## Shared types

### `UserDto` (`data` for profile, user CRUD, activate/deactivate, subscription update)

| Field                    | Type           | Notes |
|--------------------------|----------------|--------|
| `id`                     | string \| null | User id |
| `email`                  | string         | |
| `firstName`              | string \| null | |
| `lastName`               | string \| null | |
| `subscriptionType`       | string (enum)  | `Free`, `Premium`, `PremiumMonthly`, `PremiumYearly` |
| `status`                 | string (enum)  | `Active`, `Suspended`, `Inactive` |
| `imageUrl`               | string \| null | |
| `createdAt`              | string (ISO 8601) | |
| `isEmailVerified`        | boolean        | |
| `subscriptionExpiresAt`  | string \| null | ISO 8601 date-time |
| `aiUsage`                | object \| omitted | Omitted when null. See `AiUsageDto`. |

### `AiUsageDto`

| Field                      | Type    |
|----------------------------|---------|
| `isUnlimited`              | boolean |
| `freeCallsRemainingThisMonth` | number \| null |
| `freeCallsLimitPerMonth`   | number \| null |
| `freeCallsUsedThisMonth`   | number \| null |
| `monthKeyUtc`              | string \| null |

### `UpdateSubscriptionDto` (request body for `PATCH .../subscription`)

| Field                    | Type           | Required |
|--------------------------|----------------|----------|
| `subscriptionType`       | string (enum)  | Yes — `Free`, `Premium`, `PremiumMonthly`, `PremiumYearly` |
| `subscriptionExpiresAt`  | string \| null | No — ISO 8601 date-time |

---

## Endpoints

### `GET /api/v1/admin/profile`

**Request:** No body. Headers: Clerk admin JWT.

**Response:** `200` — `ApiResponse<UserDto>`. If the Clerk user id exists in the app DB, `data` is the full user DTO (including `aiUsage` when applicable). If not, `data` is a **Clerk-only** synthetic profile (see Authentication above).

---

### `GET /api/v1/admin/users`

**Request:** No body. Query parameters (all optional except as noted):

| Query parameter     | Type   | Default | Description |
|---------------------|--------|---------|-------------|
| `pageNumber`        | int    | `1`     | Page index (≥ 1 after server clamp) |
| `pageSize`          | int    | `10`    | Page size (≥ 1) |
| `status`            | string | —       | Filter by `AccountStatus`: `Active`, `Suspended`, `Inactive` (case-insensitive). Invalid value returns **empty** page (0 items), not an error. |
| `subscriptionType`  | string | —       | Filter by `SubscriptionType`: `Free`, `Premium`, `PremiumMonthly`, `PremiumYearly` (case-insensitive). Invalid value returns **empty** page. |
| `search`            | string | —       | Case-insensitive partial match on first name, last name, or email |

**Response:** `200` — `ApiResponse<List<UserDto?>>` with paging in **`meta`** (`totalItems`, `pageNumber`, `pageSize`, `totalPages`). Items may be null only if the underlying mapping produced null; normally each entry is a full `UserDto`.

---

### `GET /api/v1/admin/users/{userId}`

**Request:** Path `userId`. No body.

**Response:**

- `200` — `ApiResponse<UserDto>` (`data` is the user).
- `404` — `ApiResponse<UserDto>` with `success: false`, `message: "User not found"`.

---

### `PATCH /api/v1/admin/users/{userId}/activate`

**Request:** Path `userId`. No body.

**Response:** `200` — `ApiResponse<UserDto>` (`data` is the user after status set to **Active**).

If the user does not exist, the service throws **`ResourceNotFoundException`** → **`404`** with an **`ApiException`** body (see above), not `ApiResponse<T>`.

---

### `PATCH /api/v1/admin/users/{userId}/deactivate`

**Request:** Path `userId`. No body.

**Response:** `200` — `ApiResponse<UserDto>` (`data` is the user after status set to **Suspended**).

Missing user: same as activate — **`404`** + **`ApiException`**.

---

### `PATCH /api/v1/admin/users/{userId}/subscription`

**Request:**

- Path: `userId`.
- Body: JSON **`UpdateSubscriptionDto`** (see above).

**Response:**

- `200` — `ApiResponse<UserDto>` (`data` is the updated user).
- `400` — `ApiResponse<UserDto>` with `success: false`, `message: "Invalid subscription data"` (failed model validation).

---

### `GET /api/v1/admin/dashboard`

**Request:** No body.

**Response:** `200` — `ApiResponse<DashboardAnalyticsDto>`.

`DashboardAnalyticsDto` (`data`):

| Field                    | Type   |
|--------------------------|--------|
| `totalUsers`             | long   |
| `totalPremiumUsers`      | long   |
| `totalFreeUsers`         | long   |
| `activeUsers`            | long   |
| `newUsersThisMonth`      | long   |
| `premiumMonthlyUsers`    | long   |
| `premiumYearlyUsers`     | long   |
| `legacyPremiumUsers`     | long   |
| `premiumPercentage`      | number | Computed |
| `activePercentage`       | number | Computed |
| `inactiveUsers`          | long   | Computed |
| `premiumMonthlyPercentage` | number | Computed |
| `premiumYearlyPercentage`  | number | Computed |

---

### `GET /api/v1/admin/dashboard/statistics`

**Request:** Optional query:

| Query    | Description |
|----------|-------------|
| `format` | If `csv` (case-insensitive), response is **not** `ApiResponse`; see below. Otherwise JSON. |

**Response (default — JSON):** `200` — `ApiResponse<DashboardStatisticsExportDto>`.

`DashboardStatisticsExportDto` (`data`):

| Field                      | Type |
|----------------------------|------|
| `generatedAtUtc`           | string (ISO 8601) |
| `overview`                 | `DashboardOverviewStatsDto` |
| `subscriptions`            | `DashboardSubscriptionBreakdownDto` |
| `growth`                   | `DashboardGrowthStatsDto` |
| `content`                  | `DashboardContentStatsDto` |
| `aiFreeTier`               | `DashboardAiFreeTierStatsDto` |
| `activity`                 | `DashboardActivityStatsDto` |
| `signupsLast30DaysByUtcDay` | array of `{ "utcDateKey": string, "count": long }` |

**Nested objects:**

- **`overview`:** `totalUsers`, `activeUsers`, `suspendedUsers`, `emailVerifiedUsers`, `emailNotVerifiedUsers` (all long).
- **`subscriptions`:** `totalPremiumUsers`, `totalFreeUsers`, `premiumMonthlyUsers`, `premiumYearlyUsers`, `legacyPremiumUsers`, `premiumPercentageOfTotal`, `freePercentageOfTotal`.
- **`growth`:** `newUsersThisUtcMonth`, `newUsersPreviousUtcMonth`, `newUsersLast7Days`, `monthOverMonthNewUserPercentChange` (number \| null).
- **`content`:** `totalNotes`, `totalSavedVerses`.
- **`aiFreeTier`:** `currentUtcMonthKey`, `freeCallsLimitPerMonth`, `usersWithUsageTrackedThisMonth`, `totalFreeAiCallsUsedThisMonth`, `usersAtOrOverFreeLimitThisMonth`.
- **`activity`:** `activitiesLast30Days`, `aiAnalysisActivitiesLast30Days`, `addNoteActivitiesLast30Days`, `readBibleActivitiesLast30Days`.

**Response (`format=csv`):** `200`

- `Content-Type: text/csv; charset=utf-8`
- `Content-Disposition: attachment; filename="rhemapp-dashboard-statistics-<yyyy-MM-dd-HHmm>Z.csv"`
- Body: UTF-8 BOM + CSV text derived from the same statistics payload (not the `ApiResponse` wrapper).

---

### `GET /api/v1/admin/users/{userId}/ai-quota`

**Request:** Path `userId`. No body.

**Response:** `200` — `ApiResponse<AdminUserAiQuotaDto>`.

`AdminUserAiQuotaDto` (`data`):

| Field                 | Type        |
|-----------------------|-------------|
| `userId`              | string      |
| `storedMonthKey`      | string \| null |
| `storedUsedInMonth`   | int         |
| `aiUsage`             | `AiUsageDto` |

---

### `POST /api/v1/admin/users/{userId}/ai-quota/reset`

**Request:** Path `userId`. No body.

**Response:** `200` — `ApiResponse<AdminUserAiQuotaDto>` (same shape as GET after reset).

---

### `POST /api/v1/admin/users/{userId}/ai-quota/set-remaining`

**Request:**

- Path: `userId`.
- Query: **`remainingThisMonth`** (int). If missing or unparsable, the handler passes **`0`**.

**Response:** `200` — `ApiResponse<AdminUserAiQuotaDto>`.

---

### `POST /api/v1/admin/account-deletion/purge`

**Request:** No body. Triggers hard purge of soft-deleted users whose grace period has expired (`PurgeExpiredAsync` with current UTC time).

**Response:** `200` — JSON envelope where `data` is an object:

```json
{
  "success": true,
  "data": { "purged": 0 },
  "message": "Request successful",
  "meta": null
}
```

`purged` is the number of users purged (**int**).

---

## Source of truth

Route definitions and status codes are implemented in `Api.Functions/Handlers/Admin/AdminFunctions.cs`. DTOs live under `Application/Dtos/`. If behavior changes, update this document to match.
