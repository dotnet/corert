// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Internal.IL.Stubs;
using Internal.IL;
using Debug = System.Diagnostics.Debug;
using ILLocalVariable = Internal.IL.Stubs.ILLocalVariable;


namespace Internal.TypeSystem.Interop
{
    enum MarshallerKind
    {
        Unknown,
        BlittableValue,
        Array,
        BlittableArray,
        Bool,   // 4 byte bool
        CBool,  // 1 byte bool
        Enum,
        AnsiChar,  // Marshal char (Unicode 16bits) for byte (Ansi 8bits)
        UnicodeChar,
        AnsiCharArray,
        ByValArray,
        ByValAnsiCharArray, // Particular case of ByValArray because the conversion between wide Char and Byte need special treatment.
        AnsiString,
        UnicodeString,
        ByValUnicodeString,
        AnsiStringBuilder,
        UnicodeStringBuilder,
        FunctionPointer,
        SafeHandle,
        CriticalHandle,
        HandleRef,
        VoidReturn,
        Variant,
        Object,
        OleDateTime,
        Decimal,
        Guid,
        Struct,
        BlittableStruct,
        BlittableStructPtr,   // Additional indirection on top of blittable struct. Used by MarshalAs(LpStruct)
        Invalid
    }
    public enum MarshalDirection
    {
        Forward,    // safe-to-unsafe / managed-to-native
        Reverse,    // unsafe-to-safe / native-to-managed
    }

    public enum MarshallerType
    {
        Argument,
        Element,
        Field
    }

    // Each type of marshaller knows how to generate the marshalling code for the argument it marshals.
    // Marshallers contain method related marshalling information (which is common to all the Marshallers)
    // and also argument specific marshalling informaiton
    abstract class Marshaller
    {
        #region Instance state information
        public TypeSystemContext Context;
        public InteropStateManager InteropStateManager;
        public MarshallerKind MarshallerKind;
        public MarshallerType MarshallerType;
        public MarshalAsDescriptor MarshalAsDescriptor;
        public MarshallerKind ElementMarshallerKind;
        public int Index;
        public TypeDesc ManagedType;
        public TypeDesc ManagedParameterType;
        public PInvokeFlags PinvokeFlags;
        protected Marshaller[] Marshallers;
        private TypeDesc _nativeType;
        private TypeDesc _nativeParamType;
        

        /// <summary>
        /// Native Type of the value being marshalled
        /// For by-ref scenarios (ref T), Native Type is T
        /// </summary>
        public TypeDesc NativeType
        {
            get
            {
                if (_nativeType == null)
                {
                    _nativeType = MarshalHelpers.GetNativeTypeFromMarshallerKind(
                        ManagedType,
                        MarshallerKind,
                        ElementMarshallerKind,
                        InteropStateManager,
                        MarshalAsDescriptor);
                    Debug.Assert(_nativeType != null);
                }

                return _nativeType;
            }
        }

        /// <summary>
        /// NativeType appears in function parameters
        /// For by-ref scenarios (ref T), NativeParameterType is T*
        /// </summary>
        public TypeDesc NativeParameterType
        {
            get
            {
                if (_nativeParamType == null)
                {
                    TypeDesc nativeParamType = NativeType;
                    if (IsNativeByRef)
                        nativeParamType = nativeParamType.MakePointerType();
                    _nativeParamType = nativeParamType;
                }

                return _nativeParamType;
            }
        }

        /// <summary>
        ///  Indicates whether cleanup is necessay if this marshaller is used
        ///  as an element of an array marshaller
        /// </summary>
        internal virtual bool CleanupRequired
        {
            get
            {
                return false;
            }
        }

        public bool In;
        public bool Out;
        public bool Return;
        public bool IsManagedByRef;                     // Whether managed argument is passed by ref
        public bool IsNativeByRef;                      // Whether native argument is passed by byref
                                                        // There are special cases (such as LpStruct, and class) that 
                                                        // isNativeByRef != IsManagedByRef
        public MarshalDirection MarshalDirection;
        protected PInvokeILCodeStreams _ilCodeStreams;
        protected Home _managedHome;
        protected Home _nativeHome;
        #endregion

        enum HomeType
        {
            Arg,
            Local,
            ByRefArg,
            ByRefLocal
        }

        /// <summary>
        /// Abstraction for handling by-ref and non-by-ref locals/arguments
        /// </summary>
        internal class Home
        {
            public Home(ILLocalVariable var, TypeDesc type, bool isByRef)
            {
                _homeType = isByRef ? HomeType.ByRefLocal : HomeType.Local;
                _type = type;
                _var = var;
            }

            public Home(int argIndex, TypeDesc type, bool isByRef)
            {
                _homeType = isByRef ? HomeType.ByRefArg : HomeType.Arg;
                _type = type;
                _argIndex = argIndex;
            }

