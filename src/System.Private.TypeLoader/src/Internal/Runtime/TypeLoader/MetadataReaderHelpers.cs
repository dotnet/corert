// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using global::System;
using global::System.Reflection;
using global::Internal.Metadata.NativeFormat;

using Debug = System.Diagnostics.Debug;
using AssemblyFlags = Internal.Metadata.NativeFormat.AssemblyFlags;

namespace Internal.Runtime.TypeLoader
{
    public static class MetadataReaderHelpers
    {
        public static bool CompareTypeReferenceAcrossModules(TypeReferenceHandle tr1, MetadataReader mr1, TypeReferenceHandle tr2, MetadataReader mr2)
        {
            TypeReference trData1 = mr1.GetTypeReference(tr1);
            TypeReference trData2 = mr2.GetTypeReference(tr2);
            if (!trData1.TypeName.StringEquals(trData2.TypeName.GetConstantStringValue(mr2).Value, mr1))
                return false;

            if (trData1.ParentNamespaceOrType.HandleType != trData2.ParentNamespaceOrType.HandleType)
                return false;

            if (trData1.ParentNamespaceOrType.HandleType == HandleType.TypeReference)
                return CompareTypeReferenceAcrossModules(trData1.ParentNamespaceOrType.ToTypeReferenceHandle(mr1), mr1, trData2.ParentNamespaceOrType.ToTypeReferenceHandle(mr2), mr2);

            return CompareNamespaceReferenceAcrossModules(trData1.ParentNamespaceOrType.ToNamespaceReferenceHandle(mr1), mr1, trData2.ParentNamespaceOrType.ToNamespaceReferenceHandle(mr2), mr2);
        }

        public static bool CompareNamespaceReferenceAcrossModules(NamespaceReferenceHandle nr1, MetadataReader mr1, NamespaceReferenceHandle nr2, MetadataReader mr2)
        {
            NamespaceReference nrData1 = mr1.GetNamespaceReference(nr1);
            NamespaceReference nrData2 = mr2.GetNamespaceReference(nr2);

            if (nrData1.Name.IsNull(mr1) != nrData2.Name.IsNull(mr2))
                return false;

            if (!nrData1.Name.IsNull(mr1))
            {
                if (!nrData1.Name.StringEquals(nrData2.Name.GetConstantStringValue(mr2).Value, mr1))
                    return false;
            }

            if (nrData1.ParentScopeOrNamespace.HandleType != nrData1.ParentScopeOrNamespace.HandleType)
                return false;

            if (nrData1.ParentScopeOrNamespace.HandleType == HandleType.NamespaceReference)
                return CompareNamespaceReferenceAcrossModules(nrData1.ParentScopeOrNamespace.ToNamespaceReferenceHandle(mr1), mr1, nrData2.ParentScopeOrNamespace.ToNamespaceReferenceHandle(mr2), mr2);

            return CompareScopeReferenceAcrossModules(nrData1.ParentScopeOrNamespace.ToScopeReferenceHandle(mr1), mr1, nrData2.ParentScopeOrNamespace.ToScopeReferenceHandle(mr2), mr2);
        }

        public static bool CompareScopeReferenceAcrossModules(ScopeReferenceHandle sr1, MetadataReader mr1, ScopeReferenceHandle sr2, MetadataReader mr2)
        {
            ScopeReference srData1 = mr1.GetScopeReference(sr1);
            ScopeReference srData2 = mr2.GetScopeReference(sr2);
            if (!srData1.Name.StringEquals(srData2.Name.GetConstantStringValue(mr2).Value, mr1))
                return false;

            if (!srData1.Culture.StringEquals(srData2.Culture.GetConstantStringValue(mr2).Value, mr1))
                return false;

            if (srData1.MajorVersion != srData2.MajorVersion)
                return false;

            if (srData1.MinorVersion != srData2.MinorVersion)
                return false;

            if (srData1.RevisionNumber != srData2.RevisionNumber)
                return false;

            if (srData1.BuildNumber != srData2.BuildNumber)
                return false;

            return ComparePublicKeyOrTokens(srData1.PublicKeyOrToken, srData1.Flags.HasFlag(AssemblyFlags.PublicKey), srData2.PublicKeyOrToken, srData2.Flags.HasFlag(AssemblyFlags.PublicKey));
        }

        public static bool CompareTypeReferenceToDefinition(TypeReferenceHandle tr1, MetadataReader mr1, TypeDefinitionHandle td2, MetadataReader mr2)
        {
            // TODO! The correct implementation here is probably to call into the assembly binder, but that's not available due to layering.
            // For now, just implement comparison, which will be equivalent in all cases until we support loading multiple copies of the same assembly

            TypeReference trData1 = mr1.GetTypeReference(tr1);
            TypeDefinition tdData2 = mr2.GetTypeDefinition(td2);

            if (!trData1.TypeName.StringEquals(tdData2.Name.GetConstantStringValue(mr2).Value, mr1))
                return false;

            switch (trData1.ParentNamespaceOrType.HandleType)
            {
                case HandleType.TypeReference:
                    if (tdData2.EnclosingType.IsNull(mr2))
                        return false;

                    return CompareTypeReferenceToDefinition(trData1.ParentNamespaceOrType.ToTypeReferenceHandle(mr1), mr1, tdData2.EnclosingType, mr2);

                case HandleType.NamespaceReference:
                    return CompareNamespaceReferenceToDefinition(trData1.ParentNamespaceOrType.ToNamespaceReferenceHandle(mr1), mr1, tdData2.NamespaceDefinition, mr2);

                default:
                    Debug.Assert(false);
                    throw new BadImageFormatException();
            }
        }

