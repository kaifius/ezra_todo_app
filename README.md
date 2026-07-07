# Task Manager

This is a basic MVP Task Management app built with .NET and React.

## Local setup
Requires the [.NET SDK](https://dotnet.microsoft.com/download). On a mac, install via homebrew with `brew install --cask dotnet-sdk`

To run the app: run `dotnet run --project src/TaskManager.Web` and then go to https://localhost:5001

To run the tests: `dotnet test`

## The feature set
A logged-in user can:
- Create todo items (tasks)
- Mark & un-mark tasks completed
- Edit or delete existing tasks, regardless of completion state
- Hide all completed items
- Delete all completed items
- Reorder tasks

### Reasoning for chosen features
In my mind, the bare minimum feature set needed to make a to-do app usable is
- The ability to log in
  - As a user I need the to-dos to be my own, not one central list from all users
  - As a user I want my to-dos to be private and inaccessible to other users
- The ability to create to-dos (the central feature)
- The ability to mark to-dos complete (making to-dos is not helpful if you can't check them off)
- The ability to delete to-dos (so the user isn't stuck with mistake entries)

I chose to additionally include the ability to edit items, show/hide all completed items, delete all completed items, and reorder items, because they are low-cost features to add, and they give the user the ability to keep their list clean and at least somewhat tailored to their preferences. This would possibly avoid some user frustration that could prevent early adoption.

### A note on what's left out
One feature that I considered including but felt like it was beyond what's necessary for a usable MVP was allowing each user to have multiple named lists rather than just one central list. If that were to be a fast-follow feature, here are the data changes that would be necessary to support it:

#### Data model changes
- add a `lists` table (`id`, `user_id`, `name`, `created_at`, `deleted_at`)
- add a `list_id` foreign key to `tasks` - a task belongs to a list, a list has many tasks.

#### Data Migration
- Create one `list` for each user with a generic name (e.g. "{user.name}'s Tasks") and associate all the user's tasks to that list
- Once backfill is complete, make the `list_id` column in the `tasks` table non-nullable.
- This could be done cleanly before the UX is built by explicitly making a has-one relationship between `users` and `lists` (which would later be changed to a has-many), and then associating all newly created tasks to the user's one list.
