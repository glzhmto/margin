// Rows of the shared Supabase backend (the same tables the web version uses).
// Field names are the database column names on purpose: the App layer (de)serializes these
// classes directly, so web and iPhone users read and write exactly the same shapes.
using System;
using System.Collections.Generic;

namespace Margin.Core
{
    /// <summary>Public profile of a reader (table <c>profiles</c>).</summary>
    [Serializable]
    public class Profile
    {
        public string id;
        public string username;
        public string name;
        public string bio;
        public string university;
        public string color;
        public bool is_creator;
        public bool is_guest;
        public string current_book_id;
        public int? current_page;
        public string momentum_level;
        public int pages_this_week;
        public List<string> books_read = new List<string>();
        public List<string> favorites = new List<string>();
        public DateTime created_at;
    }

    public static class FriendshipStatus
    {
        public const string Pending = "pending";
        public const string Accepted = "accepted";
    }

    /// <summary>A friend request or an accepted friendship (table <c>friendships</c>).</summary>
    [Serializable]
    public class Friendship
    {
        public long id;
        public string requester;
        public string addressee;
        public string status;
        /// <summary>The book both friends agreed to read together, or null.</summary>
        public string shared_book_id;

        public bool IsAccepted => status == FriendshipStatus.Accepted;
        public bool Involves(string userId) => requester == userId || addressee == userId;
        /// <summary>The other person in the row, seen from <paramref name="me"/>.</summary>
        public string OtherThan(string me) => requester == me ? addressee : requester;
    }

    public static class ActivityKind
    {
        public const string Started = "started";
        public const string Pages = "pages";
        public const string Finished = "finished";
        public const string Quiz = "quiz";
        public const string Review = "review";
        public const string Post = "post";
        public const string Joined = "joined";
    }

    /// <summary>The <c>data</c> column of an activity row. Only the fields of its kind are set.</summary>
    [Serializable]
    public class ActivityData
    {
        public int? from;       // pages
        public int? to;         // pages
        public int? chapter;    // quiz
        public int? correct;    // quiz
        public int? total;      // quiz
        public double? rating;  // review
        public long? post_id;   // post
        public string title;    // post
        public string kind;     // post
    }

    /// <summary>Something a reader did, shown to friends and followers (table <c>activity</c>).</summary>
    [Serializable]
    public class Activity
    {
        public long id;
        public string user_id;
        public string kind;
        public string book_id;
        public ActivityData data = new ActivityData();
        public DateTime created_at;
    }

    public static class PostKind
    {
        public const string List = "list";
        public const string Text = "text";
        public const string Video = "video";
        public static readonly string[] All = { List, Text, Video };
    }

    /// <summary>One book in a creator's list. Stored as JSON inside <c>posts.items</c> (camelCase, like the web).</summary>
    [Serializable]
    public class PostItem
    {
        public string bookId;
        public string note;
    }

    /// <summary>The few author fields embedded with a post or review (<c>profiles(username,name,color)</c>).</summary>
    [Serializable]
    public class AuthorRef
    {
        public string username;
        public string name;
        public string color;
    }

    /// <summary>A creator's post: a book list, a note or a YouTube video (table <c>posts</c>).</summary>
    [Serializable]
    public class Post
    {
        public long id;
        public string author;
        public string kind;
        public string title;
        public string body;
        public string video_url;
        public List<PostItem> items = new List<PostItem>();
        public DateTime created_at;
        public AuthorRef profiles;
    }

    /// <summary>A Margin reader's review of a book (table <c>reviews</c>, one per user and book).</summary>
    [Serializable]
    public class Review
    {
        public long id;
        public string user_id;
        public string book_id;
        public double? rating;
        public string body;
        public DateTime created_at;
        public AuthorRef profiles;
    }

    /// <summary>One printed copy for sale (from the <c>book-prices</c> edge function).</summary>
    [Serializable]
    public class PriceOffer
    {
        public string title;
        public decimal price;
        public string url;
    }

    /// <summary>Response of the <c>book-prices</c> edge function.</summary>
    [Serializable]
    public class PriceQuote
    {
        public List<PriceOffer> offers = new List<PriceOffer>();
        public DateTime? fetchedAt;
    }
}
