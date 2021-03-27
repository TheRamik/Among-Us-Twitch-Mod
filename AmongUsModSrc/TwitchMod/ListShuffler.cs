using System;
using System.Collections.Generic;

namespace TwitchMod
{
    public static class ListShuffler
    {
        private static Random rng = new Random();

        //Based on Fisher-Yates shuffle
        //Credit: https://stackoverflow.com/questions/273313/randomize-a-listt
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}