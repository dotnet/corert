// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class ThreadStaticsIndexNode : ObjectNode, ISymbolNode
    {
        string _prefix;

        public ThreadStaticsIndexNode(string prefix)  
        {
            _prefix = prefix;
        }

        public string MangledName
        {
            get
            {
                return GetMangledName(_prefix);
            }
        }

        public static string GetMangledName(string prefix)
        {
            return  "_tls_index_" + prefix;
        }

        public int Offset => 0;
        
        protected override string GetName() => this.GetMangledName();

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(_prefix));
        }        

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override bool IsShareable => false;            

        public override bool StaticDependenciesAreComputed => true;

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return factory.ThreadStaticsRegion.ShouldSkipEmittingObjectNode(factory);
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            // Emit an aliased symbol named _tls_index for native P/Invoke code that uses TLS. This is required
            // because we do not link against libcmt.lib.
            ObjectAndOffsetSymbolNode aliasedSymbol = new ObjectAndOffsetSymbolNode(this, objData.CountBytes, "_tls_index", false);
            objData.AddSymbol(aliasedSymbol);

            // This is the TLS index field which is a 4-byte integer. Emit an 8-byte interger which includes a
            // 4-byte padding to make an pointer-sized alignment for the subsequent fields for all targets.
            objData.EmitLong(0); 

            // Allocate and initialize the IMAGE_TLS_DIRECTORY PE data structure used by the OS loader to determine
            // TLS allocations. The structure is defined by the OS as following:
            /*
                struct _IMAGE_TLS_DIRECTORY32
                {
                    DWORD StartAddressOfRawData;
                    DWORD EndAddressOfRawData;
                    DWORD AddressOfIndex;
                    DWORD AddressOfCallBacks;
                    DWORD SizeOfZeroFill;
                    DWORD Characteristics;
                }

                struct _IMAGE_TLS_DIRECTORY64
                {
                    ULONGLONG StartAddressOfRawData;
                    ULONGLONG EndAddressOfRawData;
                    ULONGLONG AddressOfIndex;
                    ULONGLONG AddressOfCallBacks;
                    DWORD SizeOfZeroFill;
                    DWORD Characteristics;
                }
            */
            // In order to utilize linker support, the struct variable needs to be named _tls_used
            ObjectAndOffsetSymbolNode structSymbol = new ObjectAndOffsetSymbolNode(this, objData.CountBytes, "_tls_used", false);
            objData.AddSymbol(structSymbol);
            objData.EmitPointerReloc(factory.ThreadStaticsRegion.StartSymbol);     // start of tls data
            objData.EmitPointerReloc(factory.ThreadStaticsRegion.EndSymbol);     // end of tls data
            objData.EmitPointerReloc(this);     // address of tls_index
            objData.EmitZeroPointer();          // pointer to call back array
            objData.EmitInt(0);                 // size of tls zero fill
            objData.EmitInt(0);                 // characteristics

            return objData.ToObjectData();
        }
    }
}
