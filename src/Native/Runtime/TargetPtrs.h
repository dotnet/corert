//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#ifndef _TARGETPTRS_H_
#define _TARGETPTRS_H_


#if defined(BINDER)

#ifdef TARGET_X64
typedef UInt64 UIntTarget;
#elif defined(TARGET_X86)
typedef UInt32 UIntTarget;
#elif defined(TARGET_ARM)
typedef UInt32 UIntTarget;
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
typedef TargetPtr<struct GenericInstanceDesc>   TgtPTR_GenericInstanceDesc;
typedef TargetPtr<class Thread>                 TgtPTR_Thread;
typedef TargetPtr<struct CORINFO_Object>        TgtPTR_CORINFO_Object;
typedef TargetPtr<struct StaticGcDesc>          TgtPTR_StaticGcDesc;

#elif defined(RHDUMP)
#ifdef TARGET_X64
typedef UInt64 UIntTarget;
#elif defined(TARGET_X86)
typedef UInt32 UIntTarget;
#elif defined(TARGET_ARM)
typedef UInt32 UIntTarget;
#else
#error unexpected target architecture
#endif

typedef UIntTarget TgtPTR_UInt8;
typedef UIntTarget TgtPTR_UInt32;
typedef UIntTarget TgtPTR_Void;
typedef UIntTarget TgtPTR_EEType;
typedef UIntTarget TgtPTR_GenericInstanceDesc;
typedef UIntTarget TgtPTR_Thread;
typedef UIntTarget TgtPTR_CORINFO_Object;
typedef UIntTarget TgtPTR_StaticGcDesc;

#else

typedef DPTR(class EEType) PTR_EEType;
typedef SPTR(struct GenericInstanceDesc) PTR_GenericInstanceDesc;
typedef SPTR(struct StaticGcDesc) PTR_StaticGcDesc;

#ifdef TARGET_X64
typedef UInt64 UIntTarget;
#elif defined(TARGET_X86)
typedef UInt32 UIntTarget;
#elif defined(TARGET_ARM)
typedef UInt32 UIntTarget;
#else
#error unexpected target architecture
#endif

typedef PTR_UInt8                       TgtPTR_UInt8;
typedef PTR_UInt32                      TgtPTR_UInt32;
typedef void *                          TgtPTR_Void;
typedef PTR_EEType                      TgtPTR_EEType;
typedef PTR_GenericInstanceDesc         TgtPTR_GenericInstanceDesc;
typedef class Thread *                  TgtPTR_Thread;
typedef struct CORINFO_Object *         TgtPTR_CORINFO_Object;
typedef PTR_StaticGcDesc                TgtPTR_StaticGcDesc;

#endif // BINDER

#endif // !_TARGETPTRS_H_
