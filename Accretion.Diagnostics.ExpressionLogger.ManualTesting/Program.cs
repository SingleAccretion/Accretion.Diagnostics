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

            1.Log();

            new object().Log();

            object kek = null;
            kek.Log();

            ConsoleColor.Black.ToString().Log();
            ConsoleColor.Black.ToString()?.Log();

            Console.WriteLine(new Action(() => Console.WriteLine(10)));

            foreach (ConsoleColor color in Enum.GetValues(typeof(ConsoleColor)))
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"This is {Enum.GetName(typeof(ConsoleColor), color)}");
            }
        }

        public static bool IsEmpty(Array array)
        {
            ExpressionLogger.Log(array.Length);
            return array.Length > 0;
        }
    }
}