// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#ifndef _TARGETPTRS_H_
#define _TARGETPTRS_H_


#if defined(BINDER)

#ifdef _TARGET_AMD64_
typedef UInt64 UIntTarget;
#elif defined(_TARGET_X86_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM64_)
typedef UInt64 UIntTarget;
#else
#error unexpected target architecture
#endif

//
// Primitive pointer wrapper class very much like __DPtr<type> from daccess.h.  
//
template<typename type>
class TargetPtr
{
    union 
    {
        type*       m_ptr;
        UIntTarget  m_doNotUse;
    };

public:
    TargetPtr< type >(void) { }

    explicit TargetPtr< type >(type * host) { m_ptr = host; }

    TargetPtr<type>& operator=(const TargetPtr<type>& ptr)
    {
        m_ptr = ptr.GetAddr();
        return *this;
    }
    TargetPtr<type>& operator=(type* ptr)
    {
        m_ptr = ptr;
        return *this;
    }
    

    operator type*() const
    {
        return m_ptr;
    }
    type* operator->() const
    {
        return m_ptr;
    }


    type* GetAddr(void) const
    {
        return m_ptr;
    }
    type* SetAddr(type* ptr)
    {
        m_ptr = ptr;
        return ptr;
    }
};

typedef TargetPtr<UInt8>                        TgtPTR_UInt8;
typedef TargetPtr<UInt32>                       TgtPTR_UInt32;
typedef TargetPtr<void>                         TgtPTR_Void;
typedef TargetPtr<class EEType>                 TgtPTR_EEType;
typedef TargetPtr<class Thread>                 TgtPTR_Thread;
typedef TargetPtr<struct CORINFO_Object>        TgtPTR_CORINFO_Object;
typedef TargetPtr<struct StaticGcDesc>          TgtPTR_StaticGcDesc;

#elif defined(RHDUMP)
#ifdef _TARGET_AMD64_
typedef UInt64 UIntTarget;
#elif defined(_TARGET_X86_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM64_)
typedef UInt64 UIntTarget;
#else
#error unexpected target architecture
#endif

typedef UIntTarget TgtPTR_UInt8;
typedef UIntTarget TgtPTR_UInt32;
typedef UIntTarget TgtPTR_Void;
typedef UIntTarget TgtPTR_EEType;
typedef UIntTarget TgtPTR_Thread;
typedef UIntTarget TgtPTR_CORINFO_Object;
typedef UIntTarget TgtPTR_StaticGcDesc;

#else

typedef DPTR(class EEType) PTR_EEType;
typedef SPTR(struct StaticGcDesc) PTR_StaticGcDesc;

#ifdef _TARGET_AMD64_
typedef UInt64 UIntTarget;
#elif defined(_TARGET_X86_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM_)
typedef UInt32 UIntTarget;
#elif defined(_TARGET_ARM64_)
typedef UInt64 UIntTarget;
#elif defined(_TARGET_WASM_)
typedef UInt32 UIntTarget;
#else
#error unexpected target architecture
#endif

typedef PTR_UInt8                       TgtPTR_UInt8;
typedef PTR_UInt32                      TgtPTR_UInt32;
typedef void *                          TgtPTR_Void;
typedef PTR_EEType                      TgtPTR_EEType;
typedef class Thread *                  TgtPTR_Thread;
typedef struct CORINFO_Object *         TgtPTR_CORINFO_Object;
typedef PTR_StaticGcDesc                TgtPTR_StaticGcDesc;

#endif // BINDER

#endif // !_TARGETPTRS_H_
