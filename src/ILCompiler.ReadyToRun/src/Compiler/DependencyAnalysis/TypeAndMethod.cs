// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis.ReadyToRun;

namespace ILCompiler.DependencyAnalysis
{
    internal struct TypeAndMethod : IEquatable<TypeAndMethod>
    {
        public readonly MethodDesc TargetMethod;
        public readonly TypeDesc Type;
        public readonly MethodDesc OriginalMethod;
        public readonly ModuleToken MethodToken;
        public readonly bool IsUnboxingStub;
        public readonly bool IsInstantiatingStub;

        public TypeAndMethod(MethodDesc targetMethod, TypeDesc type, MethodDesc originalMethod, ModuleToken methodToken, bool isUnboxingStub, bool isInstantiatingStub)
        {
            TargetMethod = targetMethod;
            Type = type;
            OriginalMethod = originalMethod;
            MethodToken = methodToken;
            IsUnboxingStub = isUnboxingStub;
            IsInstantiatingStub = isInstantiatingStub;
        }

        public bool Equals(TypeAndMethod other)
        {
            return TargetMethod == other.TargetMethod && 
                   Type == other.Type &&
                   OriginalMethod == other.OriginalMethod &&
                   MethodToken.Equals(other.MethodToken) &&
                   IsUnboxingStub == other.IsUnboxingStub &&
                   IsInstantiatingStub == other.IsInstantiatingStub;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeAndMethod other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Type?.GetHashCode() ?? 0) ^ 
                unchecked(TargetMethod.GetHashCode() * 31 
                    + (OriginalMethod?.GetHashCode() * 199 ?? 0)
                    + MethodToken.GetHashCode() * 97) ^ 
                (IsUnboxingStub ? -0x80000000 : 0) ^ 
                (IsInstantiatingStub ? 0x40000000 : 0);
        }
    }
}
