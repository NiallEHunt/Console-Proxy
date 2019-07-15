using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace ConsoleProxy
{
    public class Firewall
    {
        // List of all the Urls that are blocked. Stored as Regex in the form .*somesite.com
        private static List<Regex> blockedURLs;
        public static bool IsActive { get; private set; }

        // Reads the blocked urls from the text file and sets isActive to true
        public static void Initialise()
        {
            IsActive = true;
            blockedURLs = new List<Regex>();
            using (StreamReader reader = new StreamReader("..\\..\\BlockedURLs.txt"))
            {
                string line = reader.ReadLine();
                
                while (line != null)
                {
                    Regex regex = new Regex(line);
                    blockedURLs.Add(regex);

                    line = reader.ReadLine();
                }

                reader.Close();
            }
        }

        // Takes a string and adds the regex to the BlockedURLs list and also writes the url to the blocked URLs file
        public static void BlockURL(string url)
        {
            try
            {
                Regex regex = new Regex(url);
                blockedURLs.Add(regex);

                using (StreamWriter writer = File.AppendText("..\\..\\BlockedURLs.txt"))
                {
                    writer.WriteLine(url);

                    writer.Close();
                }
            }
            catch(Exception)
            {
                Console.WriteLine("Invalid URL. Please write the URL which you would like to ban in the form 'www.somesite.com'");
            }
        }

        // Removes a url from the List 
        public static void UnblockURL(string url)
        {
            Regex urlRegex = new Regex(url);
            Console.WriteLine(url);
            if (blockedURLs.Contains(urlRegex))
            {
                blockedURLs.Remove(urlRegex);
                Console.WriteLine("Unblocked " + url);
            }
        }

        // Checks to see if the request is going to a site that contains the blocked url
        public static bool isAllowed(string url)
        {
            return !blockedURLs.Any(blockedURL => blockedURL.Match(url).Success);
        }
    }
}
