# Margin

Margin, the reading app. An iPhone app made in Unity for university students who read books on paper, alone or with friends. Every reading session starts in Focus mode: you pick a time, read while distracting apps stay quiet, and when the timer ends Margin tells you. Then you note the page you reached, answer a short quiz if you finished a chapter, and you are done for the day.

There is also a web version of the same app and a website. Both versions share one account system, so friends can use either one.

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
- Focus mode: a timer and app silencing. You pick from real apps with their real icons, but the silencing is simulated (the real version needs a special permission from Apple).
- Accounts: sign up with email and password, log in, or try it as a guest. A guest can later save the account and keep everything.
- Friends (from Home): search by @username or name, share an invite link (with a copy button), send and accept requests, see a friend's profile and read a book together. Friends see your current book and page, your momentum and your finished books. There are no rankings.
- Creators: some readers post book lists, short texts or YouTube videos. You can follow them and their posts show up on Home.
- Book page: reviews from Margin readers, real quotes from Goodreads reviews, and "Buy it" with real Gandhi prices for printed copies and links to other Mexican stores.
- Explore, Library and Profile: search books and readers; your library (reading now, want to read, read, and the books you read before Margin), your reading diary with yearly stats and goals; achievements and lists.
- Choosing a book: a short quiz and "Find your next book", which learns from what you read and rated. Starting a book has its own screen, and finishing one shows the achievements it unlocked.
- Import from Goodreads (your exported CSV), export your data, and delete your account (it is removed from the server).
- A banner on Home for the FIL Guadalajara 2026 book fair. You can close it and it goes away after the fair.

Inside the app, AI was only used to generate the quiz questions, and we reviewed them. The app does not call any AI and never writes or gives opinions for the user.

## Mode
We did the project in Guided Mode and Challenge Mode.
- Guided Mode. We declare it in [AIUsage.md](AIUsage.md).
- Challenge Mode: on top of the basics we added chapter quizzes, book recommendations, reminders that don't nag, Focus mode, and accounts and friends on a real backend. The code is split into logic and app, and the values are set in a config file.

## How to run it
The fastest way to see it: open the website at https://marginreads.vercel.app/ or the web app at https://marginreads.vercel.app/app/ and sign up.

Unity app: you need Unity 6 (6000.0.x) with iOS Build Support. For the iPhone, Xcode 16 and an iPhone with iOS 15 or later.

In the editor
1. In Unity Hub: Add › Add project from disk and pick this folder.
2. When it opens, `Assets/Editor/ProjectSetup.cs` creates the scene and sets up iOS (it is also in the menu Margin › Set up project).
3. Open Window › General › Device Simulator, pick an iPhone (for example iPhone 15) and press Play.

On iPhone
1. File › Build Settings › iOS › Switch Platform › Build.
2. Open `Unity-iPhone.xcodeproj` and pick your team in Signing & Capabilities.
3. Connect the iPhone and press Run.

Config: the numbers for the algorithm, the reminders and the quiz are in `Assets/Resources/Data/tuning.json`.

Backend: accounts, friends, posts, reviews and prices live in a Supabase project. The app reads its address and public key from `Assets/Resources/Data/backend.json`, so there is nothing to install. "Try it without an account" uses Supabase anonymous sign-ins, which are on.

For the demo: Settings › Simulate next day moves the app's clock one day ahead, so you can show the reading momentum change.

## Folders
- `Assets/Scripts/Core`: plain C# logic (reading momentum, check-in, quiz, recommendations, reminders).
- `Assets/Scripts/App`: the Unity app (UI Toolkit). `Web/Screens` has the screens and `Services/Net` talks to the backend.
- `screenshots/`: screens of the final app used in DesignJustification.md.
- `Assets/Resources`: styles and data (books, quizzes, quotes, reviews, backend config, app icons, the FIL banner).
- `preview/`: the web app (plain JS), published at /app/.
- `site/`: the Margin website, published at the root of the same link.

## Links
- Figma: https://www.figma.com/design/xRbcsTimzkKMhNWUL2aVYl (page "Lo-fi flow" › Present; the older high-fidelity version is on page "Main flow")
- Website: https://marginreads.vercel.app/
- Web app: https://marginreads.vercel.app/app/
- Build: PENDING
- Commercial: PENDING

Other documents: [DesignJustification.md](DesignJustification.md), [UserResearch.md](UserResearch.md), [UserTesting.md](UserTesting.md), [AIUsage.md](AIUsage.md).
