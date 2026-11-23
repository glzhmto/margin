# AI Usage

> How we used AI in this project, what we checked, and who checked it.

We used **Claude (Anthropic) in Claude Code** for most of the work listed below.

Two things in the app *look* made by a tool but are **not AI**:

- App icons come from **Simple Icons**
- Book covers come from **Open Library**

---

## Where we used AI

| What for | How we checked or changed it | Checked by |
|---|---|---|
| **Project plan and reading the rubric** | We discussed it together and changed the scope (for example, app silencing stayed simulated). | Mateo and Carlos |
| **Research sources, survey and interview guide** | We opened and read the sources. | Carlos (lead) and Mateo |
| **Logic code** (`Assets/Scripts/Core`), including the social rules, and its tests | 98 tests pass with `dotnet test`. | Mateo (lead) and Carlos |
| **Unity interface code**, including accounts, Friends, creators and "Buy it" | The new parts were first compiled outside Unity. Carlos then opened and tested them in Unity, checking for missing UI styling and build errors, and reported what he found to Mateo. Mateo fixed the problems with Claude Code. **Changes we made:** enabled the `UnityWebRequestTexture` package in the Package Manager and linked our `margen.uss` design system to the `PanelSettings`. | Carlos (testing) and Mateo (fixes) |
| **Quiz questions** (`books.json`) | We checked each question against the book to make sure the exact sentences exist on the right pages for the scavenger hunt. Doubts remain about *Aura* (parts III and IV), *Chronicle of a Death Foretold* (ch. 3) and *The Little Prince* (ch. VI). | Mateo and Carlos |
| **Figma prototype**, including edits made with Claude through the Figma connector | Reviewed the prototype and the edits. | Mateo and Carlos |
| **Web app** (`preview/`), **backend** (Supabase tables, security rules, the `book-prices` function) and **website** (`site/`) | Live end-to-end tests with real accounts: sign up, friend requests, reading together, posts, reviews, prices. | Mateo and Carlos |
| **Real Goodreads reviews**, with their author and link | We checked each one on the original page. | Mateo and Carlos |
| **Color contrast**, adjusting two colors | We checked the values with WebAIM Contrast Checker. | Mateo and Carlos |
| **Commercial video** | Generated with AI and then edited by Mateo. | Carlos |
| **Cutting down, simplifying and translating these documents** into English | We read them and filled in the interview and testing data ourselves. | Carlos (lead) and Mateo |

---

## AI inside the app

**Only the quiz questions**, which were generated beforehand and which we reviewed.

- The app does **not** connect to any AI.
- Recommendations come from rules we wrote (`Recommender.cs`).
- AI never writes, summarizes or gives opinions for the user.

---

## What we did ourselves

We did the **interviews, the survey and the user tests** ourselves with real people. Both of us made decisions on scope, design and content throughout, and both of us tested the builds.

### Mateo

- Led the core application build and bug fixing with Claude Code
- Integrated the Supabase backend
- Resolved the Unity C# merge conflicts and UI Toolkit rendering issues
- Produced and edited the commercial
- Reviewed the logic code, the Figma prototype and the documentation

### Carlos

- Led the project documentation (the Sustainable Lifecycle Plan, AI Usage and User Testing reports), with Claude helping to cut, simplify and translate
- Conducted the initial user research, interviews and surveys, and analyzed the CSV survey data
- Tested the builds in Unity, troubleshot issues, and reported specific errors and feedback to guide Mateo's Claude Code workflow
- Reviewed the logic code, the quiz questions and the commercial