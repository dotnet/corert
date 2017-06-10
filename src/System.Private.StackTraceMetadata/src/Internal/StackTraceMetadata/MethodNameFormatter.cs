// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Internal.Metadata.NativeFormat;

namespace Internal.StackTraceMetadata
{
    class MethodNameFormatter
    {
        /// <summary>
        /// Metadata reader used for the purpose of method name formatting.
        /// </summary>
        private readonly MetadataReader _metadataReader;

        /// <summary>
        /// String builder used to construct formatted method name.
        /// </summary>
        private readonly StringBuilder _outputBuilder;

        /// <summary>
        /// Initialize the reader used for method name formatting.
        /// </summary>
        private MethodNameFormatter(MetadataReader metadataReader)
        {
            _metadataReader = metadataReader;
            _outputBuilder = new StringBuilder();
        }

        public static string FormatMethodName(MetadataReader metadataReader, Handle methodHandle)
        {
            MethodNameFormatter formatter = new MethodNameFormatter(metadataReader);
            formatter.EmitMethodName(methodHandle);
            return formatter._outputBuilder.ToString();
        }

        /// <summary>
        /// Emit a given method signature to a specified string builder.
        /// </summary>
        /// <param name="methodToken">Method reference or instantiation token</param>
        private void EmitMethodName(Handle methodHandle)
        {
            switch (methodHandle.HandleType)
            {
                case HandleType.MemberReference:
                    EmitMethodReferenceName(methodHandle.ToMemberReferenceHandle(_metadataReader));
                    break;
    
                case HandleType.MethodInstantiation:
                    EmitMethodInstantiationName(methodHandle.ToMethodInstantiationHandle(_metadataReader));
                    break;
    
                default:
                    Debug.Assert(false);
                    _outputBuilder.Append("???");
                    break;
            }
        }
    
        /// <summary>
        /// Emit method reference to the output string builder.
        /// </summary>
        /// <param name="memberRefHandle">Member reference handle</param>
        private void EmitMethodReferenceName(MemberReferenceHandle memberRefHandle)
        {
            MemberReference methodRef = _metadataReader.GetMemberReference(memberRefHandle);
            MethodSignature methodSignature;
            EmitReturnTypeContainingTypeAndMethodName(methodRef, out methodSignature);
            EmitMethodParameters(methodSignature);
        }
    
        /// <summary>
        /// Emit generic method instantiation to the output string builder.
        /// </summary>
        /// <param name="methodInstHandle">Method instantiation handle</param>
        private void EmitMethodInstantiationName(MethodInstantiationHandle methodInstHandle)
        {
            MethodInstantiation methodInst = _metadataReader.GetMethodInstantiation(methodInstHandle);
            MemberReferenceHandle methodRefHandle = methodInst.Method.ToMemberReferenceHandle(_metadataReader);
            MemberReference methodRef = methodRefHandle.GetMemberReference(_metadataReader);
            MethodSignature methodSignature;
            EmitReturnTypeContainingTypeAndMethodName(methodRef, out methodSignature);
            EmitGenericArguments(methodInst.GenericTypeArguments);
            EmitMethodParameters(methodSignature);
        }
    
        /// <summary>
        /// Emit containing type and method name and extract the method signature from a method reference.
        /// </summary>
        /// <param name="methodRef">Method reference to format</param>
        /// <param name="methodSignature">Output method signature</param>
        private void EmitReturnTypeContainingTypeAndMethodName(MemberReference methodRef, out MethodSignature methodSignature)
        {
            methodSignature = _metadataReader.GetMethodSignature(methodRef.Signature.ToMethodSignatureHandle(_metadataReader));
            EmitTypeName(methodSignature.ReturnType, namespaceQualified: false);
            _outputBuilder.Append(" ");
            EmitTypeName(methodRef.Parent, namespaceQualified: true);
            _outputBuilder.Append(".");
            EmitString(methodRef.Name);
        }
    
        /// <summary>
        /// Emit parenthesized method argument type list.
        /// </summary>
        /// <param name="methodSignature">Method signature to use for parameter formatting</param>
        private void EmitMethodParameters(MethodSignature methodSignature)
        {
            _outputBuilder.Append("(");
            EmitTypeVector(methodSignature.Parameters);
            _outputBuilder.Append(")");
        }
    
