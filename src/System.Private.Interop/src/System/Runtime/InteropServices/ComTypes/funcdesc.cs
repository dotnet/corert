// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FUNCDESC
    {
        public int memid;                   //MEMBERID memid;
        public IntPtr lprgscode;            // /* [size_is(cScodes)] */ SCODE RPC_FAR *lprgscode;
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
