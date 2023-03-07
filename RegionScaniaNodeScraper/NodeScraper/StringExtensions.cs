

namespace RegionScaniaNodeScraper.NodeScraper
{
    public static class StringExtensions
    {
        //if the link contains a path like (or consecutives) ../, then create a proper relative path
        public static string ExtractUpPath(this string reluri, Uri baseUri)
        {
            if (reluri is null || string.IsNullOrEmpty(reluri)) return string.Empty;
            if (!reluri.Contains(@"../")) return reluri;

            var stepupwardsCount = reluri.Split("../").Count();
            var segments = baseUri.Segments;
            string ret = string.Empty;
            for (int i = 1; i < segments.Length - stepupwardsCount; i++)
                ret += segments[i];

            return ret + reluri.Replace(@"../", string.Empty);
        }
    }
}
