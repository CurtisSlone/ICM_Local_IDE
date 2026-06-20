using System;

namespace App.Core
{
    public class Statistics
    {
        public static double Mean(double[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("Array cannot be null or empty.");
            }

            double sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum / values.Length;
        }

        public static double Min(double[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("Array cannot be null or empty.");
            }

            double min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min)
                {
                    min = values[i];
                }
            }
            return min;
        }

        public static double Max(double[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("Array cannot be null or empty.");
            }

            double max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }
            return max;
        }
    }
}
