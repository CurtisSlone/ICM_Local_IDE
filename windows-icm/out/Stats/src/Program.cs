using System;
using System.Globalization;

namespace App
{
    internal static class Program
    {
        internal static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Console.Error.WriteLine("Usage: Stats <number> [<number> ...]");
                return 2;
            }

            double[] values = new double[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (!double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                {
                    Console.Error.WriteLine("Usage: Stats <number> [<number> ...]");
                    return 2;
                }
            }

            try
            {
                double mean = App.Core.Statistics.Mean(values);
                double min = App.Core.Statistics.Min(values);
                double max = App.Core.Statistics.Max(values);

                Console.WriteLine("mean: " + mean.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("min: " + min.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("max: " + max.ToString(CultureInfo.InvariantCulture));
            }
            catch (ArgumentException)
            {
                Console.Error.WriteLine("Usage: Stats <number> [<number> ...]");
                return 2;
            }

            return 0;
        }
    }
}