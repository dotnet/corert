// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Internal.JitInterface
{
// CorInfoHelpFunc defines the set of helpers (accessed via the ICorDynamicInfo::getHelperFtn())
// These helpers can be called by native code which executes in the runtime.
// Compilers can emit calls to these helpers.
//
// The signatures of the helpers are below (see RuntimeHelperArgumentCheck)
//
//  NOTE: CorInfoHelpFunc is closely related to MdilHelpFunc!!!
//  
//  - changing the order of jit helper ordinals works fine
//  However:
//  - adding a jit helpers requires usually the addition of a corresponding MdilHelper
//  - removing a jit helper (or changing its arguments) should be done only sparingly
//    and needs discussion with an "MDIL person".
//  Please have a look also at the comment prepending the definition of MdilHelpFunc
//

    public enum CorInfoHelpFunc
    {
        CORINFO_HELP_UNDEF,         // invalid value. This should never be used

        /* Arithmetic helpers */

        CORINFO_HELP_DIV,           // For the ARM 32-bit integer divide uses a helper call :-(
        CORINFO_HELP_MOD,
        CORINFO_HELP_UDIV,
        CORINFO_HELP_UMOD,

        CORINFO_HELP_LLSH,
        CORINFO_HELP_LRSH,
        CORINFO_HELP_LRSZ,
        CORINFO_HELP_LMUL,
        CORINFO_HELP_LMUL_OVF,
        CORINFO_HELP_ULMUL_OVF,
        CORINFO_HELP_LDIV,
        CORINFO_HELP_LMOD,
        CORINFO_HELP_ULDIV,
        CORINFO_HELP_ULMOD,
        CORINFO_HELP_LNG2DBL,               // Convert a signed int64 to a double
        CORINFO_HELP_ULNG2DBL,              // Convert a unsigned int64 to a double
        CORINFO_HELP_DBL2INT,
        CORINFO_HELP_DBL2INT_OVF,
        CORINFO_HELP_DBL2LNG,
        CORINFO_HELP_DBL2LNG_OVF,
        CORINFO_HELP_DBL2UINT,
        CORINFO_HELP_DBL2UINT_OVF,
        CORINFO_HELP_DBL2ULNG,
        CORINFO_HELP_DBL2ULNG_OVF,
        CORINFO_HELP_FLTREM,
        CORINFO_HELP_DBLREM,
        CORINFO_HELP_FLTROUND,
        CORINFO_HELP_DBLROUND,

        /* Allocating a new object. Always use ICorClassInfo::getNewHelper() to decide 
           which is the right helper to use to allocate an object of a given type. */

        CORINFO_HELP_NEW_CROSSCONTEXT,  // cross context new object
        CORINFO_HELP_NEWFAST,
        CORINFO_HELP_NEWSFAST,          // allocator for small, non-finalizer, non-array object
        CORINFO_HELP_NEWSFAST_ALIGN8,   // allocator for small, non-finalizer, non-array object, 8 byte aligned
        CORINFO_HELP_NEW_MDARR,         // multi-dim array helper (with or without lower bounds)
        CORINFO_HELP_NEWARR_1_DIRECT,   // helper for any one dimensional array creation
        CORINFO_HELP_NEWARR_1_OBJ,      // optimized 1-D object arrays
        CORINFO_HELP_NEWARR_1_VC,       // optimized 1-D value class arrays
        CORINFO_HELP_NEWARR_1_ALIGN8,   // like VC, but aligns the array start

        CORINFO_HELP_STRCNS,            // create a new string literal
    #if !RYUJIT_CTPBUILD
        CORINFO_HELP_STRCNS_CURRENT_MODULE, // create a new string literal from the current module (used by NGen code)
    #endif
        /* Object model */

        CORINFO_HELP_INITCLASS,         // Initialize class if not already initialized
        CORINFO_HELP_INITINSTCLASS,     // Initialize class for instantiated type

        // Use ICorClassInfo::getCastingHelper to determine
        // the right helper to use

        CORINFO_HELP_ISINSTANCEOFINTERFACE, // Optimized helper for interfaces
        CORINFO_HELP_ISINSTANCEOFARRAY,  // Optimized helper for arrays
        CORINFO_HELP_ISINSTANCEOFCLASS, // Optimized helper for classes
        CORINFO_HELP_ISINSTANCEOFANY,   // Slow helper for any type

        CORINFO_HELP_CHKCASTINTERFACE,
        CORINFO_HELP_CHKCASTARRAY,
        CORINFO_HELP_CHKCASTCLASS,
        CORINFO_HELP_CHKCASTANY,
        CORINFO_HELP_CHKCASTCLASS_SPECIAL, // Optimized helper for classes. Assumes that the trivial cases 
                                        // has been taken care of by the inlined check

        CORINFO_HELP_BOX,
        CORINFO_HELP_BOX_NULLABLE,      // special form of boxing for Nullable<T>
        CORINFO_HELP_UNBOX,
        CORINFO_HELP_UNBOX_NULLABLE,    // special form of unboxing for Nullable<T>
        CORINFO_HELP_GETREFANY,         // Extract the byref from a TypedReference, checking that it is the expected type

        CORINFO_HELP_ARRADDR_ST,        // assign to element of object array with type-checking
        CORINFO_HELP_LDELEMA_REF,       // does a precise type comparision and returns address

        /* Exceptions */

        CORINFO_HELP_THROW,             // Throw an exception object
        CORINFO_HELP_RETHROW,           // Rethrow the currently active exception
        CORINFO_HELP_USER_BREAKPOINT,   // For a user program to break to the debugger
        CORINFO_HELP_RNGCHKFAIL,        // array bounds check failed
        CORINFO_HELP_OVERFLOW,          // throw an overflow exception
        CORINFO_HELP_THROWDIVZERO,      // throw a divide by zero exception
    #if !RYUJIT_CTPBUILD
        CORINFO_HELP_THROWNULLREF,      // throw a null reference exception
    #endif

        CORINFO_HELP_INTERNALTHROW,     // Support for really fast jit
        CORINFO_HELP_VERIFICATION,      // Throw a VerificationException
        CORINFO_HELP_SEC_UNMGDCODE_EXCPT, // throw a security unmanaged code exception
        CORINFO_HELP_FAIL_FAST,         // Kill the process avoiding any exceptions or stack and data dependencies (use for GuardStack unsafe buffer checks)

        CORINFO_HELP_METHOD_ACCESS_EXCEPTION,//Throw an access exception due to a failed member/class access check.
        CORINFO_HELP_FIELD_ACCESS_EXCEPTION,
        CORINFO_HELP_CLASS_ACCESS_EXCEPTION,

        CORINFO_HELP_ENDCATCH,          // call back into the EE at the end of a catch block

        /* Synchronization */

        CORINFO_HELP_MON_ENTER,
        CORINFO_HELP_MON_EXIT,
        CORINFO_HELP_MON_ENTER_STATIC,
        CORINFO_HELP_MON_EXIT_STATIC,

        CORINFO_HELP_GETCLASSFROMMETHODPARAM, // Given a generics method handle, returns a class handle
        CORINFO_HELP_GETSYNCFROMCLASSHANDLE,  // Given a generics class handle, returns the sync monitor 
                                              // in its ManagedClassObject

        /* Security callout support */

        CORINFO_HELP_SECURITY_PROLOG,   // Required if CORINFO_FLG_SECURITYCHECK is set, or CORINFO_FLG_NOSECURITYWRAP is not set
        CORINFO_HELP_SECURITY_PROLOG_FRAMED, // Slow version of CORINFO_HELP_SECURITY_PROLOG. Used for instrumentation.

        CORINFO_HELP_METHOD_ACCESS_CHECK, // Callouts to runtime security access checks
        CORINFO_HELP_FIELD_ACCESS_CHECK,
        CORINFO_HELP_CLASS_ACCESS_CHECK,

        CORINFO_HELP_DELEGATE_SECURITY_CHECK, // Callout to delegate security transparency check

        /* Verification runtime callout support */

        CORINFO_HELP_VERIFICATION_RUNTIME_CHECK, // Do a Demand for UnmanagedCode permission at runtime

        /* GC support */

        CORINFO_HELP_STOP_FOR_GC,       // Call GC (force a GC)
        CORINFO_HELP_POLL_GC,           // Ask GC if it wants to collect

        CORINFO_HELP_STRESS_GC,         // Force a GC, but then update the JITTED code to be a noop call
        CORINFO_HELP_CHECK_OBJ,         // confirm that ECX is a valid object pointer (debugging only)

        /* GC Write barrier support */

        CORINFO_HELP_ASSIGN_REF,        // universal helpers with F_CALL_CONV calling convention
        CORINFO_HELP_CHECKED_ASSIGN_REF,
        CORINFO_HELP_ASSIGN_REF_ENSURE_NONHEAP,  // Do the store, and ensure that the target was not in the heap.

        CORINFO_HELP_ASSIGN_BYREF,
        CORINFO_HELP_ASSIGN_STRUCT,


        /* Accessing fields */

        // For COM object support (using COM get/set routines to update object)
        // and EnC and cross-context support
        CORINFO_HELP_GETFIELD8,
        CORINFO_HELP_SETFIELD8,
        CORINFO_HELP_GETFIELD16,
        CORINFO_HELP_SETFIELD16,
        CORINFO_HELP_GETFIELD32,
        CORINFO_HELP_SETFIELD32,
        CORINFO_HELP_GETFIELD64,
        CORINFO_HELP_SETFIELD64,
        CORINFO_HELP_GETFIELDOBJ,
        CORINFO_HELP_SETFIELDOBJ,
        CORINFO_HELP_GETFIELDSTRUCT,
        CORINFO_HELP_SETFIELDSTRUCT,
        CORINFO_HELP_GETFIELDFLOAT,
        CORINFO_HELP_SETFIELDFLOAT,
        CORINFO_HELP_GETFIELDDOUBLE,
        CORINFO_HELP_SETFIELDDOUBLE,

        CORINFO_HELP_GETFIELDADDR,

        CORINFO_HELP_GETSTATICFIELDADDR_CONTEXT,    // Helper for context-static fields
        CORINFO_HELP_GETSTATICFIELDADDR_TLS,        // Helper for PE TLS fields

        // There are a variety of specialized helpers for accessing static fields. The JIT should use 
        // ICorClassInfo::getSharedStaticsOrCCtorHelper to determine which helper to use

        // Helpers for regular statics
        CORINFO_HELP_GETGENERICS_GCSTATIC_BASE,
        CORINFO_HELP_GETGENERICS_NONGCSTATIC_BASE,
        CORINFO_HELP_GETSHARED_GCSTATIC_BASE,
        CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE,
        CORINFO_HELP_GETSHARED_GCSTATIC_BASE_NOCTOR,
        CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_NOCTOR,
        CORINFO_HELP_GETSHARED_GCSTATIC_BASE_DYNAMICCLASS,
        CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_DYNAMICCLASS,
        // Helper to class initialize shared generic with dynamicclass, but not get static field address
        CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS,

        // Helpers for thread statics
        CORINFO_HELP_GETGENERICS_GCTHREADSTATIC_BASE,
        CORINFO_HELP_GETGENERICS_NONGCTHREADSTATIC_BASE,
        CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE,
        CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE,
        CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR,
        CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR,
        CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_DYNAMICCLASS,
        CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_DYNAMICCLASS,

        /* Debugger */

        CORINFO_HELP_DBG_IS_JUST_MY_CODE,    // Check if this is "JustMyCode" and needs to be stepped through.

        /* Profiling enter/leave probe addresses */
        CORINFO_HELP_PROF_FCN_ENTER,        // record the entry to a method (caller)
        CORINFO_HELP_PROF_FCN_LEAVE,        // record the completion of current method (caller)
        CORINFO_HELP_PROF_FCN_TAILCALL,     // record the completionof current method through tailcall (caller)

        /* Miscellaneous */

        CORINFO_HELP_BBT_FCN_ENTER,         // record the entry to a method for collecting Tuning data

        CORINFO_HELP_PINVOKE_CALLI,         // Indirect pinvoke call
        CORINFO_HELP_TAILCALL,              // Perform a tail call

        CORINFO_HELP_GETCURRENTMANAGEDTHREADID,

        CORINFO_HELP_INIT_PINVOKE_FRAME,   // initialize an inlined PInvoke Frame for the JIT-compiler

        CORINFO_HELP_MEMSET,                // Init block of memory
        CORINFO_HELP_MEMCPY,                // Copy block of memory

        CORINFO_HELP_RUNTIMEHANDLE_METHOD,  // determine a type/field/method handle at run-time
        CORINFO_HELP_RUNTIMEHANDLE_METHOD_LOG,// determine a type/field/method handle at run-time, with IBC logging
        CORINFO_HELP_RUNTIMEHANDLE_CLASS,    // determine a type/field/method handle at run-time
        CORINFO_HELP_RUNTIMEHANDLE_CLASS_LOG,// determine a type/field/method handle at run-time, with IBC logging

        // These helpers are required for MDIL backward compatibility only. They are not used by current JITed code.
        CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_OBSOLETE, // Convert from a TypeHandle (native structure pointer) to RuntimeTypeHandle at run-time
    #if RYUJIT_CTPBUILD
        CORINFO_HELP_METHODDESC_TO_RUNTIMEMETHODHANDLE_MAYBENULL_OBSOLETE, // Convert from a MethodDesc (native structure pointer) to RuntimeMethodHandle at run-time
    #endif
        CORINFO_HELP_METHODDESC_TO_RUNTIMEMETHODHANDLE_OBSOLETE, // Convert from a MethodDesc (native structure pointer) to RuntimeMethodHandle at run-time
        CORINFO_HELP_FIELDDESC_TO_RUNTIMEFIELDHANDLE_OBSOLETE, // Convert from a FieldDesc (native structure pointer) to RuntimeFieldHandle at run-time

        CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE, // Convert from a TypeHandle (native structure pointer) to RuntimeType at run-time
        CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL, // Convert from a TypeHandle (native structure pointer) to RuntimeType at run-time, the type may be null
        CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD, // Convert from a MethodDesc (native structure pointer) to RuntimeMethodHandle at run-time
        CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD, // Convert from a FieldDesc (native structure pointer) to RuntimeFieldHandle at run-time

        CORINFO_HELP_VIRTUAL_FUNC_PTR,      // look up a virtual method at run-time
                                            //CORINFO_HELP_VIRTUAL_FUNC_PTR_LOG,  // look up a virtual method at run-time, with IBC logging

    #if !RYUJIT_CTPBUILD
        // Not a real helpers. Instead of taking handle arguments, these helpers point to a small stub that loads the handle argument and calls the static helper.
        CORINFO_HELP_READYTORUN_NEW,
        CORINFO_HELP_READYTORUN_NEWARR_1,
        CORINFO_HELP_READYTORUN_ISINSTANCEOF,
        CORINFO_HELP_READYTORUN_CHKCAST,
        CORINFO_HELP_READYTORUN_STATIC_BASE,
        CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR,
        CORINFO_HELP_READYTORUN_DELEGATE_CTOR,
    #endif

    #if REDHAWK
        // these helpers are arbitrary since we don't have any relation to the actual CLR corinfo.h.
        CORINFO_HELP_PINVOKE,               // transition to preemptive mode for a pinvoke, frame in EAX
        CORINFO_HELP_PINVOKE_2,             // transition to preemptive mode for a pinvoke, frame in ESI / R10
        CORINFO_HELP_PINVOKE_RETURN,        // return to cooperative mode from a pinvoke
        CORINFO_HELP_REVERSE_PINVOKE,       // transition to cooperative mode for a callback from native
        CORINFO_HELP_REVERSE_PINVOKE_RETURN,// return to preemptive mode to return to native from managed
        CORINFO_HELP_REGISTER_MODULE,       // module load notification
        CORINFO_HELP_CREATECOMMANDLINE,     // get the command line from the system and return it for Main
        CORINFO_HELP_VSD_INITIAL_TARGET,    // all VSD indirection cells initially point to this function
        CORINFO_HELP_NEW_FINALIZABLE,       // allocate finalizable object
        CORINFO_HELP_SHUTDOWN,              // called when Main returns from a managed executable
        CORINFO_HELP_CHECKARRAYSTORE,       // checks that an array element assignment is of the right type
        CORINFO_HELP_CHECK_VECTOR_ELEM_ADDR,// does a precise type check on the array element type
        CORINFO_HELP_FLT2INT_OVF,           // checked float->int conversion
        CORINFO_HELP_FLT2LNG,               // float->long conversion
        CORINFO_HELP_FLT2LNG_OVF,           // checked float->long conversion
        CORINFO_HELP_FLTREM_REV,            // Bartok helper for float remainder - uses reversed param order from CLR helper
        CORINFO_HELP_DBLREM_REV,            // Bartok helper for double remainder - uses reversed param order from CLR helper
        CORINFO_HELP_HIJACKFORGCSTRESS,     // this helper hijacks the caller for GC stress
        CORINFO_HELP_INIT_GCSTRESS,         // this helper initializes the runtime for GC stress
        CORINFO_HELP_SUPPRESS_GCSTRESS,     // disables gc stress
        CORINFO_HELP_UNSUPPRESS_GCSTRESS,   // re-enables gc stress
        CORINFO_HELP_THROW_INTRA,           // Throw an exception object to a hander within the method
        CORINFO_HELP_THROW_INTER,           // Throw an exception object to a hander within the caller
        CORINFO_HELP_THROW_ARITHMETIC,      // Throw the classlib-defined arithmetic exception
        CORINFO_HELP_THROW_DIVIDE_BY_ZERO,  // Throw the classlib-defined divide by zero exception
        CORINFO_HELP_THROW_INDEX,           // Throw the classlib-defined index out of range exception
        CORINFO_HELP_THROW_OVERFLOW,        // Throw the classlib-defined overflow exception
        CORINFO_HELP_EHJUMP_SCALAR,         // Helper to jump to a handler in a different method for EH dispatch.
        CORINFO_HELP_EHJUMP_OBJECT,         // Helper to jump to a handler in a different method for EH dispatch.
        CORINFO_HELP_EHJUMP_BYREF,          // Helper to jump to a handler in a different method for EH dispatch.
        CORINFO_HELP_EHJUMP_SCALAR_GCSTRESS,// Helper to jump to a handler in a different method for EH dispatch.
        CORINFO_HELP_EHJUMP_OBJECT_GCSTRESS,// Helper to jump to a handler in a different method for EH dispatch.
        CORINFO_HELP_EHJUMP_BYREF_GCSTRESS, // Helper to jump to a handler in a different method for EH dispatch.

        // Bartok emits code with destination in ECX rather than EDX and only ever uses EDX as the reference
        // register. It also only ever specifies the checked version.
        CORINFO_HELP_CHECKED_ASSIGN_REF_EDX, // EDX hold GC ptr, want do a 'mov [ECX], EDX' and inform GC
    #endif // REDHAWK

        CORINFO_HELP_EE_PRESTUB,            // Not real JIT helper. Used in native images.

        CORINFO_HELP_EE_PRECODE_FIXUP,      // Not real JIT helper. Used for Precode fixup in native images.
        CORINFO_HELP_EE_PINVOKE_FIXUP,      // Not real JIT helper. Used for PInvoke target fixup in native images.
        CORINFO_HELP_EE_VSD_FIXUP,          // Not real JIT helper. Used for VSD cell fixup in native images.
        CORINFO_HELP_EE_EXTERNAL_FIXUP,     // Not real JIT helper. Used for to fixup external method thunks in native images.
        CORINFO_HELP_EE_VTABLE_FIXUP,       // Not real JIT helper. Used for inherited vtable slot fixup in native images.

        CORINFO_HELP_EE_REMOTING_THUNK,     // Not real JIT helper. Used for remoting precode in native images.

        CORINFO_HELP_EE_PERSONALITY_ROUTINE,// Not real JIT helper. Used in native images.
        CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET,// Not real JIT helper. Used in native images to detect filter funclets.

        //
        // Keep platform-specific helpers at the end so that the ids for the platform neutral helpers stay same accross platforms
        //

    #if TARGET_X86 || _HOST_X86_ || REDHAWK // _HOST_X86_ is for altjit
                                        // NOGC_WRITE_BARRIERS JIT helper calls
                                        // Unchecked versions EDX is required to point into GC heap
        CORINFO_HELP_ASSIGN_REF_EAX,    // EAX holds GC ptr, do a 'mov [EDX], EAX' and inform GC
        CORINFO_HELP_ASSIGN_REF_EBX,    // EBX holds GC ptr, do a 'mov [EDX], EBX' and inform GC
        CORINFO_HELP_ASSIGN_REF_ECX,    // ECX holds GC ptr, do a 'mov [EDX], ECX' and inform GC
        CORINFO_HELP_ASSIGN_REF_ESI,    // ESI holds GC ptr, do a 'mov [EDX], ESI' and inform GC
        CORINFO_HELP_ASSIGN_REF_EDI,    // EDI holds GC ptr, do a 'mov [EDX], EDI' and inform GC
        CORINFO_HELP_ASSIGN_REF_EBP,    // EBP holds GC ptr, do a 'mov [EDX], EBP' and inform GC

        CORINFO_HELP_CHECKED_ASSIGN_REF_EAX,  // These are the same as ASSIGN_REF above ...
        CORINFO_HELP_CHECKED_ASSIGN_REF_EBX,  // ... but also check if EDX points into heap.
        CORINFO_HELP_CHECKED_ASSIGN_REF_ECX,
        CORINFO_HELP_CHECKED_ASSIGN_REF_ESI,
        CORINFO_HELP_CHECKED_ASSIGN_REF_EDI,
        CORINFO_HELP_CHECKED_ASSIGN_REF_EBP,
    #endif

        CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR, // Return the reference to a counter to decide to take cloned path in debug stress.
        CORINFO_HELP_DEBUG_LOG_LOOP_CLONING, // Print a message that a loop cloning optimization has occurred in debug mode.

        CORINFO_HELP_THROW_ARGUMENTEXCEPTION,           // throw ArgumentException
        CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION, // throw ArgumentOutOfRangeException

        CORINFO_HELP_COUNT,
    }
}
