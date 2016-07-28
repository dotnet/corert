// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.MethodInfos;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Metadata.NativeFormat;

using TargetException = System.ArgumentException;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // The runtime's implementation of TypeInfo's for array types. 
    //
    internal sealed partial class RuntimeArrayTypeInfo : RuntimeHasElementTypeInfo
    {
        private RuntimeArrayTypeInfo(UnificationKey key, bool multiDim, int rank)
            : base(key)
        {
            Debug.Assert(multiDim || rank == 1);
            _multiDim = multiDim;
            _rank = rank;
        }

        public sealed override TypeAttributes Attributes
        {
            get
            {
                return TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Serializable;
            }
        }

        public sealed override int GetArrayRank()
        {
            return _rank;
        }

        internal sealed override bool InternalIsMultiDimArray
        {
            get
            {
                return _multiDim;
            }
        }

        internal sealed override IEnumerable<RuntimeConstructorInfo> SyntheticConstructors
        {
            get
            {
                bool multiDim = this.InternalIsMultiDimArray;
                int rank = this.GetArrayRank();

                FoundationTypes foundationTypes = ReflectionCoreExecution.ExecutionDomain.FoundationTypes;
                RuntimeTypeInfo arrayType = this;
                RuntimeTypeInfo countType = foundationTypes.SystemInt32.CastToRuntimeTypeInfo();
                RuntimeTypeInfo voidType = foundationTypes.SystemVoid.CastToRuntimeTypeInfo();

                {
                    RuntimeTypeInfo[] ctorParametersAndReturn = new RuntimeTypeInfo[rank + 1];
                    ctorParametersAndReturn[0] = voidType;
                    for (int i = 0; i < rank; i++)
                        ctorParametersAndReturn[i + 1] = countType;
                    yield return RuntimeSyntheticConstructorInfo.GetRuntimeSyntheticConstructorInfo(
                        SyntheticMethodId.ArrayCtor,
                        arrayType,
                        ctorParametersAndReturn,
                        InvokerOptions.AllowNullThis | InvokerOptions.DontWrapException,
                        delegate (Object _this, Object[] args)
                        {
                            if (rank == 1)
                            {
                                // Legacy: This seems really wrong in the rank1-multidim case (as it's a case of a synthetic constructor that's declared on T[*] returning an instance of T[])
                                // This is how the desktop behaves, however.

                                int count = (int)(args[0]);

                                RuntimeTypeInfo vectorType;
                                if (multiDim)
                                {
                                    vectorType = arrayType.InternalRuntimeElementType.GetArrayType();
                                }
                                else
                                {
                                    vectorType = arrayType;
                                }

                                return ReflectionCoreExecution.ExecutionEnvironment.NewArray(vectorType.TypeHandle, count);
                            }
                            else
                            {
                                int[] lengths = new int[rank];
                                for (int i = 0; i < rank; i++)
                                {
                                    lengths[i] = (int)(args[i]);
                                }
                                return ReflectionCoreExecution.ExecutionEnvironment.NewMultiDimArray(arrayType.TypeHandle, lengths, null);
                            }
                        }
                    );
                }

                if (!multiDim)
                {
                    //
                    // Jagged arrays also expose constructors that take multiple indices and construct a jagged matrix. For example,
                    //
                    //   String[][][][]
                    //
                    // also exposes:
                    //
                    //   .ctor(int32, int32)
                    //   .ctor(int32, int32, int32)
                    //   .ctor(int32, int32, int32, int32)
                    //

                    int parameterCount = 2;
                    RuntimeTypeInfo elementType = this.InternalRuntimeElementType;
                    while (elementType.IsArray && elementType.GetArrayRank() == 1)
                    {
                        RuntimeTypeInfo[] ctorParametersAndReturn = new RuntimeTypeInfo[parameterCount + 1];
                        ctorParametersAndReturn[0] = voidType;
                        for (int i = 0; i < parameterCount; i++)
                            ctorParametersAndReturn[i + 1] = countType;
                        yield return RuntimeSyntheticConstructorInfo.GetRuntimeSyntheticConstructorInfo(
                            SyntheticMethodId.ArrayCtorJagged + parameterCount,
                            arrayType,
                            ctorParametersAndReturn,
                            InvokerOptions.AllowNullThis | InvokerOptions.DontWrapException,
                            delegate (Object _this, Object[] args)
                            {
                                int[] lengths = new int[args.Length];
                                for (int i = 0; i < args.Length; i++)
                                {
                                    lengths[i] = (int)(args[i]);
                                }
                                Array jaggedArray = CreateJaggedArray(arrayType, lengths, 0);
                                return jaggedArray;
                            }
                        );
                        parameterCount++;
                        elementType = elementType.InternalRuntimeElementType;
                    }
                }

                if (multiDim)
                {
                    RuntimeTypeInfo[] ctorParametersAndReturn = new RuntimeTypeInfo[rank * 2 + 1];
                    ctorParametersAndReturn[0] = voidType;
                    for (int i = 0; i < rank * 2; i++)
                        ctorParametersAndReturn[i + 1] = countType;
                    yield return RuntimeSyntheticConstructorInfo.GetRuntimeSyntheticConstructorInfo(
                        SyntheticMethodId.ArrayMultiDimCtor,
                        arrayType,
                        ctorParametersAndReturn,
                        InvokerOptions.AllowNullThis | InvokerOptions.DontWrapException,
                        delegate (Object _this, Object[] args)
                        {
                            int[] lengths = new int[rank];
                            int[] lowerBounds = new int[rank];
                            for (int i = 0; i < rank; i++)
                            {
                                lowerBounds[i] = (int)(args[i * 2]);
                                lengths[i] = (int)(args[i * 2 + 1]);
                            }
                            return ReflectionCoreExecution.ExecutionEnvironment.NewMultiDimArray(arrayType.TypeHandle, lengths, lowerBounds);
                        }
                    );
                }
            }
        }

        internal sealed override IEnumerable<RuntimeMethodInfo> SyntheticMethods
        {
            get
            {
                int rank = this.GetArrayRank();

                FoundationTypes foundationTypes = ReflectionCoreExecution.ExecutionDomain.FoundationTypes;
                RuntimeTypeInfo indexType = foundationTypes.SystemInt32.CastToRuntimeTypeInfo();
                RuntimeTypeInfo arrayType = this;
                RuntimeTypeInfo elementType = arrayType.InternalRuntimeElementType;
                RuntimeTypeInfo voidType = foundationTypes.SystemVoid.CastToRuntimeTypeInfo();

                {
                    RuntimeTypeInfo[] getParametersAndReturn = new RuntimeTypeInfo[rank + 1];
                    getParametersAndReturn[0] = elementType;
                    for (int i = 0; i < rank; i++)
                        getParametersAndReturn[i + 1] = indexType;
                    yield return RuntimeSyntheticMethodInfo.GetRuntimeSyntheticMethodInfo(
                        SyntheticMethodId.ArrayGet,
                        "Get",
                        arrayType,
                        getParametersAndReturn,
                        InvokerOptions.None,
                        delegate (Object _this, Object[] args)
                        {
                            Array array = (Array)_this;
                            int[] indices = new int[rank];
                            for (int i = 0; i < rank; i++)
                                indices[i] = (int)(args[i]);
                            return array.GetValue(indices);
                        }
                    );
                }

                {
                    RuntimeTypeInfo[] setParametersAndReturn = new RuntimeTypeInfo[rank + 2];
                    setParametersAndReturn[0] = voidType;
                    for (int i = 0; i < rank; i++)
                        setParametersAndReturn[i + 1] = indexType;
                    setParametersAndReturn[rank + 1] = elementType;
                    yield return RuntimeSyntheticMethodInfo.GetRuntimeSyntheticMethodInfo(
                        SyntheticMethodId.ArraySet,
                        "Set",
                        arrayType,
                        setParametersAndReturn,
                        InvokerOptions.None,
                        delegate (Object _this, Object[] args)
                        {
                            Array array = (Array)_this;
                            int[] indices = new int[rank];
                            for (int i = 0; i < rank; i++)
                                indices[i] = (int)(args[i]);
                            Object value = args[rank];
                            array.SetValue(value, indices);
                            return null;
                        }
                    );
                }

                {
                    RuntimeTypeInfo[] addressParametersAndReturn = new RuntimeTypeInfo[rank + 1];
                    addressParametersAndReturn[0] = elementType.GetByRefType();
                    for (int i = 0; i < rank; i++)
                        addressParametersAndReturn[i + 1] = indexType;
                    yield return RuntimeSyntheticMethodInfo.GetRuntimeSyntheticMethodInfo(
                        SyntheticMethodId.ArrayAddress,
                        "Address",
                        arrayType,
                        addressParametersAndReturn,
                        InvokerOptions.None,
                        delegate (Object _this, Object[] args)
                        {
                            throw new NotSupportedException();
                        }
                    );
                }
            }
        }

        //
        // Returns the base type as a typeDef, Ref, or Spec. Default behavior is to QTypeDefRefOrSpec.Null, which causes BaseType to return null.
        //
        internal sealed override QTypeDefRefOrSpec TypeRefDefOrSpecForBaseType
        {
            get
            {
                return TypeDefInfoProjectionForArrays.TypeRefDefOrSpecForBaseType;
            }
        }

        //
        // Returns the *directly implemented* interfaces as typedefs, specs or refs. ImplementedInterfaces will take care of the transitive closure and
        // insertion of the TypeContext.
        //
        internal sealed override QTypeDefRefOrSpec[] TypeRefDefOrSpecsForDirectlyImplementedInterfaces
        {
            get
            {
                if (this.InternalIsMultiDimArray)
                    return Array.Empty<QTypeDefRefOrSpec>();
                else
                    return TypeDefInfoProjectionForArrays.TypeRefDefOrSpecsForDirectlyImplementedInterfaces;
            }
        }

        //
        // Returns the generic parameter substitutions to use when enumerating declared members, base class and implemented interfaces.
        //
        internal sealed override TypeContext TypeContext
        {
            get
            {
                return new TypeContext(new RuntimeTypeInfo[] { this.InternalRuntimeElementType }, null);
            }
        }

        protected sealed override bool IsArrayImpl()
        {
            return true;
        }

        protected sealed override string Suffix
        {
            get
            {
                if (!_multiDim)
                    return "[]";
                else if (_rank == 1)
                    return "[*]";
                else
                    return "[" + new string(',', _rank - 1) + "]";
            }
        }

        //
        // Arrays don't have a true typedef behind them but for the purpose of reporting base classes and interfaces, we can create a pretender.
        //
        private RuntimeTypeInfo TypeDefInfoProjectionForArrays
        {
            get
            {
                RuntimeTypeHandle projectionTypeHandleForArrays = ReflectionCoreExecution.ExecutionEnvironment.ProjectionTypeForArrays;
                RuntimeTypeInfo projectionRuntimeTypeForArrays = projectionTypeHandleForArrays.GetTypeForRuntimeTypeHandle();
                return projectionRuntimeTypeForArrays;
            }
        }

        //
        // Helper for jagged array constructors.
        //
        private Array CreateJaggedArray(RuntimeTypeInfo arrayType, int[] lengths, int index)
        {
            int length = lengths[index];
            Array jaggedArray = ReflectionCoreExecution.ExecutionEnvironment.NewArray(arrayType.TypeHandle, length);
            if (index != lengths.Length - 1)
            {
                for (int i = 0; i < length; i++)
                {
                    Array subArray = CreateJaggedArray(arrayType.InternalRuntimeElementType, lengths, index + 1);
                    jaggedArray.SetValue(subArray, i);
                }
            }
            return jaggedArray;
        }

        private readonly int _rank;
        private readonly bool _multiDim;
    }
}
