// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using Internal.Runtime.TypeLoader;

using Internal.TypeSystem;
using Internal.IL;

namespace ILCompiler
{
    public static class TypeSystemContextExtensionsForCompiler
    {
        public static bool HasLazyStaticConstructor(this TypeSystemContext context, TypeDesc type)
        {
            return type.HasStaticConstructor;
        }
        public static bool IsSpecialUnboxingThunkTargetMethod(this TypeSystemContext context, MethodDesc method)
        {
            return false;
        }
    }
}

namespace ILCompiler.DependencyAnalysis
{
    public static class EETypeNode
    {
        public static int GetVTableOffset(int pointerSize)
        {
            // THIS FACTORING IS NOT GOOD. MOVE THIS SOMEWHERE BETTER
            throw new NotImplementedException();
        }
    }

    public static class NonGCStaticsNode
    {
        public static int GetClassConstructorContextStorageSize(TargetDetails target, MetadataType type)
        {
            // THIS FACTORING IS NOT GOOD. MOVE THIS SOMEWHERE BETTER
            throw new NotImplementedException();
        }
    }

    public static class ConstructedEETypeNode
    {
        public static bool CreationAllowed(TypeDesc type)
        {
            // The type handles created by the jit environment don't distinguish between creatable and not.
            // THIS FACTORING IS NOT GOOD. MOVE THIS SOMEWHERE BETTER
            return true;
        }
    }
}
