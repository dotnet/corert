// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Provides the Volatile<T> class as a replacement for the C++ volatile keyword where it's important that
// acquire/release semantics are always observed.
//
// In particular on the ARM platform with the C++ compiler options we're using raw volatile will not preserve
// this semantic and additional memory barriers are required.
//

#if defined(_ARM_) && _ISO_VOLATILE
// ARM has a very weak memory model and very few tools to control that model. We're forced to perform a full
// memory barrier to preserve the volatile semantics. Technically this is only necessary on MP systems but we
// currently don't have a cheap way to determine the number of CPUs from this header file. Revisit this if it
// turns out to be a performance issue for the uni-proc case.
#define VOLATILE_MEMORY_BARRIER() PalMemoryBarrier()
#else
//
// On VC++, reorderings at the compiler and machine level are prevented by the use of the 
// "volatile" keyword in VolatileLoad and VolatileStore.  This should work on any CPU architecture
// targeted by VC++ with /iso_volatile-.
//
#define VOLATILE_MEMORY_BARRIER()
#endif

//
// VolatileLoad loads a T from a pointer to T.  It is guaranteed that this load will not be optimized
// away by the compiler, and that any operation that occurs after this load, in program order, will
// not be moved before this load.  In general it is not guaranteed that the load will be atomic, though
// this is the case for most aligned scalar data types.  If you need atomic loads or stores, you need
// to consult the compiler and CPU manuals to find which circumstances allow atomicity.
//
template<typename T>
inline
T VolatileLoad(T const * pt)
{
#ifndef DACCESS_COMPILE
    T val = *(T volatile const *)pt;
    VOLATILE_MEMORY_BARRIER();
#else
    T val = *pt;
#endif
    return val;
}

template<typename T>
inline
T VolatileLoadWithoutBarrier(T const * pt)
{
#ifndef DACCESS_COMPILE
    T val = *(T volatile const *)pt;
#else
    T val = *pt;
#endif
    return val;
}

template <typename T> class Volatile;

template<typename T>
inline
T VolatileLoad(Volatile<T> const * pt)
{
    return pt->Load();
}

//
// VolatileStore stores a T into the target of a pointer to T.  Is is guaranteed that this store will
// not be optimized away by the compiler, and that any operation that occurs before this store, in program
// order, will not be moved after this store.  In general, it is not guaranteed that the store will be
// atomic, though this is the case for most aligned scalar data types.  If you need atomic loads or stores,
// you need to consult the compiler and CPU manuals to find which circumstances allow atomicity.
//
template<typename T>
inline
void VolatileStore(T* pt, T val)
{
#ifndef DACCESS_COMPILE
    VOLATILE_MEMORY_BARRIER();
    *(T volatile *)pt = val;
#else
    *pt = val;
#endif
}

//
// Volatile<T> implements accesses with our volatile semantics over a variable of type T.
// Wherever you would have used a "volatile Foo" or, equivalently, "Foo volatile", use Volatile<Foo> 
// instead.  If Foo is a pointer type, use VolatilePtr.
// 
// Note that there are still some things that don't work with a Volatile<T>,
// that would have worked with a "volatile T".  For example, you can't cast a Volatile<int> to a float.
// You must instead cast to an int, then to a float.  Or you can call Load on the Volatile<int>, and
// cast the result to a float.  In general, calling Load or Store explicitly will work around 
// any problems that can't be solved by operator overloading.
// 
template <typename T>
class Volatile
{
private:
    //
    // The data which we are treating as volatile
    //
    T m_val;

public:
    //
    // Default constructor.  Results in an unitialized value!
    //
    inline Volatile() 
    {
    }

    //
    // Allow initialization of Volatile<T> from a T
    //
    inline Volatile(const T& val) 
    {
        ((volatile T &)m_val) = val;
    }

    //
    // Copy constructor
    //
    inline Volatile(const Volatile<T>& other)
    {
        ((volatile T &)m_val) = other.Load();
    }

