// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.DeveloperExperience.StackTrace
{
    internal class StackTraceTypeDescriptor
    {
        public TypeRepresentation typeRep;
        private uint? wrappedTypeId;
        private uint? rank;
        private uint? leafId;
        private uint? enclosingTypeId;
        private uint? runtimeTypeId;
        private uint[] genericArgTypeDescIds;

        public StackTraceTypeDescriptor wrappedType;
        public StackTraceTrieNode name;
        public StackTraceTypeDescriptor enclosingType;
        public StackTraceTypeDescriptor runtimeType;
        public StackTraceTypeDescriptor[] genericArgTypes;

        public enum TypeRepresentation
        {
            None = 0,
            ByRef = 1,
            Ptr = 2,
            SzArray = 3,
            Array = 4
        }

        const byte HasEnclosingTypeMask = 0x80;
        const byte IsRuntimeTypeMask = 0x40;
        const byte TypeRepresentationMask = 0x38;
        const byte NumGenericTypesMask = 0x07;

        private unsafe StackTraceTypeDescriptor(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, 
            TypeRepresentation typeRepresentation,
            uint? wrappedTypeId,
            uint? rank, 
            uint? leafId, 
            uint? enclosingTypeId, 
            uint? runtimeTypeId, 
            uint[] genericArgTypeDescIds)
        {
            this.typeRep = typeRepresentation;
            this.wrappedTypeId = wrappedTypeId;
            this.rank = rank;
            this.leafId = leafId;
            this.enclosingTypeId = enclosingTypeId;
            this.runtimeTypeId = runtimeTypeId;
            this.genericArgTypeDescIds = genericArgTypeDescIds;

            // Creates the objects from the ids.
            if (this.wrappedTypeId == null)
            {
                this.name = StackTraceTrieNode.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.trieNodeOffsets[(int)this.leafId - 1]);
            }

            if (runtimeTypeId != null)
            {
                this.runtimeType = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)this.runtimeTypeId.Value]);
            }

            if (enclosingTypeId != null)
            {
                this.enclosingType = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)this.enclosingTypeId.Value]);
            }

            if (wrappedTypeId != null)
            {
                this.wrappedType = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)this.wrappedTypeId.Value]);
            }

            if (this.genericArgTypeDescIds != null)
            {
                this.genericArgTypes = new StackTraceTypeDescriptor[genericArgTypeDescIds.Length];

                for (int i = 0; i < genericArgTypeDescIds.Length; i++)
                {
                    this.genericArgTypes[i] = StackTraceTypeDescriptor.CreateFromBuffer(stackTraceBlobIndex, buffer, stackTraceBlobIndex.typeOffsets[(int)genericArgTypeDescIds[i]]);
                }
            }
        }

        public unsafe static StackTraceTypeDescriptor CreateFromBuffer(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint offset)
        {
            uint bytesRead;
            return ReadFromBuffer(stackTraceBlobIndex, buffer, offset, out bytesRead);
        }

        public static unsafe uint CalculateSize(byte* buffer, uint offset)
        {
            uint bytesRead = 0;
            ReadFromBuffer(null, buffer, offset, out bytesRead);
            return bytesRead;
        }
        
        public override string ToString()
        {
            if (this.typeRep == TypeRepresentation.None)
            {
                string stringName = "";
                string genericParams = (genericArgTypes.Length == 0) ? "" : "<" + string.Join(",", (object[])this.genericArgTypes) + ">";
                if (runtimeType != null)
                {
                    stringName += runtimeType.ToString() + "_";
                }

                if (enclosingType != null)
                {
                    stringName += enclosingType.ToString() + "+";
                }

                stringName += name.ToString() + genericParams;
                return stringName;
            }
            else
            {
                string suffix = "";
                switch (typeRep)
                {
                    case TypeRepresentation.ByRef:
                        suffix = "&";
                        break;

                    case TypeRepresentation.Ptr:
                        suffix = "*";
                        break;

                    case TypeRepresentation.SzArray:
                        suffix = "[]";
                        break;

                    case TypeRepresentation.Array:
                        suffix = "[" + new string(',', ((int)this.rank) - 1) + "]";
                        break;
                }

                return this.wrappedType.ToString() + suffix;
            }
        }
        
        public static unsafe StackTraceTypeDescriptor ReadFromBuffer(StackTraceBlobIndex stackTraceBlobIndex, byte* buffer, uint offset, out uint bytesRead)
        {
            bool constructDescriptor = stackTraceBlobIndex != null;

            bytesRead = 0;

            byte* pMyBuffer = buffer + offset;

            // Gets the byte containing the number of generic args, the type representation and the enclosing class or runtime type bits.
            uint encodedByte = StackTraceDecoder.DecodeByte(pMyBuffer + bytesRead, ref bytesRead); // [hasEnclosingType(7) | isRuntimeType(6) | typeRepresentation(543) | numberOfGenerics(210)]
            bool hasEnclosingType = (encodedByte & HasEnclosingTypeMask) != 0;
            bool isRuntimeType = (encodedByte & IsRuntimeTypeMask) != 0;
            TypeRepresentation typeRepresentation = (TypeRepresentation)((encodedByte & TypeRepresentationMask) >> 3);
            uint numGenericArgs = encodedByte & NumGenericTypesMask;

            if (numGenericArgs == 7)
            {
                numGenericArgs += StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead); 
            }

            uint? leafId = null;
            uint? runtimeTypeId = null;
            uint? enclosingTypeId = null;
            uint? wrappedTypeId = null;
            uint? rank = null;
            uint[] genericTypeIdsList = null;

            if (typeRepresentation != TypeRepresentation.None)
            {
                wrappedTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead); // wrapped type id
                if (typeRepresentation == TypeRepresentation.Array)
                {
                    rank = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead); // rank
                }
            }
            else
            {
                leafId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);

                if (hasEnclosingType)
                {
                    enclosingTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead); // enclosing type id
                }

                if (isRuntimeType)
                {
                    runtimeTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead); // runtime type id
                }

                if (constructDescriptor)
                {
                    genericTypeIdsList = new uint[numGenericArgs];
                }

                for (uint i = 0; i < numGenericArgs; i++)
                {
                    uint genericTypeId = StackTraceDecoder.DecodeUInt(pMyBuffer + bytesRead, StackTraceDecoder.MAX_DECODE_BYTES, ref bytesRead);
                    if (constructDescriptor)
                    {
                        genericTypeIdsList[i] = genericTypeId;
                    }
                }
            }

            if (constructDescriptor)
            {
                return new StackTraceTypeDescriptor(
                        stackTraceBlobIndex,
                        buffer,
                        typeRepresentation,
                        wrappedTypeId,
                        rank,
                        leafId,
                        enclosingTypeId,
                        runtimeTypeId,
                        genericTypeIdsList);
            }
            else
            {
                return null;
            }
        }        
    }
}