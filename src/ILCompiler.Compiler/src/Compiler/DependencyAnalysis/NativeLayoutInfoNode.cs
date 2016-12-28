﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a map between reflection metadata and generated method bodies.
    /// </summary>
    internal sealed class NativeLayoutInfoNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        private NativeWriter _writer;
        private MemoryStream _writerStream;

        private Section _signaturesSection;
        private Section _ldTokenInfoSection;

        public NativeLayoutInfoNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__native_info_layout_End", true);
            _externalReferences = externalReferences;
            _writer = new NativeWriter();
            _signaturesSection = _writer.NewSection();
            _ldTokenInfoSection = _writer.NewSection();
        }

        public ISymbolNode EndSymbol
        {
            get
            {
                return _endSymbol;
            }
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__native_info_layout");
        }
        public int Offset => 0;
        public override bool IsShareable => false;

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName() => this.GetMangledName();

        public void SaveNativeLayoutInfoWriter()
        {
            if (_writerStream != null)
            {
#if DEBUG
                // Sanity check... We should not write new items to the native layout after 
                // we've already saved it.

                MemoryStream debugStream = new MemoryStream();
                _writer.Save(debugStream);
                byte[] debugStreamBytes = debugStream.ToArray();
                byte[] nativeLayoutInfoBytes = _writerStream.ToArray();
                Debug.Assert(debugStreamBytes.Length == nativeLayoutInfoBytes.Length);
                for (int i = 0; i < debugStreamBytes.Length; i++)
                    Debug.Assert(debugStreamBytes[i] == nativeLayoutInfoBytes[i]);
#endif
                return;
            }

            _writerStream = new MemoryStream();
            _writer.Save(_writerStream);
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            SaveNativeLayoutInfoWriter();

            byte[] nativeLayoutInfoBytes = _writerStream.ToArray();

            _endSymbol.SetSymbolOffset(nativeLayoutInfoBytes.Length);

            return new ObjectData(nativeLayoutInfoBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }

        #region Vertex Building Functions
        public Vertex GetNativeLayoutInfoSignatureForLdToken<T>(NodeFactory factory, T methodDescOrFieldDesc)
        {
            // TODO: option to return the uninstantiated signature info. Current implementation will only encode 
            // the instantiated signature (i.e. with containing type, return type, args, etc... encoded as external types)

            MethodDesc method = methodDescOrFieldDesc as MethodDesc;
            FieldDesc field = methodDescOrFieldDesc as FieldDesc;

            Vertex signature = null;

            if (method != null)
            {
                Vertex containingType = GetNativeLayoutInfoSignatureForEEType(factory, method.OwningType);
                Vertex nameAndSig = _writer.GetMethodNameAndSigSignature(
                    method.Name, 
                    GetNativeLayoutInfoSignatureForMethodSignature(factory, method.GetTypicalMethodDefinition()));
                Vertex[] args = null;
                MethodFlags flags = 0;

                if (method.HasInstantiation)
                {
                    flags |= MethodFlags.HasInstantiation;
                    args = new Vertex[method.Instantiation.Length];
                    for (int i = 0; i < args.Length; i++)
                        args[i] = GetNativeLayoutInfoSignatureForEEType(factory, method.Instantiation[i]);
                }

                signature = _writer.GetMethodSignature((uint)flags, 0, containingType, nameAndSig, args);
            }
            else if (field != null)
            {
                // TODO
            }

            Debug.Assert(signature != null);

            signature = _ldTokenInfoSection.Place(signature);
            return signature;
        }

        public Vertex GetNativeLayoutInfoSignatureForEEType(NodeFactory factory, TypeDesc type)
        {
            IEETypeNode typeSymbol = factory.NecessaryTypeSymbol(type);
            uint typeIndex = _externalReferences.GetIndex(typeSymbol);
            return _writer.GetExternalTypeSignature(typeIndex);
        }

        public Vertex GetNativeLayoutInfoSignatureForMethodSignature(NodeFactory factory, MethodDesc method)
        {
            MethodCallingConvention methodCallingConvention = default(MethodCallingConvention);

            if (method.Signature.GenericParameterCount > 0)
                methodCallingConvention |= MethodCallingConvention.Generic;
            if (method.Signature.IsStatic)
                methodCallingConvention |= MethodCallingConvention.Static;

            int parameterCount = method.Signature.Length;
            Vertex returnType = GetNativeLayoutInfoSignatureForTypeSignature(factory, method.Signature.ReturnType);
            Vertex[] parameters = new Vertex[parameterCount];
            for (int i = 0; i < parameterCount; i++)
            {
                parameters[i] = GetNativeLayoutInfoSignatureForTypeSignature(factory, method.Signature[i]);
            }

            return _signaturesSection.Place(_writer.GetMethodSigSignature((uint)methodCallingConvention, (uint)method.Signature.GenericParameterCount, returnType, parameters));
        }

        public Vertex GetNativeLayoutInfoSignatureForMethod(NodeFactory factory, MethodDesc method, NativeWriter writerToUse = null)
        {
            if (writerToUse == null)
                writerToUse = _writer;

            // Get native layout vertices for the declaring type

            ISymbolNode declaringTypeNode = factory.NecessaryTypeSymbol(method.OwningType);
            Debug.Assert(declaringTypeNode != null);
            Vertex declaringType = writerToUse.GetUnsignedConstant(_externalReferences.GetIndex(declaringTypeNode));

            // Get a vertex sequence for the method instantiation args if any

            VertexSequence arguments = new VertexSequence();
            if (method.HasInstantiation)
            {
                foreach (var arg in method.Instantiation)
                {
                    ISymbolNode argNode = factory.NecessaryTypeSymbol(arg);
                    Debug.Assert(argNode != null);
                    arguments.Append(writerToUse.GetUnsignedConstant(_externalReferences.GetIndex(argNode)));
                }
            }

            // Get the name and sig of the method

            Vertex nameAndSig = GetNativeLayoutInfoSignatureForPlacedNameAndSignature(
                factory,
                method.Name,
                GetNativeLayoutInfoSignatureForMethodSignature(factory, method.GetTypicalMethodDefinition()),
                writerToUse);

            return writerToUse.GetTuple(declaringType, nameAndSig, arguments);
        }

        public Vertex GetNativeLayoutInfoSignatureForPlacedNameAndSignature(NodeFactory factory, string name, Vertex signature, NativeWriter writerToUse = null)
        {
            if (writerToUse == null)
                writerToUse = _writer;

            // Always use the nativeLayoutInfoWriter for names and sigs, even when writerToUse is one of the hash tables. This saves space,
            // since we can Unify more signatures, allows optimizations in comparing sigs in the same module, and prevents the dynamic
            // type loader having to know about other native layout sections (since sigs contain types). If we are using a non-native
            // layout info writer, write the sig to the native layout info, and refer to it by offset in its own section.  At runtime,
            // we will assume all names and sigs are in the native layout and find it.
            Vertex nameAndSig = _writer.GetMethodNameAndSigSignature(name, signature);
            nameAndSig = _signaturesSection.Place(nameAndSig);
            if (writerToUse != _writer)
            {
                nameAndSig = writerToUse.GetOffsetSignature(nameAndSig);
            }
            return nameAndSig;
        }

        public Vertex GetNativeLayoutInfoSignatureForPlacedTypeSignature(NodeFactory factory, TypeDesc type)
        {
            Vertex typeSignature = GetNativeLayoutInfoSignatureForTypeSignature(factory, type);
            return _signaturesSection.Place(typeSignature);
        }


        public Vertex GetNativeLayoutInfoSignatureForTypeSignature(NodeFactory factory, TypeDesc type)
        {
            Vertex signature = null;

            switch (type.Category)
            {
                case Internal.TypeSystem.TypeFlags.SzArray:
                    signature = _writer.GetModifierTypeSignature(TypeModifierKind.Array, GetNativeLayoutInfoSignatureForTypeSignature(factory, ((ArrayType)type).ElementType));
                    break;

                case Internal.TypeSystem.TypeFlags.Pointer:
                    signature = _writer.GetModifierTypeSignature(TypeModifierKind.Pointer, GetNativeLayoutInfoSignatureForTypeSignature(factory, ((PointerType)type).ParameterType));
                    break;

                case Internal.TypeSystem.TypeFlags.ByRef:
                    signature = _writer.GetModifierTypeSignature(TypeModifierKind.ByRef, GetNativeLayoutInfoSignatureForTypeSignature(factory, ((ByRefType)type).ParameterType));
                    break;

                case Internal.TypeSystem.TypeFlags.SignatureTypeVariable:
                    signature = _writer.GetVariableTypeSignature((uint)((SignatureVariable)type).Index, false);
                    break;

                case Internal.TypeSystem.TypeFlags.SignatureMethodVariable:
                    signature = _writer.GetVariableTypeSignature((uint)((SignatureMethodVariable)type).Index, true);
                    break;

                case Internal.TypeSystem.TypeFlags.Void:
                case Internal.TypeSystem.TypeFlags.Boolean:
                case Internal.TypeSystem.TypeFlags.Char:
                case Internal.TypeSystem.TypeFlags.SByte:
                case Internal.TypeSystem.TypeFlags.Byte:
                case Internal.TypeSystem.TypeFlags.Int16:
                case Internal.TypeSystem.TypeFlags.UInt16:
                case Internal.TypeSystem.TypeFlags.Int32:
                case Internal.TypeSystem.TypeFlags.UInt32:
                case Internal.TypeSystem.TypeFlags.Int64:
                case Internal.TypeSystem.TypeFlags.UInt64:
                case Internal.TypeSystem.TypeFlags.Single:
                case Internal.TypeSystem.TypeFlags.Double:
                case Internal.TypeSystem.TypeFlags.IntPtr:
                case Internal.TypeSystem.TypeFlags.UIntPtr:
                case Internal.TypeSystem.TypeFlags.Enum:
                    signature = GetNativeLayoutInfoSignatureForEEType(factory, type);
                    break;

                case Internal.TypeSystem.TypeFlags.Class:
                case Internal.TypeSystem.TypeFlags.ValueType:
                case Internal.TypeSystem.TypeFlags.Interface:
                    if (type.HasInstantiation && !type.IsGenericDefinition)
                    {
                        TypeDesc typeDef = type.GetTypeDefinition();

                        Vertex typeDefVertex = GetNativeLayoutInfoSignatureForTypeSignature(factory, typeDef);
                        Vertex[] args = new Vertex[type.Instantiation.Length];
                        for (int i = 0; i < args.Length; i++)
                            args[i] = GetNativeLayoutInfoSignatureForTypeSignature(factory, type.Instantiation[i]);

                        signature = _writer.GetInstantiationTypeSignature(typeDefVertex, args);
                    }
                    else
                    {
                        signature = GetNativeLayoutInfoSignatureForEEType(factory, type);
                    }
                    break;

                // TODO case Internal.TypeSystem.TypeFlags.Array:
                // TODO case Internal.TypeSystem.TypeFlags.FunctionPointer:

                default:
                    throw new NotImplementedException("NYI");
            }

            Debug.Assert(signature != null);
            return signature;
        }
        #endregion
    }
}
