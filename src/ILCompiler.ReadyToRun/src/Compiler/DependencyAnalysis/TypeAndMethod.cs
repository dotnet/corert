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
        public readonly TypeDesc Type;
        public readonly MethodDesc Method;
        public readonly ModuleToken MethodToken;
        public readonly bool IsUnboxingStub;
        public readonly bool IsInstantiatingStub;

        public TypeAndMethod(TypeDesc type, MethodDesc method, ModuleToken methodToken, bool isUnboxingStub, bool isInstantiatingStub)
        {
            Type = type;
            Method = method;
            MethodToken = methodToken;
            IsUnboxingStub = isUnboxingStub;
            IsInstantiatingStub = isInstantiatingStub;
        }

        public bool Equals(TypeAndMethod other)
        {
            return Type == other.Type &&
                   Method == other.Method &&
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
                unchecked(Method.GetHashCode() * 31  + MethodToken.GetHashCode() * 97) ^ 
                (IsUnboxingStub ? -0x80000000 : 0) ^ 
                (IsInstantiatingStub ? 0x40000000 : 0);
        }
    }
}
