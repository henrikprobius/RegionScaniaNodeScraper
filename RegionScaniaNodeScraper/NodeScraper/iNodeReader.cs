namespace RegionScaniaNodeScraper.NodeScraper
{
    public interface iNodeReader
    {
        public (string message, bool success) ReadNode();

        public static CancellationTokenSource CancellationTokenSource { get; }

        public static string LocalRootFolder { get; set; }

        public Uri BaseUri { get; }
    }
}
