// Who is who to me: friends, requests in both directions and the creators I follow.
// Built from the raw friendship rows, so every screen asks the same question the same way.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Margin.Core
{
    public enum RelationKind
    {
        /// <summary>It's me.</summary>
        Self,
        /// <summary>No row between us.</summary>
        None,
        /// <summary>Accepted friendship.</summary>
        Friend,
        /// <summary>They asked me; I can accept or decline.</summary>
        Incoming,
        /// <summary>I asked them; I can cancel.</summary>
        Outgoing,
    }

    public struct Relation
    {
        public RelationKind Kind;
        /// <summary>The friendship row behind it (null for Self and None).</summary>
        public Friendship Row;
    }

    /// <summary>A friendship row with the other person's profile.</summary>
    public class Connection
    {
        public Friendship Row;
        public Profile Person;
    }

    /// <summary>Something worth telling the user after a refresh.</summary>
    public struct SocialNews
    {
        public bool NewRequest;   // false = one of my requests was accepted
        public Profile Person;
    }

    public class SocialGraph
    {
        public string Me { get; private set; }
        public List<Connection> Friends { get; private set; } = new List<Connection>();
        public List<Connection> Incoming { get; private set; } = new List<Connection>();
        public List<Connection> Outgoing { get; private set; } = new List<Connection>();
        public HashSet<string> Following { get; private set; } = new HashSet<string>();

        public static readonly SocialGraph Empty = new SocialGraph();

        /// <param name="me">My user id.</param>
        /// <param name="rows">Every friendship row that involves me.</param>
        /// <param name="following">Ids of the creators I follow.</param>
        /// <param name="profile">Looks up a profile by id (rows without a known profile are skipped).</param>
        public static SocialGraph Build(string me, IEnumerable<Friendship> rows, IEnumerable<string> following, Func<string, Profile> profile)
        {
            var g = new SocialGraph { Me = me, Following = new HashSet<string>(following ?? Enumerable.Empty<string>()) };
            foreach (var r in rows ?? Enumerable.Empty<Friendship>())
            {
                if (r == null || !r.Involves(me)) continue;
                var p = profile(r.OtherThan(me));
                if (p == null) continue;
                var c = new Connection { Row = r, Person = p };
                if (r.IsAccepted) g.Friends.Add(c);
                else if (r.addressee == me) g.Incoming.Add(c);
                else g.Outgoing.Add(c);
            }
            g.Friends = g.Friends.OrderBy(c => AccountRules.DisplayName(c.Person), StringComparer.OrdinalIgnoreCase).ToList();
            return g;
        }

        public Relation Resolve(string userId)
        {
            if (userId == Me) return new Relation { Kind = RelationKind.Self };
            var f = Friends.Find(c => c.Person.id == userId);
            if (f != null) return new Relation { Kind = RelationKind.Friend, Row = f.Row };
            var i = Incoming.Find(c => c.Person.id == userId);
            if (i != null) return new Relation { Kind = RelationKind.Incoming, Row = i.Row };
            var o = Outgoing.Find(c => c.Person.id == userId);
            if (o != null) return new Relation { Kind = RelationKind.Outgoing, Row = o.Row };
            return new Relation { Kind = RelationKind.None };
        }

        public bool IsFriend(string userId) => Resolve(userId).Kind == RelationKind.Friend;
        public bool IsFollowing(string userId) => Following.Contains(userId);

        /// <summary>We may see someone's activity when they are a friend or a creator I follow (same as the database rule).</summary>
        public bool CanSeeActivityOf(string userId) => IsFriend(userId) || IsFollowing(userId);

        public Connection FriendConnection(string userId) => Friends.Find(c => c.Person.id == userId);

        /// <summary>Friends I agreed to read a book with (only books in the catalog).</summary>
        public List<Connection> Pacts(Catalog catalog) =>
            Friends.Where(c => !string.IsNullOrEmpty(c.Row.shared_book_id) && catalog.FindBook(c.Row.shared_book_id) != null).ToList();

        /// <summary>Friends with a current book from the catalog.</summary>
        public List<Connection> ReadingNow(Catalog catalog) =>
            Friends.Where(c => !string.IsNullOrEmpty(c.Person.current_book_id) && catalog.FindBook(c.Person.current_book_id) != null).ToList();

        /// <summary>New friend requests and newly accepted requests since <paramref name="before"/>.</summary>
        public List<SocialNews> NewsSince(SocialGraph before)
        {
            var news = new List<SocialNews>();
            if (before == null || before.Me != Me) return news;
            foreach (var c in Incoming)
                if (before.Resolve(c.Person.id).Kind == RelationKind.None)
                    news.Add(new SocialNews { NewRequest = true, Person = c.Person });
            foreach (var c in Friends)
                if (before.Resolve(c.Person.id).Kind == RelationKind.Outgoing)
                    news.Add(new SocialNews { NewRequest = false, Person = c.Person });
            return news;
        }
    }
}
