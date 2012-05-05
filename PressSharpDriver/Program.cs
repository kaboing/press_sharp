using System;
using System.Xml.Linq;
using PressSharp;

namespace PressSharpDriver
{
    class Program
    {
        public static void Main(string[] args)
        {
            var wpXml = XDocument.Load(@"c:\wp.xml");

            var blog = new Blog(wpXml);
            var posts = blog.GetPosts();

            foreach (var post in posts)
            {
                Console.WriteLine(post.Body);
                Console.ReadKey();
                Console.Clear();
            }

            Console.WriteLine("Done.");
            Console.ReadKey();
        }
    }
}
