// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Functionality related to determinstic ordering of types and members
    //
    // Many places within a compiler need a way to generate deterministically ordered lists
    // that may be a result of non-deterministic processes. Multi-threaded compilation is a good
    // example of such source of nondeterminism. Even though the order of the results of a multi-threaded
    // compilation may be non-deterministic, the output of the compiler needs to be deterministic.
    // The compiler achieves that by sorting the results of the compilation.
    //
    // While it's easy to compare types that are in the same category (e.g. named types within an assembly
    // could be compared by their names or tokens), it's difficult to have a scheme where each category would know
    // how to compare itself to other categories (does "array of pointers to uint" sort before a "byref
    // to an object"?). The nature of the type system potentially allows for an unlimited number of TypeDesc
    // descendants.
    // 
    // We solve this problem by only requiring each TypeDesc or MethodDesc descendant to know how
    // to sort itself with respect to other instances of the same type.
    // Comparisons between different categories of types are centralized to a single location that
    // can provide rules to sort them.
    public abstract class TypeSystemComparer : IComparer<TypeDesc>
    {
        public abstract int Compare(TypeDesc x, TypeDesc y);
    }

    public class TypeSystemComparer<T> : TypeSystemComparer
        where T: struct, ITypeSystemClassComparer /* We require a struct for perf reasons - it avoids interface calls */
    {
        private T _classComparer;

        public TypeSystemComparer(T classComparer)
        {
            _classComparer = classComparer;
        }

        public override int Compare(TypeDesc x, TypeDesc y)
        {
            if (x == y)
            {
                return 0;
            }

            // This comparison should be optimized by most .NET codegens to not actually require reflection.
            if (x.GetType() != y.GetType())
            {
                int result = _classComparer.CompareClasses(x, y);
                Debug.Assert(result != 0);
                return result;
            }
            else
            {
                int result = x.CompareToImpl(y, this);
                Debug.Assert(result != 0);
                return result;
            }
        }
    }

    public interface ITypeSystemClassComparer
    {
        int CompareClasses(TypeDesc x, TypeDesc y);
    }
}
