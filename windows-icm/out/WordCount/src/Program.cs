using System;
using System.IO;
using System.Text;

namespace App
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: wc <file>");
                return 2;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine("wc: " + filePath + ": No such file or directory");
                return 1;
            }

            int lineCount = 0;
            int wordCount = 0;
            int charCount = 0;
            bool inWord = false;

            try
            {
                using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineCount++;
                        charCount += line.Length;

                        foreach (char c in line)
                        {
                            if (char.IsWhiteSpace(c))
                            {
                                if (inWord)
                                {
                                    wordCount++;
                                    inWord = false;
                                }
                            }
                            else
                            {
                                inWord = true;
                            }
                        }
                    }

                    if (inWord)
                    {
                        wordCount++;
                    }
                }

                Console.WriteLine(lineCount + " " + wordCount + " " + charCount + " " + filePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("wc: Error reading file: " + ex.Message);
                return 1;
            }

            return 0;
        }
    }
}
