using System;
using System.Collections.Generic;

namespace App.Core
{
    public class Evaluator
    {
        private static int _position;
        private static List<string> _tokens;

        public static double Evaluate(string expression)
        {
            _tokens = Tokenizer.Tokenize(expression);
            _position = 0;

            double result = ParseExpression();
            
            if (_position < _tokens.Count)
            {
                throw new FormatException("Unexpected token: " + _tokens[_position]);
            }

            return result;
        }

        private static double ParseExpression()
        {
            double result = ParseTerm();

            while (_position < _tokens.Count && 
                   (_tokens[_position] == "+" || _tokens[_position] == "-"))
            {
                string op = _tokens[_position];
                _position++;
                double term = ParseTerm();
                if (op == "+")
                    result += term;
                else
                    result -= term;
            }

            return result;
        }

        private static double ParseTerm()
        {
            double result = ParseFactor();

            while (_position < _tokens.Count && 
                   (_tokens[_position] == "*" || _tokens[_position] == "/"))
            {
                string op = _tokens[_position];
                _position++;
                double factor = ParseFactor();
                if (op == "*")
                    result *= factor;
                else
                    result /= factor;
            }

            return result;
        }

        private static double ParseFactor()
        {
            if (_position >= _tokens.Count)
            {
                throw new FormatException("Unexpected end of expression");
            }

            string token = _tokens[_position];

            if (token == "(")
            {
                _position++;
                double result = ParseExpression();
                if (_position >= _tokens.Count || _tokens[_position] != ")")
                {
                    throw new FormatException("Missing closing parenthesis");
                }
                _position++;
                return result;
            }
            else if (token == "-")
            {
                _position++;
                return -ParseFactor();
            }
            else if (token == "+")
            {
                _position++;
                return ParseFactor();
            }
            else
            {
                double number;
                if (double.TryParse(token, out number))
                {
                    _position++;
                    return number;
                }
                else
                {
                    throw new FormatException("Unexpected token: " + token);
                }
            }
        }
    }
}