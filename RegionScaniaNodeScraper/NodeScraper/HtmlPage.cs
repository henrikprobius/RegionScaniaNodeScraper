using HtmlAgilityPack;

namespace RegionScaniaNodeScraper.NodeScraper
{
    /// <summary>
    /// Represents a htmlpage as a stream in the constructor
    /// </summary>   
    public class HtmlPage
    {
        protected HtmlDocument doc = new();

        public HtmlPage(Stream? data)
        {
            doc.Load(data);
        }

       
        /// <summary>
        /// Writes the loaded htmlpage to the local filesystem.
        /// </summary>
        /// <returns> Returns false if the file already was downloaded,otherwise true </returns>
        public bool WritePageToLocalDisc(Uri baseUri, string path)
        {
            if (File.Exists(path))
                return false;

            Console.WriteLine("Page downloaded " + baseUri.ToString());
            try
            {
                using (Stream dest = File.Open(path, FileMode.CreateNew))
                {
                    doc?.Save(dest);
                    dest.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception writing htmldocument to file: " + path + ". Message is " + e.Message);
            }

            return true;
        }



        /// <summary>
        /// Extracts all file references, images , script, css etc that exists in the loaded html file
        /// </summary>
        /// <returns> IEnumerable of string , full path</returns>
        public IEnumerable<string> ExtractFileRefsOnHtmlPage(Uri baseUri)
        {

            //get all filerefs
            var theImageset = doc.DocumentNode.Descendants("img")
                            .Select(e => e.GetAttributeValue("src", null))
                            .Where(s => !String.IsNullOrEmpty(s));

            //get all filerefs denoted with <link>
            var theCssFiles = doc.DocumentNode.Descendants("link")
            .Select(e => e.GetAttributeValue("href", null))
            .Where(s => !String.IsNullOrEmpty(s));

            HashSet<string> set = new HashSet<string>();
            foreach (String node in theCssFiles.Concat(theImageset)) //convert to set => no duplicates
            {
                if (node is null) continue;
                set.Add(node.ExtractUpPath(baseUri));
            }

            return set;
        }


        /// <summary>
        /// Extracts all anchortags in the loaded html page
        /// </summary>
        /// <returns> IEnumerable of string, fullpath </returns>
        public IEnumerable<string> ExtractAllHrefNodes(Uri baseUri, Uri rootUri)
        {

            // Use XPath to select all the anchor tags in the HTML document
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a");

            HashSet<string> returnSet = new HashSet<string>();

            string? href;
            foreach (HtmlNode node in nodes)
            {
                // Get the value of the href attribute
                href = node.GetAttributeValue("href", "");

                if (href is null || href == "index.html") continue;

                if (!href.Contains("index.html") && !href.Contains(@"/"))
                {
                    href = baseUri.AbsolutePath.Replace("index.html", href);
                    if (href.StartsWith("/"))
                        href = href.Remove(0, 1);
                }

                if (!href.StartsWith(@"../") && href.EndsWith(@"/index.html") && !baseUri.ToString().EndsWith("index.html"))
                {
                    var segments = baseUri.Segments;
                    href = baseUri.AbsolutePath.Replace(segments[segments.Length - 1], href);
                    if (href.StartsWith("/"))
                        href = href.Remove(0, 1);
                }

                //if the link is like this: ../../path1/path2/file.html, calculate the proper relative path
                href = href.ExtractUpPath(baseUri);

                //handle when the page contains a link to itself
                if (rootUri.ToString() + href == baseUri.ToString())
                {
                    continue;
                }

                returnSet.Add(href.Trim());

            }
            return returnSet;
        }
    }//class
}
