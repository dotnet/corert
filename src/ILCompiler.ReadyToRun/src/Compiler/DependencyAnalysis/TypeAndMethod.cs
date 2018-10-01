using System;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal struct TypeAndMethod : IEquatable<TypeAndMethod>
    {
        public readonly TypeDesc Type;
        public readonly MethodDesc Method;
        public readonly bool IsUnboxingStub;
        public readonly bool IsInstantiatingStub;

        public TypeAndMethod(TypeDesc type, MethodDesc method, bool isUnboxingStub, bool isInstantiatingStub)
        {
            Type = type;
            Method = method;
            IsUnboxingStub = isUnboxingStub;
            IsInstantiatingStub = isInstantiatingStub;
        }

        public bool Equals(TypeAndMethod other)
        {
            return Type == other.Type &&
                   Method == other.Method &&
                   IsUnboxingStub == other.IsUnboxingStub &&
                   IsInstantiatingStub == other.IsInstantiatingStub;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeAndMethod other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Type?.GetHashCode() ?? 0) ^ unchecked(Method.GetHashCode() * 31) ^ (IsUnboxingStub ? -0x80000000 : 0) ^ (IsInstantiatingStub ? 0x40000000 : 0);
        }
    }
}
