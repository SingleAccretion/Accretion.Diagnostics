﻿using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class SameLineLogMethodUsages
    {
        public SameLineLogMethodUsages(LogMethodUsage firstUsage)
        {
            Usages = new List<LogMethodUsage> { firstUsage };
            FilePath = firstUsage.FilePath;
            LineNumber = firstUsage.LineNumber;
        }

        public string FilePath { get; private set; }
        public int LineNumber { get; private set; }
        public List<LogMethodUsage> Usages { get; }

        public void Add(LogMethodUsage usage)
        {
            Debug.Assert(usage.FilePath == FilePath && usage.LineNumber == LineNumber);
            Usages.Add(usage);
        }

        public void Rebase(LogMethodUsage usage)
        {
            Debug.Assert(usage.FilePath != FilePath || usage.LineNumber != LineNumber);
            Usages.Clear();
            FilePath = usage.FilePath;
            LineNumber = usage.LineNumber;
            Usages.Add(usage);
        }

        public bool AreIndistinguishableFrom(LogMethodUsage usage) =>
            AreOnTheSameLineAs(usage) && (Usages.Any(x => SymbolEqualityComparer.Default.Equals(x.Type, usage.Type)) || IsOpenGeneric(usage.Type));

        public bool AreOnTheSameLineAs(LogMethodUsage usage) => usage.FilePath == FilePath && usage.LineNumber == LineNumber;

        public override string ToString() => $"{Path.GetFileName(FilePath)}:{LineNumber} - {Usages.Count}";

        private static bool IsOpenGeneric(ITypeSymbol type) => type switch
        {
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(x => IsOpenGeneric(x)),
            IArrayTypeSymbol arrayType => IsOpenGeneric(arrayType.ElementType),
            IPointerTypeSymbol pointerType => IsOpenGeneric(pointerType.PointedAtType),
            ITypeParameterSymbol => true,
            _ => throw new NotImplementedException($"The case of the a type being {type} is not covered - it should be impossible.")
        };
    }
}