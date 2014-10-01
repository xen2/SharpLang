// Source: https://github.com/mono/mono/blob/master/mcs/tools/linker/Mono.Linker.Steps/TypeMapStep.cs
//
// TypeMapStep.cs
//
// Author:
//   Jb Evain (jbevain@novell.com)
//
// (C) 2009 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    /// <summary>
    /// Various useful extension methods for Cecil.
    /// </summary>
    static partial class CecilExtensions
    {
        public static Function TryMatchMethod(Class @class, MethodReference method, bool checkBases = true)
        {
            while (@class != null)
            {
                foreach (var candidate in @class.Functions)
                    if (MethodMatch(candidate.MethodReference, method))
                        return candidate;

                if (!checkBases)
                    return null;

                @class = @class.BaseType;
            }

            return null;
        }

        static bool MethodMatch(MethodReference candidate, MethodReference method)
        {
            var candidateResolved = candidate.Resolve();

            // Check overrides
            if (candidateResolved.HasOverrides)
            {
                foreach (var @override in candidateResolved.Overrides)
                {
                    var resolvedOverride = ResolveGenericsVisitor.Process(candidate, @override);
                    return MemberEqualityComparer.Default.Equals(resolvedOverride, method);
                }
            }

            if (!candidateResolved.IsVirtual)
                return false;

            if (candidate.Name != method.Name)
                return false;

            if (!TypeMatch(ResolveGenericsVisitor.Process(candidate, candidate.ReturnType), ResolveGenericsVisitor.Process(method, method.ReturnType)))
                return false;

            if (candidate.Parameters.Count != method.Parameters.Count)
                return false;

            for (int i = 0; i < candidate.Parameters.Count; i++)
                if (!TypeMatch(ResolveGenericsVisitor.Process(candidate, candidate.Parameters[i].ParameterType), ResolveGenericsVisitor.Process(method, method.Parameters[i].ParameterType)))
                    return false;

            return true;
        }

        static bool TypeMatch(IModifierType a, IModifierType b)
        {
            if (!TypeMatch(a.ModifierType, b.ModifierType))
                return false;

            return TypeMatch(a.ElementType, b.ElementType);
        }

        static bool TypeMatch(TypeSpecification a, TypeSpecification b)
        {
            if (a is GenericInstanceType)
                return TypeMatch((GenericInstanceType)a, (GenericInstanceType)b);

            if (a is IModifierType)
                return TypeMatch((IModifierType)a, (IModifierType)b);

            return TypeMatch(a.ElementType, b.ElementType);
        }

        static bool TypeMatch(GenericInstanceType a, GenericInstanceType b)
        {
            if (!TypeMatch(a.ElementType, b.ElementType))
                return false;

            if (a.GenericArguments.Count != b.GenericArguments.Count)
                return false;

            if (a.GenericArguments.Count == 0)
                return true;

            for (int i = 0; i < a.GenericArguments.Count; i++)
                if (!TypeMatch(a.GenericArguments[i], b.GenericArguments[i]))
                    return false;

            return true;
        }

        static bool TypeMatch(TypeReference a, TypeReference b)
        {
            if (a is GenericParameter)
                return true;

            if (a is TypeSpecification || b is TypeSpecification)
            {
                if (a.GetType() != b.GetType())
                    return false;

                return TypeMatch((TypeSpecification)a, (TypeSpecification)b);
            }

            return a.FullName == b.FullName;
        }
    }
}