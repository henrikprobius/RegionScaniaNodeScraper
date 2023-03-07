namespace RegionScaniaNodeScraper.NodeScraper
{
        public class NodeReader : iNodeReader
        {
            //holds the pure domain the scraping is done from
            protected static Uri RootUri { get; set; } = null!;

            /// <summary>
            /// Holds the http-client provided by the static HttpClientyFactory member
            /// </summary>
            /// <returns></returns>
            protected HttpClient? client = null;

            
            /// <summary>
            /// Holds the downloaded html document
            /// </summary>           
            protected HtmlPage? doc;


            /// <summary>
            /// -1 means use all cores in the CPU
            /// </summary>            
            protected static ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = -1 };


            /// <summary>
            /// The static httpclientFactory that creates a http-client for every node
            /// </summary>            
            public static IHttpClientFactory HttpClientFactory { get; set; } = null!;


            /// <summary>
            /// Token for the abillity to cancel the whole scraping
            /// </summary>
            public static CancellationTokenSource CancellationTokenSource { get; } = new();


            /// <summary>
            /// The local rootfolder where all the files and subdirectories are saved
            /// </summary>
            public static string LocalRootFolder { get; set; }

    
            /// <summary>
            /// The local folder where all the files for a specific node are saved, calculated in constructor for each node.
            /// </summary>
            protected string FullLocalFilePath = string.Empty;


            /// <summary>
            /// The Uri for the html page being downloaded
            /// </summary>
            public Uri BaseUri { get; }


            public NodeReader(string uri)
            {
                if (string.IsNullOrEmpty(uri) || string.IsNullOrWhiteSpace(uri)) throw new ArgumentNullException("Wrong Uri-format");

                //when called the first time, extract the pure domain like (https://books.toscrape.com/)
                if (NodeReader.RootUri is null)
                {
                    var tmp = new Uri(uri);
                    string requested = tmp.Scheme + Uri.SchemeDelimiter + tmp.Host + @"/";
                    NodeReader.RootUri = new Uri(requested);
                }

                //do not allow an Uri without a file ref like  this (https://books.toscrape.com/), pad with index.html if so
                if (!uri.Contains(".html"))
                    uri += "index.html";

                if (!uri.StartsWith(NodeReader.RootUri.ToString()))  //if it wasn't a full Uri, pad with RootUri
                    BaseUri = new(NodeReader.RootUri.AbsoluteUri + uri);
                else
                    BaseUri = new(uri);

                //calculate the local path where the downloaded file is to be saved
                var tmpPath = BaseUri.AbsoluteUri.Replace(NodeReader.RootUri.AbsoluteUri, "/");

                if (tmpPath.StartsWith("/"))
                    tmpPath = tmpPath.Remove(0, 1);
                FullLocalFilePath = Path.Combine(LocalRootFolder, tmpPath);
                parallelOptions.CancellationToken = NodeReader.CancellationTokenSource.Token;
                client = HttpClientFactory.CreateClient();
            }



            /// <summary>
            /// Reads and downloads a node (a specific Uri ) and all associated files
            /// </summary>
            /// <returns> a tuple (string message, bool success) </returns>
            public (String message, bool success) ReadNode()
            {
                //already downloaded? if so, assume that all attached files also are downloaded, no need to continue         
                if (File.Exists(this.FullLocalFilePath))
                {
                    this.client = null;
                    return ("File " + this.BaseUri.ToString() + " already downloaded.", true);
                }
                    

                //get the Htmlpage
                var content = this.client.GetAsync(BaseUri);
                var respons = content.GetAwaiter().GetResult();
                respons.EnsureSuccessStatusCode();

                //already downloaded? if so, assume that all attached files also are downloaded, no need to continue
                if (File.Exists(this.FullLocalFilePath))
                {
                    this.client = null;
                    return ("File " + this.BaseUri.ToString() + " already downloaded.", true);
                }   

                if (content is null)
                {                    
                    this.client = null;
                    return ("Html content is invalid", false);
                }

                this.doc = new HtmlPage(respons.Content.ReadAsStream());

                // if already downloaded, then assume all associated files are already downloaded also, exit
                if (!this.doc.WritePageToLocalDisc(this.BaseUri, this.FullLocalFilePath))
                {
                    this.client = null;
                    this.doc = null;
                    return ("File " + this.BaseUri.ToString() + " already downloaded.", true);
                }
          

                //extract and download all other file links on the current page
                var filesOnPage = doc.ExtractFileRefsOnHtmlPage(this.BaseUri);
                var task = DownloadFilesAsync(filesOnPage);
                task.GetAwaiter().GetResult();

                var theNodeset = doc.ExtractAllHrefNodes(BaseUri, NodeReader.RootUri);

                DownloadSubNodes(theNodeset);

                this.client = null;
                this.doc = null;
                return ("OK", true);
            }

            
            /// <summary>
            /// Executes downloading of all html-files by creating a NodeReader for each one and call ReadNode()
            /// </summary>
            /// <returns></returns>
            private void DownloadSubNodes(IEnumerable<string> theNodeset)
            {
                Parallel.ForEach(theNodeset, parallelOptions, node =>
                {
                    if(CancellationTokenSource.Token.IsCancellationRequested)
                        return; 

                    //check so the file is not already downloaded
                    if (!File.Exists(Path.Combine(NodeReader.LocalRootFolder, node)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(LocalRootFolder, node)));
                        var subNode = NodeReaderFactory.Instance(node,LocalRootFolder);
                        try
                        {
                            var g = subNode.ReadNode();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Exception downloading Node: " + subNode.BaseUri.ToString() + ". Message: " + e.Message);
                            subNode = null;
                        }
                    }
                });
            }


            /// <summary>
            /// Downloading files contained in the parameter list
            /// </summary>
            /// <returns>True if downloading went OK</returns>
            private async Task<bool> DownloadFilesAsync(IEnumerable<string> theFileset)
            {
                HttpResponseMessage respons;

                Stream streamToReadFrom;
                Stream streamToWriteTo;
                string? imgPath;

                foreach (String img in theFileset)
                {
                    if (CancellationTokenSource.Token.IsCancellationRequested)
                        return true;
                    imgPath = Path.Combine(LocalRootFolder, img);

                    if (File.Exists(imgPath))//already downloaded?
                        continue;

                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(LocalRootFolder, img)));


                respons = await client.GetAsync(RootUri.ToString() + img);
                    respons.EnsureSuccessStatusCode();

                    if (File.Exists(imgPath))//already downloaded?
                        continue;

                    streamToReadFrom = await respons.Content.ReadAsStreamAsync();
                    try
                    {
                        using (streamToWriteTo = File.Open(imgPath, FileMode.CreateNew))
                        {
                            streamToReadFrom.CopyTo(streamToWriteTo);
                            streamToWriteTo.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception in metod DownloadFilesAsync() for file " + imgPath + ". Message is " + e.Message);
                    }
                }

                return true;
            }


        }//class


}
