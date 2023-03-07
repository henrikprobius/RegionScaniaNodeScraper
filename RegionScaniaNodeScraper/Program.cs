
using RegionScaniaNodeScraper.NodeScraper;


iNodeReader s = NodeReaderFactory.Instance(@"https://books.toscrape.com/index.html", @"C:\Temp\slask\");

var result = s.ReadNode();



