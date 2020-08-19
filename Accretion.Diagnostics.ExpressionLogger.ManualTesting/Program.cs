using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Accretion.Diagnostics.ExpressionLogger.ManualTesting
{
    public class Program
    {
        public static async Task Main()
        {
            ExpressionLogger.Log(new Action(() => Console.WriteLine(10)));

            IsEmpty(new int[] { });

            new List<int>().Log(); new List<double>().Log();

            new Func<IEnumerable<IReadOnlyCollection<object>>>(() => default).Log();
            new Dictionary<ValueTask<int>[], KeyValuePair<List<List<int>[]>, double>>().Log();

            2.Log(); 2.0f.Log(); 3.0.Log();
            new object().Log();
            new int?(10).Log();

            new int[] { 1, 2, 3 }.Log();
            new int?[] { 1, 2, 3 }.Log();
            new int?[] { 1, 2, 3 }.Log();
            new char[] { 'h', 'e', 'l', 'l', 'o', 'w', 'o', 'r', 'l', 'd' }.Log();
            new string[] { "hello", "world" }.Log();
            new Dictionary<int, string> { { 1, "1" }, { 2, "2" }, { 3, "3" } }.Log();

            object kek = null;
            kek.Log();

            ConsoleColor.Red.ToString()?.Log();
            ConsoleColor.Black.ToString().Log();

            Generic<byte>.Get().Log();

            Generic<int>.Get().Log(); Generic<double>.Get().Log(); Generic<float>.Get().Log();

            Generic<IEnumerable<int>>.Get().Log(); Generic<IEnumerable<double>>.Get().Log(); Generic<IEnumerable<float>>.Get().Log();

            Generic<int[]>.Get().Log();

            typeof(int).Log();
            typeof(List<int>).Log();
            typeof(List<int>.Enumerator).Log();
            typeof(Generic<int>.NestedNoNGeneric).Log();
            typeof(Generic<int>.NestedGeneric<double>).Log();
            typeof(Generic<int>.NestedGenericDouble<double, float>).Log();
            typeof(Generic<int>.NestedNoNGeneric.NestedNestedNonGeneric).Log();
            typeof(Generic<int>.NestedGenericDouble<double, float>.NestedNestedNoNGeneric).Log();
            typeof(Generic<int>.NestedGenericDouble<double, float>.NestedNestedGeneric<short>).Log();
            typeof(Generic<Generic<Generic<int>.NestedGeneric<double>>>.NestedGenericDouble<Generic<double>.NestedNoNGeneric, Generic<float>.NestedGenericDouble<byte, sbyte>.NestedNestedGeneric<int>>.NestedNestedGeneric<bool>).Log();

            new[] { 1, 2, 3 }.
                Select(x => 2 * x).Log();

            ExpressionLogger.Log(new[] { 4, 5, 6 }.
                Select(x => 2 * x));

            await Methods.AsyncWait().Log();

            new object().
                Log().
                    Log().
                        Log();

            ExpressionLogger.Log(
                ExpressionLogger.Log(
                    ExpressionLogger.Log(1)));
        }

        public static bool IsEmpty(Array array)
        {
            (array.Length > 0).Log();
            return array.Length > 0;
        }
    }

    public class Generic<T>
    {
        public static T Get() => default;

        public static Z GetZ<Z>()
        {
            default(IEnumerable<Z>).Log();
            default(Z[]).Log();
            return default(Z).Log();
        }

        public struct NestedNoNGeneric
        {
            public struct NestedNestedNonGeneric { }
        }

        public struct NestedGeneric<U> { }

        public struct NestedGenericDouble<Z, R>
        {
            public struct NestedNestedNoNGeneric { }
            public struct NestedNestedGeneric<P> { }
        }
    }

    public static class Methods
    {
        public static Task<int> AsyncWait() => Task.FromResult(50);
    }
}