// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Provides simple configuration support through environment variables. Each variable is lazily inspected on
// first query and the resulting value cached for future use. To keep things simple we support reading only
// 32-bit hex quantities and a zero value is considered equivalent to the environment variable not being
// defined. We can get more sophisticated if needs be, but the hope is that very few configuration values are
// exposed in this manner.
//
// Values can also be configured through an rhconfig.ini file.  The file must be and ASCII text file, must be
// placed next to the executing assembly, and be named rhconfig.ini.  The file consists of one config entry per line
// in the format: <Key>=<Value> 
// example:
// RH_HeapVerify=1
// RH_BreakOnAssert=1
//


#ifndef DACCESS_COMPILE

#if defined(_DEBUG) || !defined(APP_LOCAL_RUNTIME)
#define RH_ENVIRONMENT_VARIABLE_CONFIG_ENABLED
#endif

class RhConfig
{

#define CONFIG_INI_FILENAME L"rhconfig.ini"
#define CONFIG_INI_NOT_AVAIL (void*)0x1  //signal for ini file failed to load
#define CONFIG_KEY_MAXLEN 50             //arbitrary max length of config keys increase if needed
#define CONFIG_VAL_MAXLEN 8              //32 bit uint in hex

private:
    struct ConfigPair
    {
    public:
        TCHAR Key[CONFIG_KEY_MAXLEN + 1];  //maxlen + null terminator
        TCHAR Value[CONFIG_VAL_MAXLEN + 1]; //maxlen + null terminator
    };

    //g_iniSettings is a buffer of ConfigPair structs which when initialized is of length RCV_Count
    //the first N settings which are set in rhconfig.ini will be initialized and the remainder with have 
    //empty string "\0" as a Key and Value
    //
    //if the buffer has not been initialized (ie the ini file has not been read) the value will be NULL
    //if we already attempted to initialize the file and could not find or read the contents the 
    //value will be CONFIG_INI_NOT_AVAIL to distinguish from the unitialized buffer.
    //
    //NOTE: g_iniSettings is only set in ReadConfigIni and must be set atomically only once
    //      using PalInterlockedCompareExchangePointer to avoid races when initializing
private:
    void* volatile g_iniSettings = NULL;

public:

#define DEFINE_VALUE_ACCESSOR(_name, defaultVal)        \
    UInt32 Get##_name()                                 \
    {                                                   \
        if (m_uiConfigValuesRead & (1 << RCV_##_name))  \
            return m_uiConfigValues[RCV_##_name];       \
        UInt32 uiValue = ReadConfigValue(_T("RH_") _T(#_name), defaultVal); \
        m_uiConfigValues[RCV_##_name] = uiValue;        \
        m_uiConfigValuesRead |= 1 << RCV_##_name;       \
        return uiValue;                                 \
    }


#ifdef _DEBUG
#define DEBUG_CONFIG_VALUE(_name) DEFINE_VALUE_ACCESSOR(_name, 0)
#define DEBUG_CONFIG_VALUE_WITH_DEFAULT(_name, defaultVal) DEFINE_VALUE_ACCESSOR(_name, defaultVal)
#else
#define DEBUG_CONFIG_VALUE(_name) 
#define DEBUG_CONFIG_VALUE_WITH_DEFAULT(_name, defaultVal) 
#endif
#define RETAIL_CONFIG_VALUE(_name) DEFINE_VALUE_ACCESSOR(_name, 0)
#define RETAIL_CONFIG_VALUE_WITH_DEFAULT(_name, defaultVal) DEFINE_VALUE_ACCESSOR(_name, defaultVal)
#include "RhConfigValues.h"
#undef DEBUG_CONFIG_VALUE
#undef RETAIL_CONFIG_VALUE
#undef DEBUG_CONFIG_VALUE_WITH_DEFAULT
#undef RETAIL_CONFIG_VALUE_WITH_DEFAULT

private:

    UInt32 ReadConfigValue(_In_z_ const TCHAR *wszName, UInt32 uiDefault);

    enum RhConfigValue
    {
#define DEBUG_CONFIG_VALUE(_name) RCV_##_name,
#define RETAIL_CONFIG_VALUE(_name) RCV_##_name,
#define DEBUG_CONFIG_VALUE_WITH_DEFAULT(_name, defaultVal) RCV_##_name,
#define RETAIL_CONFIG_VALUE_WITH_DEFAULT(_name, defaultVal) RCV_##_name,
#include "RhConfigValues.h"
#undef DEBUG_CONFIG_VALUE
#undef RETAIL_CONFIG_VALUE
#undef DEBUG_CONFIG_VALUE_WITH_DEFAULT
#undef RETAIL_CONFIG_VALUE_WITH_DEFAULT
        RCV_Count
    };
    
//accomidate for the maximum number of config values plus sizable buffer for whitespace 2K
#define CONFIG_FILE_MAXLEN RCV_Count * sizeof(ConfigPair) + 2000  

private:
    _Ret_maybenull_z_ TCHAR* GetConfigPath();

    //Parses one line of rhconfig.ini and populates values in the passed in configPair
    //returns: true if the parsing was successful, false if the parsing failed. 
    //NOTE: if the method fails configPair is left in an unitialized state
    bool ParseConfigLine(_Out_ ConfigPair* configPair, _In_z_ const char * line);

    //reads the configuration values from rhconfig.ini and updates g_iniSettings
    //if the file is read succesfully and g_iniSettings will be set to a valid ConfigPair[] of length RCV_Count.
    //if the file does not exist or reading the file fails,  g_iniSettings is set to CONFIG_INI_NOT_AVAIL
    //NOTE: all return paths must set g_iniSettings 
    void ReadConfigIni();

    //reads a config value from rhconfig.ini into outputBuffer buffer returning the length of the value.
    //lazily reads the file so if the file is not yet read, it will read it on first called
    //if the file is not avaliable, or unreadable zero will always be returned
    //cchOutputBuffer is the maximum number of characters to write to outputBuffer
    UInt32 GetIniVariable(_In_z_ const TCHAR* configName, _Out_writes_all_(cchOutputBuffer) TCHAR* outputBuffer, _In_ UInt32 cchOutputBuffer);

    static bool priv_isspace(char c)
    {
        return (c == ' ') || (c == '\t') || (c == '\n') || (c == '\r');
    }


    UInt32  m_uiConfigValuesRead;
    UInt32  m_uiConfigValues[RCV_Count];
};

extern RhConfig * g_pRhConfig;

#endif //!DACCESS_COMPILE
