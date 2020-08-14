using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Accretion.Diagnostics.ExpressionLogger.ManualTesting
{
    public class Program
    {
        public static void Main()
        {
            ExpressionLogger.Log(new Action(() => Console.WriteLine(10)));

            IsEmpty(new int[] { });

            new List<int>().Log(); new List<double>().Log();

            new Task<IEnumerable<IReadOnlyCollection<object>>>(() => default).Log();
            
            1.Log();
            2.Log(); 2.0f.Log(); 3.0.Log();

            new object().Log();

            object kek = null;
            kek.Log();

            ConsoleColor.Red.ToString()?.Log();
            ConsoleColor.Black.ToString().Log();

            Generic<byte>.Get().Log();

            Generic<int>.Get().Log(); Generic<double>.Get().Log(); Generic<float>.Get().Log();
            
            Generic<IEnumerable<int>>.Get().Log(); Generic<IEnumerable<double>>.Get().Log(); Generic<IEnumerable<float>>.Get().Log(); 

            Generic<int[]>.Get().Log();
        }

        public static bool IsEmpty(Array array)
        {
            array.Length.Log();
            return array.Length > 0;
        }
    }

    public static class Generic<T>
    {
        public static T Get() => default;
    }
}