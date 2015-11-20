//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#ifndef DACCESS_COMPILE
#include "CommonTypes.h"
#include "daccess.h"
#include "CommonMacros.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "event.h"
#include "RWLock.h"
#include "threadstore.h"
#include "RuntimeInstance.h"
#include "module.h"
#include "RhConfig.h"

#include <string.h>

UInt32 RhConfig::ReadConfigValue(_In_z_ const WCHAR *wszName)
{
    WCHAR wszBuffer[CONFIG_VAL_MAXLEN + 1]; // 8 hex digits plus a nul terminator.
    const UInt32 cchBuffer = sizeof(wszBuffer) / sizeof(wszBuffer[0]);

    UInt32 cchResult = 0;

#ifdef RH_ENVIRONMENT_VARIABLE_CONFIG_ENABLED
    cchResult = PalGetEnvironmentVariableW(wszName, wszBuffer, cchBuffer);
#endif // RH_ENVIRONMENT_VARIABLE_CONFIG_ENABLED

    //if the config key wasn't found in the environment 
    if ((cchResult == 0) || (cchResult >= cchBuffer))
        cchResult = GetIniVariable(wszName, wszBuffer, cchBuffer);

    if ((cchResult == 0) || (cchResult >= cchBuffer))
        return 0;

    UInt32 uiResult = 0;

    for (UInt32 i = 0; i < cchResult; i++)
    {
        uiResult <<= 4;

        WCHAR ch = wszBuffer[i];
        if ((ch >= L'0') && (ch <= L'9'))
            uiResult += ch - L'0';
        else if ((ch >= L'a') && (ch <= L'f'))
            uiResult += (ch - L'a') + 10;
        else if ((ch >= L'A') && (ch <= L'F'))
            uiResult += (ch - L'A') + 10;
        else
            return 0;
    }

    return uiResult;
}

//reads a config value from rhconfig.ini into outputBuffer buffer returning the length of the value.
//lazily reads the file so if the file is not yet read, it will read it on first called
//if the file is not avaliable, or unreadable zero will always be returned
//cchOuputBuffer is the maximum number of characters to write to outputBuffer
//cchOutputBuffer must be a size >= CONFIG_VAL_MAXLEN + 1
UInt32 RhConfig::GetIniVariable(_In_z_ const WCHAR* configName, _Out_writes_all_(cchBuff) WCHAR* outputBuffer, _In_ UInt32 cchOuputBuffer)
{
    //the buffer needs to be big enough to read the value buffer + null terminator
    if (cchOuputBuffer < CONFIG_VAL_MAXLEN + 1)
    {
        return 0;
    }

    //if we haven't read the config yet try to read
    if (g_iniSettings == NULL)
    {
        ReadConfigIni();
    }

    //if the config wasn't read or reading failed return 0 immediately
    if (g_iniSettings == CONFIG_INI_NOT_AVAIL)
    {
        return 0;
    }

    //find the first name which matches (case insensitive to be compat with environment variable counterpart)
    for (int iSettings = 0; iSettings < RCV_Count; iSettings++)
    {
        if (_wcsicmp(configName, ((ConfigPair*)g_iniSettings)[iSettings].Key) == 0)
        {
            bool nullTerm = FALSE;

            UInt32 iValue;

            for (iValue = 0; (iValue < CONFIG_VAL_MAXLEN + 1) && (iValue < (Int32)cchOuputBuffer); iValue++)
            {
                outputBuffer[iValue] = ((ConfigPair*)g_iniSettings)[iSettings].Value[iValue];

                if (outputBuffer[iValue] == '\0')
                {
                    nullTerm = true;
                    break;
                }
            }

            //return the length of the config value if null terminated else return zero
            return nullTerm ? iValue : 0;
        }
    }

    //if the config key was not found return 0
    return 0;
}

