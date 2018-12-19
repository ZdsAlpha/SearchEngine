using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Helper;
using System.Diagnostics;

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
        static void Main2()
        {
            for (int i = 0; i < 1000; i++)
                Test(null);
        }
        static void Test(string[] args)
        {
            Random r = new Random();
            int length = (int)(r.NextDouble() * 1000);
            byte[] data = new byte[length * 16];
            int[] nums = new int[length];
            for (int i = 0; i < length; i++)
            {
                nums[i] = r.Next();
                byte[] num_bytes = BitConverter.GetBytes(nums[i]);
                Buffer.BlockCopy(num_bytes, 0, data, i * 16, 4);
                Buffer.BlockCopy(num_bytes, 0, data, i * 16 + 4, 4);
            }
            Functions.QuickSort(data, 16, 0, 4);
            Functions.QuickSort(data, 16, 0, 4);
            int last_num = int.MinValue;
            for (int i = 0; i < length; i++)
            {
                int num = BitConverter.ToInt32(data, i * 16);
                if (num != BitConverter.ToInt32(data, i * 16 + 4))
                    throw new Exception("Block copy error!");
                if (num < last_num)
                    throw new Exception("Sorting error!");
                last_num = num;
            }
            Array.Sort(nums);
            uint[] index = Functions.Index2(data, 16, 0);
            for (int i = 0; i < length; i++)
            {
                int _i = GetIndex(data, 16, 0, BitConverter.GetBytes(nums[i]), index);
                if (_i != i)
                    throw new Exception("Searching/Indexing error!");
            }
            Console.WriteLine("Tets complete!");
        }

        static void Main(string[] args)
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
                Console.WriteLine("Enter word: ");
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
                    uint freq = BitConverter.ToUInt32(pages, i * 8);
                    uint titleCRC = BitConverter.ToUInt32(pages, i * 8 + 4);
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

        static void _Main(string[] args)
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
            
            FileStream repository = new FileStream(repositoryPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 2);
            FileStream repositoryIndex = new FileStream(repositoryIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 4);
            FileStream lexicon = new FileStream(lexiconPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 16);
            FileStream lexiconIndex = new FileStream(lexiconIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 4);

            BinaryReader repositoryReader = new BinaryReader(repository);
            BinaryReader repositoryIndexReader = new BinaryReader(repositoryIndex);
            BinaryWriter lexiconWriter = new BinaryWriter(lexicon);
            BinaryWriter lexiconIndexWriter = new BinaryWriter(lexiconIndex);

            Stopwatch stopwatch = new Stopwatch();
            IntSpace space = new IntSpace();
            StringBuilder word = new StringBuilder();
            bool ascii = true;
            stopwatch.Start();
            int seconds = 0;
            for (int page_index = 0; page_index < repositoryIndex.Length / 16; page_index++) 
            {
                uint page_id = repositoryIndexReader.ReadUInt32();
                ulong page_pos = repositoryIndexReader.ReadUInt64();
                uint page_length = repositoryIndexReader.ReadUInt32();
                if (repository.Position != (long)page_pos)
                    repository.Position = (long)page_pos;
                string content = Encoding.UTF8.GetString(repositoryReader.ReadBytes((int)page_length));

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
                                ulong wordPos = (ulong)lexicon.Position;
                                uint wordSize = (uint)wordBytes.Length;
                                lexiconIndexWriter.Write(CRC);
                                lexiconIndexWriter.Write(wordPos);
                                lexiconIndexWriter.Write(wordSize);
                                lexiconWriter.Write(wordBytes);
                            }
                        }
                        word.Clear();
                        ascii = true;
                    }
                }

                word.Clear();
                ascii = true;
                if (stopwatch.Elapsed.TotalSeconds > seconds)
                {
                    Console.Clear();
                    Console.WriteLine("Time Elasped: " + stopwatch.Elapsed.ToString());
                    Console.WriteLine("Processed: " + Functions.SizeSuffix(repository.Position));
                    Console.WriteLine("Total: " + Functions.SizeSuffix(repository.Length));
                    Console.WriteLine("Words Count: " + space.Length.ToString());
                    seconds++;
                }
            }
            stopwatch.Stop();
            Console.WriteLine("Total time: " + stopwatch.Elapsed.ToString());
            
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
            byte[] lexiconIndex = File.ReadAllBytes(lexiconSortedIndexPath);
            uint[] lexiconIndex2 = Functions.Index2(lexiconIndex, 16, 0);

            FileStream repository = new FileStream(repositoryPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 2);
            FileStream repositoryIndex = new FileStream(repositoryIndexPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 4);
            FileStream forwardIndex = new FileStream(forwardIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 16);
            FileStream forwardIndexIndex = new FileStream(forwardIndexIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 4);

            BinaryReader repositoryReader = new BinaryReader(repository);
            BinaryReader repositoryIndexReader = new BinaryReader(repositoryIndex);
            BinaryWriter forwardIndexWriter = new BinaryWriter(forwardIndex);
            BinaryWriter forwardIndexIndexWriter = new BinaryWriter(forwardIndexIndex);

            Stopwatch stopwatch = new Stopwatch();
            StringBuilder word = new StringBuilder();
            bool ascii = true;
            stopwatch.Start();
            int seconds = 0;
            for (int page_index = 0; page_index < repositoryIndex.Length / 16; page_index++)
            {
                int[] wordcount = new int[lexiconIndex.Length / 16];
                uint page_id = repositoryIndexReader.ReadUInt32();
                ulong page_pos = repositoryIndexReader.ReadUInt64();
                uint page_length = repositoryIndexReader.ReadUInt32();
                if (repository.Position != (long)page_pos)
                    repository.Position = (long)page_pos;
                string content = Encoding.UTF8.GetString(repositoryReader.ReadBytes((int)page_length));

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
                            int start;
                            int stop;
                            Functions.BinarySearch(lexiconIndex, 16, 0, BitConverter.GetBytes(CRC), out start, out stop, lexiconIndex2);
                            if (stop - start > 0) throw new Exception("Collision detected!");
                            if (start == stop)
                                wordcount[start]++;
                        }
                        word.Clear();
                        ascii = true;
                    }
                }

                MemoryStream wordIdList = new MemoryStream();
                BinaryWriter wordIdListWriter = new BinaryWriter(wordIdList);
                for (int i = 0; i < wordcount.Length; i++)
                {
                    if (wordcount[i] != 0)
                    {
                        wordIdListWriter.Write(lexiconIndex, i * 16, 4);
                        wordIdListWriter.Write(wordcount[i]);
                    }
                }
                wordIdListWriter.Flush();
                byte[] wordIdListBin = wordIdList.ToArray();
                wordIdListWriter.Dispose();
                wordIdList.Dispose();

                ulong position = (ulong)forwardIndex.Position;
                uint length = (uint)wordIdListBin.Length;
                forwardIndexIndexWriter.Write(page_id);
                forwardIndexIndexWriter.Write(position);
                forwardIndexIndexWriter.Write(length);
                forwardIndexWriter.Write(wordIdListBin);

                word.Clear();
                ascii = true;
                if (stopwatch.Elapsed.TotalSeconds > seconds)
                {
                    Console.Clear();
                    Console.WriteLine("Time Elasped: " + stopwatch.Elapsed.ToString());
                    Console.WriteLine("Processed: " + Functions.SizeSuffix(repository.Position));
                    Console.WriteLine("Total: " + Functions.SizeSuffix(repository.Length));
                    Console.WriteLine("Pages processed: " + page_index.ToString());
                    seconds++;
                }
            }
            stopwatch.Stop();
            Console.WriteLine("Total time: " + stopwatch.Elapsed.ToString());

            repositoryReader.Dispose();
            repositoryIndexReader.Dispose();
            forwardIndexWriter.Dispose();
            forwardIndexIndexWriter.Dispose();
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

            ulong cf = 0;
            for (int i = 0; i < wordcount.Length; i++)
                if (wordcount[i] != 0)
                {
                    reverseIndexIndexWriter.Write(lexiconIndex, i * 16, 4);
                    reverseIndexIndexWriter.Write(cf * 4);
                    reverseIndexIndexWriter.Write(wordcount[i] * 4);
                    cf += (ulong)wordcount[i];
                }
                else
                    throw new Exception("Word count cannot be zero");
            int[] wordpos = new int[wordcount.Length];
            for (int i = 1; i < wordpos.Length; i++)
                wordpos[i] = wordpos[i - 1] + wordcount[i - 1];
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
                        reverseIndex.Position = wordpos[start] * 8 + wordcount2[start] * 8;
                        reverseIndexWriter.Write(wordCRC);
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
