using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using Zds.Flow.Machinery.Objects;
using Helper;
using Zds.Flow.ExceptionHandling;
using Zds.Flow.Updatables;
using System.Threading;

namespace Indexer
{
    class Program
    {
        const string repositoryPath = "wikitext.txt";
        const string repositoryIndexPath = "wikitext.index";

        const string lexiconPath = "lexicon.txt";
        const string lexiconIndexPath = "lexicon.index";

        const string lexiconSortedIndexPath = "lexicon_sorted.index";

        const string forwardIndexPath = "forward.index";
        const string forwardIndexIndexPath = "forwrad.index2";

        const string reverseIndexPath = "reverse.index";
        const string reverseIndexIndexPath = "reverse.index2";

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
        static int GetIndex(byte[] data, int size, int offset, byte[] obj, uint[] index)
        {
            int start;
            int stop;
            Functions.BinarySearch(data, size, offset, obj, out start, out stop, index);
            if (stop - start > 0)
                throw new Exception("Collision detected!");
            else if (start == stop)
                return start;
            return -1;
        }

        static void Main2(string[] args)
        {
            byte[] wordIndex = File.ReadAllBytes("lexicon_sorted.index");
            byte[] forwardIndex = File.ReadAllBytes("forwrad.index2");
            Functions.QuickSort(wordIndex, 16, 0, 4);
            Functions.QuickSort(forwardIndex, 16, 0, 4);
            uint[] wordIndex2 = Functions.Index2(wordIndex, 16, 0);
            uint[] forwardIndex2 = Functions.Index2(forwardIndex, 16, 0);
            while (true)
            {
                Console.Write("Enter title: ");
                string title = Console.ReadLine();
                uint titleCRC = Crc32.Compute(title);
                int f_index = GetIndex(forwardIndex, 16, 0, BitConverter.GetBytes(titleCRC), forwardIndex2);
                long f_pos = (long)BitConverter.ToUInt64(forwardIndex, f_index * 16 + 4);
                int f_len = (int)BitConverter.ToUInt32(forwardIndex, f_index * 16 + 12);
                byte[] words = ReadBytes("forward.index", f_pos, f_len);
                Functions.QuickSort(words, 8, 4, 4);
                for (int i = 0; i < Math.Min(words.Length / 8, 1000); i++)
                {
                    uint wordCRC = BitConverter.ToUInt32(words, ((words.Length / 8) - i - 1) * 8);
                    uint freq = BitConverter.ToUInt32(words, (words.Length / 8 - i - 1) * 8 + 4);
                    int w_index = GetIndex(wordIndex, 16, 0, BitConverter.GetBytes(wordCRC), wordIndex2);
                    long w_pos = (long)BitConverter.ToUInt64(wordIndex, w_index * 16 + 4);
                    int w_len = (int)BitConverter.ToUInt32(wordIndex, w_index * 16 + 12);
                    string word = Encoding.UTF8.GetString(ReadBytes("lexicon.txt", w_pos, w_len)).TrimEnd('\r', '\n');
                    Console.WriteLine(word + "\t" + freq.ToString());
                }
            }
        }

