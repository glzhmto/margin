using System;
using Margin.Core;

namespace Margin.App
{
    /// <summary>
    /// The whole brand voice in one place. Tone: a firm manifesto, never guilt-tripping.
    /// Changing the name or the wording = editing this file.
    /// </summary>
    public static class Copy
    {
        public const string BrandName = "Margin";
        public const string Tagline = "Read it yourself.";   // PROVISIONAL

        public static readonly string[][] Manifesto =
        {
            new[] { "They sold us summaries.", "They sold us answers. They sold us not having to think." },
            new[] { "We chose the long way.", "Read the whole book. Underline it. Talk it over with friends, at a table, no screens." },
            new[] { "No one can read for you.", "This app won't write for you or think for you. It helps you get back to paper, then gets out of the way." },
        };

        // Onboarding
        public const string StepName = "What should we call you?";
        public const string StepNameHint = "Just to say hi. It stays on your phone.";
        public const string StepGoal = "How many pages a day?";
        public const string StepGoalHint = "Start small. Ten pages every day beat fifty pages once.";
        public const string StepDays = "Which days will you read?";
        public const string StepDaysHint = "The other days are planned rest. They don't count against you.";
        public const string StepWhen = "When will you read?";
        public const string StepWhenHint = "Tie reading to something you already do and it becomes automatic.";
        public const string StepBook = "What will you read?";
        public const string StepBookHint = "Pick a shelf and a book. Paper is best.";
        public const string StepFocus = "Protect your attention";
        public const string StepFocusHint = "While you read, we can silence the apps that steal your time. You choose which ones.";
        public const string PactTitle = "Your pact";
        public const string PactSign = "Sign and start";

        /// <summary>Each one completes "When I ...".</summary>
        public static readonly string[] IntentionSuggestions =
        {
            "finish dinner", "get on the subway", "get into bed", "have my first coffee",
        };

