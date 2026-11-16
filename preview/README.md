# Margin · web preview (v5)

A clickable replica of the app, to review it without Unity. It is the design reference for version 5. The official deliverable is still the Unity project, which will be updated from this one.

## Open
```
python3 -m http.server 8765      # from the repository root
```
Then open http://localhost:8765/preview/ and sign up with your email. There is no demo account anymore: every account is a real one.

The published version is at https://marginreads.vercel.app/app/ (built and deployed with the local scripts `tools/build-site.sh` and `tools/deploy-preview.sh`).

## Real data (Supabase)
Accounts, friends, creators, posts, activity and Margin reviews are real. They live in a Supabase project (Postgres with row level security on every table). The browser only uses the publishable key; no secret key is in this folder.

- Accounts: email and password (Supabase Auth). Sign up asks for name, email, @username (checked live), password and, optionally, your university. Email confirmation is turned off in the project, so you can log in right away. Password reset and email change send real emails.
- Guest: "Try it without an account" uses an anonymous Supabase user. It only works when "Allow anonymous sign-ins" is on in the Supabase dashboard (it is off right now). "Save your account" adds an email and password later and keeps the diary.
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
- The book page also quotes 72 real reviews from Goodreads readers, word for word with author, date and link (`Assets/Resources/Data/reviews.json`).

## Buy it and FIL
- The book page has "Buy it": real prices for printed copies from Librerías Gandhi (the `book-prices` edge function, cached 24 h) and search links to Amazon México, Mercado Libre, Sanborns and El Sótano.
- Home shows a FIL Guadalajara 2026 banner. It can be closed and it goes away after the fair.

## What is still simulated
- "Simulate the next day" in the side panel only moves this browser's clock for the demo.
- Focus mode can't block other apps from a browser; the screen says so. Everything else (accounts, friends, prices, reviews, Goodreads import, account deletion) is real.

## Files
- `js/net.js`: Supabase client, auth, saving, and all social queries.
- `js/screens-social.js`: friends, invite and QR, reader profiles, shared reads, creator posts.
- `js/commerce.js`: "Buy it" and the FIL banner.
- `js/core.js`: a 1:1 port of `Assets/Scripts/Core` (unchanged).
- `vendor/`: supabase-js 2.49.4 and qrcode-generator 1.4.4, pinned copies.
- Covers: Open Library Covers API (`Assets/Resources/Covers/`).
