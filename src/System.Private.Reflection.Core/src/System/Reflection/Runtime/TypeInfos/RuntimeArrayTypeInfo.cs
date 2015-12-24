// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.MethodInfos;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

using TargetException = System.ArgumentException;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // The runtime's implementation of TypeInfo's for array types. 
    //

    internal sealed partial class RuntimeArrayTypeInfo : RuntimeHasElementTypeInfo
    {
        private RuntimeArrayTypeInfo(RuntimeType hasElementType)
            : base(hasElementType)
        {
            Debug.Assert(hasElementType.IsArray);
        }

        public sealed override TypeAttributes Attributes
        {
            get
            {
                return TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Serializable;
            }
        }

        internal sealed override IEnumerable<RuntimeConstructorInfo> SyntheticConstructors
        {
            get
            {
                bool multiDim = this.RuntimeType.InternalIsMultiDimArray;
                int rank = this.RuntimeType.GetArrayRank();

                ReflectionDomain reflectionDomain = this.ReflectionDomain;
                FoundationTypes foundationTypes = reflectionDomain.FoundationTypes;
                RuntimeType arrayType = this.RuntimeType;
                RuntimeType countType = foundationTypes.SystemInt32.AsConfirmedRuntimeType();
                RuntimeType voidType = foundationTypes.SystemVoid.AsConfirmedRuntimeType();

                {
                    RuntimeType[] ctorParametersAndReturn = new RuntimeType[rank + 1];
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

                                RuntimeType vectorType;
                                if (multiDim)
                                {
                                    vectorType = ReflectionCoreNonPortable.GetArrayType(arrayType.InternalRuntimeElementType);
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
                    RuntimeType elementType = this.RuntimeType.InternalRuntimeElementType;
                    while (elementType.IsArray && elementType.GetArrayRank() == 1)
                    {
                        RuntimeType[] ctorParametersAndReturn = new RuntimeType[parameterCount + 1];
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
                    RuntimeType[] ctorParametersAndReturn = new RuntimeType[rank * 2 + 1];
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
                int rank = this.RuntimeType.GetArrayRank();

                ReflectionDomain reflectionDomain = this.ReflectionDomain;
                FoundationTypes foundationTypes = reflectionDomain.FoundationTypes;
                RuntimeType indexType = foundationTypes.SystemInt32.AsConfirmedRuntimeType();
                RuntimeType arrayType = this.RuntimeType;
                RuntimeType elementType = arrayType.InternalRuntimeElementType;
                RuntimeType voidType = foundationTypes.SystemVoid.AsConfirmedRuntimeType();

                {
                    RuntimeType[] getParametersAndReturn = new RuntimeType[rank + 1];
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
                    RuntimeType[] setParametersAndReturn = new RuntimeType[rank + 2];
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
                    RuntimeType[] addressParametersAndReturn = new RuntimeType[rank + 1];
                    addressParametersAndReturn[0] = ReflectionCoreNonPortable.GetByRefType(elementType);
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
                if (this.RuntimeType.InternalIsMultiDimArray)
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
                return new TypeContext(new RuntimeType[] { this.RuntimeType.InternalRuntimeElementType }, null);
            }
        }

        //
        // Arrays don't have a true typedef behind them but for the purpose of reporting base classes and interfaces, we can create a pretender.
        //
        private RuntimeNamedTypeInfo TypeDefInfoProjectionForArrays
        {
            get
            {
                Debug.Assert(this.ReflectionDomain == ReflectionCoreExecution.ExecutionDomain, "User Reflectable Domains not yet implemented.");
                RuntimeTypeHandle projectionTypeHandleForArrays = ReflectionCoreExecution.ExecutionEnvironment.ProjectionTypeForArrays;
                RuntimeType projectionRuntimeTypeForArrays = ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(projectionTypeHandleForArrays);
                return projectionRuntimeTypeForArrays.GetRuntimeTypeInfo<RuntimeNamedTypeInfo>();
            }
        }

        //
        // Helper for jagged array constructors.
        //
        private Array CreateJaggedArray(RuntimeType arrayType, int[] lengths, int index)
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
    }
}
