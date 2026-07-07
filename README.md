# Task Manager

This is a basic MVP Task Management app built with .NET and React.

## Local setup

### Prerequisites
- The [.NET SDK](https://dotnet.microsoft.com/download) (10.0+). On a mac: `brew install --cask dotnet-sdk`
- [Node.js](https://nodejs.org) (18+), which provides `npm`, to build the React frontend. On a mac: `brew install node`

### Run the app
The React frontend (in `ClientApp/`) builds into the backend's `wwwroot/`, which the .NET app then serves. So build the frontend once, then run the backend:

```bash
# 1. Build the React frontend
cd ClientApp
npm install
npm run build
cd ..

# 2. Run the backend (creates the local SQLite database on first launch)
dotnet run --project TaskManager
```

Then open the printed `http://localhost:<port>` URL. You can register a new account and sign in.

### Frontend dev mode (optional)
For iterating on the React code with hot reload, run the backend (`dotnet run --project TaskManager`) and the Vite dev server (`cd ClientApp && npm run dev`) side by side, then open the Vite URL (`http://localhost:5173`). Vite proxies the auth API to the backend so the session cookie works across both dev servers.

### Run the tests
Backend — integration tests covering the auth endpoints (run from the repo root):

```bash
dotnet test
```

Frontend — unit tests (run from `ClientApp/`, after `npm install`):

```bash
cd ClientApp
npm test
```

## The feature set
A logged-in user can:
- Create todo items (tasks)
- Mark & un-mark tasks completed
- Edit, delete and reorder existing tasks, regardless of completion state
- Hide all completed items
- Delete all completed items

### Reasoning for chosen features
In my mind, the bare minimum feature set needed to make a to-do app usable is
- The ability to log in
  - As a user I need the to-dos to be my own, not one central list from all users
  - As a user I want my to-dos to be private and inaccessible to other users
- The ability to create to-dos (the central feature)
- The ability to mark to-dos complete (making to-dos is not helpful if you can't check them off)
- The ability to delete to-dos (so the user isn't stuck with mistake entries)

I chose to additionally include the ability to edit items, show/hide all completed items, delete all completed items, and reorder items, because they are low-cost features to add, and they give the user the ability to keep their list clean and at least somewhat tailored to their preferences. This would possibly avoid some user frustration that could prevent early adoption.

### Deliberately left out: i18n, auth niceties
- For simplicity, I chose not to handle any internationalization and instead use static strings for user-facing content, but in a production app all of the strings would be internationalized.
- There's no email service hooked up here, so no password reset flow or 2FA

### Deliberately left out: multiple lists
One feature that I considered including, but felt like it was a bit beyond what's necessary for a usable MVP, was allowing each user to have multiple named lists rather than just one central list. If that were to be a fast-follow feature, here are the data changes that would be necessary to support it:

#### Data model changes
- add a `lists` table (`id`, `user_id`, `name`, `created_at`, `updated_at`, `deleted_at`)
- add a `list_id` foreign key to `tasks` - a task belongs to a list, a list has many tasks.

#### Data Migration
- Create one `list` for each user with a generic name (e.g. "{user.name}'s Tasks") and associate all the user's tasks to that list
- Once backfill is complete, make the `list_id` column in the `tasks` table non-nullable.
- This could be done cleanly before the UX is built by explicitly making a has-one relationship between `users` and `lists` (which would later be changed to a has-many), and then associating all new tasks on creation to the user's one list.

## Misc architectural notes
### Soft deletion for tasks, but not for users
Tasks are soft-deleted (`deleted_at` is set instead of removing the row); users are hard-deleted. The difference is deliberate:

- **Tasks:** deletion is a frequent, unceremonious action and "delete all completed" removes many at once, so accidental loss is easy. Keeping the rows gives a safety net and an easy path to undo / "trash" later.
- **Users:** soft delete adds friction with no upside here. Identity enforces a unique email, so a soft-deleted user would block that address from ever being reused, and there's no requirement to restore accounts. Hard-deleting also lets the task foreign key's cascade cleanly remove their tasks, instead of leaving orphaned rows behind a dead cascade.

### Auth
Registration and login are built on [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity) via `AddIdentityCore` — so password hashing, the user store, and lockout are Identity's, not hand-rolled. The four HTTP endpoints under `/account` (`register`, `login`, `logout`, `me`) are written explicitly rather than mounted with `MapIdentityApi`, so the app exposes only what it uses: `MapIdentityApi` would also add a bearer-token scheme and endpoints for email confirmation, password reset, and 2FA, none of which are wired up here.

The session is held in a single `HttpOnly` cookie — never exposed to JavaScript, which avoids the token-in-`localStorage` XSS risk. The cookie is `SameSite=Lax`, which blocks standard cross-site request forgery; a production build with state-changing flows would additionally add anti-forgery tokens.

Identity's schema is used as-is minus roles: `AppDbContext` derives from `IdentityUserContext` (not `IdentityDbContext`), which omits the role tables this app has no use for.

