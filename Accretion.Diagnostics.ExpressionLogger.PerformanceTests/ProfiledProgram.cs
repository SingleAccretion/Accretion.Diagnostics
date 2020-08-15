using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Accretion.Diagnostics.ExpressionLogger.PerformanceTests
{
    class ProfiledProgram
    {
        static void Main()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"
            using System;
            using System.Collections.Generic;
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
                        new string[] { ""hello"", ""world"" }.Log();
                        new Dictionary<int, string> { { 1, ""1"" }, { 2, ""2"" }, { 3, ""3"" } }.Log();
            
                        object kek = null;
                        kek.Log();
            
                        ConsoleColor.Red.ToString()?.Log();
                        ConsoleColor.Black.ToString().Log();
            
                        Generic<byte>.Get().Log();
            
                        Generic<int>.Get().Log(); Generic<double>.Get().Log(); Generic<float>.Get().Log();
            
                        Generic<IEnumerable<int>>.Get().Log(); Generic<IEnumerable<double>>.Get().Log(); Generic<IEnumerable<float>>.Get().Log();
            
                        Generic<int[]>.Get().Log();
            
                        await Methods.AsyncWait().Log();
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
            
                    public static Z GetZ<Z>()
                    {
                        default(IEnumerable<Z>).Log();
                        default(Z[]).Log();
                        return default(Z).Log();
                    }
                }
            
                public static class Methods
                {
                    public static Task<int> AsyncWait() => Task.FromResult(50);
                }
            }
            ");

            var netAssembliesDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var refs = new List<string>();
            foreach (var reference in new[] { "System.Runtime.dll" })
            {
                refs.Add(netAssembliesDirectory + "\\" + reference);
            }

            var compilation = CSharpCompilation.Create("TestAssembly").
                WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication).WithUsings("System", "System.Collections.Generic", "System.Threading.Tasks")).
                AddSyntaxTrees(syntaxTree).
                AddReferences(refs.Where(r => r.Length != 0).Select(r => MetadataReference.CreateFromFile(r)));

            var generator = new ExpressionLoggerGenerator();
            for (int i = 0; i < 100; i++)
            {
                compilation = CSharpCompilation.Create("TestAssembly").
                    WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication).WithUsings("System", "System.Collections.Generic", "System.Threading.Tasks")).
                    AddSyntaxTrees(syntaxTree).
                    AddReferences(refs.Where(r => r.Length != 0).Select(r => MetadataReference.CreateFromFile(r)));

                generator.Execute(d => { }, (h, s) => { }, compilation);
            }
        }
    }
}
