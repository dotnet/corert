// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.Reflection;
using Microsoft.Win32;

namespace System.Diagnostics.Tracing
{
    public partial class EventSource : IDisposable
    {
        private int GetParameterCount(EventMetadata eventData)
        {
            int paramCount;
            if(eventData.Parameters == null)
            {
                paramCount = eventData.ParameterTypes.Length;
            }
            else
            {
                paramCount = eventData.Parameters.Length;
            }
            
            return paramCount;
        }

        private Type GetDataType(EventMetadata eventData, int parameterId)
        {
            Type dataType;
            if(eventData.Parameters == null)
            {
                dataType = EventTypeToType(eventData.ParameterTypes[parameterId]);
            }
            else
            {
                dataType = eventData.Parameters[parameterId].ParameterType;
            }
            
            return dataType;
        }

        private static readonly bool m_EventSourcePreventRecursion = true;

        /*
         EventMetadata was public in the separate System.Diagnostics.Tracing assembly(pre NS2.0), 
         now the move to CoreLib marked them as private.
         While they are technically private (it's a contract used between the library and the ILC toolchain), 
         we need them to be rooted and exported from shared library for the system to work.
         For now I'm simply marking them as public again.A cleaner solution might be to use.rd.xml to 
         root them and modify shared library definition to force export them.
        */
#if ES_BUILD_PN
        public
#else
        internal
#endif
        partial struct EventMetadata
        {
            public EventMetadata(EventDescriptor descriptor,
                EventTags tags,
                bool enabledForAnyListener,
                bool enabledForETW,
                string name,
                string message,
                EventParameterType[] parameterTypes)
            {
                this.Descriptor = descriptor;
                this.Tags = tags;
                this.EnabledForAnyListener = enabledForAnyListener;
                this.EnabledForETW = enabledForETW;
#if FEATURE_PERFTRACING
                this.EnabledForEventPipe = false;
#endif
                this.TriggersActivityTracking = 0;
                this.Name = name;
                this.Message = message;
                this.Parameters = null;
                this.TraceLoggingEventTypes = null;
                this.ActivityOptions = EventActivityOptions.None;
                this.ParameterTypes = parameterTypes;
                this.HasRelatedActivityID = false;
                this.EventHandle = IntPtr.Zero;
            }
        }
        
        public enum EventParameterType
        {
            Boolean,
            Byte,
            SByte,
            Char,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            IntPtr,
            Single,
            Double,
            Decimal,
            Guid,
            String
        }

        private Type EventTypeToType(EventParameterType type)
        {
            switch (type)
            {
                case EventParameterType.Boolean:
                    return typeof(bool);
                case EventParameterType.Byte:
                    return typeof(byte);
                case EventParameterType.SByte:
                    return typeof(sbyte);
                case EventParameterType.Char:
                    return typeof(char);
                case EventParameterType.Int16:
                    return typeof(short);
                case EventParameterType.UInt16:
                    return typeof(ushort);
                case EventParameterType.Int32:
                    return typeof(int);
                case EventParameterType.UInt32:
                    return typeof(uint);
                case EventParameterType.Int64:
                    return typeof(long);
                case EventParameterType.UInt64:
                    return typeof(ulong);
                case EventParameterType.IntPtr:
                    return typeof(IntPtr);
                case EventParameterType.Single:
                    return typeof(float);
                case EventParameterType.Double:
                    return typeof(double);
                case EventParameterType.Decimal:
                    return typeof(decimal);
                case EventParameterType.Guid:
                    return typeof(Guid);
                case EventParameterType.String:
                    return typeof(string);
                default:
                    // TODO: should I throw an exception here?
                    return null;
            }
        }
    }
}
