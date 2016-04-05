// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace NoStl
{
    //
    // Simple vector that avoids using the STL but provides a familiar interface
    //
    template <class T>
    class vector
    {
    public:
        vector()
            : m_data(0), m_capacity(0), m_count(0)
        {
        }

        T &operator[](unsigned int i)
        {
            assert(i < m_count);
            assert(m_data);
            
            return m_data[i];
        }
        
        void push_back(const T &val)
        {
            if (!m_data || m_count >= m_capacity)
                resize();
        
            if (m_data)
                m_data[m_count++] = val;
        }
        
        T &at(unsigned int i)
        {
            assert(m_data);
            assert(i < m_count);
            
            return m_data[i];
        }
        
        unsigned int size() const
        {
            assert(m_count == 0 || m_data);
            return m_count;
        }
        
        unsigned int capacity() const
        {
            return m_capacity;
        }
        
        void clear()
        {
            if (m_data)
                delete [] m_data;
            
            m_data = 0;
            m_capacity = 0;
            m_count = 0;
        }

        ~vector()
        {
            delete[] m_data;
        }

        vector(vector<T>& other) : m_count(0), m_capacity(0), m_data(0)
        {
            copy(other);
        }

        vector<T>& operator= (vector<T>& other)
        {
            // Avoid self-assignment
            if (this != &other)
            {
                clear();
                copy(other);
            }

            return this;
        }

    private:
        void resize(unsigned int capacity = 0)
        {
            if (capacity == 0)
            {
                if (m_capacity)
                {
                    capacity = m_capacity << 1;
                }
                else
                {
                    capacity = 0x10;
                }
            }
            
            if (capacity > m_capacity)
            {
                T *buffer = new T[capacity];
                memcpy(buffer, m_data, sizeof(T)*m_count);
                
                delete [] m_data;
                m_data = buffer;
                
                m_capacity = capacity;
            }
        }

        void copy(vector<T>& other)
        {
            resize(other.m_capacity);
            for (int i = 0; i < other.size(); ++i)
            {
                push_back(other[i]);
            }
            assert(size() == other.size());
        }
        
    private:
        T *m_data;
        unsigned int m_capacity;
        unsigned int m_count;
    };
}
