using System;

namespace App.Core
{
    public static class Stats
    {
        public static double Average(int[] xs)
        {
            if (xs == null)
            {
                throw new ArgumentNullException("xs");
            }
            
            if (xs.Length == 0)
            {
                throw new ArgumentException("xs must not be empty");
            }
            
            int sum = 0;
            foreach (int x in xs)
            {
                sum += x;
            }
            
            return (double)sum / xs.Length;
        }
    }
}