    //
    // Loads the value of the volatile variable.  See code:VolatileLoad for the semantics of this operation.
    //
    inline T Load() const
    {
        return VolatileLoad(&m_val);
    }

    //
    // Loads the value of the volatile variable atomically without erecting the memory barrier.
    //
    inline T LoadWithoutBarrier() const
    {
        return ((volatile T &)m_val);
    }

    //
    // Stores a new value to the volatile variable.  See code:VolatileStore for the semantics of this
    // operation.
    //
    inline void Store(const T& val) 
    {
        VolatileStore(&m_val, val);
    }


    //
    // Stores a new value to the volatile variable atomically without erecting the memory barrier.
    //
    inline void StoreWithoutBarrier(const T& val) const
    {
        ((volatile T &)m_val) = val;
    }


    //
    // Gets a pointer to the volatile variable.  This is dangerous, as it permits the variable to be
    // accessed without using Load and Store, but it is necessary for passing Volatile<T> to APIs like
    // InterlockedIncrement.
    //
    inline volatile T* GetPointer() { return (volatile T*)&m_val; }


    //
    // Gets the raw value of the variable.  This is dangerous, as it permits the variable to be
    // accessed without using Load and Store
    //
    inline T& RawValue() { return m_val; }

    //
    // Allow casts from Volatile<T> to T.  Note that this allows implicit casts, so you can
    // pass a Volatile<T> directly to a method that expects a T.
    //
    inline operator T() const 
    {
        return this->Load();
    }

    //
    // Assignment from T
    //
    inline Volatile<T>& operator=(T val) {Store(val); return *this;}

    //
    // Get the address of the volatile variable.  This is dangerous, as it allows the value of the 
    // volatile variable to be accessed directly, without going through Load and Store, but it is
    // necessary for passing Volatile<T> to APIs like InterlockedIncrement.  Note that we are returning
    // a pointer to a volatile T here, so we cannot accidentally pass this pointer to an API that 
    // expects a normal pointer.
    //
    inline T volatile * operator&() {return this->GetPointer();}
    inline T volatile const * operator&() const {return this->GetPointer();}

    //
    // Comparison operators
    //
    template<typename TOther>
    inline bool operator==(const TOther& other) const {return this->Load() == other;}

    template<typename TOther>
    inline bool operator!=(const TOther& other) const {return this->Load() != other;}

    //
    // Miscellaneous operators.  Add more as necessary.
    //
	inline Volatile<T>& operator+=(T val) {Store(this->Load() + val); return *this;}
	inline Volatile<T>& operator-=(T val) {Store(this->Load() - val); return *this;}
    inline Volatile<T>& operator|=(T val) {Store(this->Load() | val); return *this;}
    inline Volatile<T>& operator&=(T val) {Store(this->Load() & val); return *this;}
    inline bool operator!() const { return !this->Load();}

    //
    // Prefix increment
    //
    inline Volatile& operator++() {this->Store(this->Load()+1); return *this;}

    //
    // Postfix increment
    //
    inline T operator++(int) {T val = this->Load(); this->Store(val+1); return val;}

    //
    // Prefix decrement
    //
    inline Volatile& operator--() {this->Store(this->Load()-1); return *this;}

    //
    // Postfix decrement
    //
    inline T operator--(int) {T val = this->Load(); this->Store(val-1); return val;}
};

#define RAW_KEYWORD(x) x

#ifdef DACCESS_COMPILE
// No need to use volatile in DAC builds - DAC is single-threaded and the target
// process is suspended.
#define VOLATILE(T) T
#else

// Disable use of Volatile<T> for GC/HandleTable code except on platforms where it's absolutely necessary.
#if defined(_MSC_VER) && !defined(_ARM_)
#define VOLATILE(T) T RAW_KEYWORD(volatile)
#else
#define VOLATILE(T) Volatile<T>
#endif

#endif // DACCESS_COMPILE
