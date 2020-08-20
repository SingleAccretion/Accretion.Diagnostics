using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class LogUsagesCollector : ISyntaxReceiver
    {
        private readonly List<InvocationExpressionSyntax> _invocations = new List<InvocationExpressionSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            static bool IsLogName(SimpleNameSyntax nameSyntax) => nameSyntax.Identifier.ValueText == LogMethodName;

            if (syntaxNode is InvocationExpressionSyntax invocation)
            {
                //This takes care of both static and non-static accesses to the log method
                //In the first case, Expression is simply the ExpressionLogger identifier
                //In the second, it is the "this" expression
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess && IsLogName(memberAccess.Name) ||
                    invocation.Expression is MemberBindingExpressionSyntax memberBinding && IsLogName(memberBinding.Name))
                {
                    _invocations.Add(invocation);
                }
            }
        }

        public IEnumerable<LogUsage> CollectUsages(Compilation compilation, IMethodSymbol logMethod, Action<Diagnostic> reportDiagnostic)
        {
            (string FilePath, int LineNumber) currentPosition = default;
            var clusteredUsages = new List<LogUsage>();
            foreach (var usage in CollectAllUsages(compilation, logMethod))
            {
                var usagePosition = (usage.FilePath, usage.LineNumber);
                
                if (usagePosition == currentPosition)
                {
                    if (!clusteredUsages.Any(x => SymbolEqualityComparer.Default.Equals(x.Type, usage.Type) || IsOpenGeneric(usage.Type)))
                    {
                        clusteredUsages.Add(usage);
                    }
                    else
                    {
                        reportDiagnostic(Diagnostics.DuplicateLogUsage(usage.LogLocation));
                    }
                }
                else
                {
                    foreach (var collectedUsage in clusteredUsages)
                    {
                        yield return collectedUsage;
                    }
                    currentPosition = usagePosition;
                    clusteredUsages.Clear();
                    clusteredUsages.Add(usage);
                }                
            }
        }

        private IEnumerable<LogUsage> CollectAllUsages(Compilation compilation, IMethodSymbol logMethodDefinition)
        {
            foreach (var invocation in _invocations)
            {
                var semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                var symbol = symbolInfo.Symbol;

                if (symbol is IMethodSymbol method)
                {
                    var definition = method.ReducedFrom ?? method.OriginalDefinition;

                    if (SymbolEqualityComparer.Default.Equals(definition, logMethodDefinition))
                    {
                        yield return ExtractLogUsage(invocation, method.TypeArguments[0]);
                    }
                }
            }
        }

        private static LogUsage ExtractLogUsage(InvocationExpressionSyntax invocation, ITypeSymbol type)
        {
            //We need a separate line number because [CallerLineNumber] reports the line number of the argument
            Location logLocation;
            CSharpSyntaxNode expression;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var args = invocation.ArgumentList.Arguments;
                expression = args.Count == 0 ? memberAccess.Expression : args[0];
                logLocation = memberAccess.Name.GetLocation();
            }
            else if (invocation.Parent is ConditionalAccessExpressionSyntax conditionalAccess &&
                     conditionalAccess.WhenNotNull == invocation &&
                     invocation.Expression is MemberBindingExpressionSyntax bindingAccess)
            {
                expression = conditionalAccess.Expression;
                logLocation = bindingAccess.Name.GetLocation();
            }
            else
            {
                expression = SyntaxFactory.IdentifierName("This invocation form is not supported by the ExpressionLogger. Please log an issue on github.com/SingleAccretion/Accretion.Diagnostics.");
                logLocation = LogUsage.DummyUsage.LogLocation;
            }

            var lineNumber = invocation.ArgumentList.OpenParenToken.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            return new LogUsage(expression.ToString(), lineNumber, logLocation, expression.GetLocation(), type);
        }

        private static bool IsOpenGeneric(ITypeSymbol type) => type switch
        {
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(x => IsOpenGeneric(x)),
            IArrayTypeSymbol arrayType => IsOpenGeneric(arrayType.ElementType),
            IPointerTypeSymbol pointerType => IsOpenGeneric(pointerType.PointedAtType),
            ITypeParameterSymbol => true,
            _ => throw new NotImplementedException($"The case of the a type being {type} is not covered - it should be impossible?.")
        };
    }
}
