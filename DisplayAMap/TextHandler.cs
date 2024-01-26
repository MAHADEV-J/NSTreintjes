using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisplayAMap
{
    internal static class TextHandler
    {
        public static string PlacenameHandler(string text, int decider)
        {
            string[] words = text.Split(' ');

            if (words.Length == 1 && text.Length >= 14)
            {
                return decider == 0 ? "Volgende stop:\n" : ReplaceEnOrInsertNewline(text);
            }
            if (words.Length >= 2)
            {
                return decider == 0 ? "Volgende stop:\n" : ReplaceSpaceWithNewline(text);
            }
            return decider == 0 ? "Volgende stop:" : text;
        }

        static string ReplaceSpaceWithNewline(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                // Handle empty or null strings
                return input;
            }

            int length = input.Length;
            int middleIndex = length / 2;

            // Find the index of the closest space to the center
            int closestSpaceIndex = FindClosestSpaceIndex(input, middleIndex);

            if (closestSpaceIndex != -1)
            {
                // Replace the space with a newline character
                char[] resultArray = input.ToCharArray();
                resultArray[closestSpaceIndex] = '\n';
                return new string(resultArray);
            }
            else
            {
                // No space found, return the original string
                return input;
            }
        }

        static int FindClosestSpaceIndex(string input, int index)
        {
            // Look for a space to the left and right of the given index
            for (int i = 0; i <= index; i++)
            {
                // Check left side
                if (index - i >= 0 && input[index - i] == ' ')
                {
                    return index - i;
                }

                // Check right side
                if (index + i < input.Length && input[index + i] == ' ')
                {
                    return index + i;
                }
            }

            // No space found
            return -1;
        }

        static string ReplaceEnOrInsertNewline(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                // Handle empty or null strings
                return input;
            }

            // Find the index of "en" in the string
            int enIndex = input.IndexOf("en");

            if (enIndex != -1)
            {
                // Replace "en" with "en-\n"
                return input.Insert(enIndex + 2, "-\n");
            }
            else
            {
                // If "en" is not found, insert "\n" in the middle of the string
                int middleIndex = input.Length / 2;
                return input.Insert(middleIndex, "\n");
            }
        }
    }
}
