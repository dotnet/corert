// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILVerify
{
    enum VerifierError
    {
        //E_HRESULT           "[HRESULT 0x%08X]"
        //E_OFFSET            "[offset 0x%08X]"
        //E_OPCODE            "[opcode %s]"
        //E_OPERAND           "[operand 0x%08X]"
        //E_TOKEN             "[token  0x%08X]"
        //E_EXCEPT            "[exception #0x%08X]"
        //E_STACK_SLOT        "[stack slot 0x%08X]"
        //E_LOC               "[local variable #0x%08X]"
        //E_LOC_BYNAME        "[local variable '%s']"
        //E_ARG               "[argument #0x%08x]"
        //E_FOUND             "[found %s]"
        //E_EXPECTED          "[expected %s]"

        //E_UNKNOWN_OPCODE     "Unknown opcode [0x%08X]."
        //E_SIG_CALLCONV       "Unknown calling convention [0x%08X]."
        //E_SIG_ELEMTYPE       "Unknown ELEMENT_TYPE [0x%08x]."

        //E_RET_SIG           "[return sig]"
        //E_FIELD_SIG         "[field sig]"

        //E_INTERNAL           "Internal error."
        //E_STACK_TOO_LARGE    "Stack is too large."
        //E_ARRAY_NAME_LONG    "Array name is too long."

        //E_FALLTHRU           "fall through end of the method without returning."
        //E_TRY_GTEQ_END       "try start >= try end."
        //E_TRYEND_GT_CS       "try end > code size."
        //E_HND_GTEQ_END       "handler start >= handler end."
        //E_HNDEND_GT_CS       "handler end > code size."
        //E_TRY_START          "Try starts in the middle of an instruction."
        //E_HND_START          "Handler starts in the middle of an instruction."
        //E_TRY_OVERLAP        "Try block overlap with another block."
        //E_TRY_EQ_HND_FIL     "Try and filter/handler blocks are equivalent."
        //E_TRY_SHARE_FIN_FAL  "Shared try has finally or fault handler."
        //E_HND_OVERLAP        "Handler block overlaps with another block."
        //E_HND_EQ             "Handler block is the same as another block."
        //E_FIL_OVERLAP        "Filter block overlaps with another block."
        //E_FIL_EQ             "Filter block is the same as another block."
        //E_FIL_CONT_TRY       "Filter contains try."
        //E_FIL_CONT_HND       "Filter contains handler."
        //E_FIL_CONT_FIL       "Nested filters."
        //E_FIL_GTEQ_CS        "filter >= code size."
        //E_FIL_START          "Filter starts in the middle of an instruction."
        //E_FALLTHRU_EXCEP     "fallthru the end of an exception block."
        //E_FALLTHRU_INTO_HND  "fallthru into an exception handler."
        //E_FALLTHRU_INTO_FIL  "fallthru into an exception filter."
        //E_LEAVE              "Leave from outside a try or catch block."
        Rethrow,                        //"Rethrow from outside a catch handler."
        Endfinally,                     //"Endfinally from outside a finally handler."
        Endfilter,                      //"Endfilter from outside an exception filter block."
        //E_ENDFILTER_MISSING  "Missing Endfilter."
        BranchIntoTry,          //"Branch into try block."
        BranchIntoHandler,      //"Branch into exception handler block."
        BranchIntoFilter,       //"Branch into exception filter block."
        BranchOutOfTry,         //"Branch out of try block."
        BranchOutOfHandler,     //"Branch out of exception handler block."
        BranchOutOfFilter,      //"Branch out of exception filter block."
        //E_BR_OUTOF_FIN       "Branch out of finally block."
        ReturnFromTry,          //"Return out of try block."
        ReturnFromHandler,      //"Return out of exception handler block."
        ReturnFromFilter,       //"Return out of exception filter block."
        //E_BAD_JMP_TARGET     "jmp / exception into the middle of an instruction."
        //E_PATH_LOC           "Non-compatible types depending on path."
        //E_PATH_THIS          "Init state for this differs depending on path."
        PathStackUnexpected,    //"Non-compatible types on stack depending on path."
        PathStackDepth,         //"Stack depth differs depending on path."
        //E_THIS               "Instance variable (this) missing."
        //E_THIS_UNINIT_EXCEP  "Uninitialized this on entering a try block."
        ThisUninitStore,        // Store into this when it is uninitialized.
        ThisUninitReturn,       // Return from .ctor when this is uninitialized.
        //E_THIS_UNINIT_V_RET  "Return from .ctor before all fields are initialized."
        //E_THIS_UNINIT_BR     "Branch back when this is uninitialized."
        LdftnCtor,       //"ldftn/ldvirtftn not allowed on .ctor."
        //StackNotEq,                     // "Non-compatible types on the stack."
        StackUnexpected,                // Unexpected type on the stack.
        StackUnexpectedArrayType,       // Unexpected array type on the stack.
        //E_STACK_EXCEPTION    "Missing stack slot for exception."
        StackOverflow,                  // Stack overflow.
        StackUnderflow,                 // Stack underflow.
        //E_STACK_EMPTY        "Stack empty."
        UninitStack,                    // Uninitialized item on stack.
        ExpectedIntegerType,            // Expected I, I4, or I8 on the stack.
        ExpectedFloatType,              // Expected R, R4, or R8 on the stack.
        //E_STACK_NO_R_I8      "unexpected R, R4, R8, or I8 on the stack."
        ExpectedNumericType,            // Expected numeric type on the stack.
        StackObjRef,                    // "Expected an ObjRef on the stack."
        //E_STACK_P_OBJREF     "Expected address of an ObjRef on the stack."
        StackByRef,                     // Expected ByRef on the stack.
        StackMethod,            // Expected pointer to function on the stack.
        //E_STACK_ARRAY_SD     "Expected single dimension array on the stack."
        //E_STACK_VALCLASS     "Expected value type instance on the stack."
        //E_STACK_P_VALCLASS   "Expected address of value type on the stack."
        //E_STACK_NO_VALCLASS  "Unexpected value type instance on the stack."
        //E_LOC_DEAD           "Local variable is unusable at this point."
        UnrecognizedLocalNumber,        // Unrecognized local variable number.
        UnrecognizedArgumentNumber,     // Unrecognized argument number.
        ExpectedTypeToken,              // Expected type token.
        TokenResolve,                   // Unable to resolve token.
        //E_TOKEN_TYPE         "Unable to resolve type of the token."
        ExpectedMethodToken,            // Expected memberRef, memberDef or methodSpec token.
        //E_TOKEN_TYPE_FIELD   "Expected memberRef or fieldDef token."
        //E_TOKEN_TYPE_SIG     "Expected signature token."
        ExpectedFieldToken,             // Expected field token.
        //  E_TOKEN_TYPE_SIG     "Expected signature token."
        Unverifiable,                   // Instruction can not be verified.
        StringOperand,                  // Operand does not point to a valid string ref.
        ReturnPtrToStack,               // Return type is ByRef, TypedReference, ArgHandle, or ArgIterator.
        ReturnVoid,                     // Stack must be empty on return from a void function.
        ReturnMissing,                  // Return value missing on the stack.
        ReturnEmpty,                    // Stack must contain only the return value.
        //E_RET_UNINIT         "Return uninitialized data."
        //E_ARRAY_ACCESS       "Illegal array access."
        //E_ARRAY_V_STORE      "Store non Object type into Object array."
        ExpectedArray,                  // Expected single-dimension zero-based array.
        //E_ARRAY_SD_PTR       "Expected single dimension array of pointer types."
        //E_ARRAY_FIELD        "Array field access is denied."
        //E_ARGLIST            "Allowed only in vararg methods."
        ValueTypeExpected,              // Value type expected.
        //E_OPEN_DLGT_PROT_ACC "Protected method access through an open instance delegate is not verifiable."
        //E_METHOD_ACCESS      "Method is not visible."
        //E_FIELD_ACCESS       "Field is not visible."
        //E_DEAD               "Item is unusable at this point."
        ExpectedStaticField,            // Expected static field.
        //E_FIELD_NO_STATIC    "Expected non-static field."
        //E_ADDR               "Address of not allowed for this item."
        //E_ADDR_BYREF         "Address of not allowed for ByRef."
        //E_ADDR_LITERAL       "Address of not allowed for literal field."
        //E_INITONLY           "Cannot change initonly field outside its .ctor."
        //E_WRITE_RVA_STATIC   "Cannot modify an imaged based (RVA) static"
        //E_THROW              "Cannot throw this object."
        CallVirtOnValueType,        // Callvirt on a value type method.
        //E_CALL_SIG           "Call signature mismatch."
        //E_CALL_STATIC        "Static function expected."
        //E_CTOR               ".ctor expected."
        //E_CTOR_VIRT          "Cannot use callvirt on .ctor."
        //E_CTOR_OR_SUPER      "Only super::ctor or typeof(this)::ctor allowed here."
        //E_CTOR_MUL_INIT      "Possible call to .ctor more than once."
        //E_SIG                "Unrecognized signature."
        //E_SIG_ARRAY          "Cannot resolve Array type."
        //E_SIG_ARRAY_PTR      "Array of ELEMENT_TYPE_PTR."
        ArrayByRef,                     // Array of ELEMENT_TYPE_BYREF or ELEMENT_TYPE_TYPEDBYREF.
        //E_SIG_ELEM_PTR       "ELEMENT_TYPE_PTR cannot be verified."
        //E_SIG_VARARG         "Unexpected vararg."
        //E_SIG_VOID           "Unexpected Void."
        ByrefOfByref,                   // ByRef of ByRef.
        //E_CODE_SIZE_ZERO     "Code size is zero."
        //E_BAD_VARARG         "Unrecognized use of vararg."
        TailCall,                       // Missing call/callvirt/calli.
        TailByRef,                      // Cannot pass ByRef to a tail call.
        //E_TAIL_RET           "Missing ret."
        TailRetVoid,                    // Void ret type expected for tail call.
        //E_TAIL_RET_TYPE      "Tail call return type not compatible."
        TailStackEmpty,                 // Stack not empty after tail call.
        //E_METHOD_END         "Method ends in the middle of an instruction."
        //E_BAD_BRANCH         "Branch out of the method."
        //E_FIN_OVERLAP        "Finally handler blocks overlap."
        //E_LEXICAL_NESTING    "Lexical nesting."
        Volatile,                       // Missing ldsfld, stsfld, ldind, stind, ldfld, stfld, ldobj, stobj, initblk, or cpblk.
        Unaligned,                      // Missing ldind, stind, ldfld, stfld, ldobj, stobj, initblk, cpblk.
        //E_INNERMOST_FIRST    "Innermost exception blocks should be declared first."
        //E_CALLI_VIRTUAL      "Calli not allowed on virtual methods."
        CallAbstract,               // Call not allowed on abstract methods.
        //E_NOT_IN_GC_HEAP     "Value type with NotInGCHeap attribute being created on the GC heap."
        TryNonEmptyStack,           // Attempt to enter a try block with nonempty stack.
        DelegateCtor,           // Unrecognized arguments for delegate .ctor.
        //E_DLGT_BB            "Delegate .ctor not allowed at the start of a basic block when the function pointer argument is a virtual method."
        //E_DLGT_PATTERN       "Dup, ldvirtftn, newobj delegate::.ctor() pattern expected (in the same basic block)."
        //E_DLGT_LDFTN         "Ldftn or ldvirtftn instruction required before call to a delegate .ctor."
        //E_FTN_ABSTRACT       "Attempt to load address of an abstract method."
        //E_SIG_C_VC           "ELEMENT_TYPE_CLASS ValueClass in signature."
        //E_SIG_VC_C           "ELEMENT_TYPE_VALUETYPE non-ValueClass in signature."
        //E_BOX_PTR_TO_STACK   "Box operation on TypedReference, ArgHandle, or ArgIterator."
        //E_SIG_BYREF_TB_AH    "ByRef of TypedReference, ArgHandle, or ArgIterator."
        //E_SIG_ARRAY_TB_AH    "Array of TypedReference, ArgHandle, or ArgIterator."
        EndfilterStack,                 //"Stack not empty when leaving an exception filter."
        DelegateCtorSigI,        // Unrecognized delegate .ctor signature; expected Native Int.
        DelegateCtorSigO,        // Unrecognized delegate .ctor signature; expected Object.
        //E_RA_PTR_TO_STACK    "Mkrefany on TypedReference, ArgHandle, or ArgIterator."
        //E_CATCH_VALUE_TYPE   "Value type not allowed as catch type."
        //E_CATCH_BYREF        "ByRef not allowed as catch type."
        //E_FIL_PRECEED_HND    "filter block should immediately precede handler block"
        LdvirtftnOnStatic,      // ldvirtftn on static.
        CallVirtOnStatic,       // callvirt on static.
        InitLocals,             // initlocals must be set for verifiable methods with one or more local variables.
        //E_BR_TO_EXCEPTION    "branch/leave to the beginning of a catch/filter handler"
        CallCtor,               // call to .ctor only allowed to initialize this pointer from within a .ctor. Try newobj.
        
        ////@GENERICSVER: new generics related error messages
        ExpectedValClassObjRefVariable, // Value type, ObjRef type or variable type expected.
        //E_STACK_P_VALCLASS_OBJREF_VAR  "Expected address of value type, ObjRef type or variable type on the stack."
        //E_SIG_VAR_PARAM            "Unrecognized type parameter of enclosing class."
        //E_SIG_MVAR_PARAM           "Unrecognized type parameter of enclosing method."
        //E_SIG_VAR_ARG              "Unrecognized type argument of referenced class instantiation."
        //E_SIG_MVAR_ARG             "Unrecognized type argument of referenced method instantiation."
        //E_SIG_GENERICINST          "Cannot resolve generic type."
        //E_SIG_METHOD_INST          "Method instantiation contains non boxable type arguments."
        //E_SIG_METHOD_PARENT_INST   "Method parent instantiation contains non boxable type arguments."
        //E_SIG_FIELD_PARENT_INST    "Field parent instantiation contains non boxable type arguments."
        //E_CALLCONV_NOT_GENERICINST "Unrecognized calling convention for an instantiated generic method."
        //E_TOKEN_BAD_METHOD_SPEC    "Unrecognized generic method in method instantiation."
        ReadOnly,                       // Missing ldelema or call following readonly prefix.
        Constrained,                    // Missing callvirt following constrained prefix.

        //E_CIRCULAR_VAR_CONSTRAINTS "Method parent has circular class type parameter constraints."
        //E_CIRCULAR_MVAR_CONSTRAINTS "Method has circular method type parameter constraints."

        UnsatisfiedMethodInst,                // Method instantiation has unsatisfied method type parameter constraints.
        UnsatisfiedMethodParentInst,          // Method parent instantiation has unsatisfied class type parameter constraints.
        //E_UNSATISFIED_FIELD_PARENT_INST    "Field parent instantiation has unsatisfied class type parameter constraints."
        //E_UNSATISFIED_BOX_OPERAND          "Type operand of box instruction has unsatisfied class type parameter constraints."
        ConstrainedCallWithNonByRefThis,      // The 'this' argument to a constrained call must have ByRef type.
        //E_CONSTRAINED_OF_NON_VARIABLE_TYPE "The operand to a constrained prefix instruction must be a type parameter."
        //E_READONLY_UNEXPECTED_CALLEE       "The readonly prefix may only be applied to calls to array methods returning ByRefs."
        ReadOnlyIllegalWrite,                 // "Illegal write to readonly ByRef."
        //E_READONLY_IN_MKREFANY              "A readonly ByRef cannot be used with mkrefany."
        //E_UNALIGNED_ALIGNMENT      "Alignment specified for 'unaligned' prefix must be 1, 2, or 4."
        //E_TAILCALL_INSIDE_EH       "The tail.call (or calli or callvirt) instruction cannot be used to transfer control out of a try, filter, catch, or finally block."
        //E_BACKWARD_BRANCH          "Stack height at all points must be determinable in a single forward scan of IL."
        //E_CALL_TO_VTYPE_BASE       "Call to base type of valuetype."
        //E_NEWOBJ_OF_ABSTRACT_CLASS "Cannot construct an instance of abstract class."
        UnmanagedPointer,                     // Unmanaged pointers are not a verifiable type.
        //E_LDFTN_NON_FINAL_VIRTUAL  "Cannot LDFTN a non-final virtual method for delegate creation if target object is potentially not the same type as the method class."
        //E_FIELD_OVERLAP      "Accessing type with overlapping fields."
        //E_THIS_MISMATCH      "The 'this' parameter to the call must be the calling method's 'this' parameter."
        //E_STACK_I_I4         "Expected I4 on the stack."

        //E_BAD_PE             "Unverifiable PE Header/native stub."
        //E_BAD_MD             "Unrecognized metadata, unable to verify IL."
        //E_BAD_APPDOMAIN      "Unrecognized appdomain pointer."

        //E_TYPELOAD           "Type load failed."
        //E_PE_LOAD            "Module load failed."

        //IDS_E_FORMATTING     "Error formatting message."
        //IDS_E_ILERROR        "[IL]: Error: "
        //IDS_E_GLOBAL         "<GlobalFunction>"
        //IDS_E_MDTOKEN        "[mdToken=0x%x]"
    }
}
