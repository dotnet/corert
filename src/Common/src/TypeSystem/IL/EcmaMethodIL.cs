// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.IL
{
    public class EcmaMethodIL : MethodIL
    {
        private EcmaModule _module;
        private MethodBodyBlock _methodBody;

        // Cached values
        private byte[] _ilBytes;
        private LocalVariableDefinition[] _locals;
        private ILExceptionRegion[] _ilExceptionRegions;

        static public EcmaMethodIL Create(EcmaMethod method)
        {
            var rva = method.MetadataReader.GetMethodDefinition(method.Handle).RelativeVirtualAddress;
            if (rva == 0)
                return null;
            return new EcmaMethodIL(method.Module, method.Module.PEReader.GetMethodBody(rva));
        }

        public EcmaMethodIL(EcmaModule module, MethodBodyBlock methodBody)
        {
            _module = module;
            _methodBody = methodBody;
        }

        // Avoid unnecessary copy
        private static byte[] DangerousGetUnderlyingArray(ImmutableArray<byte> array)
        {
            var union = new ByteArrayUnion();
            union.ImmutableArray = array;
            return union.UnderlyingArray;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct ByteArrayUnion
        {
            [FieldOffset(0)]
            internal byte[] UnderlyingArray;

            [FieldOffset(0)]
            internal ImmutableArray<byte> ImmutableArray;
        }

        public override byte[] GetILBytes()
        {
            if (_ilBytes != null)
                return _ilBytes;

            byte[] ilBytes = DangerousGetUnderlyingArray(_methodBody.GetILContent());
            return (_ilBytes = ilBytes);
        }

        public override bool GetInitLocals()
        {
            return _methodBody.LocalVariablesInitialized;
        }

        public override int GetMaxStack()
        {
            return _methodBody.MaxStack;
        }

        public override LocalVariableDefinition[] GetLocals()
        {
            if (_locals != null)
                return _locals;

            var metadataReader = _module.MetadataReader;
            var localSignature = _methodBody.LocalSignature;
            if (localSignature.IsNil)
                return Array.Empty<LocalVariableDefinition>();
            BlobReader signatureReader = metadataReader.GetBlobReader(metadataReader.GetStandaloneSignature(localSignature).Signature);

            EcmaSignatureParser parser = new EcmaSignatureParser(_module, signatureReader);
            LocalVariableDefinition[] locals = parser.ParseLocalsSignature();
            return (_locals = locals);
        }

        public override ILExceptionRegion[] GetExceptionRegions()
        {
            if (_ilExceptionRegions != null)
                return _ilExceptionRegions;

            ImmutableArray<ExceptionRegion> exceptionRegions = _methodBody.ExceptionRegions;
            ILExceptionRegion[] ilExceptionRegions;

            int length = exceptionRegions.Length;
            if (length == 0)
            {
                ilExceptionRegions = Array.Empty<ILExceptionRegion>();
            }
            else
            {
                ilExceptionRegions = new ILExceptionRegion[length];
                for (int i = 0; i < length; i++)
                {
                    var exceptionRegion = exceptionRegions[i];

                    ilExceptionRegions[i] = new ILExceptionRegion(
                        (ILExceptionRegionKind)exceptionRegion.Kind, // assumes that ILExceptionRegionKind and ExceptionRegionKind enums are in sync
                        exceptionRegion.TryOffset,
                        exceptionRegion.TryLength,
                        exceptionRegion.HandlerOffset,
                        exceptionRegion.HandlerLength,
                        MetadataTokens.GetToken(exceptionRegion.CatchType),
                        exceptionRegion.FilterOffset);
                }
            }

            return (_ilExceptionRegions = ilExceptionRegions);
        }

        public override object GetObject(int token)
        {
            // UserStrings cannot be wrapped in EntityHandle
            if ((token & 0xFF000000) == 0x70000000)
                return _module.GetUserString(MetadataTokens.UserStringHandle(token));

            return _module.GetObject(MetadataTokens.EntityHandle(token));
        }
    }
}
