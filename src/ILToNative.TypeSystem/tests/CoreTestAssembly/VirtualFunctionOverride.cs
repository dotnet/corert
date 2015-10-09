using System;

namespace VirtualFunctionOverride
{
    interface IIFaceWithGenericMethod
    {
        void GenMethod<T>();
    }

    class HasMethodInterfaceOverrideOfGenericMethod : IIFaceWithGenericMethod
    {
        void IIFaceWithGenericMethod.GenMethod<T>() { }
    }
}
