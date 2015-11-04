//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#ifndef __UNIX_HANDLE_H__
#define __UNIX_HANDLE_H__

enum class UnixHandleType
{
    Thread,
    Mutex,
    Event
};

// TODO: add validity check for usage / closing?
class UnixHandleBase
{
    UnixHandleType m_type;
protected:
    UnixHandleBase(UnixHandleType type)
    : m_type(type)
    {
    }

public:
    virtual ~UnixHandleBase()
    {
    }

    UnixHandleType GetType()
    {
        return m_type;
    }
};

template<UnixHandleType HT, typename T>
class UnixHandle : UnixHandleBase
{
    T m_object;
public:

    UnixHandle(T object)
    : UnixHandleBase(HT),
      m_object(object)
    {
    }

    T* GetObject()
    {
        return &m_object;
    }
};

#endif // __UNIX_HANDLE_H__
