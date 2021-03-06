using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Zds.Flow.Machinery.Objects;
using Zds.Flow.ExceptionHandling;
using Zds.Flow.Updatables;
using DataStructs;

namespace Indexer
{
    class Program
    {
        const string repositoryPath = "wikitext.txt";
        const string repositoryIndexPath = "wikitext.index";

        const string lexiconPath = "lexicon.txt";
        const string lexiconIndexPath = "lexicon.index";

        const string lexiconSortedIndexPath = "lexicon_sorted.index";

        const string forwardIndexPath = "forward_index.bin";
        const string forwardIndexIndexPath = "forwrad_index.index";

        const string frequencyPath = "frequency.bin";
        const string wordsCountPath = "wordscount.bin";

        const string frequencySortedPath = "frequency_sorted.bin";
        const string wordsCountSortedPath = "wordscount_sorted.bin";

        const string reverseIndexPath = "reverse_index.bin";
        const string reverseIndexIndexPath = "reverse_index.index";

        const string reverseIndexSortedIndexPath = "reverse_index2.index";
        const string reverseIndexSortedPath = "reverse_index_sorted.bin";

        const string reverseIndexIndexSortedPath = "reverse_index_sorted.index";

        static void Main(string[] args)
        {
            Console.WriteLine("Creating index...");
            if (!Lexicon_outputs.All((path) => File.Exists(path)))
                if (!Lexicon()) throw new Exception("Unable to generate lexicon!");
            if (!SortLexicon_outputs.All((path) => File.Exists(path)))
                if (!SortLexicon()) throw new Exception("Unable to sort lexicon!");
            if (!ForwardIndex_output.All((path) => File.Exists(path)))
                if (!ForwardIndex()) throw new Exception("Unable to forward index!");
            if (!WordsCount_output.All((path) => File.Exists(path)))
                if (!WordsCount()) throw new Exception("Unable to count words!");
            if (!SortFrequency_output.All((path) => File.Exists(path)))
                if (!SortFrequency()) throw new Exception("Unable to sort frequencies!");
            if (!SortWordsCount_output.All((path) => File.Exists(path)))
                if (!SortWordsCount()) throw new Exception("Unable to sort words count!");
            if (!ReverseIndex_outputs.All((path) => File.Exists(path)))
                if (!ReverseIndex()) throw new Exception("Unable to reverse index!");
            if (!SortReverseIndex_outputs.All((path) => File.Exists(path)))
                if (!SortReverseIndex()) throw new Exception("Unable to sort reverse index!");
            if (!SortReverseIndexIndex_outputs.All((path) => File.Exists(path)))
                if (!SortReverseIndexIndex()) throw new Exception("Unable to sort reverse index index!");
            Console.WriteLine("Index created!");
            Console.ReadKey();
        }
        static string[] Lexicon_inputs = new string[] { repositoryPath, repositoryIndexPath };
        static string[] Lexicon_outputs = new string[] { lexiconPath, lexiconIndexPath };
        static bool Lexicon()
        {
            Console.WriteLine("Generating lexicon...");
            if (!Functions.VerifyIO(Lexicon_inputs, Lexicon_outputs)) return false;
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
                Interlocked.Increment(ref pagesProcessed);
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
            Console.WriteLine("Sorting lexicon...");
            if (!Functions.VerifyIO(SortLexicon_inputs, SortLexicon_outputs)) return false;
            byte[] data = File.ReadAllBytes(lexiconIndexPath);
            Functions.QuickSort(data, 16, 0, 4);
            File.WriteAllBytes(lexiconSortedIndexPath, data);
            return true;
        }
        static string[] ForwardIndex_inputs = new string[] { repositoryPath, repositoryIndexPath, lexiconSortedIndexPath };
        static string[] ForwardIndex_output = new string[] { forwardIndexPath, forwardIndexIndexPath };
        static bool ForwardIndex()
        {
            Console.WriteLine("Generating forward index...");
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
                    Interlocked.Increment(ref pagesProcessed);
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

            forwardIndexWriter.Flush();
            forwardIndexIndexWriter.Flush();

            forwardIndex.Flush();
            forwardIndexIndex.Flush();

            repositoryReader.Dispose();
            repositoryIndexReader.Dispose();
            forwardIndexWriter.Dispose();
            forwardIndexIndexWriter.Dispose();

            repository.Dispose();
            repositoryIndex.Dispose();
            forwardIndex.Dispose();
            forwardIndexIndex.Dispose();
            return true;
        }
        static string[] WordsCount_inputs = new string[] { lexiconSortedIndexPath, forwardIndexPath };
        static string[] WordsCount_output = new string[] { frequencyPath , wordsCountPath};
        static bool WordsCount()
        {
            const uint wordsPerLock = 256;
            const uint chunkSize = 1024 * 16;
            Console.WriteLine("Counting words...");
            if (!Functions.VerifyIO(WordsCount_inputs, WordsCount_output)) return false;
            byte[] lexiconIndex = File.ReadAllBytes(lexiconSortedIndexPath);
            FileStream forwardIndex = new FileStream(forwardIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 64);
            FileStream frequencyStream = new FileStream(frequencyPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);
            FileStream wordsCountStream = new FileStream(wordsCountPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);

            BinaryReader forwardIndexReader = new BinaryReader(forwardIndex);
            BinaryWriter frequencyWriter = new BinaryWriter(frequencyStream);
            BinaryWriter wordsCountWriter = new BinaryWriter(wordsCountStream);

            Stopwatch stopwatch = new Stopwatch();
            uint[] lexiconIndex3 = Functions.Index(lexiconIndex, 16, 0, 3);
            long[] frequency = new long[lexiconIndex.Length / 16];
            long[] wordsCount = new long[frequency.Length];
            object[] locks = new object[frequency.Length / wordsPerLock + 1];
            long totalChunks = (long)Math.Ceiling((decimal)forwardIndex.Length / (16 * chunkSize));
            long chunksRead = 0;
            long chunksProcessed = 0;

            for (int i = 0; i < locks.Length; i++)
                locks[i] = new object();

            //Creating Machinery
            AsyncSink<byte[]> sink = null;
            sink = new AsyncSink<byte[]>(
                (byte[] data) =>
            {
                int words = data.Length / 8;
                using (MemoryStream memory = new MemoryStream(data))
                {
                    using(BinaryReader reader = new BinaryReader(memory))
                    {
                        uint CRC;
                        uint freq;
                        int index;
                        object index_lock;
                        for (int i = 0; i < words; i++)
                        {
                            CRC = reader.ReadUInt32();
                            freq = reader.ReadUInt32();
                            index = Functions.GetIndex(lexiconIndex, 16, 0, BitConverter.GetBytes(CRC), lexiconIndex3);
                            index_lock = locks[index / wordsPerLock];
                            lock (index_lock)
                                frequency[index] += freq;
                            Interlocked.Increment(ref wordsCount[index]);
                        }
                    }
                }
                Interlocked.Increment(ref chunksProcessed);
                return true;
            });

            //Setting up
            sink.MaxThreads = 8;
            sink.Recursive = true;

            //Exception handler
            ExceptionHandler eh = new ConsoleLogger();
            eh.Add(sink);

            //Updater
            long lastIndexPos = 0;
            long lastChunksRead = 0;
            long lastChunksProcessed = 0;
            SyncTimer timer = null;
            timer = new SyncTimer(
                (ISyncTimer sender, ref TimeSpan time) =>
                {
                    long pos_diff = forwardIndex.Position - lastIndexPos;
                    long pr_diff = chunksRead - lastChunksRead;
                    long pp_diff = chunksProcessed - lastChunksProcessed;
                    Console.Title = "Index: " + Functions.SizeSuffix(forwardIndex.Position) + "/" + Functions.SizeSuffix(forwardIndex.Length) + "(" + Functions.SizeSuffix(pos_diff) + "/s), " +
                                    "CR: " + chunksRead.ToString() + "/" + totalChunks.ToString() + "(" + pr_diff.ToString() + "/s), " +
                                    "CP: " + chunksProcessed.ToString() + "/" + totalChunks.ToString() + "(" + pp_diff.ToString() + "/s), ";
                    lastIndexPos = forwardIndex.Position;
                    lastChunksRead = chunksRead;
                    lastChunksProcessed = chunksProcessed;
                });

            //Starting pipeline
            stopwatch.Start();
            timer.Start();
            sink.Start();

            //Creating virtual source
            for (int i = 0; i < forwardIndex.Length / (16 * chunkSize); i++)
            {
                byte[] data = forwardIndexReader.ReadBytes((int)(chunkSize * 16));
                while (!sink.Receive(data))
                    Thread.Sleep(1);
                chunksRead += 1;
            }
            long remaining = forwardIndex.Length % (16 * chunkSize);
            if (remaining > 0)
            {
                byte[] data = forwardIndexReader.ReadBytes((int)remaining);
                while (!sink.Receive(data))
                    Thread.Sleep(1);
                chunksRead += 1;
            }

            //Waiting to finish
            while (chunksProcessed != totalChunks)
                Thread.Sleep(1);
            stopwatch.Stop();

            //Cleaning resources
            timer.Destroy();
            sink.Destroy();

            Console.WriteLine("Flushing...");

            for (int i = 0; i < frequency.Length; i++)
            {
                frequencyWriter.Write(lexiconIndex, i * 16, 4);
                frequencyWriter.Write((uint)frequency[i]);
                wordsCountWriter.Write(lexiconIndex, i * 16, 4);
                wordsCountWriter.Write((uint)wordsCount[i]);
            }

            Console.WriteLine("Time taken: " + stopwatch.Elapsed.ToString());

            frequencyWriter.Flush();
            wordsCountWriter.Flush();

            frequencyStream.Flush();
            wordsCountStream.Flush();

            forwardIndexReader.Dispose();
            frequencyWriter.Dispose();
            wordsCountWriter.Dispose();

            forwardIndex.Dispose();
            frequencyStream.Dispose();
            wordsCountStream.Dispose();
            return true;
        }
        static string[] SortFrequency_inputs = new string[] { frequencyPath };
        static string[] SortFrequency_output = new string[] { frequencySortedPath };
        static bool SortFrequency()
        {
            Console.WriteLine("Sorting frequencies...");
            if (!Functions.VerifyIO(SortFrequency_inputs, SortFrequency_output)) return false;
            byte[] data = File.ReadAllBytes(frequencyPath);
            Functions.QuickSort(data, 8, 0, 4);
            File.WriteAllBytes(frequencySortedPath, data);
            return true;
        }
        static string[] SortWordsCount_inputs = new string[] { wordsCountPath };
        static string[] SortWordsCount_output = new string[] { wordsCountSortedPath };
        static bool SortWordsCount()
        {
            Console.WriteLine("Sorting words count...");
            if (!Functions.VerifyIO(SortWordsCount_inputs, SortWordsCount_output)) return false;
            byte[] data = File.ReadAllBytes(wordsCountPath);
            Functions.QuickSort(data, 8, 0, 4);
            File.WriteAllBytes(wordsCountSortedPath, data);
            return true;
        }
        static string[] ReverseIndex_inputs = new string[] { wordsCountPath, forwardIndexPath, forwardIndexIndexPath };
        static string[] ReverseIndex_outputs = new string[] { reverseIndexPath, reverseIndexIndexPath };
        static bool ReverseIndex()
        {
            const uint wordsPerLock = 256;
            const long chunkSize = (long)(1024 * 1024 * 1024 * (decimal)1.5);
            if (chunkSize % 4 != 0) throw new Exception("Invalid chunk size");
            Console.WriteLine("Creating reverse indices...");
            if (!Functions.VerifyIO(ReverseIndex_inputs, ReverseIndex_outputs)) return false;
            byte[] wordsCountBytes = File.ReadAllBytes(wordsCountPath);
            uint[] wordsCountIndexIndex = Functions.Index(wordsCountBytes, 8, 0, 3);
            uint[] wordsCount = new uint[wordsCountBytes.Length / 8];
            for (int i = 0; i < wordsCount.Length; i++)
                wordsCount[i] = BitConverter.ToUInt32(wordsCountBytes, i * 8 + 4);
            long[] wordsOffsets = new long[wordsCount.Length];
            for (int i = 1; i < wordsCount.Length; i++)
                wordsOffsets[i] = wordsOffsets[i - 1] + wordsCount[i - 1];
            uint[] written = new uint[wordsCount.Length];
            object[] locks = new object[wordsCount.Length / wordsPerLock + 1];
            for (int i = 0; i < locks.Length; i++)
                locks[i] = new object();

            FileStream forwardIndex = new FileStream(forwardIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 64);
            FileStream forwardIndexIndex = new FileStream(forwardIndexIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 16);
            FileStream reverseIndex = new FileStream(reverseIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 64);
            FileStream reverseIndexIndex = new FileStream(reverseIndexIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);

            BinaryReader forwardIndexReader = new BinaryReader(forwardIndex);
            BinaryReader forwardIndexIndexReader = new BinaryReader(forwardIndexIndex);
            BinaryWriter reverseIndexWriter = new BinaryWriter(reverseIndex);
            BinaryWriter reverseIndexIndexWriter = new BinaryWriter(reverseIndexIndex);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            long iterations = (long)Math.Ceiling((decimal)forwardIndex.Length / chunkSize);
            for (long iteration = 0; iteration < iterations; iteration++)
            {
                long currentChunkSize;
                currentChunkSize = chunkSize;
                if (iteration == iterations - 1)
                    currentChunkSize = forwardIndex.Length % chunkSize;
                byte[] chunk = new byte[currentChunkSize];
                forwardIndex.Position = 0;
                forwardIndexIndex.Position = 0;
                Console.WriteLine("Resolving chunk (" + (iteration + 1).ToString() + "/" + iterations.ToString() + ")");

                long file_start = iteration * chunkSize;
                long file_end = iteration * chunkSize + currentChunkSize;

                long totalPages = forwardIndexIndex.Length / 16;
                long pagesRead = 0;
                long pagesProcessed = 0;

                //Creating Machinery
                AsyncSink<Tuple<byte[], byte[]>> sink = null;
                sink = new AsyncSink<Tuple<byte[], byte[]>>(
                    (Tuple<byte[], byte[]> wordsFreq) =>
                    {
                        byte[] CRC = wordsFreq.Item1;
                        byte[] data = wordsFreq.Item2;
                        int words = data.Length / 8;
                        for (int i = 0; i < words; i++)
                        {
                            byte[] wordCRC = new byte[4];
                            Buffer.BlockCopy(data, i * 8, wordCRC, 0, 4);
                            byte[] freq = new byte[4];
                            Buffer.BlockCopy(data, i * 8 + 4, freq, 0, 4);
                            int index = Functions.GetIndex(wordsCountBytes, 8, 0, wordCRC, wordsCountIndexIndex);
                            long position = -1;
                            lock (locks[index / wordsPerLock]) 
                            {
                                position = (wordsOffsets[index] + written[index]) * 8;
                                if (written[index] == wordsCount[index] || (position < file_start || position >= file_end))
                                    continue;
                                written[index]++;
                            }
                            int local_position = (int)(position - file_start);
                            Buffer.BlockCopy(CRC, 0, chunk, local_position, 4);
                            Buffer.BlockCopy(freq, 0, chunk, local_position + 4, 4);
                        }
                        Interlocked.Increment(ref pagesProcessed);
                        return true;
                    });

                //Setting up
                sink.MaxThreads = 8;
                sink.Queue = new Zds.Flow.Collections.SafeRound<Tuple<byte[], byte[]>>(1024 * 8);
                sink.Recursive = true;

                //Exception handler
                ExceptionHandler eh = new ConsoleLogger();
                eh.Add(sink);

                //Updater
                long lastIndexPos = 0;
                long lastPagesRead = 0;
                long lastPagesProcessed = 0;
                SyncTimer timer = null;
                timer = new SyncTimer(
                    (ISyncTimer sender, ref TimeSpan time) =>
                    {
                        long pos_diff = forwardIndex.Position - lastIndexPos;
                        long pr_diff = pagesRead - lastPagesRead;
                        long pp_diff = pagesProcessed - lastPagesProcessed;
                        Console.Title = "Index: " + Functions.SizeSuffix(forwardIndex.Position) + "/" + Functions.SizeSuffix(forwardIndex.Length) + "(" + Functions.SizeSuffix(pos_diff) + "/s), " +
                                        "PR: " + pagesRead.ToString() + "/" + totalPages.ToString() + "(" + pr_diff.ToString() + "/s), " +
                                        "PP: " + pagesProcessed.ToString() + "/" + totalPages.ToString() + "(" + pp_diff.ToString() + "/s), ";
                        lastIndexPos = forwardIndex.Position;
                        lastPagesRead = pagesRead;
                        lastPagesProcessed = pagesProcessed;
                    });

                //Starting pipeline
                timer.Start();
                sink.Start();

                //Creating virtual source
                for (int i = 0; i < forwardIndexIndex.Length / 16; i++)
                {
                    byte[] pageCRC = forwardIndexIndexReader.ReadBytes(4);
                    long page_pos = (long)forwardIndexIndexReader.ReadUInt64();
                    int page_len = (int)forwardIndexIndexReader.ReadUInt32();
                    if (forwardIndex.Position != page_pos)
                        forwardIndex.Position = page_pos;
                    byte[] data = forwardIndexReader.ReadBytes(page_len);
                    Tuple<byte[], byte[]> tuple = new Tuple<byte[], byte[]>(pageCRC, data);
                    while (!sink.Receive(tuple))
                        Thread.Sleep(1);
                    pagesRead += 1;
                }

                //Waiting to finish
                while (pagesProcessed != totalPages)
                    Thread.Sleep(1);

                //Cleaning resources
                timer.Destroy();
                sink.Destroy();

                Console.WriteLine("Writing chunk...");
                reverseIndexWriter.Write(chunk);
            }

            Console.WriteLine("Writing index file...");

            for (int i = 0; i < wordsCount.Length; i++)
            {
                reverseIndexIndexWriter.Write(wordsCountBytes, i * 8, 4);
                reverseIndexIndexWriter.Write((ulong)wordsOffsets[i] * 8);
                reverseIndexIndexWriter.Write(wordsCount[i] * 8);
            }

            stopwatch.Stop();

            Console.WriteLine("Time taken: " + stopwatch.Elapsed.ToString());

            Console.WriteLine("Verifying reverse index...");

            for (int i = 0; i < wordsCount.Length; i++)
                if (written[i] != wordsCount[i])
                    throw new Exception("Reverse index not fully generated!");

            reverseIndexWriter.Flush();
            reverseIndexIndexWriter.Flush();

            reverseIndex.Flush();
            reverseIndexIndex.Flush();

            forwardIndexReader.Dispose();
            forwardIndexIndexReader.Dispose();
            reverseIndexWriter.Dispose();
            reverseIndexIndexWriter.Dispose();

            forwardIndex.Dispose();
            forwardIndexIndex.Dispose();
            reverseIndex.Dispose();
            reverseIndexIndex.Dispose();

            return true;
        }
        static string[] SortReverseIndex_inputs = new string[] { reverseIndexPath, reverseIndexIndexPath };
        static string[] SortReverseIndex_outputs = new string[] {reverseIndexSortedIndexPath , reverseIndexSortedPath };
        static bool SortReverseIndex()
        {
            Console.WriteLine("Sorting reverse index...");
            if (!Functions.VerifyIO(SortReverseIndex_inputs, SortReverseIndex_outputs)) return false;

            FileStream reverseIndex = new FileStream(reverseIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 64);
            FileStream reverseIndexIndex = new FileStream(reverseIndexIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 16);
            FileStream reverseIndexSorted = new FileStream(reverseIndexSortedPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 64);
            FileStream reverseIndexSortedIndex = new FileStream(reverseIndexSortedIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 16);

            BinaryReader reverseIndexReader = new BinaryReader(reverseIndex);
            BinaryReader reverseIndexIndexReader = new BinaryReader(reverseIndexIndex);
            BinaryWriter reverseIndexSortedWriter = new BinaryWriter(reverseIndexSorted);
            BinaryWriter reverseIndexSortedIndexWriter = new BinaryWriter(reverseIndexSortedIndex);

            Stopwatch stopwatch = new Stopwatch();
            long totalWords = reverseIndexIndex.Length / 16;
            long wordsRead = 0;
            long wordsProcessed = 0;

            //Creating Machinery
            AsyncConverter<Tuple<uint, byte[]>, Tuple<uint, byte[]>> converter = null;
            converter = new AsyncConverter<Tuple<uint, byte[]>, Tuple<uint, byte[]>>(
                (Tuple<uint, byte[]> input, ref Tuple<uint, byte[]> output) =>
                {
                    Functions.QuickSort(input.Item2, 8, 4, 4);
                    output = input;
                    return true;
                });
            SyncSink<Tuple<uint, byte[]>> sink = null;
            sink = new SyncSink<Tuple<uint, byte[]>>(
                (Tuple<uint, byte[]> wordsfreq) =>
                {
                    uint wordCRC = wordsfreq.Item1;
                    byte[] frequencies = wordsfreq.Item2;
                    reverseIndexSortedIndexWriter.Write(wordCRC);
                    reverseIndexSortedIndexWriter.Write((ulong)reverseIndexSorted.Position);
                    reverseIndexSortedIndexWriter.Write((uint)frequencies.Length);
                    reverseIndexSortedWriter.Write(frequencies);
                    Interlocked.Increment(ref wordsProcessed);
                    return true;
                });

            //Setting up
            converter.Queue = new Zds.Flow.Collections.SafeRound<Tuple<uint, byte[]>>(1024 * 4);
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
            long lastIndexPos = 0;
            long lastWordRead = 0;
            long lastWordProcessed = 0;
            SyncTimer timer = null;
            timer = new SyncTimer(
                (ISyncTimer sender, ref TimeSpan time) =>
                {
                    long pos_diff = reverseIndex.Position - lastIndexPos;
                    long pr_diff = wordsRead - lastWordRead;
                    long pp_diff = wordsProcessed - lastWordProcessed;
                    Console.Title = "Repos: " + Functions.SizeSuffix(reverseIndex.Position) + "/" + Functions.SizeSuffix(reverseIndex.Length) + "(" + Functions.SizeSuffix(pos_diff) + "/s), " +
                                    "WR: " + wordsRead.ToString() + "/" + totalWords.ToString() + "(" + pr_diff.ToString() + "/s), " +
                                    "WW: " + wordsProcessed.ToString() + "/" + totalWords.ToString() + "(" + pp_diff.ToString() + "/s), ";
                    lastIndexPos = reverseIndex.Position;
                    lastWordRead = wordsRead;
                    lastWordProcessed = wordsProcessed;
                });

            //Starting pipeline
            stopwatch.Start();
            timer.Start();
            converter.Start();
            sink.Start();

            //Creating virtual source
            for (wordsRead = 0; wordsRead < totalWords; wordsRead++)
            {
                uint word_id = reverseIndexIndexReader.ReadUInt32();
                ulong content_pos = reverseIndexIndexReader.ReadUInt64();
                uint content_length = reverseIndexIndexReader.ReadUInt32();
                if (reverseIndex.Position != (long)content_pos)
                    reverseIndex.Position = (long)content_pos;
                byte[] content = reverseIndexReader.ReadBytes((int)content_length);
                var tuple = new Tuple<uint, byte[]>(word_id, content);
                while (!converter.Receive(tuple))
                    Thread.Sleep(1);
            }

            //Waiting to finish
            while (wordsProcessed != totalWords)
                Thread.Sleep(1);
            stopwatch.Stop();

            //Cleaning resources
            timer.Destroy();
            converter.Destroy();
            sink.Destroy();

            Console.WriteLine("Time taken: " + stopwatch.Elapsed.ToString());

            reverseIndexSortedWriter.Flush();
            reverseIndexSortedIndexWriter.Flush();

            reverseIndexSorted.Flush();
            reverseIndexSortedIndex.Flush();

            reverseIndexReader.Dispose();
            reverseIndexIndexReader.Dispose();
            reverseIndexSortedWriter.Dispose();
            reverseIndexSortedIndexWriter.Dispose();

            reverseIndex.Dispose();
            reverseIndexIndex.Dispose();
            reverseIndexSorted.Dispose();
            reverseIndexSortedIndex.Dispose();

            return true;
        }
        static string[] SortReverseIndexIndex_inputs = new string[] { reverseIndexSortedIndexPath };
        static string[] SortReverseIndexIndex_outputs = new string[] { reverseIndexIndexSortedPath };
        static bool SortReverseIndexIndex()
        {
            if (!Functions.VerifyIO(SortReverseIndexIndex_inputs, SortReverseIndexIndex_outputs)) return false;
            Console.WriteLine("Sorting reverse index index...");
            byte[] data = File.ReadAllBytes(reverseIndexSortedIndexPath);
            Functions.QuickSort(data, 16, 0, 4);
            File.WriteAllBytes(reverseIndexIndexSortedPath, data);
            return true;
        }
    }
}