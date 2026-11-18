# Technical documentation

How the code is organized and how to change it.

## Folders

- `Assets/Scripts/Core`: the logic in plain C# (no Unity). Reading momentum, the check-in, the quiz, recommendations, reminders and the social rules.
- `Assets/Scripts/App`: the Unity app. Screens, saving, notifications and the connection to the backend.
- `Assets/Editor`: creates the scene and sets up the iOS build.
- `Assets/Resources/Data`: the data files (`books.json`, `reviews.json`, `quotes.json`, `tuning.json`, `backend.json`).
- `Assets/Resources/UI/app.uss`: colors and styles.
- `Assets/Tests/EditMode` and `tests-dotnet/`: the tests.
- `preview/`: the web version of the app (plain JavaScript).
- `site/`: the website.

Core doesn't use Unity at all, so we can test it with `dotnet test` in a few seconds. The screens only show what Core calculates.

## Core (main files)

- `HabitEngine.cs`: reading momentum. `M = 0.85 · M_previous + 0.15 · min(pages / goal, 1.5)`.
- `ReadingService.cs`: checks and saves the page you log, and sees if you finished a chapter.
- `Edition.cs`: adjusts the chapters to your edition, because every publisher has a different number of pages.
- `QuizEngine.cs`: grades the chapter quiz.
- `Recommender.cs`: simple recommendations with a reason ("Shares themes with...").
- `TasteQuiz.cs`: the quiz to choose your first book.
- `NextBook.cs`: "Find your next book". It mixes your answers with what you read, rated, dropped or want to read.
- `ReminderPlanner.cs`: at most 4 reminders a week, fewer if you ignore them, none if you already read.
- `Tuning.cs`: the numbers you can change in `tuning.json`.
- `Social/`: usernames, friends, activity, posts, store links and the backend config.

## App

The Unity screens are a port of the web app, screen by screen, so both work the same. Most names are the same in both (`HomeScreen`, `LogSheet`, `Nav.Push`, `Data.Save`).

- `MarginApp.cs`: starts the app, loads the data and the session, and opens the first screen.
- `Web/H.cs` and `Web/Components.cs`: small helpers to build the UI (cover, stars, avatar, buttons, sheets, confetti).
- `Web/Nav.cs`: navigation between screens.
- `Web/Screens/`: the screens. `AuthScreens.cs` (sign up, log in, setup, the book quizzes), `MainScreens.cs` (Home, Explore, Library, Profile), `BookFlowScreens.cs` (book page, log, quiz, reflection, start and finish a book, Focus mode), `SettingsSocialScreens.cs` (settings, friends, posts), `LibraryAdd.cs` and `BookSearch.cs`.
- `Services/`: saving the diary, notifications, the Focus mode app list and the backend connection (`Services/Net/`).

## Backend

Accounts, friends, posts, reviews and prices are in a Supabase project. The web and the iPhone app use the same one, so friends can use either.

- Tables: `profiles` (public info and profile photo), `reading_states` (your private diary), `friendships`, `follows`, `activity`, `posts`, `reviews`, `book_prices`.
- Every table has row level security. The apps only have the public key (in `backend.json`).
- `book-prices` is an edge function that gets real Gandhi prices and saves them for 24 h.
- The diary is saved on the phone first, so logging works without internet. Then it syncs.
- Guest mode uses anonymous sign-in. "Save your account" adds an email and password later.

## Main flow

```
Welcome → Setup (first time) → Home → Log reading
  → finished a chapter with a quiz? → Quiz → Reflection (optional) → Session done
  → finished the book? → Achievements and review
```

Starting a book always goes through the "A new book" screen.

## Config

`Assets/Resources/Data/tuning.json` changes how the app behaves without touching code: the momentum numbers, the number of reminders, the max pages per check-in, the quiz pass ratio and the recommender weights. If a value doesn't make sense, the app ignores it and uses the default. In the web the shared defaults are at the top of `preview/js/data.js` (`PLAN`, `FOCUS`, `TIMING`).

## How to add things

- A book: add it to `books.json` with its chapters. Add the cover in `Covers/` and the reviews in `reviews.json` if you have them. The data tests check that it is well formed.
- A screen: make it in the web first, then copy it to a class that inherits from `WebScreen` and open it with `Nav.Push(new MyScreen())`.
- Colors: only change the tokens in `app.uss` and `preview/css/app.css`.

## Tests

- `dotnet test tests-dotnet`: 123 tests for Core and the data files.
- In Unity: Window › General › Test Runner › EditMode › Run All.
- Web: `node --test preview/tests/*.test.mjs` checks that the web logic follows the same rules as the C# one.
