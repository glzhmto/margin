# Margin

An iPhone app made in Unity that helps university students get back to reading whole books on paper. The idea is that you use it very little: you note the page you are on, answer a short quiz when you finish a chapter, and close it to keep reading.

Final project for Human Computer Interaction, Universidad Panamericana, 2026.
Team: Mateo González Herrera and Carlos Santiago Cruz.

## Problem
More and more students ask AI to summarize or write for them, and reading a whole book has become rare. 55 % of university students who use AI say it makes them think less deeply (Inside Higher Ed, 2025). In Mexico the main reasons for not reading are lack of interest and lack of time (INEGI, MOLEC 2025). Also, almost every habit app runs on streaks and notifications that make you feel guilty, and that is the same thing that already takes our time.

## Users
Mexican university students aged 18 to 25 who want to read more but lose to their phone. More detail in [UserResearch.md](UserResearch.md).

## What the app does
- Onboarding: you set how many pages a day, which days, "When ___, I will read" and which book.
- Daily check-in: you type the page you reached. It takes about 10 seconds.
- Chapter quiz: 2 or 3 optional questions with an explanation.
- Reflection: optional, written by you, with the "Written by a human" stamp.
- Reading momentum: instead of a streak you can lose, an average that drops only a little if you skip a day.
- Focus mode: a timer and app silencing (simulated; the real version needs a special permission from Apple).

AI was only used to generate the quiz questions, and we reviewed them. The app does not call any AI and never writes or gives opinions for the user.

## Mode
We did the project in Guided Mode and Challenge Mode.
- Guided Mode. We declare it in [AIUsage.md](AIUsage.md).
- Challenge Mode: on top of the basics we added chapter quizzes, book recommendations, reminders that don't nag, Focus mode and Demo mode. The code is split into logic and app, it has automated tests, the values are set in a config file and there is technical documentation.

## How to run it
You need Unity 6 (6000.0.x) with iOS Build Support. For the iPhone, Xcode 16 and an iPhone with iOS 15 or later.

In the editor
1. In Unity Hub: Add › Add project from disk and pick this folder.
2. When it opens, `Assets/Editor/ProjectSetup.cs` creates the scene and sets up iOS (it is also in the menu Margin › Set up project).
3. In Game view pick an iPhone resolution (1170×2532) and press Play.

On iPhone
1. File › Build Settings › iOS › Switch Platform › Build.
2. Open `Unity-iPhone.xcodeproj` and pick your team in Signing & Capabilities.
3. Connect the iPhone and press Run.

Config: the numbers for the algorithm, the reminders and the quiz are in `Assets/Resources/Data/tuning.json`.

Tests: in Unity, Window › General › Test Runner › EditMode › Run All. Without Unity: `dotnet test tests-dotnet`.

For the demo: Settings › Demo mode loads two weeks of sample history.

## Folders
- `Assets/Scripts/Core`: plain C# logic (habit, check-in, quiz, recommendations, reminders), with tests.
- `Assets/Scripts/App`: the Unity app (UI Toolkit).
- `Assets/Resources`: styles and data (books, quizzes, quotes).
- `docs/`: plan, research and [technical docs](docs/TECHNICAL.md) (architecture, config and how to extend).

## Links
- Figma: https://www.figma.com/design/xRbcsTimzkKMhNWUL2aVYl (page "Main flow" › Present)
- Build: PENDING
- Commercial: PENDING

Other documents: [DesignJustification.md](DesignJustification.md), [UserResearch.md](UserResearch.md), [UserTesting.md](UserTesting.md), [AIUsage.md](AIUsage.md).