        /// <summary>
        /// "When I finish dinner". The intention is written to follow "When I", so a leading "I "
        /// typed by the user is dropped to avoid "When I I ...".
        /// </summary>
        public static string WhenI(string intention)
        {
            var s = (intention ?? "").Trim();
            if (s.StartsWith("I ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2).TrimStart();
            return "When I " + s;
        }

        // Home
        public static string Greeting(string name) => string.IsNullOrWhiteSpace(name) ? "Hi." : $"Hi, {name}.";
        public const string LogReading = "Log reading";
        public const string StartFocus = "Focus mode";
        public const string NextInShelf = "Next on your shelf";

        public static string LevelName(MomentumLevel l)
        {
            switch (l)
            {
                case MomentumLevel.Kindling: return "Kindling";
                case MomentumLevel.Steady: return "Steady";
                case MomentumLevel.Strong: return "Strong";
                default: return "Unstoppable";
            }
        }

        // Check-in
        public const string CheckInTitle = "What page did you get to?";
        public const string CheckInCta = "Log it";

        // Quiz
        public const string QuizOfferTitle = "You finished a chapter.";
        public const string QuizOfferBody = "A few questions to make it stick? Recalling what you read is the best way not to forget it. It's optional.";
        public const string QuizAccept = "Take the quiz";
        public const string QuizSkip = "Not now";
        public const string QuizAiNote = "Questions written with AI and checked by people. The AI asks; you think.";

        // Reflection
        public const string ReflectionTitle = "In your words";
        public const string ReflectionSeal = "WRITTEN BY A HUMAN";
        public const string ReflectionNoAi = "No autocomplete and no AI suggestions here. What you write is yours.";
        public static readonly string[] ReflectionPrompts =
        {
            "What did this chapter leave you with?",
            "Which line would you underline, and why?",
            "Which character would you argue with?",
            "What would you tell a friend about what you read?",
        };

        // Closing
        public const string ClosingTitle = "Done.";
        public const string ClosingBody = "Now close the app and go read.";

        // Notifications
        public static string ReminderTitle(UserProfile p) =>
            string.IsNullOrWhiteSpace(p.goals.intention) ? "Your book is waiting" : $"{WhenI(p.goals.intention)}…";
        public const string ReminderFallback = "A few pages today. That's all.";

        // ---------- Network ----------
        public const string NetFail = "Couldn't reach Margin. Check your connection and try again.";
        public const string Loading = "Loading…";
        public const string LoadFail = "Couldn't load this. Check your connection.";

        // ---------- Accounts ----------
        public const string WelcomeLine = "Your reading diary, for paper books.";
        public const string WelcomeSub = "Read with your friends, at your own pace.";
        public const string SignUpWithEmail = "Sign up with email";
        public const string HaveAccount = "I already have an account";
        public const string TryAsGuest = "Try it without an account";
        public const string TermsNote = "By continuing you accept the Terms of Use and the Privacy Notice.";
        public const string TermsCheck = "I accept the Terms of Use and the Privacy Notice. I am 13 or older.";
        public const string SignUpTitle = "Create account";
        public const string SignUpHint = "Your diary is saved to your account, so it follows you to any device, the web included.";
        public const string SaveGuestTitle = "Save your account";
        public const string SaveGuestHint = "Add an email and a password. You keep everything you logged, and friends can find you by name.";
        public const string CreateAccount = "Create account";
        public const string Creating = "Creating…";
        public const string SaveAccount = "Save account";
        public const string Saving = "Saving…";
        public const string LogInTitle = "Log in";
        public const string LogInCta = "Log in";
        public const string LoggingIn = "Logging in…";
        public const string LogInMissing = "Enter your email and password.";
        public const string NameHelp = "How friends will see you.";
        public const string UsernameHelp = "Friends find you by this name.";
        public const string UsernameChecking = "Checking…";
        public const string UsernameCheckFailed = "Couldn't check the name right now. Try again.";
        public static string UsernameAvailable(string u) => $"@{u} is available.";
        public static string UsernameTaken(string u) => $"@{u} is taken. Try another one.";
        public const string PasswordHelp = "At least 8 characters, with at least one number.";
        public static readonly string[] PasswordStrength = { "Too weak", "Weak", "Okay", "Good", "Strong" };
        public const string UniversityHelp = "Optional. We suggest friends from the same place.";
        public const string GuestProfileNotUpdated = "Your email is saved, but the profile didn't update. Try again.";
        public const string AccountSaved = "Account saved. Friends can find you now.";
        public static string WelcomeBack(string name) => string.IsNullOrWhiteSpace(name) ? "Welcome back." : $"Welcome back, {name}.";

        // ---------- Friends ----------
        public const string FriendsTitle = "Friends";
        public const string FriendsHint = "Read with people you know. They see your book and page, never your notes.";
        public const string FindFriends = "Find friends";
        public const string SearchPlaceholder = "@username, name or invite link";
        public const string Requests = "Requests";
        public const string YourFriends = "Your friends";
        public const string NoFriends = "No friends yet. Invite someone: reading is easier when someone asks you how it's going.";
        public const string InviteTitle = "Invite a friend";
        public const string InviteHint = "Share your link. When they open it and sign up, they land on your profile.";
        public const string CopyLink = "Copy link";
        public const string LinkCopied = "Link copied. Paste it in your chat.";
        public const string Sent = "Sent";
        public const string Suggestions = "Suggestions";
        public static string FromUniversity(string u) => $"From {u}";
        public const string AddUniversity = "Add your university in Settings and we'll suggest people from there.";
        public const string NobodyFromUniversity = "Nobody else from there yet. Share your invite link with classmates.";
        public static string NoReaderCalled(string q) => $"No reader called “{q}”. Check the spelling, or send them your invite link.";
        public const string Searching = "Searching…";
        public const string GuestFriendsNote = "You're using Margin as a guest. Save your account so friends can find you by name.";
        public static string WantsToBeFriend(string name) => $"{name} wants to be your friend";
        public static string AcceptedYou(string name) => $"{name} accepted your request";
        public static string FriendsNow(string name) => $"You and {name} are friends now";
        public static string RequestSentTo(string name) => $"Request sent to {name}";
        public const string AddFriend = "Add friend";
        public const string Accept = "Accept";
        public const string Decline = "Decline";
        public const string Requested = "Requested";
        public const string IsFriend = "Friends";
        public const string Follow = "Follow";
        public const string Following = "Following";
        public static string RemoveFriendTitle(string name) => $"Remove {name} as a friend?";
        public const string RemoveFriendBody = "You'll stop seeing each other's reading. They won't be notified.";
        public const string Remove = "Remove";
        public static string CancelRequestTitle(string name) => $"Cancel your request to {name}?";
        public const string CancelRequest = "Cancel request";
        public const string Keep = "Keep it";

        // ---------- Reader profile ----------
        public static string OnMarginSince(DateTime d) => $"on Margin since {Months[d.Month - 1]} {d.Year}";
        public const string CreatorBadge = "Creator";
        public const string ReadingTogether = "Reading together";
        public const string ReadTogetherHint = "Pick a book you'll both read. You'll each see the other's page.";
        public const string ReadTogether = "Read a book together";
        public const string ChangeBook = "Change book";
        public const string StopTogether = "Stop reading together";
        public const string StopTogetherTitle = "Stop reading together?";
        public static string StopTogetherBody(string name) => $"{name} will see the shared book removed too.";
        public static string ReadingWith(string name) => $"with {name}";
        public static string NowReadingTogether(string book, string name) => $"Reading {book} with {name}";
        public static string PickTogetherHint(string name) => $"Pick the book you and {name} will read. Each of you logs pages as usual.";
        public const string YoureReadingIt = "You're reading it";
        public static string TheyreReadingIt(string name) => $"{name} is reading it";
        public const string NowSection = "Now";
        public const string NotReading = "Not reading anything right now.";
        public const string RecentActivity = "Recent activity";
        public const string FinishedBooks = "Finished";
        public const string Favorites = "Favorite books";
        public const string PostsSection = "Posts";
        public const string NoPosts = "No posts yet.";
        public const string RequestPendingTitle = "Request sent";
        public static string RequestPendingBody(string name) => $"When {name} accepts, you'll see each other's current book and page.";
        public const string ReadTogetherTitle = "Read together";
        public static string AddToSee(string name) => $"Add {name} as a friend to see what they're reading and share a book.";
        public static string ReaderNotFound(string u) => $"We couldn't find @{u}. The link may be old, or they changed their username.";
        public const string MomentumLabel = "reading momentum";
        public static string BooksFinished(int n) => n == 1 ? "book finished" : "books finished";

        // ---------- Home ----------
        public const string FriendsReadingNow = "Friends reading now";
        public const string FriendsActivity = "Friends' activity";
        public const string NothingNew = "Nothing new yet. When your friends log pages, finish a book or post, it shows up here.";
        public const string FilTitle = "FIL Guadalajara · Nov 28 – Dec 6";
        public const string FilBody = "The largest book fair in Spanish turns 40. Italy is the guest of honor.";
        public const string FilCta = "Get tickets";
        public const string Hide = "Hide";

        // ---------- Book: buying and reviews ----------
        public const string BuyIt = "Buy it";
        public const string CheckingPrices = "Checking prices at Gandhi…";
        public const string NoGandhiStock = "Gandhi has no printed copies in stock right now.";
        public const string LowestAtGandhi = "Lowest price at Gandhi";
        public const string AtGandhi = "Gandhi";
        public const string SearchThisBook = "Search this book";
        public const string ReviewsSection = "Reviews on Margin";
        public const string NoReviews = "No reviews from Margin readers yet.";
        public const string YourReview = "Your review";
        public const string ReviewPlaceholder = "What did you think? No spoilers, please.";
        public const string SaveReview = "Save review";
        public const string ReviewSaved = "Review saved";
        public const string DeleteReview = "Delete my review";
        public const string ReviewDeleted = "Review deleted";
        public const string PickRating = "Pick a rating first.";
        public const string SignInToReview = "Log in to rate this book and write a review.";

        // ---------- Creators and posts ----------
        public const string CreatorsSection = "Creators";
        public const string NoCreators = "No creators yet. Turn it on in Settings and be the first.";
        public const string LatestPosts = "Latest posts";
        public const string CreatorAccount = "Creator account";
        public const string CreatorHint = "Publish book lists, notes and videos. Every reader can see them; your followers get them in their feed.";
        public const string NewPost = "New post";
        public const string Publish = "Publish";
        public const string Publishing = "Publishing…";
        public const string Published = "Published";
        public const string PostFootnote = "Written by a person on Margin. No like counts, no autoplay: read it and go read your book.";
        public const string PostPublicNote = "Public to every Margin reader. Your followers see it in their feed. Write it yourself: no AI text.";
        public const string WatchOnYouTube = "Watch on YouTube";
        public const string DeletePostTitle = "Delete this post?";
        public const string DeletePostBody = "Your followers won't see it anymore.";
        public const string Delete = "Delete";
        public const string PostDeleted = "Post deleted";
        public const string PostGone = "This post is gone. The creator may have deleted it.";
        public const string AddBooks = "Add books";
        public const string Done = "Done";
        public const string CreatorsOnly = "Turn on Creator account in Settings to publish.";
        public const string NotePlaceholder = "A short note: why this one? (optional)";
        public const string YouTubeHelp = "Only YouTube links. It opens in YouTube, never plays on its own.";
        public const string TurnOnCreatorFirst = "Turn on Creator account first (Settings).";

        // ---------- Settings: account ----------
        public const string AccountSection = "Account";
        public const string LogOut = "Log out";
        public const string LogOutTitle = "Log out?";
        public const string LogOutBody = "Your diary stays in your account. Log in again on any device to pick it up.";
        public const string GuestBody = "You're a guest. Save your account so you don't lose your diary if you change phones.";
        public const string UniversityLabel = "University";
        public const string Saved = "Saved";
        public const string PrivacyNote = "Your diary is saved privately in your account (and on this phone). Friends only see your current book and page, your momentum and the books you finished. We don't use your writing to train any AI.";
        public const string Unsynced = "Some changes are only on this phone. They'll sync when you're back online.";

        static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        public static readonly string[] DayLetters = { "S", "M", "T", "W", "T", "F", "S" }; // index = DayOfWeek (0 = Sunday)
    }
}