            public void LoadValue(ILCodeStream stream)
            {
                switch (_homeType)
                {
                    case HomeType.Arg:
                        stream.EmitLdArg(_argIndex);
                        break;
                    case HomeType.ByRefArg:
                        stream.EmitLdArg(_argIndex);
                        stream.EmitLdInd(_type);
                        break;
                    case HomeType.Local:
                        stream.EmitLdLoc(_var);
                        break;
                    case HomeType.ByRefLocal:
                        stream.EmitLdLoc(_var);
                        stream.EmitLdInd(_type);
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            public void LoadAddr(ILCodeStream stream)
            {
                switch (_homeType)
                {
                    case HomeType.Arg:
                        stream.EmitLdArga(_argIndex);
                        break;
                    case HomeType.ByRefArg:
                        stream.EmitLdArg(_argIndex);
                        break;
                    case HomeType.Local:
                        stream.EmitLdLoca(_var);
                        break;
                    case HomeType.ByRefLocal:
                        stream.EmitLdLoc(_var);
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            public void StoreValue(ILCodeStream stream)
            {
                switch (_homeType)
                {
                    case HomeType.Arg:
                        Debug.Assert(false, "Unexpectting setting value on non-byref arg");
                        break;
                    case HomeType.Local:
                        stream.EmitStLoc(_var);
                        break;
                    default:
                        // Storing by-ref arg/local is not supported because StInd require
                        // address to be pushed first. Instead we need to introduce a non-byref 
                        // local and propagate value as needed for by-ref arguments
                        Debug.Assert(false);
                        break;
                }
            }

            HomeType _homeType;
            TypeDesc _type;
            ILLocalVariable _var;
            int _argIndex;
        }

        #region Creation of marshallers

        /// <summary>
        /// Protected ctor
        /// Only Marshaller.CreateMarshaller can create a marshaller
        /// </summary>
        protected Marshaller()
        {
        }

        /// <summary>
        /// Create a marshaller
        /// </summary>
        /// <param name="parameterType">type of the parameter to marshal</param>
        /// <returns>The created Marshaller</returns>
        public static Marshaller CreateMarshaller(TypeDesc parameterType,
            MarshallerType marshallerType,
            MarshalAsDescriptor marshalAs,
            MarshalDirection direction,
            Marshaller[] marshallers,
            InteropStateManager interopStateManager,
            int index,
            PInvokeFlags flags,
            bool isIn,
            bool isOut,
            bool isReturn)
        {
            MarshallerKind elementMarshallerKind;
            MarshallerKind marshallerKind = MarshalHelpers.GetMarshallerKind(parameterType,
                                                marshalAs,
                                                isReturn,
                                                flags.CharSet == CharSet.Ansi,
                                                marshallerType,
                                                out elementMarshallerKind);

            TypeSystemContext context = parameterType.Context;
            // Create the marshaller based on MarshallerKind
            Marshaller marshaller = Marshaller.CreateMarshaller(marshallerKind);
            marshaller.Context = context;
            marshaller.InteropStateManager = interopStateManager;
            marshaller.MarshallerKind = marshallerKind;
            marshaller.MarshallerType = marshallerType;
            marshaller.ElementMarshallerKind = elementMarshallerKind;
            marshaller.ManagedParameterType = parameterType;
            marshaller.ManagedType = parameterType.IsByRef? parameterType.GetParameterType() : parameterType;
            marshaller.Return = isReturn;
            marshaller.IsManagedByRef = parameterType.IsByRef;
            marshaller.IsNativeByRef = marshaller.IsManagedByRef /* || isRetVal || LpStruct /etc */;
            marshaller.In = isIn;
            marshaller.MarshalDirection = direction;
            marshaller.MarshalAsDescriptor = marshalAs;
            marshaller.Marshallers = marshallers;
            marshaller.Index = index;
            marshaller.PinvokeFlags = flags;

            //
            // Desktop ignores [Out] on marshaling scenarios where they don't make sense (such as passing
            // value types and string as [out] without byref). 
            //
            if (marshaller.IsManagedByRef)
            {
                // Passing as [Out] by ref is valid
                marshaller.Out = isOut;
            }
            else
            {
                // Passing as [Out] is valid only if it is not ValueType nor string
                if (!parameterType.IsValueType && !parameterType.IsString)
                    marshaller.Out = isOut;
            }

            if (!marshaller.In && !marshaller.Out)
            {
                //
                // Rules for in/out
                // 1. ByRef args: [in]/[out] implied by default
                // 2. StringBuilder: [in, out] by default
                // 3. non-ByRef args: [In] is implied if no [In]/[Out] is specified
                //
                if (marshaller.IsManagedByRef)
                {
                    marshaller.In = true;
                    marshaller.Out = true;
                }
                else if (InteropTypes.IsStringBuilder(context, parameterType))
                {
                    marshaller.In = true;
                    marshaller.Out = true;
                }
                else
                {
                    marshaller.In = true;
                }
            }

            // For unicodestring/ansistring, ignore out when it's in
            if (!marshaller.IsManagedByRef && marshaller.In)
            {
                if (marshaller.MarshallerKind == MarshallerKind.AnsiString || marshaller.MarshallerKind == MarshallerKind.UnicodeString)
                    marshaller.Out = false;
            }

            return marshaller;
        }

        protected static Marshaller CreateMarshaller(MarshallerKind kind)
        {
            switch (kind)
            {
                case MarshallerKind.Enum:
                case MarshallerKind.BlittableValue:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.UnicodeChar:
                    return new BlittableValueMarshaller();
                case MarshallerKind.Array:
                    return new ArrayMarshaller();
                case MarshallerKind.BlittableArray:
                    return new BlittableArrayMarshaller();
                case MarshallerKind.Bool:
                    return new BooleanMarshaller();
                case MarshallerKind.AnsiString:
                    return new AnsiStringMarshaller();
                case MarshallerKind.UnicodeString:
                    return new UnicodeStringMarshaller();
                case MarshallerKind.SafeHandle:
                    return new SafeHandleMarshaller();
                case MarshallerKind.UnicodeStringBuilder:
                    return new UnicodeStringBuilderMarshaller();
                case MarshallerKind.VoidReturn:
                    return new VoidReturnMarshaller();
                case MarshallerKind.FunctionPointer:
                    return new DelegateMarshaller();
                case MarshallerKind.Struct:
                    return new StructMarshaller();
                default:
                    throw new NotSupportedException();
            }
        }

        public bool IsMarshallingRequired()
        {
            if (Out)
                return true;

            switch (MarshallerKind)
            {
                case MarshallerKind.Enum:
                case MarshallerKind.BlittableValue:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.UnicodeChar:
                case MarshallerKind.VoidReturn:
                    return false;
            }
            return true;
        }
        #endregion

        public virtual void EmitMarshallingIL(PInvokeILCodeStreams pInvokeILCodeStreams)
        {
            _ilCodeStreams = pInvokeILCodeStreams;

            switch (MarshallerType)
            {
                case MarshallerType.Argument: EmitArgumentMarshallingIL(); return;
                case MarshallerType.Element: EmitElementMarshallingIL(); return;
                case MarshallerType.Field: EmitFieldMarshallingIL(); return;
            }
        }

        public void EmitArgumentMarshallingIL()
        {
            switch (MarshalDirection)
            {
                case MarshalDirection.Forward: EmitForwardArgumentMarshallingIL(); return;
                case MarshalDirection.Reverse: EmitReverseArgumentMarshallingIL(); return;
            }
        }

        public void EmitElementMarshallingIL()
        {
            switch (MarshalDirection)
            {
                case MarshalDirection.Forward: EmitForwardElementMarshallingIL(); return;
                case MarshalDirection.Reverse: EmitReverseElementMarshallingIL(); return;
            }
        }

        public void EmitFieldMarshallingIL()
        {
            switch (MarshalDirection)
            {
                case MarshalDirection.Forward: EmitForwardFieldMarshallingIL(); return;
                case MarshalDirection.Reverse: EmitReverseFieldMarshallingIL(); return;
            }
        }

        protected virtual void EmitForwardArgumentMarshallingIL()
        {
            if (Return)
            {
                EmitMarshalReturnValueManagedToNative();
            }
            else
            {
                EmitMarshalArgumentManagedToNative();
            }
        }

        protected virtual void EmitReverseArgumentMarshallingIL()
        {
            if (Return)
            {
                EmitMarshalReturnValueNativeToManaged();
            }
            else
            {
                EmitMarshalArgumentNativeToManaged();
            }
        }

        protected virtual void EmitForwardElementMarshallingIL()
        {
            if (In)
                EmitMarshalElementManagedToNative();
            else
                EmitMarshalElementNativeToManaged();
        }
        
        protected virtual void EmitReverseElementMarshallingIL()
        {
            if (In)
                EmitMarshalElementNativeToManaged();
            else
                EmitMarshalElementManagedToNative();
        }

        protected virtual void EmitForwardFieldMarshallingIL()
        {
            if (In)
                EmitMarshalFieldManagedToNative();
            else
                EmitMarshalFieldNativeToManaged();
        }

        protected virtual void EmitReverseFieldMarshallingIL()
        {
            if (In)
                EmitMarshalFieldNativeToManaged();
            else
                EmitMarshalFieldManagedToNative();
        }


        protected virtual void EmitMarshalReturnValueManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            SetupArgumentsForReturnValueMarshalling();

            StoreNativeValue(_ilCodeStreams.ReturnValueMarshallingCodeStream);

            AllocAndTransformNativeToManaged(_ilCodeStreams.ReturnValueMarshallingCodeStream);

            LoadManagedValue(_ilCodeStreams.ReturnValueMarshallingCodeStream);
        }

        protected virtual void SetupArguments()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            if (MarshalDirection == MarshalDirection.Forward)
            {
                // Due to StInd order (address, value), we can't do the following:
                //   LoadValue
                //   StoreManagedValue (LdArg + StInd)
                // The way to work around this is to put it in a local
                if (IsManagedByRef)
                    _managedHome = new Home(emitter.NewLocal(ManagedType), ManagedType, false);
                else
                    _managedHome = new Home(Index - 1, ManagedType, false);
                _nativeHome = new Home(emitter.NewLocal(NativeType), NativeType, isByRef: false);
            }
            else
            {
                _managedHome = new Home(emitter.NewLocal(ManagedType), ManagedType, isByRef: false);
                if (IsNativeByRef)
                    _nativeHome = new Home(emitter.NewLocal(NativeType), NativeType, isByRef: false);
                else
                    _nativeHome = new Home(Index - 1, NativeType, isByRef: false);
            }
        }

        protected virtual void SetupArgumentsForElementMarshalling()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            _managedHome = new Home(emitter.NewLocal(ManagedType), ManagedType, isByRef: false);
            _nativeHome = new Home(emitter.NewLocal(NativeType), NativeType, isByRef: false);
        }

