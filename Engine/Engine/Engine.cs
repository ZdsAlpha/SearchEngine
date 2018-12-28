using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using Zds.Flow.Machinery.Objects;
using DataStructs;
using Zds.Flow.ExceptionHandling;

namespace Engine
{
    public class Engine
    {
        const uint TitleScore = 100;
        static byte[] TitlesIndex;
        static uint[] TitlesIndex2;
        static byte[] TitlesRIndex;
        static uint[] TitlesRIndex2;
        static byte[] TitlesFreq;
        static uint[] TitlesFreqIndex;
        static byte[] ReposIndex;
        static uint[] ReposIndex2;
        static byte[] ReposRIndex;
        static uint[] ReposRIndex2;
        static byte[] ReposFreq;
        static uint[] ReposFreqIndex;
        static uint TotalPages;
        static void Main(string[] args)
        {
            Random random = new Random();
            Console.WriteLine("Initializing server...");
            LoadEngine();
            HttpListener server = new HttpListener();
            int port = random.Next(10000, 20000);
            string serverUrl = "http://localhost:" + port.ToString();
            server.Prefixes.Add(serverUrl + "/");
            Console.WriteLine("Server listening on " + serverUrl);
            server.Start();
            Process.Start(serverUrl);
            Stopwatch stopwatch = new Stopwatch();
            AsyncSink<HttpListenerContext> sink;
            sink = new AsyncSink<HttpListenerContext>(
                (context) =>
            {
                try
                {
                    var request = context.Request;
                    var response = context.Response;
                    Uri uri = new Uri(serverUrl + request.RawUrl);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    if (uri.LocalPath == "/search" && query.AllKeys.Contains("query") && query["query"] != "")
                    {
                        Console.WriteLine("Searching: " + query["query"]);
                        stopwatch.Restart();
                        string[] titles = new string[] { };
                        int totalResults = 0;
                        Tuple<string[],int> results = Search(query["query"]);
                        if (results != null)
                        {
                            titles = results.Item1;
                            totalResults = results.Item2;
                        }
                        stopwatch.Stop();
                        StringBuilder listitems = new StringBuilder();
                        foreach (string title in titles)
                            listitems.Append(Properties.Resources.ListItem.Replace("##PAGE_TITLE##", title).Replace("##PAGE_URL##", "https://en.wikipedia.org/wiki/" + title));
                        string content = Properties.Resources.Results.Replace("##LOGO_BASE64##", Convert.ToBase64String(Properties.Resources.logo));
                        content = content.Replace("##SEARCH_QUERY##", query["query"]);
                        Functions.Response(response, content.Replace("##LIST_ITEMS##", listitems.ToString()));
                        Console.WriteLine(totalResults.ToString() + " results in " + stopwatch.Elapsed.TotalSeconds.ToString() + " seconds!");
                    }
                    else
                    {
                        string content = Properties.Resources.FrontPage.Replace("##LOGO_BASE64##", Convert.ToBase64String(Properties.Resources.logo));
                        Functions.Response(response, content);
                    }

                }
                catch (Exception ex)
                { }
                return true;
            });
            ExceptionHandler handler = new ExceptionHandler();
            handler.Add(sink);
            sink.Start();
            while (true)
            {
                var context = server.GetContext();
                while (!sink.Receive(context))
                    Thread.Sleep(1);
            }
        }
        static void LoadEngine()
        {
            Console.WriteLine("Loading titles index...");
            TitlesIndex = File.ReadAllBytes("titles.index");
            Console.WriteLine("Calculating titles double index...");
            TitlesIndex2 = DataStructs.Functions.Index(TitlesIndex, 16, 0, 3);
            Console.WriteLine("Loading titles reverse double index...");
            TitlesRIndex = File.ReadAllBytes("titles_reverse_index.index");
            Console.WriteLine("Calculating titles reverse triple index...");
            TitlesRIndex2 = DataStructs.Functions.Index(TitlesRIndex, 16, 0, 3);
            Console.WriteLine("Loading words frequency in titles...");
            TitlesFreq = File.ReadAllBytes("titles_frequency.bin");
            Console.WriteLine("Calculating titles frequency index...");
            TitlesFreqIndex = DataStructs.Functions.Index(TitlesFreq, 8, 0, 3);
            Console.WriteLine("Loading repos index...");
            ReposIndex = File.ReadAllBytes("repos.index");
            Console.WriteLine("Calculating repos double index...");
            ReposIndex2 = DataStructs.Functions.Index(ReposIndex, 16, 0, 3);
            Console.WriteLine("Loading repos reverse double index...");
            ReposRIndex = File.ReadAllBytes("repos_reverse_index.index");
            Console.WriteLine("Calculating titles reverse triple index...");
            ReposRIndex2 = DataStructs.Functions.Index(ReposRIndex, 16, 0, 3);
            Console.WriteLine("Loading words frequency in repos...");
            ReposFreq = File.ReadAllBytes("repos_frequency.bin");
            Console.WriteLine("Calculating repos frequency index...");
            ReposFreqIndex = DataStructs.Functions.Index(ReposFreq, 8, 0, 3);
            TotalPages = (uint)(TitlesIndex.Length / 16);
        }
        static Tuple<string[], int> Search(string query)
        {
            string[] words = query.Split(' ').Select((word) => word.ToLower()).ToArray();
            BinaryTree<HashValuePair<int>> wordsFreq = null;
            foreach (var word in words)
            {
                var pair = new HashValuePair<int>(Crc32.Compute(word), 0);
                if (wordsFreq == null)
                    wordsFreq = new BinaryTree<HashValuePair<int>>(pair);
                else
                {
                    pair = wordsFreq.Insert(pair);
                }
                pair.Value++;
            }
            if (wordsFreq == null) return null;
            BinaryTree<HashValuePair<decimal>> scores = null;
            foreach (var word in wordsFreq)
            {
                uint wordCRC = word.Hash;
                int count = word.Value;
                uint t_wordFreq = GetWordFrequency(wordCRC, TitlesFreq, TitlesFreqIndex);
                decimal t_mean = (decimal)t_wordFreq / TotalPages;
                Tuple<uint, uint>[] t_titles = null;
                if (t_wordFreq != 0) t_titles = Search(wordCRC, "titles_reverse_index.bin", TitlesRIndex, TitlesRIndex2);
                uint r_wordFreq = GetWordFrequency(wordCRC, ReposFreq, ReposFreqIndex);
                decimal r_mean = (decimal)r_wordFreq / TotalPages;
                Tuple<uint, uint>[] r_titles = null;
                if (r_wordFreq != 0) r_titles = Search(wordCRC, "repos_reverse_index.bin", ReposRIndex, ReposRIndex2);
                if (t_titles != null)
                    foreach (var freq in t_titles)
                    {
                        HashValuePair<decimal> pair = new HashValuePair<decimal>(freq.Item1, 0);
                        if (scores == null)
                            scores = new BinaryTree<HashValuePair<decimal>>(pair);
                        else
                            pair = scores.Insert(pair);
                        pair.Value += TitleScore * (decimal)freq.Item2 * count;
                    }
                if (r_titles != null)
                    foreach (var freq in r_titles)
                    {
                        HashValuePair<decimal> pair = new HashValuePair<decimal>(freq.Item1, 0);
                        if (scores == null)
                            scores = new BinaryTree<HashValuePair<decimal>>(pair);
                        else
                            pair = scores.Insert(pair);
                        pair.Value += ((decimal)freq.Item2 / r_mean) * count;
                    }
            }
            if (scores == null) return null;
            var pages = scores.ToArray();
            var sorted_pages = (from page in pages
                               orderby page.Value descending
                               select page).ToArray();
            List<string> titles = new List<string>();
            for (int i = 0; i < Math.Min(sorted_pages.Length, 1000); i++)
                titles.Add(GetTitle(sorted_pages[i].Hash, TitlesIndex, TitlesIndex2));
            return new Tuple<string[],int>(titles.ToArray(),pages.Length);
        }
        static Tuple<uint,uint>[] Search(uint wordCRC, string index, byte[] index2, uint[] index3, int length = 1000)
        {
            List<Tuple<uint, uint>> pages = new List<Tuple<uint, uint>>();
            int r_index = DataStructs.Functions.GetIndex(index2, 16, 0, BitConverter.GetBytes(wordCRC), index3);
            if (r_index == -1) pages.ToArray();
            long r_pos = (long)BitConverter.ToUInt64(index2, r_index * 16 + 4);
            int r_len = (int)BitConverter.ToUInt32(index2, r_index * 16 + 12);
            byte[] raw_pages = ReadBytes(index, r_pos, r_len);
            for (int i = 0; i < Math.Min(length, raw_pages.Length / 8); i++)
            {
                uint titleCRC = BitConverter.ToUInt32(raw_pages, (raw_pages.Length / 8 - i - 1) * 8);
                uint freq = BitConverter.ToUInt32(raw_pages, (raw_pages.Length / 8 - i - 1) * 8 + 4);
                pages.Add(new Tuple<uint, uint>(titleCRC, freq));
            }
            return pages.ToArray();
        }
        static string GetTitle(uint titleCRC, byte[] index, uint[] index2)
        {
            int t_index = DataStructs.Functions.GetIndex(index, 16, 0, BitConverter.GetBytes(titleCRC), index2);
            long t_pos = (long)BitConverter.ToUInt64(index, t_index * 16 + 4);
            int t_len = (int)BitConverter.ToUInt32(index, t_index * 16 + 12);
            return Encoding.UTF8.GetString(ReadBytes("titles.txt", t_pos, t_len)).TrimEnd('\r', '\n');
        }
        static uint GetWordFrequency(uint wordCRC, byte[] frequency, uint[] index)
        {
            int w_index = DataStructs.Functions.GetIndex(frequency, 8, 0, BitConverter.GetBytes(wordCRC),index);
            if (w_index == -1) return 0;
            return BitConverter.ToUInt32(frequency, w_index * 8 + 4);
        }
        static byte[] ReadBytes(string file, long pos, int len)
        {
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader bn = new BinaryReader(fs))
                {
                    fs.Position = pos;
                    return bn.ReadBytes(len);
                }
            }
        }
    }
}
