// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Internal.IL;

using Microsoft.DiaSymReader;

namespace ILCompiler
{
    // For now, open PDB files using legacy desktop SymBinder

    class PdbSymbolProvider
    {
        [Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        private interface IMetaDataDispenser
        {
            // We need to be able to call OpenScope, which is the 2nd vtable slot.
            // Thus we need this one placeholder here to occupy the first slot..
            void DefineScope_Placeholder();

            [PreserveSig]
            int OpenScope([In, MarshalAs(UnmanagedType.LPWStr)] String szScope, [In] Int32 dwOpenFlags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out Object punk);

            // Don't need any other methods.
        }

        // Since we're just blindly passing this interface through managed code to the Symbinder, we don't care about actually
        // importing the specific methods.
        // This needs to be public so that we can call Marshal.GetComInterfaceForObject() on it to get the
        // underlying metadata pointer.
        [Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        public interface IMetadataImport
        {
            // Just need a single placeholder method so that it doesn't complain about an empty interface.
            void Placeholder();
        }

        [DllImport("clr.dll")]
        private static extern int MetaDataGetDispenser([In] ref Guid rclsid,
                                                       [In] ref Guid riid,
                                                       [Out, MarshalAs(UnmanagedType.Interface)] out Object ppv);

        [DllImport("ole32.dll")]
        static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter,
                                           Int32 dwClsContext,
                                           ref Guid riid,
                                           [MarshalAs(UnmanagedType.Interface)] out object ppv);

        void ThrowExceptionForHR(int hr)
        {
            Marshal.ThrowExceptionForHR(hr, new IntPtr(-1));
        }

        IMetaDataDispenser _metadataDispenser;

        ISymUnmanagedBinder _symBinder;

        public PdbSymbolProvider()
        {
            try
            {
                // Create a COM Metadata dispenser
                Guid dispenserClassID = new Guid(0xe5cb7a31, 0x7512, 0x11d2, 0x89, 0xce, 0x00, 0x80, 0xc7, 0x92, 0xe5, 0xd8); // CLSID_CorMetaDataDispenser
                Guid dispenserIID = new Guid(0x809c652e, 0x7396, 0x11d2, 0x97, 0x71, 0x00, 0xa0, 0xc9, 0xb4, 0xd5, 0x0c); // IID_IMetaDataDispenser
                object objDispenser;
                if (MetaDataGetDispenser(ref dispenserClassID, ref dispenserIID, out objDispenser) < 0)
                    return;
                _metadataDispenser = (IMetaDataDispenser)objDispenser;

                Guid symBinderClassID = new Guid(0x0A29FF9E, 0x7F9C, 0x4437, 0x8B, 0x11, 0xF4, 0x24, 0x49, 0x1E, 0x39, 0x31); // CLSID_CorSymBinder
                Guid symBinderIID = new Guid(0xAA544d42, 0x28CB, 0x11d3, 0xbd, 0x22, 0x00, 0x00, 0xf8, 0x08, 0x49, 0xbd); // IID_ISymUnmanagedBinder
                object objBinder;
                if (CoCreateInstance(ref symBinderClassID,
                                 IntPtr.Zero, // pUnkOuter
                                 1, // CLSCTX_INPROC_SERVER
                                 ref symBinderIID,
                                 out objBinder) < 0)
                    return;
                _symBinder = (ISymUnmanagedBinder)objBinder;
            }
            catch
            {
            }
        }

        public ISymUnmanagedReader GetSymbolReaderForFile(string metadataFileName)
        {
            if (!File.Exists(Path.ChangeExtension(metadataFileName, ".pdb")))
                return null;

            if (_metadataDispenser == null || _symBinder == null)
                return null;

            try
            {
                Guid importerIID = new Guid(0x7dac8207, 0xd3ae, 0x4c75, 0x9b, 0x67, 0x92, 0x80, 0x1a, 0x49, 0x7d, 0x44); // IID_IMetaDataImport

                // Open an metadata importer on the given filename. We'll end up passing this importer straight
                // through to the Binder.
                object objImporter;
                if (_metadataDispenser.OpenScope(metadataFileName, 0x00000010 /* read only */, ref importerIID, out objImporter) < 0)
                    return null;

                ISymUnmanagedReader reader;
                if (_symBinder.GetReaderForFile(objImporter, metadataFileName, "", out reader) < 0)
                    return null;
                return reader;
            }
            catch
            {
                return null;
            }
        }

        Dictionary<ISymUnmanagedDocument, string> _urlCache = new Dictionary<ISymUnmanagedDocument, string>();

        private string GetUrl(ISymUnmanagedDocument doc)
        {
            string url;
            if (_urlCache.TryGetValue(doc, out url))
                return url;

            int urlLength;
            ThrowExceptionForHR(doc.GetUrl(0, out urlLength, null));

            // urlLength includes terminating '\0'
            char[] urlBuffer = new char[urlLength];
            ThrowExceptionForHR(doc.GetUrl(urlLength, out urlLength, urlBuffer));

            url = new string(urlBuffer, 0, urlLength - 1);
            _urlCache.Add(doc, url);
            return url;
        }

        public IEnumerable<ILSequencePoint> GetSequencePointsForMethod(ISymUnmanagedReader reader, int methodToken)
        {
            ISymUnmanagedMethod symbolMethod;
            if (reader.GetMethod(methodToken, out symbolMethod) < 0)
                yield break;

            int count;
            ThrowExceptionForHR(symbolMethod.GetSequencePointCount(out count));

            ISymUnmanagedDocument[] docs = new ISymUnmanagedDocument[count];
            int[] lineNumbers = new int[count];
            int[] ilOffsets = new int[count];

            ThrowExceptionForHR(symbolMethod.GetSequencePoints(count, out count, ilOffsets, docs, lineNumbers, null, null, null));

            for (int i = 0; i < count; i++)
            {
                if (lineNumbers[i] == 0xFEEFEE)
                    continue;

                yield return new ILSequencePoint() { Document = GetUrl(docs[i]), LineNumber = lineNumbers[i], Offset = ilOffsets[i] };
            }
        }

        //
        // Gather the local details in a scope and then recurse to child scopes
        //
        private void ProbeScopeForLocals(List<LocalVariable> variables, ISymUnmanagedScope scope)
        {
            int localCount;
            ThrowExceptionForHR(scope.GetLocalCount(out localCount));

            ISymUnmanagedVariable[] locals = new ISymUnmanagedVariable[localCount];
            ThrowExceptionForHR(scope.GetLocals(localCount, out localCount, locals));

            for (int i = 0; i < localCount; i++)
            {
                var local = locals[i];

                int slot;
                ThrowExceptionForHR(local.GetAddressField1(out slot));

                int nameLength;
                ThrowExceptionForHR(local.GetName(0, out nameLength, null));

                // nameLength includes terminating '\0'
                char[] nameBuffer = new char[nameLength];
                ThrowExceptionForHR(local.GetName(nameLength, out nameLength, nameBuffer));

                int attributes;
                ThrowExceptionForHR(local.GetAttributes(out attributes));

                variables.Add(new LocalVariable() { Slot = slot, Name = new String(nameBuffer, 0, nameLength - 1), CompilerGenerated = (attributes & 0x1) != 0 });
            }

            int childrenCount;
            ThrowExceptionForHR(scope.GetChildren(0, out childrenCount, null));

            ISymUnmanagedScope[] children = new ISymUnmanagedScope[childrenCount];
            ThrowExceptionForHR(scope.GetChildren(childrenCount, out childrenCount, children));

            for (int i = 0; i < childrenCount; i++)
            {
                ProbeScopeForLocals(variables, children[i]);
            }
        }

        //
        // Recursively scan the scopes for a method stored in a PDB and gather the local slots
        // and names for all of them.  This assumes a CSC-like compiler that doesn't re-use
        // local slots in the same method across scopes.
        //
        public IEnumerable<LocalVariable> GetLocalVariableNamesForMethod(ISymUnmanagedReader reader, int methodToken)
        {
            ISymUnmanagedMethod symbolMethod;
            if (reader.GetMethod(methodToken, out symbolMethod) < 0)
                return null;

            ISymUnmanagedScope rootScope;
            ThrowExceptionForHR(symbolMethod.GetRootScope(out rootScope));

            var variables = new List<LocalVariable>();
            ProbeScopeForLocals(variables, rootScope);
            return variables;
        }
    }
}
