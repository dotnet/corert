// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;
using System.Diagnostics;

namespace ILCompiler
{
    //
    // The naming format of these names is known to the debugger
    // 
    public class WindowsNodeMangler : NodeMangler
    {
        // Mangled name of boxed version of a type
        public sealed override string MangledBoxedTypeName(TypeDesc type)
        {
            Debug.Assert(type.IsValueType);
            return "Boxed_" + NameMangler.GetMangledTypeName(type);
        }

        public sealed override string EEType(TypeDesc type)
        {
            string mangledJustTypeName;

            if (type.IsValueType)
                mangledJustTypeName = MangledBoxedTypeName(type);
            else
                mangledJustTypeName = NameMangler.GetMangledTypeName(type);
            return mangledJustTypeName + "::`vftable'";
        }

        public sealed override string GCStatics(TypeDesc type)
        {
            return NameMangler.GetMangledTypeName(type) + "::__GCSTATICS";
        }

        public sealed override string NonGCStatics(TypeDesc type)
        {
            return NameMangler.GetMangledTypeName(type) + "::__NONGCSTATICS";
        }

        public sealed override string ThreadStatics(TypeDesc type)
        {
            return NameMangler.CompilationUnitPrefix + NameMangler.GetMangledTypeName(type) + "::__THREADSTATICS";
        }

        public sealed override string TypeGenericDictionary(TypeDesc type)
        {
            return GenericDictionaryNamePrefix + NameMangler.GetMangledTypeName(type);
        }

        public override string MethodGenericDictionary(MethodDesc method)
        {
            return GenericDictionaryNamePrefix + NameMangler.GetMangledMethodName(method);
        }
    }
}
