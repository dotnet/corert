// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
namespace System.Runtime.InteropServices{

    using System;
    using System.Reflection;
    using System.Diagnostics.Contracts;

    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(false)]
    internal sealed class TypeIdentifierAttribute : Attribute
    {
        internal TypeIdentifierAttribute() { }
        internal TypeIdentifierAttribute(string scope, string identifier) { Scope_ = scope; Identifier_ = identifier; }

        internal String Scope { get { return Scope_; } }
        internal String Identifier { get { return Identifier_; } }

        internal String Scope_;
        internal String Identifier_;
    }

    // To be used on methods that sink reverse P/Invoke calls.
    // This attribute is a CoreCLR-only security measure, currently ignored by the desktop CLR.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class AllowReversePInvokeCallsAttribute : Attribute
    {
        internal AllowReversePInvokeCallsAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class DispIdAttribute : Attribute
    {
        internal int _val;
        internal DispIdAttribute(int dispId)
        {
            _val = dispId;
        }
        internal int Value { get { return _val; } }
    }

    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum ComInterfaceType
    {
        InterfaceIsDual = 0,
        InterfaceIsIUnknown = 1,
        InterfaceIsIDispatch = 2,

        [System.Runtime.InteropServices.ComVisible(false)]
        InterfaceIsIInspectable = 3,
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class InterfaceTypeAttribute : Attribute
    {
        internal ComInterfaceType _val;
        internal InterfaceTypeAttribute(ComInterfaceType interfaceType)
        {
            _val = interfaceType;
        }
        internal InterfaceTypeAttribute(short interfaceType)
        {
            _val = (ComInterfaceType)interfaceType;
        }
        internal ComInterfaceType Value { get { return _val; } }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComDefaultInterfaceAttribute : Attribute
    {
        internal Type _val;

        internal ComDefaultInterfaceAttribute(Type defaultInterface)
        {
            _val = defaultInterface;
        }

        internal Type Value { get { return _val; } }
    }

    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum ClassInterfaceType
    {
        None = 0,
        AutoDispatch = 1,
        AutoDual = 2
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ClassInterfaceAttribute : Attribute
    {
        internal ClassInterfaceType _val;
        internal ClassInterfaceAttribute(ClassInterfaceType classInterfaceType)
        {
            _val = classInterfaceType;

        }
        internal ClassInterfaceAttribute(short classInterfaceType)
        {
            _val = (ClassInterfaceType)classInterfaceType;
        }
        internal ClassInterfaceType Value { get { return _val; } }
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class TypeLibImportClassAttribute : Attribute
    {
        internal String _importClassName;
        internal TypeLibImportClassAttribute(Type importClass)
        {
            _importClassName = importClass.ToString();
        }
        internal String Value { get { return _importClassName; } }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class LCIDConversionAttribute : Attribute
    {
        internal int _val;
        internal LCIDConversionAttribute(int lcid)
        {
            _val = lcid;
        }
        internal int Value { get {return _val;} } 
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComRegisterFunctionAttribute : Attribute
    {
        internal ComRegisterFunctionAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComUnregisterFunctionAttribute : Attribute
    {
        internal ComUnregisterFunctionAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ProgIdAttribute : Attribute
    {
        internal String _val;
        internal ProgIdAttribute(String progId)
        {
            _val = progId;
        }
        internal String Value { get {return _val;} }  
    }
    
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ImportedFromTypeLibAttribute : Attribute
    {
        internal String _val;
        internal ImportedFromTypeLibAttribute(String tlbFile)
        {
            _val = tlbFile;
        }
        internal String Value { get {return _val;} }
    }

    [Obsolete("The IDispatchImplAttribute is deprecated.", false)]
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum IDispatchImplType
    {
        SystemDefinedImpl   = 0,
        InternalImpl        = 1,
        CompatibleImpl      = 2,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, Inherited = false)] 
    [Obsolete("This attribute is deprecated and will be removed in a future version.", false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class IDispatchImplAttribute : Attribute
    {
        internal IDispatchImplType _val;
        internal IDispatchImplAttribute(IDispatchImplType implType)
        {
            _val = implType;
        }
        internal IDispatchImplAttribute(short implType)
        {
            _val = (IDispatchImplType)implType;
        }
        internal IDispatchImplType Value { get {return _val;} }   
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComSourceInterfacesAttribute : Attribute
    {
        internal String _val;
        internal ComSourceInterfacesAttribute(String sourceInterfaces)
        {
            _val = sourceInterfaces;
        }
        internal ComSourceInterfacesAttribute(Type sourceInterface)
        {
            _val = sourceInterface.FullName;
        }
        internal ComSourceInterfacesAttribute(Type sourceInterface1, Type sourceInterface2)
        {
            _val = sourceInterface1.FullName + "\0" + sourceInterface2.FullName;
        }
        internal ComSourceInterfacesAttribute(Type sourceInterface1, Type sourceInterface2, Type sourceInterface3)
        {
            _val = sourceInterface1.FullName + "\0" + sourceInterface2.FullName + "\0" + sourceInterface3.FullName;
        }
        internal ComSourceInterfacesAttribute(Type sourceInterface1, Type sourceInterface2, Type sourceInterface3, Type sourceInterface4)
        {
            _val = sourceInterface1.FullName + "\0" + sourceInterface2.FullName + "\0" + sourceInterface3.FullName + "\0" + sourceInterface4.FullName;
        }
        internal String Value { get {return _val;} }  
    }    

    [AttributeUsage(AttributeTargets.All, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComConversionLossAttribute : Attribute
    {
        internal ComConversionLossAttribute()
        {
        }
    }
    
[Serializable]
[Flags()]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum TypeLibTypeFlags
    {
        FAppObject      = 0x0001,
        FCanCreate      = 0x0002,
        FLicensed       = 0x0004,
        FPreDeclId      = 0x0008,
        FHidden         = 0x0010,
        FControl        = 0x0020,
        FDual           = 0x0040,
        FNonExtensible  = 0x0080,
        FOleAutomation  = 0x0100,
        FRestricted     = 0x0200,
        FAggregatable   = 0x0400,
        FReplaceable    = 0x0800,
        FDispatchable   = 0x1000,
        FReverseBind    = 0x2000,
    }
    
[Serializable]
[Flags()]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum TypeLibFuncFlags
    {   
        FRestricted         = 0x0001,
        FSource             = 0x0002,
        FBindable           = 0x0004,
        FRequestEdit        = 0x0008,
        FDisplayBind        = 0x0010,
        FDefaultBind        = 0x0020,
        FHidden             = 0x0040,
        FUsesGetLastError   = 0x0080,
        FDefaultCollelem    = 0x0100,
        FUiDefault          = 0x0200,
        FNonBrowsable       = 0x0400,
        FReplaceable        = 0x0800,
        FImmediateBind      = 0x1000,
    }

[Serializable]
[Flags()]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum TypeLibVarFlags
    {   
        FReadOnly           = 0x0001,
        FSource             = 0x0002,
        FBindable           = 0x0004,
        FRequestEdit        = 0x0008,
        FDisplayBind        = 0x0010,
        FDefaultBind        = 0x0020,
        FHidden             = 0x0040,
        FRestricted         = 0x0080,
        FDefaultCollelem    = 0x0100,
        FUiDefault          = 0x0200,
        FNonBrowsable       = 0x0400,
        FReplaceable        = 0x0800,
        FImmediateBind      = 0x1000,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class  TypeLibTypeAttribute : Attribute
    {
        internal TypeLibTypeFlags _val;
        internal TypeLibTypeAttribute(TypeLibTypeFlags flags)
        {
            _val = flags;
        }
        internal TypeLibTypeAttribute(short flags)
        {
            _val = (TypeLibTypeFlags)flags;
        }
        internal TypeLibTypeFlags Value { get {return _val;} }    
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class TypeLibFuncAttribute : Attribute
    {
        internal TypeLibFuncFlags _val;
        internal TypeLibFuncAttribute(TypeLibFuncFlags flags)
        {
            _val = flags;
        }
        internal TypeLibFuncAttribute(short flags)
        {
            _val = (TypeLibFuncFlags)flags;
        }
        internal TypeLibFuncFlags Value { get {return _val;} }    
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class TypeLibVarAttribute : Attribute
    {
        internal TypeLibVarFlags _val;
        internal TypeLibVarAttribute(TypeLibVarFlags flags)
        {
            _val = flags;
        }
        internal TypeLibVarAttribute(short flags)
        {
            _val = (TypeLibVarFlags)flags;
        }
        internal TypeLibVarFlags Value { get {return _val;} } 
    }   

    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal enum VarEnum
    {
        VT_EMPTY = 0,
        VT_NULL = 1,
        VT_I2 = 2,
        VT_I4 = 3,
        VT_R4 = 4,
        VT_R8 = 5,
        VT_CY = 6,
        VT_DATE = 7,
        VT_BSTR = 8,
        VT_DISPATCH         = 9,
        VT_ERROR = 10,
        VT_BOOL = 11,
        VT_VARIANT = 12,
        VT_UNKNOWN = 13,
        VT_DECIMAL = 14,
        VT_I1 = 16,
        VT_UI1 = 17,
        VT_UI2 = 18,
        VT_UI4 = 19,
        VT_I8 = 20,
        VT_UI8 = 21,
        VT_INT = 22,
        VT_UINT = 23,
        VT_VOID = 24,
        VT_HRESULT = 25,
        VT_PTR = 26,
        VT_SAFEARRAY = 27,
        VT_CARRAY = 28,
        VT_USERDEFINED = 29,
        VT_LPSTR = 30,
        VT_LPWSTR = 31,
        VT_RECORD = 36,
        VT_FILETIME = 64,
        VT_BLOB = 65,
        VT_STREAM = 66,
        VT_STORAGE = 67,
        VT_STREAMED_OBJECT = 68,
        VT_STORED_OBJECT = 69,
        VT_BLOB_OBJECT = 70,
        VT_CF = 71,
        VT_CLSID = 72,
        VT_VECTOR = 0x1000,
        VT_ARRAY = 0x2000,
        VT_BYREF = 0x4000
    }

    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    // Note that this enum should remain in-sync with the CorNativeType enum in corhdr.h
    internal enum UnmanagedType
    {
        Bool = 0x2,         // 4 byte boolean value (true != 0, false == 0)

        I1 = 0x3,         // 1 byte signed value

        U1 = 0x4,         // 1 byte unsigned value

        I2 = 0x5,         // 2 byte signed value

        U2 = 0x6,         // 2 byte unsigned value

        I4 = 0x7,         // 4 byte signed value

        U4 = 0x8,         // 4 byte unsigned value

        I8 = 0x9,         // 8 byte signed value

        U8 = 0xa,         // 8 byte unsigned value

        R4 = 0xb,         // 4 byte floating point

        R8 = 0xc,         // 8 byte floating point

        Currency = 0xf,         // A currency

        BStr             = 0x13,        // OLE Unicode BSTR

        LPStr = 0x14,        // Ptr to SBCS string

        LPWStr = 0x15,        // Ptr to Unicode string

        LPTStr = 0x16,        // Ptr to OS preferred (SBCS/Unicode) string

        ByValTStr = 0x17,        // OS preferred (SBCS/Unicode) inline string (only valid in structs)

        IUnknown = 0x19,        // COM IUnknown pointer. 

        IDispatch        = 0x1a,        // COM IDispatch pointer

        Struct = 0x1b,        // Structure

        Interface        = 0x1c,        // COM interface

        SafeArray        = 0x1d,        // OLE SafeArray

        ByValArray = 0x1e,        // Array of fixed size (only valid in structs)

        SysInt = 0x1f,        // Hardware natural sized signed integer

        SysUInt = 0x20,

        VBByRefStr       = 0x22,         

        AnsiBStr         = 0x23,        // OLE BSTR containing SBCS characters

        TBStr            = 0x24,        // Ptr to OS preferred (SBCS/Unicode) BSTR

        VariantBool      = 0x25,        // OLE defined BOOLEAN (2 bytes, true == -1, false == 0)

        FunctionPtr = 0x26,        // Function pointer

        AsAny = 0x28,        // Paired with Object type and does runtime marshalling determination

        LPArray = 0x2a,        // C style array

        LPStruct = 0x2b,        // Pointer to a structure

        CustomMarshaler  = 0x2c,        

        Error = 0x2d,

        [System.Runtime.InteropServices.ComVisible(false)]
        IInspectable     = 0x2e,
        
        [System.Runtime.InteropServices.ComVisible(false)]
        HString          = 0x2f,        // Windows Runtime HSTRING
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.ReturnValue, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal unsafe sealed class MarshalAsAttribute : Attribute
    {
        internal UnmanagedType _val;
        internal MarshalAsAttribute(UnmanagedType unmanagedType)
        {
            _val = unmanagedType;
        }
        internal MarshalAsAttribute(short unmanagedType)
        {
            _val = (UnmanagedType)unmanagedType;
        }
        internal UnmanagedType Value { get { return _val; } }

        // Fields used with SubType = ByValArray and LPArray.
        // Array size =  parameter(PI) * PM + C
        public int SizeConst;                // constant C
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComImportAttribute : Attribute
    {
        internal ComImportAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct | AttributeTargets.Delegate, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class GuidAttribute : Attribute
    {
        internal String _val;
        internal GuidAttribute(String guid)
        {
            _val = guid;
        }
        internal String Value { get { return _val; } }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class PreserveSigAttribute : Attribute
    {
        internal PreserveSigAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class InAttribute : Attribute
    {
        internal InAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class OptionalAttribute : Attribute
    {
        internal OptionalAttribute()
        {
        }
    }

    [Flags]
    internal enum DllImportSearchPath
    {
        UseDllDirectoryForDependencies = 0x100,
        ApplicationDirectory = 0x200,
        UserDirectories = 0x400,
        System32 = 0x800,
        SafeDirectories = 0x1000,
        AssemblyDirectory = 0x2,
        LegacyBehavior = 0x0
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method, AllowMultiple = false)]
    [System.Runtime.InteropServices.ComVisible(false)]
    internal sealed class DefaultDllImportSearchPathsAttribute : Attribute
    {
        internal DllImportSearchPath _paths;
        internal DefaultDllImportSearchPathsAttribute(DllImportSearchPath paths)
        {
            _paths = paths;
        }

        internal DllImportSearchPath Paths { get { return _paths; } }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComAliasNameAttribute : Attribute
    {
        internal String _val;
        internal ComAliasNameAttribute(String alias)
        {
            _val = alias;
        }
        internal String Value { get {return _val;} }  
    }    

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class AutomationProxyAttribute : Attribute
    {
        internal bool _val;
        internal AutomationProxyAttribute(bool val)
        {
            _val = val;
        }
        internal bool Value { get {return _val;} }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class PrimaryInteropAssemblyAttribute : Attribute
    {
        internal int _major;
        internal int _minor;
        
        internal PrimaryInteropAssemblyAttribute(int major, int minor)
        {
            _major = major;
            _minor = minor;
        }
        
        internal int MajorVersion { get {return _major;} }
        internal int MinorVersion { get {return _minor;} }
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class CoClassAttribute : Attribute
    {
        internal Type _CoClass;

        internal CoClassAttribute(Type coClass)
        {
            _CoClass = coClass;
        }

        internal Type CoClass { get { return _CoClass; } }
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComEventInterfaceAttribute : Attribute
    {
        internal Type _SourceInterface;
        internal Type _EventProvider;
        
        internal ComEventInterfaceAttribute(Type SourceInterface, Type EventProvider)
        {
            _SourceInterface = SourceInterface;
            _EventProvider = EventProvider;
        }

        internal Type SourceInterface { get {return _SourceInterface;} }       
        internal Type EventProvider { get {return _EventProvider;} }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class TypeLibVersionAttribute : Attribute
    {
        internal int _major;
        internal int _minor;
        
        internal TypeLibVersionAttribute(int major, int minor)
        {
            _major = major;
            _minor = minor;
        }
        
        internal int MajorVersion { get {return _major;} }
        internal int MinorVersion { get {return _minor;} }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class ComCompatibleVersionAttribute : Attribute
    {
        internal int _major;
        internal int _minor;
        internal int _build;
        internal int _revision;
        
        internal ComCompatibleVersionAttribute(int major, int minor, int build, int revision)
        {
            _major = major;
            _minor = minor;
            _build = build;
            _revision = revision;
        }
        
        internal int MajorVersion { get {return _major;} }
        internal int MinorVersion { get {return _minor;} }
        internal int BuildNumber { get {return _build;} }
        internal int RevisionNumber { get {return _revision;} }
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class BestFitMappingAttribute : Attribute
    {
        internal bool _bestFitMapping;

        internal BestFitMappingAttribute(bool BestFitMapping)
        {
            _bestFitMapping = BestFitMapping;
        }

        internal bool BestFitMapping { get { return _bestFitMapping; } }
    }

    [AttributeUsage(AttributeTargets.Module, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class DefaultCharSetAttribute : Attribute
    {
        internal CharSet _CharSet;

        internal DefaultCharSetAttribute(CharSet charSet)
        {
            _CharSet = charSet;
        }

        internal CharSet CharSet { get { return _CharSet; } }
    }

    [Obsolete("This attribute has been deprecated.  Application Domains no longer respect Activation Context boundaries in IDispatch calls.", false)]
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    internal sealed class SetWin32ContextInIDispatchAttribute : Attribute
    {
        internal SetWin32ContextInIDispatchAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    [System.Runtime.InteropServices.ComVisible(false)]
    internal sealed class ManagedToNativeComInteropStubAttribute : Attribute
    {
        internal Type _classType;
        internal String _methodName;

        internal ManagedToNativeComInteropStubAttribute(Type classType, String methodName)
        {
            _classType = classType;
            _methodName = methodName;
        }

        internal Type ClassType { get { return _classType; } }
        internal String MethodName { get { return _methodName; } }
    }    

}

