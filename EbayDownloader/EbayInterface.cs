using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace EbayDownloader
{
    public static class EbayInterface
    {
        [Serializable]
        public struct TransactionRecord
        {
            public double Price;
            public int Quantity;
            public DateTime TimeStamp;
        }

        [Serializable]
        public struct Listing
        {
            public TransactionRecord[] Transactions;
            public double currentPrice;
            public string URL;
            public string Name;
            public int totalSold; // If 0, that means the listing is new
        }

        public enum PageMode
        {
            All,
            Num,
        }

        public enum IndexMode
        {
            TransactionHistory,
            ListingOnly
        }

        public static TransactionRecord CreateTransactionRecord(double price, int quanity, DateTime timeStamp)
        {
            TransactionRecord res = new TransactionRecord();
            res.Price = price;
            res.Quantity = quanity;
            res.TimeStamp = timeStamp;
            return res;
        }

        public static void ExportToCSV(Listing[] listings, string file)
        {
            StreamWriter writer = File.CreateText(file);
            writer.WriteLine("price,totalSold,url");
            for (int i = 0; i < listings.Length; i++)
            {
                writer.WriteLine(listings[i].currentPrice + "," + listings[i].totalSold + ",\"" + listings[i].URL+"\"");
            }
            writer.Close();
            writer.Dispose();
        }

        public static void SerializeToFile(Listing[] listings, string file)
        {
            XmlSerializer xsSubmit = new XmlSerializer(typeof(Listing[]));
            string xml = "";

            using (StringWriter sww = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sww))
                {
                    xsSubmit.Serialize(writer, listings);
                    xml = sww.ToString(); // Your XML
                    File.WriteAllText(file, xml);
                }
            }
        }

        public static Listing[] DeserializeFromFile(string file)
        {
            XmlSerializer xsSubmit = new XmlSerializer(typeof(Listing[]));

            Listing[] res;

            using (Stream reader = new FileStream(file, FileMode.Open))
            {
                // Call the Deserialize method to restore the object's state.
                res = (Listing[])xsSubmit.Deserialize(reader);
            }

            return res;
        }

        public class Search
        {
            string Keywords;
            string Exclude;
            PageMode Pagemode;
            int PageNum;
            IndexMode IndexMode;
            Tuple<string, int> Proxy; //, string> Proxy;

            private string GenerateRequest()
            {
                return "https://www.ebay.com/sch/i.html?_from=R40&_nkw=" + HttpUtility.UrlEncode(Keywords) + "&_inkw=1&_exkw=" + HttpUtility.UrlEncode(Exclude) + "&_sacat=0&_udlo=&_udhi=&_ftrt=901&_ftrv=1&_sabdlo=&_sabdhi=&_samilow=&_samihi=&_sadis=15&_stpos=40404&_sargn=-1%26saslc%3D1&_salic=1&_sop=12&_dmd=1&_ipg=200&_fosrp=1";
            }

            public Search(string keywords, PageMode pagemode, IndexMode indexmode = IndexMode.ListingOnly, string exclude = "", int pageNum = 1, Tuple<string, int> proxy = null)//, string> proxy = null)
            {
                Keywords = keywords;
                Exclude = exclude;
                Pagemode = pagemode;
                PageNum = pageNum;
                IndexMode = indexmode;
                Proxy = proxy;
            }

            private string GetHtml(string url)
            {
                string html = string.Empty;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AutomaticDecompression = DecompressionMethods.GZip;

                if (Proxy != null) request.Proxy = new WebProxy(Proxy.Item1, Proxy.Item2);

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    html = reader.ReadToEnd();
                }
                return html;
            }

            private string XpathResSelect = "//li[contains(@class,\"sresult lvresult clearfix li\")]/h3[@class=\"lvtitle\"]/a";
            private string XpathNextPage = "//a[@class=\"gspr next\"]";

            public string[] GetAllPageHrefs(bool log = true)
            {
                string url = GenerateRequest();
                List<string> res = new List<string>();
                if(Pagemode == PageMode.Num)
                {
                    for (int i = 0; i < PageNum; i++)
                    {
                        if (log) Console.WriteLine("{0}/{1} pages indexed", i + 1, PageNum);
                        Tuple<string[], string> HrefAndPage = GetHrefs(url);
                        res.AddRange(HrefAndPage.Item1);
                        if (log && HrefAndPage.Item2 == url) Console.WriteLine("No more pages!");
                        if (HrefAndPage.Item2 == url) break;
                        url = HrefAndPage.Item2;
                    }
                }
                else
                {
                    int i = 0;
                    while (true)
                    {
                        if (log) Console.WriteLine("{0} pages indexed", (i++) + 1);
                        Tuple<string[], string> HrefAndPage = GetHrefs(url);
                        res.AddRange(HrefAndPage.Item1);
                        if (log && HrefAndPage.Item2 == url) Console.WriteLine("No more pages!");
                        if (HrefAndPage.Item2 == url) break;
                        url = HrefAndPage.Item2;
                    }
                }
                return res.ToArray();
            }

            private Tuple<string[], string> GetHrefs(string url)
            {
                //StreamWriter writer2 = File.CreateText("get.txt");
                //writer2.Write(url);
                //writer2.Close();
                //writer2.Dispose();

                string html = GetHtml(url);

                //StreamWriter writer2 = File.CreateText("export.html");
                //writer2.Write(html);
                //writer2.Close();
                //writer2.Dispose();

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                HtmlNode nextpagenode = htmlDoc.DocumentNode.SelectSingleNode(XpathNextPage);
                string nextPage = url;
                if (nextpagenode != null)
                {
                    nextPage = WebUtility.HtmlDecode(nextpagenode.GetAttributeValue("href", "null"));
                    if (nextPage == "null") throw new Exception("Next Page Invalid");
                }

                HtmlNodeCollection hrefNodes = htmlDoc.DocumentNode.SelectNodes(XpathResSelect);

                List<string> hrefs = new List<string>();

                foreach (HtmlNode linkNode in hrefNodes)
                {
                    string href = linkNode.GetAttributeValue("href", "null");
                    if (href != "null") hrefs.Add(WebUtility.HtmlDecode(href));
                }

                return new Tuple<string[], string>(hrefs.ToArray(), nextPage);
            }

            private string XpathPostingTotalSold = "//a[@class=\"vi-txt-underline\"]";
            private string XpathCurPrice = "//span[@id=\"prcIsum\"]";
            private string XpathTitle = "//h1[@id=\"itemTitle\"]/text()";

            public Listing GetListing(string url)
            {
                string html = GetHtml(url);
                //StreamWriter writer2 = File.CreateText("export2.html");
                //writer2.Write(html);
                //writer2.Close();
                //writer2.Dispose();

                Listing listing = new Listing();

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                HtmlNode CurPriceNode = htmlDoc.DocumentNode.SelectSingleNode(XpathCurPrice);
                listing.currentPrice = parseEbayPrice(CurPriceNode.InnerText);

                HtmlNode PostingTotalSold = htmlDoc.DocumentNode.SelectSingleNode(XpathPostingTotalSold);

                //if (href == "null") throw new Exception("No HREF??");
                if (PostingTotalSold == null)
                {
                    //this listing is new and has not sold anything.
                    listing.Transactions = new TransactionRecord[0];
                    listing.totalSold = 0;
                }
                else
                {
                    listing.totalSold = int.Parse(PostingTotalSold.InnerText.Split(' ')[0].Replace(",", ""));
                    string href = PostingTotalSold.GetAttributeValue("href", "null");
                    if (IndexMode == IndexMode.TransactionHistory)
                    {
                        try
                        {
                            listing.Transactions = GetTransactionHistory(WebUtility.HtmlDecode(href));
                        }
                        catch (Exception)
                        {
                            listing.Transactions = new TransactionRecord[0];
                        }
                    }
                    else
                    {
                        listing.Transactions = new TransactionRecord[0];
                    }
                }

                listing.URL = WebUtility.HtmlDecode(url);

                HtmlNode TitleNode = htmlDoc.DocumentNode.SelectSingleNode(XpathTitle);

                listing.Name = WebUtility.HtmlDecode(TitleNode.InnerText);

                return listing;
            }

            private double parseEbayPrice(string ebayPrice)
            {
                if (ebayPrice.Contains("/"))
                {
                    return double.Parse(ebayPrice.Split(' ')[1].Split('/')[0].Substring(1));
                }
                return double.Parse(ebayPrice.Split(' ')[1].Substring(1));
            }

            string XpathOfferPrice = "//div[@class=\"pagecontainer\"]/table[2]//div[@class=\"BHbidSecBorderGrey\"]//tr/td[3]";
            string XpathOfferQuant = "//div[@class=\"pagecontainer\"]/table[2]//div[@class=\"BHbidSecBorderGrey\"]//tr/td[4]";
            string XpathOfferDateTime = "//div[@class=\"pagecontainer\"]/table[2]//div[@class=\"BHbidSecBorderGrey\"]//tr/td[5]";

            private TransactionRecord[] GetTransactionHistory(string url)
            {
                string html = GetHtml(url);
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                List<TransactionRecord> res = new List<TransactionRecord>();
                HtmlNode[] PriceNodes = htmlDoc.DocumentNode.SelectNodes(XpathOfferPrice).ToArray();
                HtmlNode[] QuantNodes = htmlDoc.DocumentNode.SelectNodes(XpathOfferQuant).ToArray();
                HtmlNode[] DateTimeNodes = htmlDoc.DocumentNode.SelectNodes(XpathOfferDateTime).ToArray();
                if (PriceNodes.Length != QuantNodes.Length || PriceNodes.Length != DateTimeNodes.Length) throw new Exception("Length mismatch!");
                for (int i = 0; i < PriceNodes.Length; i++)
                {
                    string[] priceparts = PriceNodes[i].InnerText.Split(' ');
                    if (priceparts.Length == 1) continue;
                    if (priceparts[1].Length <= 1) continue;
                    double price;
                    bool parseSuccess = double.TryParse(priceparts[1].Substring(1), out price);
                    if (!parseSuccess) continue;
                    int quantity;
                    parseSuccess = int.TryParse(QuantNodes[i].InnerText, out quantity);
                    if (!parseSuccess) continue;
                    DateTime dateTime = SimpleDateTime.ParseDateTimeZone(DateTimeNodes[i].InnerText).ToUniversalTime();
                    res.Add(CreateTransactionRecord(price, quantity, dateTime));
                }
                return res.OrderByDescending((x)=>x.TimeStamp).ToArray();
            }

            public Listing[] GetListings(string[] hrefs, bool log = true)
            {
                List<Listing> listings = new List<Listing>();
                
                for (int i = 0; i < hrefs.Length; i++)
                {
                    if (log) Console.WriteLine("Processing Listing {0}/{1}...", i + 1, hrefs.Length);
                    try
                    {
                        listings.Add(GetListing(hrefs[i]));
                    }
                    catch (Exception)
                    {
                        if (log) Console.WriteLine("Failed getting {0}!", hrefs[i]);
                    }
                }
                //return hrefs.Select((x) => GetListing(x)).ToArray();
                return listings.ToArray();
            }

            public Listing[] GetListingsMultiThreaded(string[] hrefs, bool log = false)
            {
                List<Listing> listings = new List<Listing>();

                Parallel.For(0, hrefs.Length, (i)=> {
                    if (log) Console.WriteLine("Processing Listing {0}/{1}...", i + 1);
                    try
                    {
                        Listing listing = GetListing(hrefs[i]);
                        lock (listings)
                        {
                            listings.Add(listing);
                        }
                    }
                    catch (Exception)
                    {
                        if (log) Console.WriteLine("Failed getting {0}!", hrefs[i]);
                    }
                });
                //return hrefs.Select((x) => GetListing(x)).ToArray();
                return listings.ToArray();
            }
        }
    }
}
