// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FUNCDESC
    {
        public int memid;                   //MEMBERID memid;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",  Justification="Backwards compatibility")]
        public IntPtr lprgscode;            // /* [size_is(cScodes)] */ SCODE RPC_FAR *lprgscode;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",  Justification="Backwards compatibility")]
        public IntPtr lprgelemdescParam;    // /* [size_is(cParams)] */ ELEMDESC __RPC_FAR *lprgelemdescParam;
        public FUNCKIND funckind;           //FUNCKIND funckind;
        public INVOKEKIND invkind;          //INVOKEKIND invkind;
        public CALLCONV callconv;           //CALLCONV callconv;
        public Int16 cParams;               //short cParams;
        public Int16 cParamsOpt;            //short cParamsOpt;
        public Int16 oVft;                  //short oVft;
        public Int16 cScodes;               //short cScodes;
        public ELEMDESC elemdescFunc;       //ELEMDESC elemdescFunc;
        public Int16 wFuncFlags;            //WORD wFuncFlags;
    }
}
