# AI Usage

We used Claude (Anthropic) in Claude Code for most of the work in the table. Two things in the app look made by a tool but are not AI: the app icons come from Simple Icons and the book covers from Open Library.

| What for | How we checked or changed it |
|---|---|
| Project plan and reading the rubric | We discussed it and changed the scope (for example, app silencing stayed simulated) |
| Finding sources for the research and putting together the survey and the interview guide | We opened and read the sources. Checked by: ___ |
| Logic code (`Assets/Scripts/Core`), including the social rules, and its tests | 98 tests pass with `dotnet test`. Code checked by: ___ |
| Unity interface code, including accounts, Friends, creators and "Buy it" | The new parts were only compiled outside Unity. Carlos opens and tests them in Unity: ___. Changes we made: ___ |
| Quiz questions (`books.json`) | We checked each question against the book: ___. Doubts about Aura (parts III and IV), Chronicle of a Death Foretold (ch. 3) and The Little Prince (ch. VI) |
| Figma prototype, including edits made with Claude through the Figma connector | Checked by: ___ |
| Web app (`preview/`), the backend (Supabase tables, security rules, the `book-prices` function) and the website (`site/`) | Live end-to-end tests with real accounts (sign up, friend requests, reading together, posts, reviews, prices). Tested by: ___ |
| Collecting real Goodreads reviews with their author and link | We checked them on the original page |
| Measuring color contrast and adjusting two colors | We checked the values with ___ |
| Cutting down and simplifying these documents, and translating them to English | We read them and filled in the interview and testing data ourselves. Checked by: ___ |

## AI inside the app
Only the quiz questions, which were generated beforehand and which we reviewed. The app does not connect to any AI. Recommendations come from rules we wrote (`Recommender.cs`). AI never writes, summarizes or gives opinions for the user.

## What we did ourselves
We did the interviews, the survey and the user tests ourselves with real people.
- Mateo: ___
- Carlos: ___
