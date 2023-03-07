using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace RegionScaniaNodeScraper.NodeScraper
{
    public class NodeReaderFactory
    {       
        //protected static IServiceCollection Services => new ServiceCollection();
        protected static IHttpClientFactory HttpClientFactory;

        /// <summary>
        /// Implements the Factory pattern for the iNodeReader object
        /// <returns></returns>
        public static iNodeReader Instance(string uri,string localrootfolder)
        {
            if(HttpClientFactory is null)
            {
                IServiceCollection services = new ServiceCollection();
                services.AddHttpClient<iNodeReader>(
                    configureClient: options =>
                    {
                        options.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 1.0));
                    });
                var ff = services.BuildServiceProvider();

                HttpClientFactory = ff.GetService<IHttpClientFactory>();
                NodeReader.LocalRootFolder = localrootfolder;
                
            }

            //make sure the NodeReader has access to the a Factory of http-clients
            NodeReader.HttpClientFactory = NodeReaderFactory.HttpClientFactory; 

            return new NodeReader(uri);           

        }
    }
}
