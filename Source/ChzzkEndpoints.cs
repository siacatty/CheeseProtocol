using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    internal static class ChzzkEndpoints
    {
        public static bool TryExtractChannelIdFromStudioUrl(string studioUrl, out string channelId)
        {
            channelId = null;
            if (string.IsNullOrWhiteSpace(studioUrl)) return false;

            // examples:
            // https://studio.chzzk.naver.com/fc42ea9bb15cd218dd205d1c9bd0f1cf/
            // https://studio.chzzk.naver.com/fc42ea9bb15cd218dd205d1c9bd0f1cf
            try
            {
                var u = new Uri(studioUrl.Trim());
                var segs = u.AbsolutePath.Trim('/').Split('/');
                if (segs.Length >= 1 && segs[0].Length >= 8)
                {
                    channelId = segs[0];
                    return true;
                }
            }
            catch
            {
                // maybe user pasted without scheme
                var s = studioUrl.Trim();
                if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    s = "https://" + s;

                try
                {
                    var u2 = new Uri(s);
                    var segs2 = u2.AbsolutePath.Trim('/').Split('/');
                    if (segs2.Length >= 1 && segs2[0].Length >= 8)
                    {
                        channelId = segs2[0];
                        return true;
                    }
                }
                catch { }
            }

            return false;
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