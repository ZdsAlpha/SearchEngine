using System;
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
        static void Main(string[] args)
        {
            try
            {

                Wikitext();
            }catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.ReadLine();
        }
        static void Wikitext()
        {
            Directory.CreateDirectory("Wikitext\\");
            FileStream wikipedia = new FileStream(wikipediaPath, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 2);
            FileStream titles = new FileStream(pageTitlePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 16);
            FileStream titlesIndex = new FileStream(pageTitleIndexPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 4);
            FileStream wikitext = new FileStream(wikitextPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024 * 2);
            FileStream wikitextIndex = new FileStream(wikitextIndexPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 4);
            FileStream redirects = new FileStream(redirectsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 4);

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
        }
    }
}
