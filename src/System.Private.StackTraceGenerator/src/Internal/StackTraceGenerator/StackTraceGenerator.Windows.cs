// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;
using Internal.StackGenerator.Dia;

namespace Internal.StackTraceGenerator
{
    public static class StackTraceGenerator
    {
        /// <summary>
        /// Check the AppCompat switch 'Diagnostics.DisableDiaStackTraceResolution'.
        /// This is used for testing of metadata-based stack trace resolution.
        /// </summary>
        private static bool IsDiaStackTraceResolutionDisabled()
        {
            bool disableDia = false;
            AppContext.TryGetSwitch("Diagnostics.DisableDiaStackTraceResolution", out disableDia);
            return disableDia;
        }

        //
        // Makes reasonable effort to construct one useful line of a stack trace. Returns null if it can't.
        //
        public static String CreateStackTraceString(IntPtr ip, bool includeFileInfo)
        {
            if (IsDiaStackTraceResolutionDisabled())
            {
                return null;
            }

            try
            {
                int hr;

                int rva;
                IDiaSession session = GetDiaSession(ip, out rva);
                if (session == null)
                    return null;

                StringBuilder sb = new StringBuilder();
                IDiaSymbol symbol;
                hr = session.FindSymbolByRVA(rva, SymTagEnum.SymTagFunction, out symbol);
                if (hr != S_OK)
                    return null;
                String functionName;
                hr = symbol.GetName(out functionName);
                if (hr == S_OK)
                    sb.Append(functionName.Demanglify());
                else
                    sb.Append("<Function Name Not Available>");

                sb.Append(CreateParameterListString(session, symbol));

                if (includeFileInfo)
                {
                    sb.Append(CreateSourceInfoString(session, rva));
                }

                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        //
        // Makes reasonable effort to get source info. Returns null sourceFile and 0 lineNumber/columnNumber if it can't.
        //
        public static void TryGetSourceLineInfo(IntPtr ip, out string fileName, out int lineNumber, out int columnNumber)
        {
            fileName = null;
            lineNumber = 0;
            columnNumber = 0;
            if (!IsDiaStackTraceResolutionDisabled())
            {
                int rva;
                IDiaSession session = GetDiaSession(ip, out rva);
                if (session != null)
                {
                    TryGetSourceLineInfo(session, rva, out fileName, out lineNumber, out columnNumber);
                }
            }
        }

        /// <summary>
        /// Makes reasonable effort to find the IL offset corresponding to the given address within a method.
        /// Returns StackFrame.OFFSET_UNKNOWN if not available.
        /// </summary>
        public static void TryGetILOffsetWithinMethod(IntPtr ip, out int ilOffset)
        {
            ilOffset = StackFrame.OFFSET_UNKNOWN;
            if (!IsDiaStackTraceResolutionDisabled())
            {
                int rva;
                IDiaSession session = GetDiaSession(ip, out rva);
                if (session != null)
                {
                    TryGetILOffsetInfo(session, rva, out ilOffset);
                }
            }
        }

        private static readonly Guid CLSID_DiaSource = new Guid(0xE6756135, 0x1E65, 0x4D17, 0x85, 0x76, 0x61, 0x07, 0x61, 0x39, 0x8C, 0x3C); // msdia140.dll
        private static readonly Guid IID_IDiaDataSource = new Guid(0x79F1BB5F, 0xB66E, 0x48E5, 0xB6, 0xA9, 0x15, 0x45, 0xC3, 0x23, 0xCA, 0x3D);

        //
        // Get a IDiaDataSource object
        //
        private static unsafe IDiaDataSource GetDiaDataSource()
        {
            fixed (Guid* pclsid = &CLSID_DiaSource)
            {
                fixed (Guid* piid = &IID_IDiaDataSource)
                {
                    IntPtr _dataSource;
                    int hr = CoCreateInstance(pclsid, (IntPtr)0, CLSCTX_INPROC, piid, out _dataSource);
                    if (hr == S_OK)
                        return new IDiaDataSource(_dataSource);
                }
            }
            return null;
        }

        //
        // Create the method parameter list.
        //
        private static String CreateParameterListString(IDiaSession session, IDiaSymbol symbol)
        {
            StringBuilder sb = new StringBuilder("(");

            // find the parameters
            IDiaEnumSymbols dataSymbols;
            int hr = session.FindChildren(symbol, SymTagEnum.SymTagData, null, NameSearchOptions.nsNone, out dataSymbols);
            if (hr == S_OK)
            {
                int count;
                hr = dataSymbols.Count(out count);
                if (hr == S_OK)
                {
                    for (int i = 0, iParam = 0; i < count; i++)
                    {
                        IDiaSymbol dataSym;
                        hr = dataSymbols.Item(i, out dataSym);
                        if (hr != S_OK)
                            continue;

                        DataKind dataKind;
                        hr = dataSym.GetDataKind(out dataKind);
                        if (hr != S_OK || dataKind != DataKind.DataIsParam)
                            continue;

                        string paramName;
                        hr = dataSym.GetName(out paramName);
                        if (hr != S_OK)
                        {
                            continue;
                        }

                        //this approximates the way C# displays methods by not including these hidden arguments
                        if (paramName == "InstParam" || paramName == "this")
                        {
                            continue;
                        }

                        IDiaSymbol parameterType;
                        hr = dataSym.GetType(out parameterType);
                        if (hr != S_OK)
                        {
                            continue;
                        }

                        if (iParam++ != 0)
                            sb.Append(", ");

                        sb.Append(parameterType.ToTypeString(session));
                        sb.Append(' ');
                        sb.Append(paramName);
                    }
                }
            }
            sb.Append(')');
            return sb.ToString();
        }

        //
        // Retrieve the source fileName, line number, and column
        //
        private static void TryGetSourceLineInfo(IDiaSession session, int rva, out string fileName, out int lineNumber, out int columnNumber)
        {
            fileName = null;
            lineNumber = 0;
            columnNumber = 0;
            IDiaEnumLineNumbers lineNumbers;
            int hr = session.FindLinesByRVA(rva, 1, out lineNumbers);
            if (hr == S_OK)
            {
                int numLineNumbers;
                hr = lineNumbers.Count(out numLineNumbers);
                if (hr == S_OK && numLineNumbers > 0)
                {
                    IDiaLineNumber ln;
                    hr = lineNumbers.Item(0, out ln);
                    if (hr == S_OK)
                    {
                        IDiaSourceFile sourceFile;
                        hr = ln.SourceFile(out sourceFile);
                        if (hr == S_OK)
                        {
                            hr = sourceFile.FileName(out fileName);
                            if (hr == S_OK)
                            {
                                hr = ln.LineNumber(out lineNumber);
                                if (hr == S_OK)
                                {
                                    hr = ln.ColumnNumber(out columnNumber);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void TryGetILOffsetInfo(IDiaSession session, int rva, out int ilOffset)
        {
            IDiaEnumLineNumbers lineNumbers;
            int hr = session.FindILOffsetsByRVA(rva, 1, out lineNumbers);
            if (hr == S_OK)
            {
                int numLineNumbers;
                hr = lineNumbers.Count(out numLineNumbers);
                if (hr == S_OK && numLineNumbers > 0)
                {
                    IDiaLineNumber ln;
                    hr = lineNumbers.Item(0, out ln);
                    if (hr == S_OK)
                    {
                        hr = ln.LineNumber(out ilOffset);
                        if (hr == S_OK)
                        {
                            return;
                        }
                    }
                }
            }
            ilOffset = StackFrame.OFFSET_UNKNOWN;
        }

        //
        // Generate the " in <filename>:line <line#>" section.
        //
        private static String CreateSourceInfoString(IDiaSession session, int rva)
        {
            StringBuilder sb = new StringBuilder();
            string fileName;
            int lineNumber, columnNumber;
            TryGetSourceLineInfo(session, rva, out fileName, out lineNumber, out columnNumber);
            if(!string.IsNullOrEmpty(fileName))
            {
                sb.Append(" in ").Append(fileName);
                if(lineNumber >= 0)
                {
                    sb.Append(":line ").Append(lineNumber);
                }
            }
            return sb.ToString();
        }

        //
        // Clean up all the "$2_" sweetness that ILMerge contributes and
        // replace type-name separator "::" by "."
        //
        private static String Demanglify(this String s)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                sb.Append(s[i++]);

                if (i == s.Length)
                    continue;

                if (s[i - 1] == '$' && Char.IsNumber(s[i]))
                {
                    if (i != 1 && (s[i - 2] != ' ' && s[i - 2] != '<'))
                        continue;
                    int lookAhead = i + 1;
                    while (lookAhead < s.Length && Char.IsNumber(s[lookAhead]))
                        lookAhead++;
                    if (lookAhead == s.Length || s[lookAhead] != '_')
                        continue;
                    sb = sb.Remove(sb.Length - 1, 1);
                    i = lookAhead + 1;
                }
                else if (s[i - 1] == ':' && s[i] == ':')
                {
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append('.');
                    i++;
                }
            }

            return sb.ToString();
        }

        private static String ToTypeString(this IDiaSymbol parameterType, IDiaSession session)
        {
            bool ignore;
            return parameterType.ToTypeStringWorker(session, 0, out ignore);
        }

        private static String ToTypeStringWorker(this IDiaSymbol parameterType, IDiaSession session, int recursionLevel, out bool isValueTypeOrByRef)
        {
            int hr;
            isValueTypeOrByRef = false;

            // Block runaway recursions.
            if (recursionLevel++ > 10)
                return "?";

            SymTagEnum symTag;
            hr = parameterType.GetSymTag(out symTag);
            if (hr != S_OK)
                return "?";
            if (symTag == SymTagEnum.SymTagPointerType)
            {
                bool isReference;
                hr = parameterType.GetReference(out isReference);
                if (hr != S_OK)
                    return "?";

                if (isReference)
                {
                    // An isReference pointer can mean one of two things:
                    //   1. ELEMENT_TYPE_BYREF
                    //   2. An indication that the UDT that follows is actually a class, not a struct. 
                    //
                    isValueTypeOrByRef = true;
                    IDiaSymbol targetType;
                    hr = parameterType.GetType(out targetType);
                    if (hr != S_OK)
                        return "?";
                    bool targetIsValueTypeOrByRef;
                    String targetTypeString = targetType.ToTypeStringWorker(session, recursionLevel, out targetIsValueTypeOrByRef);
                    if (targetIsValueTypeOrByRef)
                        return targetTypeString + "&";
                    else
                        return targetTypeString;
                }
                else
                {
                    // A non-isReference pointer means an ELEMENT_TYPE_PTR
                    IDiaSymbol targetType;
                    hr = parameterType.GetType(out targetType);
                    if (hr != S_OK)
                        return "?";
                    bool ignore;
                    return targetType.ToTypeStringWorker(session, recursionLevel, out ignore) + "*";
                }
            }
            else if (symTag == SymTagEnum.SymTagArrayType)
            {
                // Note: We don't actually hit this case in NUTC-generated PDB's as NUTC emits arrays as if they were UDT's with square brackets in the name.
                // But just in case NUTC ever changes its PDB emission, we'll print out the most obvious interpretation and hope we're right.
                IDiaSymbol elementType;
                hr = parameterType.GetType(out elementType);
                if (hr != S_OK)
                    return "?";
                bool ignore;
                return elementType.ToTypeStringWorker(session, recursionLevel, out ignore) + "[]";
            }
            else if (symTag == SymTagEnum.SymTagUDT || symTag == SymTagEnum.SymTagEnum)
            {
                // Need to figure out whether this is a value type as our recursive caller needs to know whether the "byref pointer" that wrapped this
                // is a true managed byref or just the "byref pointer" that wraps all non-valuetypes.
                if (symTag == SymTagEnum.SymTagEnum)
                {
                    isValueTypeOrByRef = true;
                }
                else
                {
                    IDiaEnumSymbols baseClasses;
                    hr = session.FindChildren(parameterType, SymTagEnum.SymTagBaseClass, null, 0, out baseClasses);
                    if (hr != S_OK)
                        return "?";
                    int count;
                    hr = baseClasses.Count(out count);
                    if (hr != S_OK)
                        return "?";
                    for (int i = 0; i < count; i++)
                    {
                        IDiaSymbol baseClass;
                        if (S_OK == baseClasses.Item(i, out baseClass))
                        {
                            String baseClassName;
                            if (S_OK == baseClass.GetName(out baseClassName))
                            {
                                if (baseClassName == "System::ValueType")
                                    isValueTypeOrByRef = true;
                            }
                        }
                    }
                }

                String name;
                hr = parameterType.GetName(out name);
                if (hr != S_OK)
                    return "?";
                return name.RemoveNamespaces().Demanglify();
            }
            else if (symTag == SymTagEnum.SymTagBaseType)
            {
                // Certain "primitive" types are encoded specially.
                BasicType basicType;
                hr = parameterType.GetBaseType(out basicType);
                if (hr != S_OK)
                    return "?";
                long length;
                hr = parameterType.GetLength(out length);
                if (hr != S_OK)
                    return "?";
                return ConvertBasicTypeToTypeString(basicType, length, out isValueTypeOrByRef);
            }
            else
            {
                return "?";
            }
        }

        private static String ConvertBasicTypeToTypeString(BasicType basicType, long length, out bool isValueTypeOrByRef)
        {
            isValueTypeOrByRef = true;
            switch (basicType)
            {
                case BasicType.btNoType:
                    return "Unknown";

                case BasicType.btVoid:
                    return "Void";

                case BasicType.btChar:
                    return "Byte";

                case BasicType.btWChar:
                    return "Char";

                case BasicType.btInt:
                    if (length != 1L)
                    {
                        if (length == 2L)
                        {
                            return "Int16";
                        }
                        if ((length != 4L) && (length == 8L))
                        {
                            return "Int64";
                        }
                        return "Int32";
                    }
                    return "SByte";

                case BasicType.btUInt:
                    if (length != 1L)
                    {
                        if (length == 2L)
                        {
                            return "UInt16";
                        }
                        if ((length != 4L) && (length == 8L))
                        {
                            return "UInt64";
                        }
                        return "UInt32";
                    }
                    return "Byte";

                case BasicType.btFloat:
                    if (length != 8L)
                    {
                        return "Single";
                    }
                    return "Double";

                case BasicType.btBCD:
                    return "BCD";

                case BasicType.btBool:
                    return "Boolean";

                case BasicType.btLong:
                    return "Int64";

                case BasicType.btULong:
                    return "UInt64";

                case BasicType.btCurrency:
                    return "Currency";

                case BasicType.btDate:
                    return "Date";

                case BasicType.btVariant:
                    return "Variant";

                case BasicType.btComplex:
                    return "Complex";

                case BasicType.btBit:
                    return "Bit";

                case BasicType.btBSTR:
                    return "BSTR";

                case BasicType.btHresult:
                    return "Hresult";

                default:
                    return "?";
            }
        }

        //
        // Attempt to remove namespaces from types. Unfortunately, this isn't straightforward as PDB's present generic instances as "regular types with angle brackets"
        // so "s" could be something like "System::Collections::Generics::List$1<System::String>". Worse, the PDB also uses "::" to separate nested types from
        // their outer types so these represent collateral damage. And we assume that namespaces (unlike types) have reasonable names (i.e. no names with wierd characters.)
        //
        // Fortunately, this is just for diagnostic information so we don't need to let perfect be the enemy of good.
        //
        private static String RemoveNamespaces(this String s)
        {
            int firstIndexOfColonColon = s.IndexOf("::");
            if (firstIndexOfColonColon == -1)
                return s;
            int lookBack = firstIndexOfColonColon - 1;
            for (; ;)
            {
                if (lookBack < 0)
                    break;
                if (!(Char.IsLetterOrDigit(s[lookBack]) || s[lookBack] == '_'))
                    break;
                lookBack--;
            }
            s = s.Remove(lookBack + 1, firstIndexOfColonColon - lookBack + 1);
            return s.RemoveNamespaces();
        }

        /// <summary>
        /// Locate and lazily load debug info for the native app module overlapping given
        /// virtual address.
        /// </summary>
        /// <param name="ip">Instruction pointer address (code address for the lookup)</param>
        /// <param name="rva">Output VA relative to module base</param>
        private static IDiaSession GetDiaSession(IntPtr ip, out int rva)
        {
            if (ip == IntPtr.Zero)
            {
                rva = -1;
                return null;
            }

            IntPtr moduleBase = RuntimeAugments.GetOSModuleFromPointer(ip);
            if (moduleBase == IntPtr.Zero)
            {
                rva = -1;
                return null;
            }

            rva = (int)(ip.ToInt64() - moduleBase.ToInt64());

            if (s_loadedModules == null)
            {
                // Lazily create the map from module bases to debug info
                s_loadedModules = new Dictionary<IntPtr, IDiaSession>();
            }

            // Locate module index based on base address
            IDiaSession diaSession;
            if (s_loadedModules.TryGetValue(moduleBase, out diaSession))
            {
                return diaSession;
            }

            string modulePath = RuntimeAugments.TryGetFullPathToApplicationModule(moduleBase);
            if (modulePath == null)
            {
                return null;
            }

            int indexOfLastDot = modulePath.LastIndexOf('.');
            if (indexOfLastDot == -1)
            {
                return null;
            }

            IDiaDataSource diaDataSource = GetDiaDataSource();
            if (diaDataSource == null)
            {
                return null;
            }

            // Look for .pdb next to .exe / dll - if it's not there, bail.
            String pdbPath = modulePath.Substring(0, indexOfLastDot) + ".pdb";
            int hr = diaDataSource.LoadDataFromPdb(pdbPath);
            if (hr != S_OK)
            {
                return null;
            }

            hr = diaDataSource.OpenSession(out diaSession);
            if (hr != S_OK)
            {
                return null;
            }

            s_loadedModules.Add(moduleBase, diaSession);
            return diaSession;
        }

        // CoCreateInstance is not in WindowsApp_Downlevel.lib and ExactSpelling = true is required
        // to force MCG to resolve it.
        //
        // This api is a WACK violation but it cannot be changed to CoCreateInstanceApp() without breaking the stack generator altogether.
        // The toolchain will not include this library in the dependency closure as long as (1) the program is being compiled as a store app and not a console .exe
        // and (2) the /buildType switch passed to ILC is set to the "ret".
        [DllImport("api-ms-win-core-com-l1-1-0.dll", ExactSpelling = true)]
        private static extern unsafe int CoCreateInstance(Guid* rclsid, IntPtr pUnkOuter, int dwClsContext, Guid* riid, out IntPtr ppv);

        private const int S_OK = 0;
        private const int CLSCTX_INPROC = 0x3;

        /// <summary>
        /// Loaded binary module addresses.
        /// </summary>
        [ThreadStatic]
        private static Dictionary<IntPtr, IDiaSession> s_loadedModules;
    }
}

