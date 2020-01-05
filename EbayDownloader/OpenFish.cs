using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OpenFishPort
{
    class OpenFish
    {
        //IP, PORT, COUNTRY CODE
        public List<Tuple<string, int, string>> Proxies = new List<Tuple<string, int, string>>();
        static Random rnd = new Random();

        string ListURL = "";
        string TestEndpoint = "";

        private string GetHtml(string url)
        {
            string html = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }
            return html;
        }

        public OpenFish(string listURL = "https://www.us-proxy.org/", string testEnpoint = "http://example.com/")
        {
            ListURL = listURL;
            TestEndpoint = testEnpoint;
        }

        public void Get()
        {
            string html = GetHtml(ListURL);
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            HtmlNodeCollection IpNodes = htmlDoc.DocumentNode.SelectNodes("//table[@id=\"proxylisttable\"]/tbody//td[1]");
            HtmlNodeCollection PortNodes = htmlDoc.DocumentNode.SelectNodes("//table[@id=\"proxylisttable\"]/tbody//td[2]");
            HtmlNodeCollection CountryNodes = htmlDoc.DocumentNode.SelectNodes("//table[@id=\"proxylisttable\"]/tbody//td[3]");

            if (IpNodes.Count != PortNodes.Count || IpNodes.Count != CountryNodes.Count) throw new Exception("Node length mismatch");

            for (int i = 0; i < IpNodes.Count; i++)
            {
                Proxies.Add(new Tuple<string, int, string>(IpNodes[i].InnerText, int.Parse(PortNodes[i].InnerText), CountryNodes[i].InnerText));
            }
        }

        public Tuple<string, int, string> GetRandom()
        {
            int r = rnd.Next(Proxies.Count);
            return Proxies[r];
        }

        public long TimeProxy(Tuple<string, int> proxy, bool log = true)
        {
            long time = getRespTime(TestEndpoint, new Tuple<string, int>(proxy.Item1, proxy.Item2));
            if (log)
            {
                if (time == -1) Console.WriteLine("Proxy timed out.");
                else Console.WriteLine("Proxy took {0}ms to respond.", time);
            }
            return time;
        }

        public Tuple<string, int, string, long>[] GetResponseTimesMultiThreaded(bool log = true, int timeout = 750)
        {
            Tuple<string, int, string, long>[] ret = new Tuple<string, int, string, long>[Proxies.Count];
            long timebase = getRespTime(TestEndpoint);
            if (timebase == -1) Console.WriteLine("Localhost timed out.");
            else Console.WriteLine("Localhost {0}ms to respond.", timebase);

            Parallel.For(0, Proxies.Count, (i) => {
                long time = getRespTime(TestEndpoint, new Tuple<string, int>(Proxies[i].Item1, Proxies[i].Item2), timeout, false);
                if (log)
                {
                    if (time == -1) Console.WriteLine("Proxy {0} timed out.", i + 1);
                    else Console.WriteLine("Proxy {0} took {1}ms to respond.", i + 1, time);
                }
                ret[i] = new Tuple<string, int, string, long>(Proxies[i].Item1, Proxies[i].Item2, Proxies[i].Item3, time);
            });

            return ret;
        }

        public Tuple<string, int, string, long>[] GetResponseTimes(bool log = true, int timeout = 750)
        {
            Tuple<string, int, string, long>[] ret = new Tuple<string, int, string, long>[Proxies.Count];
            long timebase = getRespTime(TestEndpoint);
            if (timebase == -1) Console.WriteLine("Localhost timed out.");
            else Console.WriteLine("Localhost {0}ms to respond.", timebase);

            for (int i = 0; i < Proxies.Count; i++)
            {
                long time = getRespTime(TestEndpoint, new Tuple<string, int>(Proxies[i].Item1, Proxies[i].Item2), timeout, log);
                if (log)
                {
                    if (time == -1) Console.WriteLine("Proxy {0}/{1} timed out.", i + 1, Proxies.Count);
                    else Console.WriteLine("Proxy {0}/{1} took {2}ms to respond.", i + 1, Proxies.Count, time);
                }
                ret[i] = new Tuple<string, int, string, long>(Proxies[i].Item1, Proxies[i].Item2, Proxies[i].Item3, time);
            }
            return ret;
        }

        public static long getRespTime(string apiEndpoint, Tuple<string, int> proxy = null, int timeout = 750, bool log = true)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            long elapsed = 0;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiEndpoint);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.Timeout = timeout;
            if (proxy != null) request.Proxy = new WebProxy(proxy.Item1, proxy.Item2);

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        timer.Stop();
                        elapsed = timer.ElapsedMilliseconds;
                    }
                    else
                    {
                        elapsed = -1;
                    }
                }
            }
            catch (Exception ex)
            {
                if (log) Console.WriteLine(ex.Message);
                return -1;
            }
            

            return elapsed;
        }
    }
}
