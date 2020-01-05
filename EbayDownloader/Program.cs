using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandParser;

namespace EbayDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            ArgumentParser argumentParser = new ArgumentParser(ArgumentParser.ArgumentFormat.KVP, true);
            argumentParser.AddArgument("key", ArgumentParser.ArgumentType.String);
            argumentParser.AddArgument("exkey", ArgumentParser.ArgumentType.String, true);
            argumentParser.AddArgument("proxy", ArgumentParser.ArgumentType.String, true);
            argumentParser.AddArgument("nPage", ArgumentParser.ArgumentType.Int);
            argumentParser.AddArgument("export", ArgumentParser.ArgumentType.String);
            argumentParser.Help =
            () => {
                Console.WriteLine("-key \"<keywords to search for>\"");
                Console.WriteLine("-exkey \"<keywords to exclude from search>\"");
                Console.WriteLine("-proxy \"<proxy:port or auto for automatic proxy selection, if omitted the program will not use a proxy>\"");
                Console.WriteLine("-nPage <number of pages to scan, -1 for all>");
                Console.WriteLine("-export <A csv file to where the data will be exported>");
            };

            string exkey = "";
            string key = "";
            string proxy = "";
            int npage = 0;
            string exportFile;

            try
            {
                ArgumentParser.ParsedArguments parsed = argumentParser.Parse(args);
                key = parsed.StringArguments["key"][0];
                exportFile = parsed.StringArguments["export"][0];
                if (parsed.StringArguments.ContainsKey("exkey"))
                    exkey = parsed.StringArguments["exkey"][0];
                if (parsed.StringArguments.ContainsKey("proxy"))
                    proxy = parsed.StringArguments["proxy"][0];
                npage = parsed.IntArguments["npage"][0];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            if (System.IO.File.Exists(exportFile))
            {
                Console.WriteLine("File '{0}' already exists!", exportFile);
                return;
            }

            bool autoProxy = false;
            Tuple<string, int> userProxy = null;

            try
            {
                if (proxy.ToLower() == "auto") autoProxy = true;
                else
                {
                    userProxy = new Tuple<string, int>(proxy.Split(':')[0], int.Parse(proxy.Split(':')[1]));
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Incorrect proxy format. Expected format: 'Address:Port' or auto");
                return;
            }

            OpenFishPort.OpenFish fish = new OpenFishPort.OpenFish();
            if (autoProxy)
            {
                fish.Get();
                Tuple<string, int, string, long>[] resp = fish.GetResponseTimesMultiThreaded();
                Tuple<string, int, string, long> optimal = resp.Where((x) => x.Item4 != -1).OrderBy((x) => x.Item4).First();
                if (optimal==null)
                {
                    Console.WriteLine("No proxies found!");
                    return;
                }
                Console.WriteLine("Using {0}:{1} ({2}) with {3}ms response", optimal.Item1, optimal.Item2, optimal.Item3, optimal.Item4);
            }

            EbayInterface.PageMode pageMode = EbayInterface.PageMode.Num;
            if (npage == -1) pageMode = EbayInterface.PageMode.All;

            EbayInterface.Search search = new EbayInterface.Search(key, pageMode, EbayInterface.IndexMode.ListingOnly, exkey, npage, userProxy);
            Console.WriteLine("Indexing Pages");
            string[] hrefs = search.GetAllPageHrefs();
            Console.WriteLine("Indexing Listings");
            EbayInterface.Listing[] listings = search.GetListingsMultiThreaded(hrefs);
            Console.WriteLine("Exporting");
            EbayInterface.ExportToCSV(listings, exportFile);


            //"23.237.173.102", 3128
            //EbayInterface.Search search = new EbayInterface.Search("apple charger", EbayInterface.PageMode.Num, EbayInterface.IndexMode.ListingOnly, "", 2, userProxy);
            //string[] hrefs = search.GetAllPageHrefs();
            //EbayInterface.Listing[] listings = search.GetListings(hrefs);

            //EbayInterface.SerializeToFile(listings, "export.xml");
            return;
        }
    }
}
