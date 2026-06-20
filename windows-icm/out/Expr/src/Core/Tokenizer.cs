using System;
using System.Collections.Generic;

namespace App.Core
{
    public class Tokenizer
    {
        public static List<string> Tokenize(string input)
        {
            List<string> tokens = new List<string>();
            if (input == null)
            {
                throw new FormatException("Input cannot be null.");
            }

            int i = 0;
            while (i < input.Length)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '(' || c == ')')
                {
                    tokens.Add(c.ToString());
                    i++;
                }
                else if (char.IsDigit(c) || c == '.')
                {
                    string number = "";
                    while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.'))
                    {
                        number += input[i];
                        i++;
                    }
                    tokens.Add(number);
                }
                else
                {
                    throw new FormatException("Unexpected character: " + c);
                }
            }

            return tokens;
        }
    }
}