        static void Main3(string[] args)
        {
            byte[] titlesIndex = File.ReadAllBytes("titles.index");
            byte[] reverseIndex = File.ReadAllBytes("reverse.index2");
            Functions.QuickSort(titlesIndex, 16, 0, 4);
            Functions.QuickSort(reverseIndex, 16, 0, 4);
            uint last_num = uint.MinValue;
            for (int i = 0; i < titlesIndex.Length / 16; i++)
            {
                uint num = BitConverter.ToUInt32(titlesIndex, i * 16);
                if (num < last_num)
                    throw new Exception("Sorting error!");
                last_num = num;
            }
            last_num = uint.MinValue;
            for (int i = 0; i < reverseIndex.Length / 16; i++)
            {
                uint num = BitConverter.ToUInt32(reverseIndex, i * 16);
                if (num < last_num)
                    throw new Exception("Sorting error!");
                last_num = num;
            }
            uint[] titlesIndex2 = Functions.Index2(titlesIndex, 16, 0);
            uint[] reverseIndex2 = Functions.Index2(reverseIndex, 16, 0);
            while (true)
            {
                Console.Write("Enter word: ");
                string word = Console.ReadLine();
                uint wordCRC = Crc32.Compute(word.ToLower());
                int r_index = GetIndex(reverseIndex, 16, 0, BitConverter.GetBytes(wordCRC), reverseIndex2);
                if (BitConverter.ToUInt32(reverseIndex, r_index * 16) != wordCRC)
                    throw new Exception("Paradox!");
                long r_pos = (long)BitConverter.ToUInt64(reverseIndex, r_index * 16 + 4);
                int r_len = (int)BitConverter.ToUInt32(reverseIndex, r_index * 16 + 12);
                byte[] pages = ReadBytes("reverse.index", r_pos, r_len);
                Functions.QuickSort(pages, 8, 4, 4);
                for (int i = 0; i < Math.Min(pages.Length / 8, 100); i++)
                {
                    uint titleCRC = BitConverter.ToUInt32(pages, (pages.Length / 8 - i - 1) * 8);
                    uint freq = BitConverter.ToUInt32(pages, (pages.Length / 8 - i - 1) * 8 + 4);
                    int t_index = GetIndex(titlesIndex, 16, 0, BitConverter.GetBytes(titleCRC), titlesIndex2);
                    if (BitConverter.ToUInt32(titlesIndex, t_index * 16) != titleCRC)
                        throw new Exception("Paradox!");
                    long t_pos = (long)BitConverter.ToUInt64(titlesIndex, t_index * 16 + 4);
                    int t_len = (int)BitConverter.ToUInt32(titlesIndex, t_index * 16 + 12);
                    string title = Encoding.UTF8.GetString(ReadBytes("titles.txt", t_pos, t_len)).TrimEnd('\r', '\n');
                    Console.WriteLine(freq.ToString() + "\t" + title);
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Creating index...");
                if (!Lexicon_outputs.All((path) => File.Exists(path)))
                    if (!Lexicon()) throw new Exception("Unable to generate lexicon!");
                if (!SortLexicon_outputs.All((path) => File.Exists(path)))
                    if (!SortLexicon()) throw new Exception("Unable to sort lexicon!");
                if (!ForwardIndex_output.All((path) => File.Exists(path)))
                    if (!ForwardIndex()) throw new Exception("Unable to forward index!");
                if (!ReverseIndex_outputs.All((path) => File.Exists(path)))
                    if (!ReverseIndex()) throw new Exception("Unable to reverse index!");
                Console.WriteLine("Index created!");
            Console.ReadKey();
        }
        static string[] Lexicon_inputs = new string[] { repositoryPath, repositoryIndexPath };
        static string[] Lexicon_outputs = new string[] { lexiconPath, lexiconIndexPath };
        static bool Lexicon()
        {
            if (!Functions.VerifyIO(Lexicon_inputs, Lexicon_outputs)) return false;
            Console.WriteLine("Generating lexicon...");
            FileStream repository = new FileStream(repositoryPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 64);
            FileStream repositoryIndex = new FileStream(repositoryIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 16);
            FileStream lexicon = new FileStream(lexiconPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);
            FileStream lexiconIndex = new FileStream(lexiconIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);

            BinaryReader repositoryReader = new BinaryReader(repository);
            BinaryReader repositoryIndexReader = new BinaryReader(repositoryIndex);
            BinaryWriter lexiconWriter = new BinaryWriter(lexicon);
            BinaryWriter lexiconIndexWriter = new BinaryWriter(lexiconIndex);

            Stopwatch stopwatch = new Stopwatch();
            SafeIntSpace space = new SafeIntSpace();

            long totalPages = repositoryIndex.Length / 16;
            long pagesRead = 0;
            long pagesProcessed = 0;

            //Creating Machinery
            AsyncConverter<string, Tuple<uint, byte[]>[]> converter = null;
            converter = new AsyncConverter<string, Tuple<uint, byte[]>[]>(
                (string content, ref Tuple<uint, byte[]>[] wordlist) =>
            {
                List<Tuple<uint, byte[]>> list = new List<Tuple<uint, byte[]>>();
                StringBuilder word = new StringBuilder();
                bool ascii = true;
                for (int i = 0; i < content.Length; i++)
                {
                    char c = content[i];
                    if (char.IsLetter(c))
                    {
                        if (c >= 128)
                            ascii = false;
                        word.Append(char.ToLower(c));
                    }
                    else if (word.Length != 0)
                    {
                        if (ascii && word.Length <= 20)
                        {
                            string _word = word.ToString();
                            uint CRC = Crc32.Compute(_word);
                            if (space.Add(CRC))
                            {
                                byte[] wordBytes = Encoding.UTF8.GetBytes(_word + "\n");
                                list.Add(new Tuple<uint, byte[]>(CRC, wordBytes));
                            }
                        }
                        word.Clear();
                        ascii = true;
                    }
                }
                wordlist = list.ToArray();
                return true;
            });
            SyncSink<Tuple<uint, byte[]>[]> sink = null;
            sink = new SyncSink<Tuple<uint, byte[]>[]>(
                (Tuple<uint, byte[]>[] wordlist) =>
            {
                foreach (var word in wordlist)
                {
                    uint CRC = word.Item1;
                    byte[] wordBytes = word.Item2;
                    ulong wordPos = (ulong)lexicon.Position;
                    uint wordSize = (uint)wordBytes.Length;
                    lexiconIndexWriter.Write(CRC);
                    lexiconIndexWriter.Write(wordPos);
                    lexiconIndexWriter.Write(wordSize);
                    lexiconWriter.Write(wordBytes);
                }
                pagesProcessed += 1;
                return true;
            });

            //Setting up
            converter.MaxThreads = 8;
            converter.MustConvert = true;
            converter.Recursive = true;
            sink.Recursive = true;

            //Connecting
            converter.Sink = sink;

            //Exception handler
            ExceptionHandler eh = new ConsoleLogger();
            eh.Add(converter);
            eh.Add(sink);

            //Updater
            long lastReposPos = 0;
            long lastPagesRead = 0;
            long lastPagesProcessed = 0;
            SyncTimer timer = null;
            timer = new SyncTimer(
                (ISyncTimer sender, ref TimeSpan time) =>
            {
                long pos_diff = repository.Position - lastReposPos;
                long pr_diff = pagesRead - lastPagesRead;
                long pp_diff = pagesProcessed - lastPagesProcessed;
                Console.Title = "Repos: " + Functions.SizeSuffix(repository.Position) + "/" + Functions.SizeSuffix(repository.Length) + "(" + Functions.SizeSuffix(pos_diff) + "/s), " +
                                "PR: " + pagesRead.ToString() + "/" + totalPages.ToString() + "(" + pr_diff.ToString() + "/s), " +
                                "PW: " + pagesProcessed.ToString() + "/" + totalPages.ToString() + "(" + pp_diff.ToString() + "/s), ";
                lastReposPos = repository.Position;
                lastPagesRead = pagesRead;
                lastPagesProcessed = pagesProcessed;
            });

            //Starting pipeline
            stopwatch.Start();
            timer.Start();
            converter.Start();
            sink.Start();

            //Creating virtual source
            for (pagesRead = 0; pagesRead < totalPages; pagesRead++)
            {
                uint page_id = repositoryIndexReader.ReadUInt32();
                ulong page_pos = repositoryIndexReader.ReadUInt64();
                uint page_length = repositoryIndexReader.ReadUInt32();
                if (repository.Position != (long)page_pos)
                    repository.Position = (long)page_pos;
                string content = Encoding.UTF8.GetString(repositoryReader.ReadBytes((int)page_length));
                while (!converter.Receive(content))
                    Thread.Sleep(1);
            }

            //Waiting to finish
            while (pagesProcessed != totalPages)
                Thread.Sleep(1);
            stopwatch.Stop();

            //Cleaning resources
            timer.Destroy();
            converter.Destroy();
            sink.Destroy();

            Console.WriteLine("Time taken: " + stopwatch.Elapsed.ToString());

            lexiconWriter.Flush();
            lexiconIndexWriter.Flush();

            lexicon.Flush();
            lexiconIndex.Flush();

            repositoryReader.Dispose();
            repositoryIndexReader.Dispose();
            lexiconWriter.Dispose();
            lexiconIndexWriter.Dispose();

            repository.Dispose();
            repositoryIndex.Dispose();
            lexicon.Dispose();
            lexiconIndex.Dispose();
            return true;
        }
        static string[] SortLexicon_inputs = new string[] { lexiconIndexPath };
        static string[] SortLexicon_outputs = new string[] { lexiconSortedIndexPath };
        static bool SortLexicon()
        {
            if (!Functions.VerifyIO(SortLexicon_inputs, SortLexicon_outputs)) return false;
            Console.WriteLine("Sorting lexicon...");
            byte[] data = File.ReadAllBytes(lexiconIndexPath);
            Functions.QuickSort(data, 16, 0, 4);
            File.WriteAllBytes(lexiconSortedIndexPath, data);
            return true;
        }
        static string[] ForwardIndex_inputs = new string[] { repositoryPath, repositoryIndexPath, lexiconSortedIndexPath };
        static string[] ForwardIndex_output = new string[] { forwardIndexPath, forwardIndexIndexPath };
        static bool ForwardIndex()
        {
            if (!Functions.VerifyIO(ForwardIndex_inputs, ForwardIndex_output)) return false;

            FileStream repository = new FileStream(repositoryPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 64);
            FileStream repositoryIndex = new FileStream(repositoryIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 16);
            FileStream forwardIndex = new FileStream(forwardIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 32);
            FileStream forwardIndexIndex = new FileStream(forwardIndexIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);

            BinaryReader repositoryReader = new BinaryReader(repository);
            BinaryReader repositoryIndexReader = new BinaryReader(repositoryIndex);
            BinaryWriter forwardIndexWriter = new BinaryWriter(forwardIndex);
            BinaryWriter forwardIndexIndexWriter = new BinaryWriter(forwardIndexIndex);

            Stopwatch stopwatch = new Stopwatch();

            long totalPages = repositoryIndex.Length / 16;
            long pagesRead = 0;
            long pagesProcessed = 0;
            //Creating Machinery
            AsyncConverter<Tuple<uint, string>, Tuple<uint, byte[]>> converter = null;
            converter = new AsyncConverter<Tuple<uint, string>, Tuple<uint, byte[]>>(
                (Tuple<uint, string> page, ref Tuple<uint, byte[]> wordsfreq) =>
                {
                    uint page_id = page.Item1;
                    string content = page.Item2;
                    BinaryTree<HashValuePair<uint>> tree = null;
                    int totalWords = 0;
                    StringBuilder word = new StringBuilder();
                    bool ascii = true;
                    for (int i = 0; i < content.Length; i++)
                    {
                        char c = content[i];
                        if (char.IsLetter(c))
                        {
                            if (c >= 128)
                                ascii = false;
                            word.Append(char.ToLower(c));
                        }
                        else if (word.Length != 0)
                        {
                            if (ascii && word.Length <= 20)
                            {
                                string _word = word.ToString();
                                uint CRC = Crc32.Compute(_word);
                                HashValuePair<uint> node = new HashValuePair<uint>(CRC, 0);
                                HashValuePair<uint> _node;
                                if (tree == null)
                                {
                                    tree = new BinaryTree<HashValuePair<uint>>(node);
                                    _node = node;
                                }
                                else
                                    _node = tree.Insert(node);
                                if (node == _node) totalWords++;
                                _node.Value++;
                            }
                            word.Clear();
                            ascii = true;
                        }
                    }
                    byte[] data;
                    using (MemoryStream memory = new MemoryStream(totalWords * 8))
                    {
                        using (BinaryWriter memWriter = new BinaryWriter(memory))
                        {
                            if (tree != null)
                                foreach (var node in tree)
                                {
                                    memWriter.Write(node.Hash);
                                    memWriter.Write(node.Value);
                                }
                            memWriter.Flush();
                            data = memory.ToArray();
                        }
                    }
                    //Functions.QuickSort(data, 8, 4, 4);
                    wordsfreq = new Tuple<uint, byte[]>(page_id, data);
                    return true;
                });
            SyncSink<Tuple<uint, byte[]>> sink = null;
            sink = new SyncSink<Tuple<uint, byte[]>>(
                (Tuple<uint, byte[]> wordsfreq) =>
                {
                    uint page_id = wordsfreq.Item1;
                    byte[] data = wordsfreq.Item2;
                    ulong position = (ulong)forwardIndex.Position;
                    uint length = (uint)data.Length;
                    forwardIndexIndexWriter.Write(page_id);
                    forwardIndexIndexWriter.Write(position);
                    forwardIndexIndexWriter.Write(length);
                    forwardIndexWriter.Write(data);
                    pagesProcessed += 1;
                    return true;
                });

            //Setting up
            converter.MaxThreads = 8;
            converter.MustConvert = true;
            converter.Recursive = true;
            sink.Recursive = true;

            //Connecting
            converter.Sink = sink;

            //Exception handler
            ExceptionHandler eh = new ConsoleLogger();
            eh.Add(converter);
            eh.Add(sink);

            //Updater
            long lastReposPos = 0;
            long lastPagesRead = 0;
            long lastPagesProcessed = 0;
            SyncTimer timer = null;
            timer = new SyncTimer(
                (ISyncTimer sender, ref TimeSpan time) =>
                {
                    long pos_diff = repository.Position - lastReposPos;
                    long pr_diff = pagesRead - lastPagesRead;
                    long pp_diff = pagesProcessed - lastPagesProcessed;
                    Console.Title = "Repos: " + Functions.SizeSuffix(repository.Position) + "/" + Functions.SizeSuffix(repository.Length) + "(" + Functions.SizeSuffix(pos_diff) + "/s), " +
                                    "PR: " + pagesRead.ToString() + "/" + totalPages.ToString() + "(" + pr_diff.ToString() + "/s), " +
                                    "PW: " + pagesProcessed.ToString() + "/" + totalPages.ToString() + "(" + pp_diff.ToString() + "/s), ";
                    lastReposPos = repository.Position;
                    lastPagesRead = pagesRead;
                    lastPagesProcessed = pagesProcessed;
                });

            //Starting pipeline
            stopwatch.Start();
            timer.Start();
            converter.Start();
            sink.Start();

            //Creating virtual source
            for (pagesRead = 0; pagesRead < totalPages; pagesRead++)
            {
                uint page_id = repositoryIndexReader.ReadUInt32();
                ulong page_pos = repositoryIndexReader.ReadUInt64();
                uint page_length = repositoryIndexReader.ReadUInt32();
                if (repository.Position != (long)page_pos)
                    repository.Position = (long)page_pos;
                string content = Encoding.UTF8.GetString(repositoryReader.ReadBytes((int)page_length));
                var tuple = new Tuple<uint, string>(page_id, content);
                while (!converter.Receive(tuple))
                    Thread.Sleep(1);
            }

            //Waiting to finish
            while (pagesProcessed != totalPages)
                Thread.Sleep(1);
            stopwatch.Stop();

            //Cleaning resources
            timer.Destroy();
            converter.Destroy();
            sink.Destroy();

            Console.WriteLine("Time taken: " + stopwatch.Elapsed.ToString());
            return true;
        }
        static string[] ReverseIndex_inputs = new string[] {lexiconSortedIndexPath, forwardIndexPath, forwardIndexIndexPath };
        static string[] ReverseIndex_outputs = new string[] { reverseIndexPath, reverseIndexIndexPath };
        static bool ReverseIndex()
        {
            if (!Functions.VerifyIO(ReverseIndex_inputs, ReverseIndex_outputs)) return false;
            Console.WriteLine("Creating reverse indices...");
            byte[] lexiconIndex = File.ReadAllBytes(lexiconSortedIndexPath);
            uint[] lexiconIndex2 = Functions.Index2(lexiconIndex, 16, 0);

            FileStream forwardIndex = new FileStream(forwardIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 16);
            FileStream forwardIndexIndex = new FileStream(forwardIndexIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 4);
            MemoryStream reverseIndex = new MemoryStream();
            reverseIndex.SetLength(forwardIndex.Length);
            FileStream reverseIndexIndex = new FileStream(reverseIndexIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 4);

            BinaryReader forwardIndexReader = new BinaryReader(forwardIndex);
            BinaryReader forwardIndexIndexReader = new BinaryReader(forwardIndexIndex);
            BinaryWriter reverseIndexWriter = new BinaryWriter(reverseIndex);
            BinaryWriter reverseIndexIndexWriter = new BinaryWriter(reverseIndexIndex);

            int[] wordcount = new int[lexiconIndex.Length / 16];
            for (int i = 0; i < forwardIndex.Length / 8; i++)
            {
                int start;
                int stop;
                Functions.BinarySearch(lexiconIndex, 16, 0, forwardIndexReader.ReadBytes(4),out start,out stop, lexiconIndex2);
                forwardIndexReader.ReadBytes(4);
                if (stop - start > 0) throw new Exception("Collision detected!");
                if (start == stop)
                    wordcount[start]++;
            }

            ulong[] wordpos = new ulong[wordcount.Length];
            ulong cf = 0;
            for (int i = 0; i < wordcount.Length; i++)
                if (wordcount[i] != 0)
                {
                    reverseIndexIndexWriter.Write(lexiconIndex, i * 16, 4);
                    reverseIndexIndexWriter.Write(cf * 8);
                    reverseIndexIndexWriter.Write(wordcount[i] * 8);
                    wordpos[i] += cf;
                    cf += (ulong)wordcount[i];
                }
                else
                    throw new Exception("Word count cannot be zero");
            int[] wordcount2 = new int[lexiconIndex.Length / 16];
            for (int i = 0; i < forwardIndexIndex.Length / 16; i++)
            {
                uint CRC = forwardIndexIndexReader.ReadUInt32();
                ulong pos = forwardIndexIndexReader.ReadUInt64();
                uint len = forwardIndexIndexReader.ReadUInt32();
                if (forwardIndex.Position != (long)pos)
                    forwardIndex.Position = (long)pos;
                for (int j = 0; j < len / 8; j++)
                {
                    uint wordCRC = forwardIndexReader.ReadUInt32();
                    uint freq = forwardIndexReader.ReadUInt32();
                    int start;
                    int stop;
                    Functions.BinarySearch(lexiconIndex, 16, 0, BitConverter.GetBytes(wordCRC), out start, out stop, lexiconIndex2);
                    if (stop - start > 0) throw new Exception("Collision detected!");
                    if (start == stop)
                    {
                        reverseIndex.Position = (long)(wordpos[start] * 8 + (ulong)wordcount2[start] * 8);
                        reverseIndexWriter.Write(CRC);
                        reverseIndexWriter.Write(freq);
                        wordcount2[start]++;
                    }
                }
            }
            Console.WriteLine("Verifying reverse indices...");
            for (int i = 0; i < wordcount.Length; i++)
                if (wordcount[i] != wordcount2[i])
                    throw new Exception("Count mismatch!");

            reverseIndexWriter.Flush();
            reverseIndexIndexWriter.Flush();

            File.WriteAllBytes(reverseIndexPath, reverseIndex.ToArray());

            reverseIndexWriter.Dispose();
            reverseIndexIndexWriter.Dispose();

            return true;
        }
    }
}
