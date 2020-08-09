using System;

namespace Accretion.Diagnostics.FancyDebug.ManualTesting
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Fancy.Debug(new Action(() => Console.WriteLine(10)));
            Console.WriteLine("Hello World!");
            Console.WriteLine(IsEmpty(new int[] { }));

            Console.WriteLine(new Action(() => Console.WriteLine(10)));
        }

        public static bool IsEmpty(Array array)
        {
            Fancy.Debug(array.Length);
            return array.Length > 0;
        }
    }
}