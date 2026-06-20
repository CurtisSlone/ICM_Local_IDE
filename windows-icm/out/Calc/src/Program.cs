using System;
using System.Globalization;

namespace App
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: Calculator <number> <operator> <number>");
                return 2;
            }

            double firstNumber;
            double secondNumber;
            string operatorString = args[1];

            try
            {
                firstNumber = double.Parse(args[0], CultureInfo.InvariantCulture);
                secondNumber = double.Parse(args[2], CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                Console.Error.WriteLine("Error: Invalid number format.");
                return 2;
            }

            double result = 0;
            bool isValidOperator = true;

            if (operatorString == "+")
            {
                result = firstNumber + secondNumber;
            }
            else if (operatorString == "-")
            {
                result = firstNumber - secondNumber;
            }
            else if (operatorString == "*")
            {
                result = firstNumber * secondNumber;
            }
            else if (operatorString == "/")
            {
                if (secondNumber == 0)
                {
                    Console.Error.WriteLine("Error: Division by zero.");
                    return 1;
                }
                result = firstNumber / secondNumber;
            }
            else
            {
                isValidOperator = false;
                Console.Error.WriteLine("Error: Invalid operator. Use one of +, -, *, /");
                return 2;
            }

            if (isValidOperator)
            {
                Console.WriteLine(result.ToString(CultureInfo.InvariantCulture));
            }

            return 0;
        }
    }
}
