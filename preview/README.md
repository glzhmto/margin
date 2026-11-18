# Margin · web preview (v5)

A clickable replica of the app, to review it without Unity. It is the design reference for version 5. The official deliverable is still the Unity project, which will be updated from this one.

## Open
```
python3 -m http.server 8765      # from the repository root
```
Then open http://localhost:8765/preview/ and sign up with your email, or tap "Try a demo account" (a guest account with six weeks of sample reading, from `DemoData` in `js/core.js`). It must be served from the repository root because it loads `Assets/Resources/`.

The published version is at https://marginreads.vercel.app/app/ (built and deployed with the local scripts `tools/build-site.sh` and `tools/deploy-preview.sh`).

## Real data (Supabase)
Accounts, friends, creators, posts, activity and Margin reviews are real. They live in a Supabase project (Postgres with row level security on every table). The browser only uses the publishable key; no secret key is in this folder.

- Accounts: email and password (Supabase Auth). Sign up asks for name, email, @username (checked live), password and, optionally, your university. Email confirmation is turned off in the project, so you can log in right away. Password reset and email change send real emails.
- Guest: "Try it without an account" and the demo account use an anonymous Supabase user ("Allow anonymous sign-ins" is on in the project). "Save your account" adds an email and password later and keeps the diary.
- Your diary: the reading state (logs, progress, quizzes, reflections, goals, lists) and your settings are saved privately in `reading_states`, about a second after each change and again when you close the tab. Only you can read it.
- Public summary: whenever your diary changes, your profile is updated with your current book and page, momentum level, pages this week, finished books and favorites. That is what friends see. You can hide your current book in Settings › Privacy. No rankings, no screen time numbers.
- Activity: starting a book, logging pages, finishing a book, a chapter quiz, a review and a creator post each write one activity row. Your friends and your followers see it. Settings › Privacy › "Only me" stops it.
- Delete account: deletes your login, diary, friendships, follows, posts, reviews and activity on the server (the delete_my_account function). You can sign up again with the same email.

## Friends
- Find friends: search by @username or name, see suggestions from your university, or share your invite link (`/preview/?add=<username>`) or its QR code. Opening the link after logging in (or after signing up) shows that profile with "Add friend".
- Requests: send, cancel, accept or decline. Incoming requests show up live (Supabase Realtime) with a badge on Home and in Notifications.
- Friend profile: their current book and page, momentum level, books finished, favorites and recent activity.
- Read a book together: from a friend's profile, pick a book. Both of you see "Reading together" on Home with each other's progress.
- Home shows "Friends reading now" and an activity feed from friends and the creators you follow.

## Creators
- Turn it on in Settings › Profile › Creator account.
- Publish from your profile: a list (books from the catalog with a short note each), a text post, or a video post (a YouTube link, embedded with youtube-nocookie and never autoplayed).
- Explore has a Creators section and the latest posts. Anyone can follow a creator; their posts show up in followers' feeds. There are no like counts and no endless feed.

## Reviews
- Rate a book and write a review on its page. Margin reviews are stored in the database, one per reader per book, and everyone sees them with the real average and histogram.
- The book page also quotes real reader reviews collected from Goodreads, word for word with author, date and link (the app shows them as "Reviews"); the ones written in Spanish are translated and marked `originalLang` (`Assets/Resources/Data/reviews.json`).

## Buy it and FIL
- The book page has "Buy it": real prices for printed copies from Librerías Gandhi (the `book-prices` edge function, cached 24 h) and search links to Amazon México and El Sótano.
- Home shows a FIL Guadalajara 2026 banner. It can be closed and it goes away after the fair.

## Choosing and starting books
- First book: a ten-question taste quiz (`TasteQuiz` in `js/core.js`), a short "Finding your match…" screen, then one spotlight book with a real review and a short list of other matches (`matchList` in `js/ui.js`). Its book page only offers "Read this".
- Next books: "Find your next book" (Explore, and Home when nothing is being read) asks five questions and ranks the catalog with `NextBook`, which learns from what the reader finished, rated, dropped and wants to read. Every result says why.
- Starting a book always goes through `StartBookScreen` (length, weeks at your pace, today's goal), with confetti on "Start reading".
- Finishing a book shows the achievements it unlocked, a rating, "Post a review" and "Share it".
- Library tab: Books (reading now, want to read, read, plus "Add books you've read" for books read before Margin), Diary, Stats and Goals.
- Profile photo: Settings › Profile. The image is cropped square, shrunk to a 160 px JPEG and stored in `profiles.avatar`.

## What is still simulated
- "Simulate the next day" in the side panel only moves this browser's clock for the demo.
- Focus mode can't block other apps from a browser; the screen says so. Everything else (accounts, friends, prices, reviews, Goodreads import, account deletion) is real.

## Files
- `js/net.js`: Supabase client, auth, saving, and all social queries.
- `js/screens-social.js`: friends, invite and QR, reader profiles, shared reads, creator posts.
- `js/commerce.js`: "Buy it" and the FIL banner.
- `js/app.js`: start-up, the desktop side panel and theme.
- `js/ui.js`: shared components (navigation, sheets, covers, avatars, stars, search, matches, confetti).
- `js/data.js`: catalog, reviews and quotes, shared defaults (`PLAN`, `FOCUS`, `TIMING`) and derived data (achievements, search).
- `js/core.js`: the port of `Assets/Scripts/Core` (momentum, check-in, quiz, recommendations, taste quiz, NextBook, reminders), plus the demo data, the clock and formatting helpers.
- `js/screens-*.js`: the screens (auth and setup, book page, reading flow, main tabs, settings, social).
- `tests/core.test.mjs`: tests for `js/core.js` that mirror the C# ones. Run `node --test preview/tests/*.test.mjs` from the repository root.
- `vendor/`: supabase-js 2.49.4 and qrcode-generator 1.4.4, pinned copies.
- Covers: Open Library Covers API (`Assets/Resources/Covers/`).