        /// <summary>
        /// Emit comma-separated list of type names into the output string builder.
        /// </summary>
        /// <param name="typeVector">Enumeration of type handles to output</param>
        private void EmitTypeVector(IEnumerable<Handle> typeVector)
        {
            bool first = true;
            foreach (Handle handle in typeVector)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    _outputBuilder.Append(", ");
                }
                EmitTypeName(handle, namespaceQualified: false);
            }
        }
    
        /// <summary>
        /// Emit the name of a given type to the output string builder.
        /// </summary>
        /// <param name="typeHandle">Type handle to format</param>
        /// <param name="namespaceQualified">When set to true, include namespace information</param>
        private void EmitTypeName(Handle typeHandle, bool namespaceQualified)
        {
            switch (typeHandle.HandleType)
            {
                case HandleType.TypeReference:
                    EmitTypeReferenceName(typeHandle.ToTypeReferenceHandle(_metadataReader), namespaceQualified);
                    break;
    
                case HandleType.TypeSpecification:
                    EmitTypeSpecificationName(typeHandle.ToTypeSpecificationHandle(_metadataReader), namespaceQualified);
                    break;
    
                case HandleType.TypeInstantiationSignature:
                    EmitTypeInstantiationName(typeHandle.ToTypeInstantiationSignatureHandle(_metadataReader), namespaceQualified);
                    break;
    
                case HandleType.SZArraySignature:
                    EmitSZArrayTypeName(typeHandle.ToSZArraySignatureHandle(_metadataReader), namespaceQualified);
                    break;
    
                case HandleType.ArraySignature:
                    EmitArrayTypeName(typeHandle.ToArraySignatureHandle(_metadataReader), namespaceQualified);
                    break;
    
                case HandleType.PointerSignature:
                    EmitPointerTypeName(typeHandle.ToPointerSignatureHandle(_metadataReader));
                    break;
    
                case HandleType.ByReferenceSignature:
                    EmitByRefTypeName(typeHandle.ToByReferenceSignatureHandle(_metadataReader));
                    break;
    
                default:
                    Debug.Assert(false);
                    _outputBuilder.Append("???");
                    break;
            }
        }

        /// <summary>
        /// Emit namespace reference.
        /// </summary>
        /// <param name="namespaceRefHandle">Namespace reference handle</param>
        private void EmitNamespaceReferenceName(NamespaceReferenceHandle namespaceRefHandle)
        {
            NamespaceReference namespaceRef = _metadataReader.GetNamespaceReference(namespaceRefHandle);
            if (!namespaceRef.ParentScopeOrNamespace.IsNull(_metadataReader) &&
                namespaceRef.ParentScopeOrNamespace.HandleType == HandleType.NamespaceReference)
            {
                EmitNamespaceReferenceName(namespaceRef.ParentScopeOrNamespace.ToNamespaceReferenceHandle(_metadataReader));
                _outputBuilder.Append('.');
            }
            EmitString(namespaceRef.Name);
        }
    
        /// <summary>
        /// Emit type reference.
        /// </summary>
        /// <param name="typeRefHandle">Type reference handle</param>
        /// <param name="namespaceQualified">When set to true, include namespace information</param>
        private void EmitTypeReferenceName(TypeReferenceHandle typeRefHandle, bool namespaceQualified)
        {
            TypeReference typeRef = _metadataReader.GetTypeReference(typeRefHandle);
            if (!typeRef.ParentNamespaceOrType.IsNull(_metadataReader))
            {
                if (typeRef.ParentNamespaceOrType.HandleType != HandleType.NamespaceReference)
                {
                    // Nested type
                    EmitTypeName(typeRef.ParentNamespaceOrType, namespaceQualified);
                    _outputBuilder.Append('+');
                }
                else if (namespaceQualified)
                {
                    EmitNamespaceReferenceName(typeRef.ParentNamespaceOrType.ToNamespaceReferenceHandle(_metadataReader));
                    _outputBuilder.Append('.');
                }
            }
            EmitString(typeRef.TypeName);
        }
    
        /// <summary>
        /// Emit an arbitrary type specification.
        /// </summary>
        /// <param name="typeSpecHandle">Type specification handle</param>
        /// <param name="namespaceQualified">When set to true, include namespace information</param>
        private void EmitTypeSpecificationName(TypeSpecificationHandle typeSpecHandle, bool namespaceQualified)
        {
            TypeSpecification typeSpec = _metadataReader.GetTypeSpecification(typeSpecHandle);
            EmitTypeName(typeSpec.Signature, namespaceQualified);
        }
    
        /// <summary>
        /// Emit generic instantiation type.
        /// </summary>
        /// <param name="typeInstHandle">Instantiated type specification signature handle</param>
        /// <param name="namespaceQualified">When set to true, include namespace information</param>
        private void EmitTypeInstantiationName(TypeInstantiationSignatureHandle typeInstHandle, bool namespaceQualified)
        {
            TypeInstantiationSignature typeInst = _metadataReader.GetTypeInstantiationSignature(typeInstHandle);
            EmitTypeName(typeInst.GenericType, namespaceQualified);
            EmitGenericArguments(typeInst.GenericTypeArguments);
        }
    
        /// <summary>
        /// Emit SZArray (single-dimensional array with zero lower bound) type.
        /// </summary>
        /// <param name="szArraySigHandle">SZArray type specification signature handle</param>
        /// <param name="namespaceQualified">When set to true, include namespace information</param>
        private void EmitSZArrayTypeName(SZArraySignatureHandle szArraySigHandle, bool namespaceQualified)
        {
            SZArraySignature szArraySig = _metadataReader.GetSZArraySignature(szArraySigHandle);
            EmitTypeName(szArraySig.ElementType, namespaceQualified);
            _outputBuilder.Append("[]");
        }
    
        /// <summary>
        /// Emit multi-dimensional array type.
        /// </summary>
        /// <param name="arraySigHandle">Multi-dimensional array type specification signature handle</param>
        /// <param name="namespaceQualified">When set to true, include namespace information</param>
        private void EmitArrayTypeName(ArraySignatureHandle arraySigHandle, bool namespaceQualified)
        {
            ArraySignature arraySig = _metadataReader.GetArraySignature(arraySigHandle);
            EmitTypeName(arraySig.ElementType, namespaceQualified);
            _outputBuilder.Append('[');
            if (arraySig.Rank > 1)
            {
                _outputBuilder.Append(',', arraySig.Rank - 1);
            }
            else
            {
                _outputBuilder.Append('*');
            }
            _outputBuilder.Append(']');
        }
    
        /// <summary>
        /// Emit pointer type.
        /// </summary>
        /// <param name="pointerSigHandle">Pointer type specification signature handle</param>
        private void EmitPointerTypeName(PointerSignatureHandle pointerSigHandle)
        {
            PointerSignature pointerSig = _metadataReader.GetPointerSignature(pointerSigHandle);
            EmitTypeName(pointerSig.Type, namespaceQualified: false);
            _outputBuilder.Append('*');
        }
    
        /// <summary>
        /// Emit by-reference type.
        /// </summary>
        /// <param name="byRefSigHandle">ByReference type specification signature handle</param>
        private void EmitByRefTypeName(ByReferenceSignatureHandle byRefSigHandle)
        {
            ByReferenceSignature byRefSig = _metadataReader.GetByReferenceSignature(byRefSigHandle);
            EmitTypeName(byRefSig.Type, namespaceQualified: false);
            _outputBuilder.Append('&');
        }
    
        /// <summary>
        /// Emit angle-bracketed list of type / method generic arguments.
        /// </summary>
        /// <param name="genericArguments">Collection of generic argument type handles</param>
        private void EmitGenericArguments(HandleCollection genericArguments)
        {
            _outputBuilder.Append('[');
            EmitTypeVector(genericArguments);
            _outputBuilder.Append(']');
        }
    
        /// <summary>
        /// Emit a string (represented by a serialized ConstantStringValue) to the output string builder.
        /// </summary>
        /// <param name="stringToken">Constant string value token (offset within stack trace native metadata)</param>
        private void EmitString(ConstantStringValueHandle stringHandle)
        {
            _outputBuilder.Append(_metadataReader.GetConstantStringValue(stringHandle).Value);
        }
    }
}
