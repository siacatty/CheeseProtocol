using System;

namespace CheeseProtocol
{
    public static class ChzzkUrlUtil
    {
        // supports: https://studio.chzzk.naver.com/{id}/
        public static bool TryExtractStudioId(string url, out string id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            url = url.Trim();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // host check (loose)
            if (!uri.Host.Contains("studio.chzzk.naver.com"))
                return false;

            var path = uri.AbsolutePath.Trim('/');
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // first segment is the id
            var seg = path.Split('/')[0].Trim();
            if (seg.Length < 10) return false; // cheap sanity
            id = seg;
            return true;
        }
    }
}