//reads the configuration values from rhconfig.ini and updates g_iniSettings
//if the file is read succesfully and g_iniSettings will be set to a valid ConfigPair[] of length RCV_Count.
//if the file does not exist or reading the file fails,  g_iniSettings is set to CONFIG_INI_NOT_AVAIL
//NOTE: all return paths must set g_iniSettings 
void RhConfig::ReadConfigIni()
{
    if (g_iniSettings == NULL)
    {
        WCHAR* configPath = GetConfigPath();

        //if we couldn't determine the path to the config set g_iniSettings to CONGIF_NOT_AVAIL
        if (configPath == NULL)
        {
            //only set if another thread hasn't initialized the buffer yet, otherwise ignore and let the first setter win
            PalInterlockedCompareExchangePointer(&g_iniSettings, CONFIG_INI_NOT_AVAIL, NULL);

            return;
        }

        //buffer is max file size + 1 for null terminator if needed
        char buff[CONFIG_FILE_MAXLEN + 1];

        //if the file read failed or the file is bigger than the specified buffer this will return zero
        UInt32 fSize = PalReadFileContents(configPath, buff, CONFIG_FILE_MAXLEN);

        //ensure the buffer is null terminated
        buff[fSize] = '\0';

        //delete the configPath
        delete[] configPath;

        //if reading the file contents failed set g_iniSettings to CONFIG_INI_NOT_AVAIL
        if (fSize == 0)
        {
            //only set if another thread hasn't initialized the buffer yet, otherwise ignore and let the first setter win
            PalInterlockedCompareExchangePointer(&g_iniSettings, CONFIG_INI_NOT_AVAIL, NULL);

            return;
        }

        ConfigPair* iniBuff = new (nothrow) ConfigPair[RCV_Count];
        if (iniBuff == NULL)
        {
            //only set if another thread hasn't initialized the buffer yet, otherwise ignore and let the first setter win
            PalInterlockedCompareExchangePointer(&g_iniSettings, CONFIG_INI_NOT_AVAIL, NULL);

            return;
        }

        UInt32 iBuff = 0;
        UInt32 iIniBuff = 0;
        char* currLine;

        //while we haven't reached the max number of config pairs, or the end of the file, read the next line
        while (iIniBuff < RCV_Count && iBuff < fSize)
        {
            //'trim' the leading whitespace
            while (priv_isspace(buff[iBuff]) && (iBuff < fSize))
                iBuff++;

            currLine = &buff[iBuff];

            //find the end of the line
            while ((buff[iBuff] != '\n') && (buff[iBuff] != '\r') && (iBuff < fSize))
                iBuff++;

            //null terminate the line
            buff[iBuff] = '\0';

            //parse the line
            //only increment iIniBuff if the parsing succeeded otherwise reuse the config struct
            if (ParseConfigLine(&iniBuff[iIniBuff], currLine))
            {
                iIniBuff++;
            }

            //advance to the next line;
            iBuff++;
        }

        //initialize the remaining config pairs to "\0"
        while (iIniBuff < RCV_Count)
        {
            iniBuff[iIniBuff].Key[0] = '\0';
            iniBuff[iIniBuff].Value[0] = '\0';
            iIniBuff++;
        }

        //if another thread initialized first let the first setter win
        //delete the iniBuff to avoid leaking memory
        if (PalInterlockedCompareExchangePointer(&g_iniSettings, iniBuff, NULL) != NULL)
        {
            delete[] iniBuff;
        }
    }

    return;
}

//returns the path to the runtime configuration ini
_Ret_maybenull_z_ WCHAR* RhConfig::GetConfigPath()
{

    WCHAR* exePathBuff;

    //get the path to rhconfig.ini, this file is expected to live along side the app 
    //to build the path get the process executable module full path strip off the file name and 
    //append rhconfig.ini
    Int32 pathLen = PalGetModuleFileName(&exePathBuff, NULL);

    if (pathLen <= 0)
    {
        return NULL;
    }
    UInt32 iLastBackslash = 0;

    for (UInt32 iPath = pathLen - 1; iPath > 0; iPath--)
    {
        if (exePathBuff[iPath] == '\\')
        {
            iLastBackslash = iPath;
            break;
        }
    }

    if (iLastBackslash == 0)
    {
        return NULL;
    }

    WCHAR* configPath = new (nothrow) WCHAR[iLastBackslash + 1 + wcslen(CONFIG_INI_FILENAME) + 1];
    if (configPath != NULL)
    {
        //copy the path base and file name
        for (UInt32 i = 0; i <= iLastBackslash; i++)
        {
            configPath[i] = exePathBuff[i];
        }

        for (UInt32 i = 0; i <= wcslen(CONFIG_INI_FILENAME); i++)
        {
            configPath[i + iLastBackslash + 1] = CONFIG_INI_FILENAME[i];
        }
    }

    return configPath;
}

//Parses one line of rhconfig.ini and populates values in the passed in configPair
//returns: true if the parsing was successful, false if the parsing failed. 
//NOTE: if the method fails configPair is left in an unitialized state
bool RhConfig::ParseConfigLine(_Out_ ConfigPair* configPair, _In_z_ const char * line)
{
    UInt32 iLine = 0;
    UInt32 iKey = 0;
    UInt32 iVal = 0;

    //while we haven't reached the end of the key signalled by '=', or the end of the line, or the key maxlen
    while (line[iLine] != '=' && line[iLine] != '\0' && iKey < CONFIG_KEY_MAXLEN)
    {
        configPair->Key[iKey++] = line[iLine++];
    }

    //if the current char is not '=' we reached the key maxlen, or the line ended return false
    if (line[iLine] != '=')
    {
        return FALSE;
    }

    configPair->Key[iKey] = '\0';

    //increment to start of the value
    iLine++;

    //while we haven't reached the end of the line, or val maxlen
    while (line[iLine] != '\0' && iVal < CONFIG_VAL_MAXLEN)
    {
        configPair->Value[iVal++] = line[iLine++];
    }

    //if the current char is not '\0' we didn't reach the end of the line return false
    if (line[iLine] != '\0')
    {
        return FALSE;
    }

    configPair->Value[iVal] = '\0';

    return TRUE;
}

#endif
