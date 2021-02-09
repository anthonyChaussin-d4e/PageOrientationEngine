using System;
using System.IO;
using PageOrientationEngine;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var temp = new DocumentInspector(@".\tessdata", "fra").DetectPageOrientation(@"C:\Users\Anthony\Downloads\Test.tiff");
            foreach (var result in temp)
                Console.WriteLine(result.ToString());

            Console.ReadKey();
        }
    }
}
