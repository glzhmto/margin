using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Margin.App.Services.Net;
using Margin.Core;

namespace Margin.App.Services
{
    /// <summary>Real Gandhi prices for printed copies (book-prices edge function), cached for the session.</summary>
    public class PriceService
    {
        readonly SupabaseClient _sb;
        readonly string _function;
        readonly Dictionary<string, PriceQuote> _cache = new Dictionary<string, PriceQuote>();

        public PriceService(SupabaseClient sb, BackendConfig cfg)
        {
            _sb = sb;
            _function = cfg.pricesFunction;
        }

        public async Task<Result<PriceQuote>> For(string bookId)
        {
            if (_cache.TryGetValue(bookId, out var cached)) return Result<PriceQuote>.Success(cached);
            var r = await _sb.Function<PriceQuote>(_function, "book=" + Uri.EscapeDataString(bookId));
            if (r.Ok && r.Value != null) _cache[bookId] = r.Value;
            else if (r.Ok) r = new Result<PriceQuote> { Ok = false, Error = Copy.NetFail };
            return r;
        }
    }
}
