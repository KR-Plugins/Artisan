﻿using System.Linq;

namespace Artisan.RawInformation
{
    internal static class HelperExtensions
    {
        public static string GetNumbers(this string input)
        {
            if (input == null) return "";
            if (input.Length == 0) return "";

            var numbers = new string(input.Where(c => char.IsDigit(c)).ToArray());
            return numbers;
        }

        public static string GetLast(this string source, int tail_length)
        {
            if (tail_length >= source.Length)
                return source;
            return source.Substring(source.Length - tail_length);
        }
    }
}
