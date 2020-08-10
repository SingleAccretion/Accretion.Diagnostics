using System;
using System.Diagnostics;

namespace Accretion.Diagnostics.ExpressionLogger.ManualTesting
{
    public class Program
    {
        public static void Main()
        {
            ExpressionLogger.Log(new Action(() => Console.WriteLine(10)));
            Console.WriteLine("Hello World!");
            Console.WriteLine(IsEmpty(new int[] { }));

            Console.WriteLine(new Action(() => Console.WriteLine(10)));
        }

        public static bool IsEmpty(Array array)
        {
            ExpressionLogger.Log(array.Length);
            return array.Length > 0;
        }
    }
}