        public static bool CompareNamespaceReferenceToDefinition(NamespaceReferenceHandle nr1, MetadataReader mr1, NamespaceDefinitionHandle nd2, MetadataReader mr2)
        {
            NamespaceReference nrData1 = mr1.GetNamespaceReference(nr1);
            NamespaceDefinition ndData2 = mr2.GetNamespaceDefinition(nd2);

            if (nrData1.Name.IsNull(mr1) != ndData2.Name.IsNull(mr2))
                return false;

            if (!nrData1.Name.IsNull(mr1))
            {
                if (!nrData1.Name.StringEquals(ndData2.Name.GetConstantStringValue(mr2).Value, mr1))
                    return false;
            }

            switch (nrData1.ParentScopeOrNamespace.HandleType)
            {
                case HandleType.NamespaceReference:
                    if (ndData2.ParentScopeOrNamespace.HandleType != HandleType.NamespaceDefinition)
                        return false;
                    return CompareNamespaceReferenceToDefinition(nrData1.ParentScopeOrNamespace.ToNamespaceReferenceHandle(mr1), mr1, ndData2.ParentScopeOrNamespace.ToNamespaceDefinitionHandle(mr2), mr2);

                case HandleType.ScopeReference:
                    if (ndData2.ParentScopeOrNamespace.HandleType != HandleType.ScopeDefinition)
                        return false;

                    return CompareScopeReferenceToDefinition(nrData1.ParentScopeOrNamespace.ToScopeReferenceHandle(mr1), mr1, ndData2.ParentScopeOrNamespace.ToScopeDefinitionHandle(mr2), mr2);

                default:
                    Debug.Assert(false);
                    throw new BadImageFormatException();
            }
        }

        public static bool CompareScopeReferenceToDefinition(ScopeReferenceHandle sr1, MetadataReader mr1, ScopeDefinitionHandle sd2, MetadataReader mr2)
        {
            ScopeReference srData1 = mr1.GetScopeReference(sr1);
            ScopeDefinition sdData2 = mr2.GetScopeDefinition(sd2);
            if (!srData1.Name.StringEquals(sdData2.Name.GetConstantStringValue(mr2).Value, mr1))
                return false;

            if (!srData1.Culture.StringEquals(sdData2.Culture.GetConstantStringValue(mr2).Value, mr1))
                return false;

            if (srData1.MajorVersion != sdData2.MajorVersion)
                return false;

            if (srData1.MinorVersion != sdData2.MinorVersion)
                return false;

            if (srData1.RevisionNumber != sdData2.RevisionNumber)
                return false;

            if (srData1.BuildNumber != sdData2.BuildNumber)
                return false;

            return ComparePublicKeyOrTokens(srData1.PublicKeyOrToken, srData1.Flags.HasFlag(AssemblyFlags.PublicKey), sdData2.PublicKey, sdData2.Flags.HasFlag(AssemblyFlags.PublicKey));
        }

        public static bool ComparePublicKeyOrTokens(ByteCollection keyOrToken1, bool isKey1, ByteCollection keyOrToken2, bool isKey2)
        {
            if (isKey1 != isKey2)
            {
                // Convert both to PublicKeyToken byte[] and compare

                byte[] token1 = ConvertByteCollectionKeyOrTokenToPublicKeyTokenByteArray(keyOrToken1, isKey1);
                byte[] token2 = ConvertByteCollectionKeyOrTokenToPublicKeyTokenByteArray(keyOrToken2, isKey2);

                if (token1.Length != token2.Length)
                    return false;

                for (int i = 0; i < token1.Length; i++)
                {
                    if (token1[i] != token2[i])
                        return false;
                }

                return true;
            }

            var enum1 = keyOrToken1.GetEnumerator();
            var enum2 = keyOrToken1.GetEnumerator();

            while (true)
            {
                bool moveNext1 = enum1.MoveNext();
                if (enum2.MoveNext() != moveNext1)
                    return false;

                if (moveNext1 == false)
                    break;

                if (enum1.Current != enum2.Current)
                    return false;
            }

            return true;
        }

        private static byte[] ConvertByteCollectionOfPublicKeyToByteArrayOfPublicKeyToken(ByteCollection publicKeyCollection)
        {
            byte[] publicKey = Internal.TypeSystem.NativeFormat.MetadataExtensions.ConvertByteCollectionToArray(publicKeyCollection);
            return AssemblyNameHelpers.ComputePublicKeyToken(publicKey);
        }

        private static byte[] ConvertByteCollectionKeyOrTokenToPublicKeyTokenByteArray(ByteCollection publicKeyOrToken, bool isKey)
        {
            if (isKey)
                return ConvertByteCollectionOfPublicKeyToByteArrayOfPublicKeyToken(publicKeyOrToken);
            else
                return Internal.TypeSystem.NativeFormat.MetadataExtensions.ConvertByteCollectionToArray(publicKeyOrToken);
        }
    }
}
