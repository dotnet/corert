// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Threading;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;

namespace Internal.TypeSystem.NativeFormat
{
    // This file has implementations of the .MethodImpl.cs logic from its base type.

    public sealed partial class NativeFormatType : MetadataType
    {
        // Virtual function related functionality
        public override MethodImplRecord[] FindMethodsImplWithMatchingDeclName(string declName)
        {
            MetadataReader metadataReader = _metadataUnit.MetadataReader;
            ArrayBuilder<MethodImplRecord> foundRecords = new ArrayBuilder<MethodImplRecord>();

            foreach (var methodImplHandle in _typeDefinition.MethodImpls)
            {
                MethodImpl methodImpl = metadataReader.GetMethodImpl(methodImplHandle);

                Handle methodDeclCheckHandle = methodImpl.MethodDeclaration;
                HandleType methodDeclHandleType = methodDeclCheckHandle.HandleType;

                bool foundRecord = false;

                switch (methodDeclHandleType)
                {
                    case HandleType.QualifiedMethod:
                        QualifiedMethod qualifiedMethod = metadataReader.GetQualifiedMethod(methodDeclCheckHandle.ToQualifiedMethodHandle(metadataReader));
                        Method method = qualifiedMethod.Method.GetMethod(metadataReader);
                        if (method.Name.StringEquals(declName, metadataReader))
                        {
                            foundRecord = true;
                        }
                        break;

                    case HandleType.MemberReference:
                        {
                            MemberReference memberRef = metadataReader.GetMemberReference(methodDeclCheckHandle.ToMemberReferenceHandle(metadataReader));

                            if (memberRef.Name.StringEquals(declName, metadataReader))
                            {
                                foundRecord = true;
                            }
                        }
                        break;
                }

                if (foundRecord)
                {
                    MethodDesc newRecordDecl = (MethodDesc)_metadataUnit.GetObject(methodImpl.MethodDeclaration, null);
                    MethodDesc newRecordBody = (MethodDesc)_metadataUnit.GetObject(methodImpl.MethodBody, null);

                    foundRecords.Add(new MethodImplRecord(newRecordDecl, newRecordBody));
                }
            }

            if (foundRecords.Count != 0)
                return foundRecords.ToArray();

            return null;
        }

        protected override MethodImplRecord[] ComputeVirtualMethodImplsForType()
        {
            ArrayBuilder<MethodImplRecord> records = new ArrayBuilder<MethodImplRecord>();

            MetadataReader metadataReader = _metadataUnit.MetadataReader;

            foreach (var methodImplHandle in _typeDefinition.MethodImpls)
            {
                MethodImpl methodImpl = metadataReader.GetMethodImpl(methodImplHandle);

                Handle methodDeclCheckHandle = methodImpl.MethodDeclaration;
                HandleType methodDeclHandleType = methodDeclCheckHandle.HandleType;

                MetadataType owningType = null;
                switch (methodDeclHandleType)
                {
                    case HandleType.QualifiedMethod:
                        QualifiedMethod qualifiedMethod = metadataReader.GetQualifiedMethod(methodDeclCheckHandle.ToQualifiedMethodHandle(metadataReader));
                        owningType = (MetadataType)_metadataUnit.GetType(qualifiedMethod.EnclosingType);
                        break;

                    case HandleType.MemberReference:
                        Handle owningTypeHandle = metadataReader.GetMemberReference(methodDeclCheckHandle.ToMemberReferenceHandle(metadataReader)).Parent;
                        owningType = _metadataUnit.GetType(owningTypeHandle) as MetadataType;
                        break;

                    default:
                        Debug.Fail("unexpected methodDeclHandleType");
                        break;
                }

                // We want to check that the type is not an interface match before actually getting the MethodDesc. 
                if (!owningType.IsInterface)
                {
                    MethodDesc newRecordDecl = (MethodDesc)_metadataUnit.GetObject(methodImpl.MethodDeclaration, null);
                    MethodDesc newRecordBody = (MethodDesc)_metadataUnit.GetObject(methodImpl.MethodBody, null);

                    records.Add(new MethodImplRecord(newRecordDecl, newRecordBody));
                }
            }

            return records.ToArray();
        }
    }
}
