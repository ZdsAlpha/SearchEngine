﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Helper;

namespace Repository
{
    class Program
    {
        const string wikipediaPath = "wikipedia.xml";

        const string wikitextPath = "wikitext.txt";
        const string wikitextIndexPath = "wikitext.index";
        const string pageTitlePath = "titles.txt";
        const string pageTitleIndexPath = "titles.index";
        const string redirectsPath = "redirects.bin";

        const string wikitextSortedIndexPath = "wikitext_sorted.index";
        const string pageTitleSortedIndexPath = "titles_sorted.index";
        const string redirectsSortedPath = "redirects_sorted.bin";

        static void _Main(string[] args)
        {
            Console.WriteLine("Creating repository...");
            try
            {
                if (!Wikitext_outputs.All((path) => File.Exists(path)))
                    if (!Wikitext()) throw new Exception("Unable to generate Wikitext");
                if (!Sorting_outputs.All((path) => File.Exists(path)))
                    if (!Sorting()) throw new Exception("Unable to sort indices");
                Console.WriteLine("Repository created!");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.ReadLine();
        }
        static string[] Wikitext_inputs = new string[] { wikipediaPath };
        static string[] Wikitext_outputs = new string[] { wikitextPath, wikitextIndexPath, pageTitlePath, pageTitleIndexPath, redirectsPath };
        static bool Wikitext()
        {
            foreach (var file in Wikitext_inputs)
                if (!File.Exists(file))
                {
                    Console.WriteLine("File " + file + " not found!");
                    return false;
                }
            if (Wikitext_outputs.Any((path) => File.Exists(path)))
            {
                Console.WriteLine("WARNING: Some files will be replaced.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            FileStream wikipedia = new FileStream(wikipediaPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 2);
            FileStream titles = new FileStream(pageTitlePath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 16);
            FileStream titlesIndex = new FileStream(pageTitleIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 4);
            FileStream wikitext = new FileStream(wikitextPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024 * 2);
            FileStream wikitextIndex = new FileStream(wikitextIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 4);
            FileStream redirects = new FileStream(redirectsPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 4);

            BinaryWriter titlesWriter = new BinaryWriter(titles);
            BinaryWriter titlesIndexWriter = new BinaryWriter(titlesIndex);
            BinaryWriter wikitextWriter = new BinaryWriter(wikitext);
            BinaryWriter wikitextIndexWriter = new BinaryWriter(wikitextIndex);
            BinaryWriter redirectsWriter = new BinaryWriter(redirects);

            using (XmlReader reader = XmlReader.Create(wikipedia))
            {
                bool is_page = false;
                int ns = 0;
                string title = null;
                string redirect = null;
                string content = null;

                string seeking = "";

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "page":
                                    is_page = true;
                                    ns = 0;
                                    title = null;
                                    redirect = null;
                                    content = null;
                                    seeking = "";
                                    break;
                                case "ns":
                                    seeking = "ns";
                                    break;
                                case "title":
                                    seeking = "title";
                                    break;
                                case "redirect":
                                    redirect = reader.GetAttribute("title");
                                    break;
                                case "text":
                                    seeking = "content";
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case XmlNodeType.Text:
                            if (!is_page)
                                continue;
                            switch (seeking)
                            {
                                case "ns":
                                    ns = int.Parse(reader.Value);
                                    seeking = "";
                                    break;
                                case "title":
                                    title = reader.Value;
                                    seeking = "";
                                    break;
                                case "content":
                                    content = reader.Value;
                                    seeking = "";
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case XmlNodeType.EndElement:
                            if (reader.Name == "page")
                            {
                                uint titleCRC = Crc32.Compute(title);
                                if (redirect == null)
                                {
                                    if (ns == 0)
                                    {
                                        byte[] titleBytes = Encoding.UTF8.GetBytes(title + "\n");
                                        byte[] contentBytes = Encoding.UTF8.GetBytes(content + "\n");
                                        ulong titlePos = (ulong)titles.Position;
                                        uint titleSize = (uint)titleBytes.Length;
                                        ulong contentPos = (ulong)wikitext.Position;
                                        uint contentSize = (uint)contentBytes.Length;
                                        titlesIndexWriter.Write(titleCRC);
                                        titlesIndexWriter.Write(titlePos);
                                        titlesIndexWriter.Write(titleSize);
                                        titlesWriter.Write(titleBytes);
                                        wikitextIndexWriter.Write(titleCRC);
                                        wikitextIndexWriter.Write(contentPos);
                                        wikitextIndexWriter.Write(contentSize);
                                        wikitextWriter.Write(contentBytes);
                                        Console.WriteLine("\"" + title + "\" (" + titleCRC.ToString() + ") " + Functions.SizeSuffix(contentSize));
                                    }
                                }
                                else
                                {
                                    uint redirectCRC = Crc32.Compute(redirect);
                                    redirectsWriter.Write(titleCRC);
                                    redirectsWriter.Write(redirectCRC);
                                }
                                is_page = false;
                                ns = 0;
                                title = null;
                                redirect = null;
                                content = null;
                                seeking = "";
                            }
                            break;
                        default:
                            break;
                    }
                }
            }

            titlesWriter.Flush();
            titlesIndexWriter.Flush();
            wikitextWriter.Flush();
            wikitextIndexWriter.Flush();
            redirectsWriter.Flush();

            titles.Flush();
            titlesIndex.Flush();
            wikitext.Flush();
            wikitextIndex.Flush();
            redirects.Flush();

            titlesWriter.Dispose();
            titlesIndexWriter.Dispose();
            wikitextWriter.Dispose();
            wikitextIndexWriter.Dispose();
            redirectsWriter.Dispose();

            wikipedia.Dispose();
            titles.Dispose();
            titlesIndex.Dispose();
            wikitext.Dispose();
            wikitextIndex.Dispose();
            redirects.Dispose();
            return true;
        }
        static string[] Sorting_inputs = new string[] { wikitextIndexPath, pageTitleIndexPath, redirectsPath };
        static string[] Sorting_outputs = new string[] { wikitextSortedIndexPath, pageTitleSortedIndexPath, redirectsSortedPath };
        static bool Sorting()
        {
            foreach (var file in Sorting_inputs)
                if (!File.Exists(file))
                {
                    Console.WriteLine("File " + file + " not found!");
                    return false;
                }
            if (Sorting_outputs.Any((path) => File.Exists(path)))
            {
                Console.WriteLine("WARNING: Some files will be replaced.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            byte[] data = null;
            Console.WriteLine("Sorting wikitext indices...");
            data = File.ReadAllBytes(wikitextIndexPath);
            Functions.QuickSort(data, 16, 0, 4);
            File.WriteAllBytes(wikitextSortedIndexPath, data);
            data = null;
            Console.WriteLine("Sorting titles indices...");
            data = File.ReadAllBytes(pageTitleIndexPath);
            Functions.QuickSort(data, 16, 0, 4);
            File.WriteAllBytes(pageTitleSortedIndexPath, data);
            data = null;
            Console.WriteLine("Sorting redirects...");
            data = File.ReadAllBytes(redirectsPath);
            Functions.QuickSort(data, 8, 0, 4);
            File.WriteAllBytes(redirectsSortedPath, data);
            data = null;
            return true;
        }

        static void ExtractPages(int pages = 1000)
        {
            FileStream titles = new FileStream(pageTitlePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 16);
            FileStream titlesIndex = new FileStream(pageTitleIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 4);
            FileStream wikitext = new FileStream(wikitextPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024 * 2);
            FileStream wikitextIndex = new FileStream(wikitextIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 4);

            FileStream minititles = new FileStream("mini_titles.txt", FileMode.Create);
            FileStream minititlesIndex = new FileStream("mini_titles.index", FileMode.Create);
            FileStream miniwikitext = new FileStream("mini_wikitext.txt", FileMode.Create);
            FileStream miniwikitextIndex = new FileStream("mini_wikitext.index", FileMode.Create);

            BinaryReader titlesReader = new BinaryReader(titles);
            BinaryReader titlesIndexReader = new BinaryReader(titlesIndex);
            BinaryReader wikitextReader = new BinaryReader(wikitext);
            BinaryReader wikitextIndexReader = new BinaryReader(wikitextIndex);

            BinaryWriter titlesWriter = new BinaryWriter(minititles);
            BinaryWriter titlesIndexWriter = new BinaryWriter(minititlesIndex);
            BinaryWriter wikitextWriter = new BinaryWriter(miniwikitext);
            BinaryWriter wikitextIndexWriter = new BinaryWriter(miniwikitextIndex);

            for (int i = 0; i < pages; i++)
            {
                uint CRC = titlesIndexReader.ReadUInt32();
                if (CRC != wikitextIndexReader.ReadUInt32()) throw new Exception("Files not synchronized!");
                ulong pos;
                uint len;
                pos = titlesIndexReader.ReadUInt64();
                len = titlesIndexReader.ReadUInt32();
                if ((long)pos != titles.Position) throw new Exception("Invalid position");
                titlesIndexWriter.Write(CRC);
                titlesIndexWriter.Write(pos);
                titlesIndexWriter.Write(len);
                titlesWriter.Write(titlesReader.ReadBytes((int)len));
                pos = wikitextIndexReader.ReadUInt64();
                len = wikitextIndexReader.ReadUInt32();
                if ((long)pos != wikitext.Position) throw new Exception("Invalid position");
                wikitextIndexWriter.Write(CRC);
                wikitextIndexWriter.Write(pos);
                wikitextIndexWriter.Write(len);
                wikitextWriter.Write(wikitextReader.ReadBytes((int)len));
            }

            titlesWriter.Flush();
            titlesIndexWriter.Flush();
            wikitextWriter.Flush();
            wikitextIndexWriter.Flush();

            titlesWriter.Dispose();
            titlesIndexWriter.Dispose();
            wikitextWriter.Dispose();
            wikitextIndexWriter.Dispose();
        }
    }
}