        protected virtual void SetupArgumentsForFieldMarshalling()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            
            //
            // these are temporary locals for propagating value
            //
            _managedHome = new Home(emitter.NewLocal(ManagedType), ManagedType, isByRef: false);
            _nativeHome = new Home(emitter.NewLocal(NativeType), NativeType, isByRef: false);
        }

        protected virtual void SetupArgumentsForReturnValueMarshalling()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            _managedHome = new Home(emitter.NewLocal(ManagedType), ManagedType, isByRef: false);
            _nativeHome = new Home(emitter.NewLocal(NativeType), NativeType, isByRef: false);
        }

        protected void LoadManagedValue(ILCodeStream stream)
        {
            _managedHome.LoadValue(stream);
        }

        protected void LoadManagedAddr(ILCodeStream stream)
        {
            _managedHome.LoadAddr(stream);
        }

        /// <summary>
        /// Loads the argument to be passed to managed functions
        /// In by-ref scenarios (ref T), it is &T
        /// </summary>
        protected void LoadManagedArg(ILCodeStream stream)
        {
            if (IsManagedByRef)
                _managedHome.LoadAddr(stream);
            else
                _managedHome.LoadValue(stream);
        }

        protected void StoreManagedValue(ILCodeStream stream)
        {
            _managedHome.StoreValue(stream);
        }

