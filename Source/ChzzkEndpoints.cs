using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;

namespace CheeseProtocol
{
    internal static class ChzzkEndpoints
    {
        private static readonly Regex ChannelIdRegex =
        new Regex(@"(?i)^[0-9a-f]{32}$", RegexOptions.Compiled);
        public static bool TryExtractChannelId(string input, out string channelId)
        {
            channelId = null;
            if (string.IsNullOrWhiteSpace(input)) return false;

            input = input.Trim();

            // If scheme is missing, prepend https:// so Uri can parse it
            if (input.IndexOf("://", StringComparison.OrdinalIgnoreCase) < 0)
                input = "https://" + input;

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host.ToLowerInvariant();

            // Accept only these hosts (you can relax if you want)
            bool isStudio = host == "studio.chzzk.naver.com";
            bool isMain = host == "chzzk.naver.com";
            if (!isStudio && !isMain) return false;

            // Split path segments
            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Patterns:
            // 1) studio: /{id} or /{id}/live
            // 2) main:  /live/{id}
            // 3) main:  /{id}
            if (segments.Length == 0) return false;

            // /live/{id}
            if (segments.Length >= 2 && segments[0].Equals("live", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateId(segments[1], out channelId);
            }

            // /{id} or /{id}/live
            // (Works for both studio + main)
            if (ValidateId(segments[0], out channelId))
                return true;

            return false;
        }

        private static bool ValidateId(string candidate, out string id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(candidate)) return false;

            // Remove any trailing stuff just in case (rare, but safe)
            candidate = candidate.Trim();

            if (!ChannelIdRegex.IsMatch(candidate))
                return false;

            id = candidate.ToLowerInvariant();
            return true;
        }

        public static bool TryResolveChatChannelId(string channelId, out string chatChannelId)
        {
            chatChannelId = null;
            if (string.IsNullOrWhiteSpace(channelId)) return false;

            var url = $"https://api.chzzk.naver.com/polling/v2/channels/{channelId}/live-status";
            var json = HttpGet(url);
            if (string.IsNullOrWhiteSpace(json)) return false;

            var root = MiniJSON.Deserialize(json) as Dictionary<string, object>;
            if (root == null) return false;

            if (!root.TryGetValue("content", out var contentObj)) return false;
            var content = contentObj as Dictionary<string, object>;
            if (content == null) return false;

            if (!content.TryGetValue("chatChannelId", out var ccidObj)) return false;
            chatChannelId = ccidObj?.ToString();
            return !string.IsNullOrWhiteSpace(chatChannelId);
        }

        public static bool TryFetchChatAccessToken(string cid, out string accTkn, out string extraTkn)
        {
            accTkn = null;
            extraTkn = null;

            try
            {
                var url = $"https://comm-api.game.naver.com/nng_main/v1/chats/access-token?channelId={Uri.EscapeDataString(cid)}&chatType=STREAMING";
                var json = HttpGet(url);
                if (string.IsNullOrWhiteSpace(json)) return false;

                var root = MiniJSON.Deserialize(json) as System.Collections.Generic.Dictionary<string, object>;
                if (root == null) return false;

                if (!root.TryGetValue("content", out var contentObj)) return false;
                var content = contentObj as System.Collections.Generic.Dictionary<string, object>;
                if (content == null) return false;

                // content.accessToken / content.extraToken
                accTkn = content.TryGetValue("accessToken", out var at) ? at?.ToString() : null;
                extraTkn = content.TryGetValue("extraToken", out var et) ? et?.ToString() : null;

                if (string.IsNullOrWhiteSpace(accTkn)) return false;

                //Log.Message("[CheeseProtocol] chatAccessToken fetched");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[CheeseProtocol] TryFetchChatAccessToken error: " + ex);
                return false;
            }
        }

        private static string HttpGet(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                req.Accept = "application/json, text/plain, */*";
                req.Headers["Accept-Language"] = "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7";

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[CheeseProtocol] HttpGet failed: {e.Message}");
                return null;
            }
        }
    }
}