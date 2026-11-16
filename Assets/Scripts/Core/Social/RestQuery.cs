using System;
using System.Collections.Generic;
using System.Linq;

namespace Margin.Core
{
    /// <summary>
    /// Builds PostgREST paths (<c>profiles?select=id&amp;username=eq.ana</c>) so filters are written
    /// once and escaped the same way everywhere. Values are URL-encoded; ids and usernames never
    /// contain PostgREST syntax, and free text goes through <see cref="AccountRules.CleanQuery"/> first.
    /// </summary>
    public class RestQuery
    {
        readonly string _table;
        readonly List<KeyValuePair<string, string>> _params = new List<KeyValuePair<string, string>>();

        RestQuery(string table) { _table = table; }

        public static RestQuery From(string table) => new RestQuery(table);

        public string Table => _table;

        public RestQuery Select(string columns) => Add("select", columns);
        public RestQuery Eq(string column, string value) => Add(column, "eq." + value);
        public RestQuery Eq(string column, bool value) => Add(column, "eq." + (value ? "true" : "false"));
        public RestQuery Eq(string column, long value) => Add(column, "eq." + value);
        public RestQuery Neq(string column, string value) => Add(column, "neq." + value);
        public RestQuery IsNull(string column) => Add(column, "is.null");
        public RestQuery NotNull(string column) => Add(column, "not.is.null");
        /// <summary>Case-insensitive match; <c>*</c> is the wildcard.</summary>
        public RestQuery ILike(string column, string pattern) => Add(column, "ilike." + pattern);
        public RestQuery NotLike(string column, string pattern) => Add(column, "not.like." + pattern);
        public RestQuery In(string column, IEnumerable<string> values) => Add(column, "in.(" + string.Join(",", values) + ")");
        /// <summary>JSON containment, e.g. <c>data=cs.{"post_id":4}</c>.</summary>
        public RestQuery Contains(string column, string json) => Add(column, "cs." + json);
        /// <summary>Any of the conditions, e.g. <c>Or("requester.eq.a", "addressee.eq.a")</c>.</summary>
        public RestQuery Or(params string[] conditions) => Add("or", "(" + string.Join(",", conditions) + ")");
        public RestQuery Order(string column, bool ascending = true) => Add("order", column + (ascending ? ".asc" : ".desc"));
        public RestQuery Limit(int n) => Add("limit", n.ToString());
        public RestQuery OnConflict(string columns) => Add("on_conflict", columns);

        RestQuery Add(string key, string value)
        {
            _params.Add(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public override string ToString() =>
            _params.Count == 0 ? _table
                : _table + "?" + string.Join("&", _params.Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value)));
    }
}