        protected void LoadNativeValue(ILCodeStream stream)
        {
            _nativeHome.LoadValue(stream);
        }

        /// <summary>
        /// Loads the argument to be passed to native functions
        /// In by-ref scenarios (ref T), it is T*
        /// </summary>
        protected void LoadNativeArg(ILCodeStream stream)
        {
            if (IsNativeByRef)
                _nativeHome.LoadAddr(stream);
            else
                _nativeHome.LoadValue(stream);
        }

       protected void LoadNativeAddr(ILCodeStream stream)
        {
            _nativeHome.LoadAddr(stream);
        }

        protected void StoreNativeValue(ILCodeStream stream)
        {
            _nativeHome.StoreValue(stream);
        }


        /// <summary>
        /// Propagate by-ref arg to corresponding local
        /// We can't load value + ldarg + ldind in the expected order, so 
        /// we had to use a non-by-ref local and manually propagate the value
        /// </summary>
        protected void PropagateFromByRefArg(ILCodeStream stream, Home home)
        {
            stream.EmitLdArg(Index - 1);
            stream.EmitLdInd(ManagedType);
            home.StoreValue(stream);
        }

        /// <summary>
        /// Propagate local to corresponding by-ref arg
        /// We can't load value + ldarg + ldind in the expected order, so 
        /// we had to use a non-by-ref local and manually propagate the value
        /// </summary>
        protected void PropagateToByRefArg(ILCodeStream stream, Home home)
        {
            stream.EmitLdArg(Index - 1);
            home.LoadValue(stream);
            stream.EmitStInd(ManagedType);
        }

        protected virtual void EmitMarshalArgumentManagedToNative()
        {
            SetupArguments();

            if (IsManagedByRef && In)
            {
                // Propagate byref arg to local
                PropagateFromByRefArg(_ilCodeStreams.MarshallingCodeStream, _managedHome);
            }

            //
            // marshal
            //
            if (IsManagedByRef && !In)
            {
                ReInitNativeTransform();
            }
            else
            {
                AllocAndTransformManagedToNative(_ilCodeStreams.MarshallingCodeStream);
            }

            LoadNativeArg(_ilCodeStreams.CallsiteSetupCodeStream);

            //
            // unmarshal
            //
            if (Out)
            {
                if (In)
                {
                    ClearManagedTransform(_ilCodeStreams.UnmarshallingCodestream);
                }

                if (IsManagedByRef && !In)
                {
                    AllocNativeToManaged(_ilCodeStreams.UnmarshallingCodestream);
                }

                TransformNativeToManaged(_ilCodeStreams.UnmarshallingCodestream);

                if (IsManagedByRef)
                {
                    // Propagate back to byref arguments
                    PropagateToByRefArg(_ilCodeStreams.UnmarshallingCodestream, _managedHome);
                }
            }
            EmitCleanupManagedToNative(_ilCodeStreams.UnmarshallingCodestream);
        }

