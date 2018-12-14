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
        const string pageTitleList = "titles.txt";
        const string pageTitleIndex = "titles.index";
        const string redirects = "redirects.bin";
        static void Main(string[] args)
        {
            Wikitext();
        }
        static void Wikitext()
        {
            Directory.CreateDirectory("Wikitext\\");
            FileStream wikipedia = new FileStream("wikipedia.xml", FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024 * 2);
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
                                if (redirect == null && ns == 0)
                                    File.WriteAllText("Wikitext\\" + title + ".txt",content,Encoding.UTF8);
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
        }
    }
}
