// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace System
{
    internal static class AppContextConfigHelper
    {
        internal static int GetConfig(string configName, int defaultValue)
        {
            object config = AppContext.GetData(configName);
            switch (config)
            {
                case string str:
                    if (str.StartsWith("0x"))
                    {
                        return Convert.ToInt32(str, 16);
                    }
                    else if (str.StartsWith("0"))
                    {
                        return Convert.ToInt32(str, 8);
                    }
                    else
                    {
                        if (!int.TryParse(str, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out int result))
                        {
                            return defaultValue;
                        }
                        return result;
                    }
                case IConvertible convertible:
                    return convertible.ToInt32(NumberFormatInfo.InvariantInfo);
            }

            return defaultValue;
        }
        internal static short GetConfig(string configName, short defaultValue)
        {
            object config = AppContext.GetData(configName);
            switch (config)
            {
                case string str:
                    if (str.StartsWith("0x"))
                    {
                        return Convert.ToInt16(str, 16);
                    }
                    else if (str.StartsWith("0"))
                    {
                        return Convert.ToInt16(str, 8);
                    }
                    else
                    {
                        if (!short.TryParse(str, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out short result))
                        {
                            return defaultValue;
                        }
                        return result;
                    }
                case IConvertible convertible:
                    return convertible.ToInt16(NumberFormatInfo.InvariantInfo);
            }

            return defaultValue;
        }
    }
}