        /// <summary>
        /// Reads managed parameter from _vManaged and writes the marshalled parameter in _vNative
        /// </summary>
        protected virtual void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            AllocManagedToNative(codeStream);
            if (In)
            {
                TransformManagedToNative(codeStream);
            }
        }

        protected virtual void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            AllocNativeToManaged(codeStream);
            TransformNativeToManaged(codeStream);
        }

        protected virtual void AllocManagedToNative(ILCodeStream codeStream)
        {
        }
        protected virtual void TransformManagedToNative(ILCodeStream codeStream)
        {
            LoadManagedValue(codeStream);
            StoreNativeValue(codeStream);
        }

        protected virtual void ClearManagedTransform(ILCodeStream codeStream)
        {
        }
        protected virtual void AllocNativeToManaged(ILCodeStream codeStream)
        {
        }

        protected virtual void TransformNativeToManaged(ILCodeStream codeStream)
        {
            LoadNativeValue(codeStream);
            StoreManagedValue(codeStream);
        }

        protected virtual void EmitCleanupManagedToNative(ILCodeStream codeStream)
        {
        }

        protected virtual void EmitMarshalReturnValueNativeToManaged()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            SetupArgumentsForReturnValueMarshalling();

            StoreManagedValue(_ilCodeStreams.ReturnValueMarshallingCodeStream);

            AllocAndTransformManagedToNative(_ilCodeStreams.ReturnValueMarshallingCodeStream);

            LoadNativeValue(_ilCodeStreams.ReturnValueMarshallingCodeStream);
        }

        protected virtual void EmitMarshalArgumentNativeToManaged()
        {
            SetupArguments();

            if (IsNativeByRef && In)
            {
                // Propagate byref arg to local
                PropagateFromByRefArg(_ilCodeStreams.MarshallingCodeStream, _nativeHome);
            }

            AllocAndTransformNativeToManaged(_ilCodeStreams.MarshallingCodeStream);

            LoadManagedArg(_ilCodeStreams.CallsiteSetupCodeStream);

            if (Out)
            {
                TransformManagedToNative(_ilCodeStreams.UnmarshallingCodestream);
                
                if (IsNativeByRef)
                {
                    // Propagate back to byref arguments
                    PropagateToByRefArg(_ilCodeStreams.UnmarshallingCodestream, _nativeHome);
                }
            }
        }

        protected virtual void EmitMarshalElementManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            SetupArgumentsForElementMarshalling();

            StoreManagedValue(marshallingCodeStream);

            // marshal
            AllocAndTransformManagedToNative(marshallingCodeStream);

            LoadNativeValue(marshallingCodeStream);
        }

        protected virtual void EmitMarshalElementNativeToManaged()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream codeStream;
            if (Return)
                codeStream = _ilCodeStreams.ReturnValueMarshallingCodeStream;
            else
                codeStream = _ilCodeStreams.UnmarshallingCodestream;

            SetupArgumentsForElementMarshalling();

            StoreNativeValue(codeStream);

            // unmarshal
            AllocAndTransformNativeToManaged(codeStream);
            LoadManagedValue(codeStream);
        }

        protected virtual void EmitMarshalFieldManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            SetupArgumentsForFieldMarshalling();
            //
            // For field marshalling we expect the value of the field is already loaded
            // in the stack. 
            //
            StoreManagedValue(marshallingCodeStream);

            // marshal
            AllocAndTransformManagedToNative(marshallingCodeStream);

            LoadNativeValue(marshallingCodeStream);
        }

        protected virtual void EmitMarshalFieldNativeToManaged()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream codeStream = _ilCodeStreams.MarshallingCodeStream;

            SetupArgumentsForFieldMarshalling();

            StoreNativeValue(codeStream);

            // unmarshal
            AllocAndTransformNativeToManaged(codeStream);
            LoadManagedValue(codeStream);
        }

        protected virtual void ReInitNativeTransform()
        {
        }

        internal virtual void EmitElementCleanup(ILCodeStream codestream, ILEmitter emitter)
        {
        }
    }

    class VoidReturnMarshaller : Marshaller
    {
        protected override void EmitMarshalReturnValueManagedToNative()
        {
        }
        protected override void EmitMarshalReturnValueNativeToManaged()
        {
        }
    }

    class BlittableValueMarshaller : Marshaller 
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            if (IsNativeByRef && MarshalDirection == MarshalDirection.Forward)
            {
                ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
                ILEmitter emitter = _ilCodeStreams.Emitter;
                ILLocalVariable native = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));

                ILLocalVariable vPinnedByRef = emitter.NewLocal(ManagedParameterType, true);
                marshallingCodeStream.EmitLdArg(Index - 1);
                marshallingCodeStream.EmitStLoc(vPinnedByRef);
                marshallingCodeStream.EmitLdLoc(vPinnedByRef);
                marshallingCodeStream.Emit(ILOpcode.conv_i);
                marshallingCodeStream.EmitStLoc(native);
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoc(native);
            }
            else
            {
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdArg(Index - 1);
            }
        }

        protected override void EmitMarshalArgumentNativeToManaged()
        {
            if (Out)
            {
                base.EmitMarshalArgumentNativeToManaged();
            }
            else
            {
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdArg(Index - 1);
            }
        }
    }

    class ArrayMarshaller : Marshaller
    {
        
        private Marshaller _elementMarshaller;

        protected Marshaller GetElementMarshaller(MarshalDirection direction)
        {
            if (_elementMarshaller == null)
            {
                _elementMarshaller = CreateMarshaller(ElementMarshallerKind);
                _elementMarshaller.MarshallerKind = ElementMarshallerKind;
                _elementMarshaller.MarshallerType = MarshallerType.Element;
                _elementMarshaller.Return = Return;
                _elementMarshaller.Context = Context;
                _elementMarshaller.ManagedType = ((ArrayType)ManagedType).ElementType;
            }
            _elementMarshaller.In = (direction == MarshalDirection);
            _elementMarshaller.Out = !In;
            _elementMarshaller.MarshalDirection = MarshalDirection;

            return _elementMarshaller;
        }

        protected virtual void EmitElementCount(ILCodeStream codeStream, MarshalDirection direction)
        {
            if (direction == MarshalDirection.Forward)
            {
                // In forward direction we skip whatever is passed through SizeParamIndex, becaus the
                // size of the managed array is already known
                LoadManagedValue(codeStream);
                codeStream.Emit(ILOpcode.ldlen);
                codeStream.Emit(ILOpcode.conv_i4);

            }
            else if (MarshalDirection == MarshalDirection.Forward
                    && MarshallerType == MarshallerType.Argument
                    && !Return
                    && !IsManagedByRef)
            {
                EmitElementCount(codeStream, MarshalDirection.Forward);
            }
            else
            { 

                uint? sizeParamIndex = MarshalAsDescriptor.SizeParamIndex;
                uint? sizeConst = MarshalAsDescriptor.SizeConst;

                if (sizeConst.HasValue)
                {
                    codeStream.EmitLdc((int)sizeConst.Value);
                }

                if (sizeParamIndex.HasValue)
                {
                    uint index = sizeParamIndex.Value;

                    if (index < 0 || index >= Marshallers.Length -1)
                    {
                        throw new InvalidProgramException("Invalid SizeParamIndex, must be between 0 and parameter count");
                    }

                    //zero-th index is for return type
                    index++;
                    var indexType = Marshallers[index].ManagedType;
                    switch (indexType.Category)
                    {
                        case TypeFlags.Byte:
                        case TypeFlags.SByte:
                        case TypeFlags.Int16:
                        case TypeFlags.UInt16:
                        case TypeFlags.Int32:
                        case TypeFlags.UInt32:
                        case TypeFlags.Int64:
                        case TypeFlags.UInt64:
                            break;
                        default:
                            throw new InvalidProgramException("Invalid SizeParamIndex, parameter must be  of type int/uint");
                    }

                    // @TODO - We can use LoadManagedValue, but that requires byref arg propagation happen in a special setup stream
                    // otherwise there is an ordering issue
                    codeStream.EmitLdArg(Marshallers[index].Index - 1);
                    if (Marshallers[index].IsManagedByRef)
                        codeStream.EmitLdInd(indexType);

                    if (sizeConst.HasValue)
                        codeStream.Emit(ILOpcode.add);
                }

                if (!sizeConst.HasValue && !sizeParamIndex.HasValue)
                {
                    // if neither sizeConst or sizeParamIndex are specified, default to 1
                    codeStream.EmitLdc(1);
                }
            }
        }

        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var arrayType = (ArrayType)ManagedType;
            var elementType = arrayType.ElementType;

            ILLocalVariable vSizeOf = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));
            ILLocalVariable vLength = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable vNativeTemp = emitter.NewLocal(NativeType);

            ILCodeLabel lNullArray = emitter.NewCodeLabel();
            ILCodeLabel lRangeCheck = emitter.NewCodeLabel();
            ILCodeLabel lLoopHeader = emitter.NewCodeLabel();

            LoadNativeAddr(codeStream);
            codeStream.Emit(ILOpcode.initobj, emitter.NewToken(NativeType));
            
            // Check for null array
            LoadManagedValue(codeStream);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);

            // allocate memory
            // nativeParameter = (byte**)CoTaskMemAllocAndZeroMemory((IntPtr)(checked(manageParameter.Length * sizeof(byte*))));

            EmitElementCount(codeStream, MarshalDirection.Forward);
            codeStream.Emit(ILOpcode.dup);
            codeStream.EmitStLoc(vLength);

            TypeDesc nativeType = ((PointerType)NativeType).ParameterType;
            if (elementType.IsPrimitive)
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(elementType));
            else
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(Context.GetWellKnownType(WellKnownType.IntPtr)));

            codeStream.Emit(ILOpcode.dup);
            codeStream.EmitStLoc(vSizeOf);
            codeStream.Emit(ILOpcode.mul_ovf);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                                Context.SystemModule.
                                    GetKnownType("System", "IntPtr").
                                        GetKnownMethod("op_Explicit", null)));

            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                                Context.GetHelperEntryPoint("InteropHelpers", "CoTaskMemAllocAndZeroMemory")));
            StoreNativeValue(codeStream);

            // initialize content
            var vIndex = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));

            LoadNativeValue(codeStream);
            codeStream.EmitStLoc(vNativeTemp);
            codeStream.EmitLdc(0);
            codeStream.EmitStLoc(vIndex);
            codeStream.Emit(ILOpcode.br, lRangeCheck);

            codeStream.EmitLabel(lLoopHeader);
            codeStream.EmitLdLoc(vNativeTemp);

            LoadManagedValue(codeStream);
            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdElem(elementType);
            // generate marshalling IL for the element
            GetElementMarshaller(MarshalDirection.Forward).EmitMarshallingIL(_ilCodeStreams);

            codeStream.EmitStInd(elementType);
            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdc(1);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vIndex);
            codeStream.EmitLdLoc(vNativeTemp);
            codeStream.EmitLdLoc(vSizeOf);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vNativeTemp);

            codeStream.EmitLabel(lRangeCheck);

            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdLoc(vLength);
            codeStream.Emit(ILOpcode.clt);
            codeStream.Emit(ILOpcode.brtrue, lLoopHeader);


            codeStream.EmitLabel(lNullArray);
        }        

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ArrayType arrayType = (ArrayType)ManagedType;

            var elementType = arrayType.ElementType;

            ILLocalVariable vSizeOf = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable vLength = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));

            ILCodeLabel lRangeCheck = emitter.NewCodeLabel();
            ILCodeLabel lLoopHeader = emitter.NewCodeLabel();

            EmitElementCount(codeStream, MarshalDirection.Reverse);

            codeStream.EmitStLoc(vLength);

            if (elementType.IsPrimitive)
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(elementType));
            else 
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(Context.GetWellKnownType(WellKnownType.IntPtr)));

            codeStream.EmitStLoc(vSizeOf);

            var vIndex = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable vNativeTemp = emitter.NewLocal(NativeType);

            LoadNativeValue(codeStream);
            codeStream.EmitStLoc(vNativeTemp);
            codeStream.EmitLdc(0);
            codeStream.EmitStLoc(vIndex);
            codeStream.Emit(ILOpcode.br, lRangeCheck);


            codeStream.EmitLabel(lLoopHeader);

            LoadManagedValue(codeStream);

            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdLoc(vNativeTemp);

            codeStream.EmitLdInd(elementType);
 
            // generate marshalling IL for the element
            GetElementMarshaller(MarshalDirection.Reverse).EmitMarshallingIL(_ilCodeStreams);

            codeStream.EmitStElem(elementType);

            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdc(1);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vIndex);
            codeStream.EmitLdLoc(vNativeTemp);
            codeStream.EmitLdLoc(vSizeOf);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vNativeTemp);


            codeStream.EmitLabel(lRangeCheck);
            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdLoc(vLength);
            codeStream.Emit(ILOpcode.clt);
            codeStream.Emit(ILOpcode.brtrue, lLoopHeader);
        }

        protected override void AllocNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ArrayType arrayType = (ArrayType) ManagedType;

            var elementType = arrayType.ElementType;
            EmitElementCount(codeStream, MarshalDirection.Reverse);
            codeStream.Emit(ILOpcode.newarr, emitter.NewToken(elementType));
            StoreManagedValue(codeStream);
        }

        protected override void EmitCleanupManagedToNative(ILCodeStream codeStream)
        {
            Marshaller elementMarshaller = GetElementMarshaller(MarshalDirection.Forward);

            // generate cleanuo code only if it is necessary
            if (elementMarshaller.CleanupRequired)
            {
                //
                //     for (index=0; index< array.length; index++)
                //         Cleanup(array[i]);
                //
                ILEmitter emitter = _ilCodeStreams.Emitter;
                var vIndex = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));
                ILLocalVariable vLength = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));

                ILCodeLabel lRangeCheck = emitter.NewCodeLabel();
                ILCodeLabel lLoopHeader = emitter.NewCodeLabel();
                ILLocalVariable vSizeOf = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));

                ArrayType arrayType = (ArrayType)ManagedType;

                var elementType = arrayType.ElementType;

                 // calculate sizeof(array[i])
                if (elementType.IsPrimitive)
                    codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(elementType));
                else
                    codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(Context.GetWellKnownType(WellKnownType.IntPtr)));

                codeStream.EmitStLoc(vSizeOf);


                // calculate array.length
                EmitElementCount(codeStream, MarshalDirection.Forward);
                codeStream.EmitStLoc(vLength);

                // load native value
                ILLocalVariable vNativeTemp = emitter.NewLocal(NativeType);
                LoadNativeValue(codeStream);
                codeStream.EmitStLoc(vNativeTemp);

                // index = 0
                codeStream.EmitLdc(0);
                codeStream.EmitStLoc(vIndex);
                codeStream.Emit(ILOpcode.br, lRangeCheck);

                codeStream.EmitLabel(lLoopHeader);
                codeStream.EmitLdLoc(vNativeTemp);
                codeStream.EmitLdInd(elementType);
                // generate cleanup code for this element
                elementMarshaller.EmitElementCleanup(codeStream, emitter);

                codeStream.EmitLdLoc(vIndex);
                codeStream.EmitLdc(1);
                codeStream.Emit(ILOpcode.add);
                codeStream.EmitStLoc(vIndex);
                codeStream.EmitLdLoc(vNativeTemp);
                codeStream.EmitLdLoc(vSizeOf);
                codeStream.Emit(ILOpcode.add);
                codeStream.EmitStLoc(vNativeTemp);

                codeStream.EmitLabel(lRangeCheck);

                codeStream.EmitLdLoc(vIndex);
                codeStream.EmitLdLoc(vLength);
                codeStream.Emit(ILOpcode.clt);
                codeStream.Emit(ILOpcode.brtrue, lLoopHeader);
            }

            LoadNativeValue(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                                Context.GetHelperEntryPoint("InteropHelpers", "CoTaskMemFree")));
        }
    }

    class BlittableArrayMarshaller : ArrayMarshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var arrayType = (ArrayType)ManagedType;
            Debug.Assert(arrayType.IsSzArray);

            ILLocalVariable vPinnedFirstElement = emitter.NewLocal(arrayType.ParameterType.MakeByRefType(), true);
            ILCodeLabel lNullArray = emitter.NewCodeLabel();

            // Check for null array, or 0 element array.
            LoadManagedValue(codeStream);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);
            LoadManagedValue(codeStream);
            codeStream.Emit(ILOpcode.ldlen);
            codeStream.Emit(ILOpcode.conv_i4);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);

            // Array has elements.
            LoadManagedValue(codeStream);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ldelema, emitter.NewToken(arrayType.ElementType));
            codeStream.EmitStLoc(vPinnedFirstElement);

            // Fall through. If array didn't have elements, vPinnedFirstElement is zeroinit.
            codeStream.EmitLabel(lNullArray);
            codeStream.EmitLdLoc(vPinnedFirstElement);
            codeStream.Emit(ILOpcode.conv_i);
            StoreNativeValue(codeStream);
        }

        protected override void ReInitNativeTransform()
        {
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.conv_u);
            StoreNativeValue(marshallingCodeStream);
        }

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            if (IsManagedByRef && !In)
                base.TransformNativeToManaged(codeStream);
        }
        protected override void EmitCleanupManagedToNative(ILCodeStream codeStream)
        {
            if (IsManagedByRef && !In)
                base.EmitCleanupManagedToNative(codeStream);
        }
    }

    class BooleanMarshaller : Marshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            LoadManagedValue(codeStream);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            StoreNativeValue(codeStream);
        }

        protected override void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            LoadNativeValue(codeStream);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            StoreManagedValue(codeStream);
        }
    }

    class UnicodeStringMarshaller : Marshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;

            //
            // Unicode marshalling. Pin the string and push a pointer to the first character on the stack.
            //
            TypeDesc stringType = Context.GetWellKnownType(WellKnownType.String);

            ILLocalVariable vPinnedString = emitter.NewLocal(stringType, true);
            ILCodeLabel lNullString = emitter.NewCodeLabel();

            LoadManagedValue(codeStream);
            codeStream.EmitStLoc(vPinnedString);
            codeStream.EmitLdLoc(vPinnedString);

            codeStream.Emit(ILOpcode.conv_i);
            codeStream.Emit(ILOpcode.dup);

            // Marshalling a null string?
            codeStream.Emit(ILOpcode.brfalse, lNullString);

            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                Context.SystemModule.
                    GetKnownType("System.Runtime.CompilerServices", "RuntimeHelpers").
                        GetKnownMethod("get_OffsetToStringData", null)));

            codeStream.Emit(ILOpcode.add);

            codeStream.EmitLabel(lNullString);
            StoreNativeValue(codeStream);
        }
    }
  

    class AnsiStringMarshaller : Marshaller
    {
        internal override bool CleanupRequired
        {
            get
            {
                return true;
            }
        }

        internal  override void EmitElementCleanup(ILCodeStream codeStream, ILEmitter emitter)
        {
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                                Context.GetHelperEntryPoint("InteropHelpers", "CoTaskMemFree")));
        }

        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;

            //
            // ANSI marshalling. Allocate a byte array, copy characters
            //
            var stringToAnsi = Context.GetHelperEntryPoint("InteropHelpers", "StringToAnsi");
            LoadManagedValue(codeStream);

            codeStream.Emit(ILOpcode.call, emitter.NewToken(stringToAnsi));

            StoreNativeValue(codeStream);
        }

        protected override void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var ansiToString = Context.GetHelperEntryPoint("InteropHelpers", "AnsiStringToString");
            LoadNativeValue(codeStream);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(ansiToString));
            StoreManagedValue(codeStream);
        }

        protected override void EmitCleanupManagedToNative(ILCodeStream codeStream)
        {
            LoadNativeValue(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                                Context.GetHelperEntryPoint("InteropHelpers", "CoTaskMemFree")));
        }

    }

    class SafeHandleMarshaller : Marshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILCodeStream callsiteCodeStream = _ilCodeStreams.CallsiteSetupCodeStream;
            ILCodeStream unmarshallingCodeStream = _ilCodeStreams.UnmarshallingCodestream;

            SetupArguments();

            if (IsManagedByRef && In)
            {
                PropagateFromByRefArg(marshallingCodeStream, _managedHome);
            }

            // we don't support [IN,OUT] together yet, either IN or OUT
             Debug.Assert(!(Out && In));

            var safeHandleType = InteropTypes.GetSafeHandleType(Context);

            if (Out && IsManagedByRef)
            {
                // 1) If this is an output parameter we need to preallocate a SafeHandle to wrap the new native handle value. We
                //    must allocate this before the native call to avoid a failure point when we already have a native resource
                //    allocated. We must allocate a new SafeHandle even if we have one on input since both input and output native
                //    handles need to be tracked and released by a SafeHandle.
                // 2) Initialize a local IntPtr that will be passed to the native call. 
                // 3) After the native call, the new handle value is written into the output SafeHandle and that SafeHandle
                //    is propagated back to the caller.
                var vOutValue = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));
                var vSafeHandle = emitter.NewLocal(ManagedType);
                marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(ManagedType.GetParameterlessConstructor()));
                marshallingCodeStream.EmitStLoc(vSafeHandle);
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoca(vOutValue);

                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                unmarshallingCodeStream.EmitLdLoc(vOutValue);
                unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                   safeHandleType.GetKnownMethod("SetHandle", null)));

                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                StoreManagedValue(unmarshallingCodeStream);

                PropagateToByRefArg(unmarshallingCodeStream, _managedHome);
            }
            else
            {
                var vAddRefed = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Boolean));
                LoadManagedValue(marshallingCodeStream);
                marshallingCodeStream.EmitLdLoca(vAddRefed);
                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousAddRef", null)));

                LoadManagedValue(marshallingCodeStream);
                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousGetHandle", null)));
                StoreNativeValue(marshallingCodeStream);

                // TODO: This should be inside finally block and only executed it the handle was addrefed
                LoadManagedValue(unmarshallingCodeStream);
                unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousRelease", null)));

                LoadNativeArg(_ilCodeStreams.CallsiteSetupCodeStream);
            }

        }

        protected override void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(ManagedType.GetParameterlessConstructor()));
            StoreManagedValue(marshallingCodeStream);

            LoadManagedValue(codeStream);
            LoadNativeValue(codeStream);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
            InteropTypes.GetSafeHandleType(Context).GetKnownMethod("SetHandle", null)));
        }
    }

    class UnicodeStringBuilderMarshaller : BlittableArrayMarshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            // TODO: Handles [out] marshalling only for now

            var stringBuilderType = Context.SystemModule.GetKnownType("System.Text", "StringBuilder");

            LoadManagedValue(codeStream);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                Context.GetHelperEntryPoint("InteropHelpers", "GetEmptyStringBuilderBuffer")));

            // back up the managed types 
            TypeDesc tempType = ManagedType;
            Home vTemp = _managedHome;

            ManagedType = Context.GetWellKnownType(WellKnownType.Char).MakeArrayType();
            _managedHome = new Home(emitter.NewLocal(ManagedType), ManagedType, isByRef: false);
            StoreManagedValue(codeStream);

            // Call the Array marshaller MarshalArgument
            base.AllocAndTransformManagedToNative(codeStream);

            //restore the types
            ManagedType = tempType;
            _managedHome = vTemp;
        }

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            LoadManagedValue(codeStream);
            LoadNativeValue(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                InteropTypes.GetStringBuilder(Context).GetKnownMethod("ReplaceBuffer", null)));
        }
    }

    class DelegateMarshaller : Marshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            LoadManagedValue(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
            Context.GetHelperEntryPoint("InteropHelpers", "GetStubForPInvokeDelegate")));
            StoreNativeValue(codeStream);
        }
    }

    class StructMarshaller : Marshaller
    {

        protected override void AllocManagedToNative(ILCodeStream codeStream)
        {
            LoadNativeAddr(codeStream);
            codeStream.Emit(ILOpcode.initobj, _ilCodeStreams.Emitter.NewToken(NativeType));
        }

        protected override void TransformManagedToNative(ILCodeStream codeStream)
        {
            LoadManagedAddr(codeStream);
            LoadNativeAddr(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                InteropStateManager.GetStructMarshallingManagedToNativeThunk(ManagedType)));
        }

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            LoadManagedAddr(codeStream);
            LoadNativeAddr(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                InteropStateManager.GetStructMarshallingNativeToManagedThunk(ManagedType)));
        }

        protected override void EmitCleanupManagedToNative(ILCodeStream codeStream)
        {
            LoadNativeAddr(codeStream);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                InteropStateManager.GetStructMarshallingCleanupThunk(ManagedType)));
        }
    }

}
