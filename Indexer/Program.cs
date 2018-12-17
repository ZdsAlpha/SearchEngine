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

        const string wordcountPath = "wordcount.bin";

        static void Main()
        {
            byte[] data = File.ReadAllBytes(lexiconIndexPath);
            Functions.QuickSort(data, 16, 0, 4);
            uint[] index = Functions.Index2(data, 16, 2);
            uint x = index[256 * 256 - 1];
        }

        static void _Main(string[] args)
        {
            Console.WriteLine("Creating index...");
            try
            {
                if (!Lexicon_outputs.All((path) => File.Exists(path)))
                    if (!Lexicon()) throw new Exception("Unable to generate lexicon!");
                Console.WriteLine("Index created!");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.ReadKey();
        }
        static string[] Lexicon_inputs = new string[] { repositoryPath, repositoryIndexPath };
        static string[] Lexicon_outputs = new string[] { lexiconPath, lexiconIndexPath };
        static bool Lexicon()
        {
            foreach (var file in Lexicon_inputs)
                if (!File.Exists(file))
                {
                    Console.WriteLine("File " + file + " not found!");
                    return false;
                }
            if (Lexicon_outputs.Any((path) => File.Exists(path)))
            {
                Console.WriteLine("WARNING: Some files will be replaced.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            
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
        
        static bool ForwardIndex()
        {
            return false;
        }
    }
}
