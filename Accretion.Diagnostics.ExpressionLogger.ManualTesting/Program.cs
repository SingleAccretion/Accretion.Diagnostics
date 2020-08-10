using System;

namespace Accretion.Diagnostics.ExpressionLogger.ManualTesting
{
    public class Program
    {
        public static void Main()
        {
            ExpressionLogger.Log(new Action(() => Console.WriteLine(10)));

            IsEmpty(new int[] { });

            1.Log(); 
            2.Log();

            new object().Log();

            object kek = null;
            kek.Log();

            ConsoleColor.Black.ToString().Log();

            ConsoleColor.Black.ToString()?.Log();
        }

        public static bool IsEmpty(Array array)
        {
            array.Length.Log();
            return array.Length > 0;
        }
    }
}