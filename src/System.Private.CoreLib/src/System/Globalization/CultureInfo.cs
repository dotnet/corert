// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

////////////////////////////////////////////////////////////////////////////
//
//
//
//  Purpose:  This class represents the software preferences of a particular
//            culture or community.  It includes information such as the
//            language, writing system, and a calendar used by the culture
//            as well as methods for common operations such as printing
//            dates and sorting strings.
//
//
//
//  !!!! NOTE WHEN CHANGING THIS CLASS !!!!
//
//  If adding or removing members to this class, please update CultureInfoBaseObject
//  in ndp/clr/src/vm/object.h. Note, the "actual" layout of the class may be
//  different than the order in which members are declared. For instance, all
//  reference types will come first in the class before value types (like ints, bools, etc)
//  regardless of the order in which they are declared. The best way to see the
//  actual order of the class is to do a !dumpobj on an instance of the managed
//  object inside of the debugger.
//
////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;

using Internal.Runtime.Augments;

namespace System.Globalization
{
    public partial class CultureInfo : IFormatProvider, ICloneable
    {
        //--------------------------------------------------------------------//
        //                        Internal Information                        //
        //--------------------------------------------------------------------//

        //--------------------------------------------------------------------//
        // Data members to be serialized:
        //--------------------------------------------------------------------//

        // We use an RFC4646 type string to construct CultureInfo.
        // This string is stored in m_name and is authoritative.
        // We use the m_cultureData to get the data for our object

        private bool _isReadOnly;
        private CompareInfo _compareInfo;
        private TextInfo _textInfo;
        internal NumberFormatInfo numInfo;
        internal DateTimeFormatInfo dateTimeInfo;
        private Calendar _calendar;
        //
        // The CultureData instance that we are going to read data from.
        // For supported culture, this will be the CultureData instance that read data from mscorlib assembly.
        // For customized culture, this will be the CultureData instance that read data from user customized culture binary file.
        //
        internal CultureData m_cultureData;

        internal bool m_isInherited;

        // Names are confusing.  Here are 3 names we have:
        //
        //  new CultureInfo()   m_name         _nonSortName    _sortName
        //      en-US           en-US           en-US           en-US
        //      de-de_phoneb    de-DE_phoneb    de-DE           de-DE_phoneb
        //      fj-fj (custom)  fj-FJ           fj-FJ           en-US (if specified sort is en-US)
        //      en              en
        //
        // Note that in Silverlight we ask the OS for the text and sort behavior, so the
        // textinfo and compareinfo names are the same as the name

        // Note that the name used to be serialized for Everett; it is now serialized
        // because alernate sorts can have alternate names.
        // This has a de-DE, de-DE_phoneb or fj-FJ style name
        internal string m_name;

        // This will hold the non sorting name to be returned from CultureInfo.Name property.
        // This has a de-DE style name even for de-DE_phoneb type cultures
        private string _nonSortName;

        // This will hold the sorting name to be returned from CultureInfo.SortName property.
        // This might be completely unrelated to the culture name if a custom culture.  Ie en-US for fj-FJ.
        // Otherwise its the sort name, ie: de-DE or de-DE_phoneb
        private string _sortName;


        //--------------------------------------------------------------------//
        //
        // Static data members
        //
        //--------------------------------------------------------------------//

        //Get the current user default culture.  This one is almost always used, so we create it by default.
        private static volatile CultureInfo s_userDefaultCulture;

        //
        // All of the following will be created on demand.
        //

        // WARNING: We allow diagnostic tools to directly inspect these three members (s_InvariantCultureInfo, s_DefaultThreadCurrentUICulture and s_DefaultThreadCurrentCulture)
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.

        //The Invariant culture;
        private static volatile CultureInfo s_InvariantCultureInfo;

        //These are defaults that we use if a thread has not opted into having an explicit culture
        private static volatile CultureInfo s_DefaultThreadCurrentUICulture;
        private static volatile CultureInfo s_DefaultThreadCurrentCulture;

        [ThreadStatic]
        private static CultureInfo s_currentThreadCulture;
        [ThreadStatic]
        private static CultureInfo s_currentThreadUICulture;

        private static readonly Lock s_lock = new Lock();
        private static volatile LowLevelDictionary<string, CultureInfo> s_NameCachedCultures;

        //The parent culture.
        private CultureInfo _parent;

        //
        // The CultureData  instance that reads the data provided by our CultureData class.
        //
        // Using a field initializer rather than a static constructor so that the whole class can be lazy
        // init.
        private static readonly bool s_init = Init();
        private static bool Init()
        {
            if (s_InvariantCultureInfo == null)
            {
                CultureInfo temp = new CultureInfo("", false);
                temp._isReadOnly = true;
                s_InvariantCultureInfo = temp;
            }

            s_userDefaultCulture = GetUserDefaultCulture();
            return true;
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  CultureInfo Constructors
        //
        ////////////////////////////////////////////////////////////////////////


        public CultureInfo(String name)
            : this(name, true)
        {
        }


        internal CultureInfo(String name, bool useUserOverride)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name",
                    SR.ArgumentNull_String);
            }

#if CORERT
            // CORERT-TODO CultureInfo
            if (s_InvariantCultureInfo == null)
            {
                m_cultureData = CultureData.GetCultureData("", useUserOverride);
            }
            else
            {
                m_cultureData = s_InvariantCultureInfo.m_cultureData;
            }
            m_name = _sortName = _nonSortName = name;
            m_isInherited = false;
#else
            // Get our data providing record
            this.m_cultureData = CultureData.GetCultureData(name, useUserOverride);

            if (this.m_cultureData == null)
                throw new CultureNotFoundException(
                    "name", name, SR.Argument_CultureNotSupported);

            this.m_name = this.m_cultureData.CultureName;
            this.m_isInherited = (this.GetType() != typeof(System.Globalization.CultureInfo));
#endif // CORERT
        }


        // We do this to try to return the system UI language and the default user languages
        // This method will fallback if this fails (like Invariant)
        //
        // TODO: It would appear that this is only ever called with userOveride = true
        // and this method only has one caller.  Can we fold it into the caller?
        private static CultureInfo GetCultureByName(String name, bool userOverride)
        {
            CultureInfo ci = null;
            // Try to get our culture
            try
            {
                ci = userOverride ? new CultureInfo(name) : CultureInfo.GetCultureInfo(name);
            }
            catch (ArgumentException)
            {
            }

            if (ci == null)
            {
                ci = InvariantCulture;
            }

            return ci;
        }

        //        //
        //        // Return a specific culture.  A tad irrelevent now since we always return valid data
        //        // for neutral locales.
        //        //
        //        // Note that there's interesting behavior that tries to find a smaller name, ala RFC4647,
        //        // if we can't find a bigger name.  That doesn't help with things like "zh" though, so
        //        // the approach is of questionable value
        //        //

        internal static bool VerifyCultureName(String cultureName, bool throwException)
        {
            // This function is used by ResourceManager.GetResourceFileName().
            // ResourceManager searches for resource using CultureInfo.Name,
            // so we should check against CultureInfo.Name.

            for (int i = 0; i < cultureName.Length; i++)
            {
                char c = cultureName[i];
                // TODO: Names can only be RFC4646 names (ie: a-zA-Z0-9) while this allows any unicode letter/digit
                if (Char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    continue;
                }
                if (throwException)
                {
                    throw new ArgumentException(SR.Format(SR.Argument_InvalidResourceCultureName, cultureName));
                }
                return false;
            }
            return true;
        }

        internal static bool VerifyCultureName(CultureInfo culture, bool throwException)
        {
            //If we have an instance of one of our CultureInfos, the user can't have changed the
            //name and we know that all names are valid in files.
            if (!culture.m_isInherited)
            {
                return true;
            }

            return VerifyCultureName(culture.Name, throwException);
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  CurrentCulture
        //
        //  This instance provides methods based on the current user settings.
        //  These settings are volatile and may change over the lifetime of the
        //  thread.
        //
        ////////////////////////////////////////////////////////////////////////

        //
        // We use the following order to return CurrentCulture and CurrentUICulture
        //      o   Use WinRT to return the current user profile language
        //      o   use current thread culture if the user already set one using CurrentCulture/CurrentUICulture
        //      o   use thread culture if the user already set one using DefaultThreadCurrentCulture
        //          or DefaultThreadCurrentUICulture
        //      o   Use NLS default user culture
        //      o   Use NLS default system culture
        //      o   Use Invariant culture
        //
        public static CultureInfo CurrentCulture
        {
            get
            {
#if CORERT
                // CORERT-TODO CultureInfo                
                return CultureInfo.InvariantCulture;
#else
                CultureInfo ci = GetUserDefaultCultureCacheOverride();
                if (ci != null)
                {
                    return ci;
                }

                if (s_currentThreadCulture != null)
                {
                    return s_currentThreadCulture;
                }

                ci = s_DefaultThreadCurrentCulture;
                if (ci != null)
                {
                    return ci;
                }

                // if s_userDefaultCulture == null means CultureInfo statics didn't get initialized yet. this can happen if there early static
                // method get executed which eventually hit the cultureInfo code while CultureInfo statics didn’t get chance to initialize
                if (s_userDefaultCulture == null)
                {
                    Init();
                }

                Debug.Assert(s_userDefaultCulture != null);
                return s_userDefaultCulture;
#endif
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                WinRTInteropCallbacks callbacks = WinRTInterop.UnsafeCallbacks;
                if (callbacks != null && callbacks.IsAppxModel())
                {
                    callbacks.SetGlobalDefaultCulture(value);
                    return;
                }

                s_currentThreadCulture = value;
            }
        }

        public static CultureInfo CurrentUICulture
        {
            get
            {
#if CORERT
                // CORERT-TODO CultureInfo
                return CultureInfo.InvariantCulture;
#else
                CultureInfo ci = GetUserDefaultCultureCacheOverride();
                if (ci != null)
                {
                    return ci;
                }

                if (s_currentThreadUICulture != null)
                {
                    return s_currentThreadUICulture;
                }

                ci = s_DefaultThreadCurrentUICulture;
                if (ci != null)
                {
                    return ci;
                }

                // if s_userDefaultCulture == null means CultureInfo statics didn't get initialized yet. this can happen if there early static
                // method get executed which eventually hit the cultureInfo code while CultureInfo statics didn’t get chance to initialize
                if (s_userDefaultCulture == null)
                {
                    Init();
                }

                Debug.Assert(s_userDefaultCulture != null);
                return s_userDefaultCulture;
#endif // CORERT
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                CultureInfo.VerifyCultureName(value, true);

                WinRTInteropCallbacks callbacks = WinRTInterop.UnsafeCallbacks;
                if (callbacks != null && callbacks.IsAppxModel())
                {
                    callbacks.SetGlobalDefaultCulture(value);
                    return;
                }

                s_currentThreadUICulture = value;
            }
        }

        public static CultureInfo DefaultThreadCurrentCulture
        {
            get { return s_DefaultThreadCurrentCulture; }
            set
            {
                // If you add pre-conditions to this method, check to see if you also need to
                // add them to Thread.CurrentCulture.set.

                s_DefaultThreadCurrentCulture = value;
            }
        }

        public static CultureInfo DefaultThreadCurrentUICulture
        {
            get { return s_DefaultThreadCurrentUICulture; }
            set
            {
                //If they're trying to use a Culture with a name that we can't use in resource lookup,
                //don't even let them set it on the thread.

                // If you add more pre-conditions to this method, check to see if you also need to
                // add them to Thread.CurrentUICulture.set.

                if (value != null)
                {
                    CultureInfo.VerifyCultureName(value, true);
                }

                s_DefaultThreadCurrentUICulture = value;
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  InvariantCulture
        //
        //  This instance provides methods, for example for casing and sorting,
        //  that are independent of the system and current user settings.  It
        //  should be used only by processes such as some system services that
        //  require such invariant results (eg. file systems).  In general,
        //  the results are not linguistically correct and do not match any
        //  culture info.
        //
        ////////////////////////////////////////////////////////////////////////


        public static CultureInfo InvariantCulture
        {
            get
            {
                return (s_InvariantCultureInfo);
            }
        }


        ////////////////////////////////////////////////////////////////////////
        //
        //  Parent
        //
        //  Return the parent CultureInfo for the current instance.
        //
        ////////////////////////////////////////////////////////////////////////

        public virtual CultureInfo Parent
        {
            get
            {
                if (null == _parent)
                {
                    try
                    {
                        string parentName = this.m_cultureData.SPARENT;

                        if (String.IsNullOrEmpty(parentName))
                        {
                            _parent = InvariantCulture;
                        }
                        else
                        {
                            _parent = new CultureInfo(parentName, this.m_cultureData.UseUserOverride);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // For whatever reason our IPARENT or SPARENT wasn't correct, so use invariant
                        // We can't allow ourselves to fail.  In case of custom cultures the parent of the
                        // current custom culture isn't installed.
                        _parent = InvariantCulture;
                    }
                }
                return _parent;
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  Name
        //
        //  Returns the full name of the CultureInfo. The name is in format like
        //  "en-US"  This version does NOT include sort information in the name.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual String Name
        {
            get
            {
                // We return non sorting name here.
                if (_nonSortName == null)
                {
                    _nonSortName = this.m_cultureData.SNAME;
                    if (_nonSortName == null)
                    {
                        _nonSortName = String.Empty;
                    }
                }
                return _nonSortName;
            }
        }

        // This one has the sort information (ie: de-DE_phoneb)
        internal String SortName
        {
            get
            {
                if (_sortName == null)
                {
                    _sortName = this.m_cultureData.SCOMPAREINFO;
                }

                return _sortName;
            }
        }


        ////////////////////////////////////////////////////////////////////////
        //
        //  DisplayName
        //
        //  Returns the full name of the CultureInfo in the localized language.
        //  For example, if the localized language of the runtime is Spanish and the CultureInfo is
        //  US English, "Ingles (Estados Unidos)" will be returned.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual String DisplayName
        {
            get
            {
                Contract.Ensures(Contract.Result<String>() != null);
                Debug.Assert(m_name != null, "[CultureInfo.DisplayName] Always expect m_name to be set");

                return m_cultureData.SLOCALIZEDDISPLAYNAME;
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  GetNativeName
        //
        //  Returns the full name of the CultureInfo in the native language.
        //  For example, if the CultureInfo is US English, "English
        //  (United States)" will be returned.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual String NativeName
        {
            get
            {
                Contract.Ensures(Contract.Result<String>() != null);
                return (this.m_cultureData.SNATIVEDISPLAYNAME);
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  GetEnglishName
        //
        //  Returns the full name of the CultureInfo in English.
        //  For example, if the CultureInfo is US English, "English
        //  (United States)" will be returned.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual String EnglishName
        {
            get
            {
                Contract.Ensures(Contract.Result<String>() != null);
                return (this.m_cultureData.SENGDISPLAYNAME);
            }
        }

        // ie: en
        public virtual String TwoLetterISOLanguageName
        {
            get
            {
                Contract.Ensures(Contract.Result<String>() != null);
                return (this.m_cultureData.SISO639LANGNAME);
            }
        }


        ////////////////////////////////////////////////////////////////////////
        //
        //  CompareInfo               Read-Only Property
        //
        //  Gets the CompareInfo for this culture.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual CompareInfo CompareInfo
        {
            get
            {
                if (_compareInfo == null)
                {
                    // Since CompareInfo's don't have any overrideable properties, get the CompareInfo from
                    // the Non-Overridden CultureInfo so that we only create one CompareInfo per culture
                    CompareInfo temp = UseUserOverride
                                        ? GetCultureInfo(this.m_name).CompareInfo
                                        : new CompareInfo(this);
                    if (OkayToCacheClassWithCompatibilityBehavior)
                    {
                        _compareInfo = temp;
                    }
                    else
                    {
                        return temp;
                    }
                }
                return (_compareInfo);
            }
        }

        static private bool OkayToCacheClassWithCompatibilityBehavior
        {
            get
            {
                return true;
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  TextInfo
        //
        //  Gets the TextInfo for this culture.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual TextInfo TextInfo
        {
            get
            {
                if (_textInfo == null)
                {
                    // Make a new textInfo
                    TextInfo tempTextInfo = new TextInfo(this.m_cultureData);
                    tempTextInfo.SetReadOnlyState(_isReadOnly);

                    if (OkayToCacheClassWithCompatibilityBehavior)
                    {
                        _textInfo = tempTextInfo;
                    }
                    else
                    {
                        return tempTextInfo;
                    }
                }
                return (_textInfo);
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //  Equals
        //
        //  Implements Object.Equals().  Returns a boolean indicating whether
        //  or not object refers to the same CultureInfo as the current instance.
        //
        ////////////////////////////////////////////////////////////////////////


        public override bool Equals(Object value)
        {
            if (Object.ReferenceEquals(this, value))
                return true;

            CultureInfo that = value as CultureInfo;

            if (that != null)
            {
                // using CompareInfo to verify the data passed through the constructor
                // CultureInfo(String cultureName, String textAndCompareCultureName)

                return (this.Name.Equals(that.Name) && this.CompareInfo.Equals(that.CompareInfo));
            }

            return (false);
        }


        ////////////////////////////////////////////////////////////////////////
        //
        //  GetHashCode
        //
        //  Implements Object.GetHashCode().  Returns the hash code for the
        //  CultureInfo.  The hash code is guaranteed to be the same for CultureInfo A
        //  and B where A.Equals(B) is true.
        //
        ////////////////////////////////////////////////////////////////////////

        public override int GetHashCode()
        {
            return (this.Name.GetHashCode() + this.CompareInfo.GetHashCode());
        }


        ////////////////////////////////////////////////////////////////////////
        //
        //  ToString
        //
        //  Implements Object.ToString().  Returns the name of the CultureInfo,
        //  eg. "de-DE_phoneb", "en-US", or "fj-FJ".
        //
        ////////////////////////////////////////////////////////////////////////


        public override String ToString()
        {
            return m_name;
        }


        public virtual Object GetFormat(Type formatType)
        {
            if (formatType == typeof(NumberFormatInfo))
                return (NumberFormat);
            if (formatType == typeof(DateTimeFormatInfo))
                return (DateTimeFormat);
            return (null);
        }

        public virtual bool IsNeutralCulture
        {
            get
            {
                return this.m_cultureData.IsNeutralCulture;
            }
        }

        public virtual NumberFormatInfo NumberFormat
        {
            get
            {
                if (numInfo == null)
                {
                    NumberFormatInfo temp = new NumberFormatInfo(this.m_cultureData);
                    temp.isReadOnly = _isReadOnly;
                    numInfo = temp;
                }
                return (numInfo);
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value", SR.ArgumentNull_Obj);
                }
                VerifyWritable();
                numInfo = value;
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //
        // GetDateTimeFormatInfo
        //
        // Create a DateTimeFormatInfo, and fill in the properties according to
        // the CultureID.
        //
        ////////////////////////////////////////////////////////////////////////
        public virtual DateTimeFormatInfo DateTimeFormat
        {
            get
            {
                if (dateTimeInfo == null)
                {
                    // Change the calendar of DTFI to the specified calendar of this CultureInfo.
                    DateTimeFormatInfo temp = new DateTimeFormatInfo(this.m_cultureData, this.Calendar);
                    temp._isReadOnly = _isReadOnly;
                    System.Threading.Interlocked.MemoryBarrier();
                    dateTimeInfo = temp;
                }
                return (dateTimeInfo);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value", SR.ArgumentNull_Obj);
                }
                VerifyWritable();
                dateTimeInfo = value;
            }
        }

        /*=================================GetCalendarInstance==========================
        **Action: Map a Win32 CALID to an instance of supported calendar.
        **Returns: An instance of calendar.
        **Arguments: calType    The Win32 CALID
        **Exceptions:
        **      Shouldn't throw exception since the calType value is from our data table or from Win32 registry.
        **      If we are in trouble (like getting a weird value from Win32 registry), just return the GregorianCalendar.
        ============================================================================*/
        internal static Calendar GetCalendarInstance(CalendarId calType)
        {
            if (calType == CalendarId.GREGORIAN)
            {
                return (new GregorianCalendar());
            }
            return GetCalendarInstanceRare(calType);
        }

        //This function exists as a shortcut to prevent us from loading all of the non-gregorian
        //calendars unless they're required.
        internal static Calendar GetCalendarInstanceRare(CalendarId calType)
        {
            Debug.Assert(calType != CalendarId.GREGORIAN, "calType!=CalendarId.GREGORIAN");

            switch (calType)
            {
                case CalendarId.GREGORIAN_US:               // Gregorian (U.S.) calendar
                case CalendarId.GREGORIAN_ME_FRENCH:        // Gregorian Middle East French calendar
                case CalendarId.GREGORIAN_ARABIC:           // Gregorian Arabic calendar
                case CalendarId.GREGORIAN_XLIT_ENGLISH:     // Gregorian Transliterated English calendar
                case CalendarId.GREGORIAN_XLIT_FRENCH:      // Gregorian Transliterated French calendar
                    return (new GregorianCalendar((GregorianCalendarTypes)calType));
                case CalendarId.TAIWAN:                     // Taiwan Era calendar
                    return (new TaiwanCalendar());
                case CalendarId.JAPAN:                      // Japanese Emperor Era calendar
                    return (new JapaneseCalendar());
                case CalendarId.KOREA:                      // Korean Tangun Era calendar
                    return (new KoreanCalendar());
                case CalendarId.THAI:                       // Thai calendar
                    return (new ThaiBuddhistCalendar());
                case CalendarId.HIJRI:                      // Hijri (Arabic Lunar) calendar
                    return (new HijriCalendar());
                case CalendarId.HEBREW:                     // Hebrew (Lunar) calendar
                    return (new HebrewCalendar());
                case CalendarId.UMALQURA:
                    return (new UmAlQuraCalendar());
                case CalendarId.PERSIAN:
                    return (new PersianCalendar());
            }
            return (new GregorianCalendar());
        }

        /*=================================Calendar==========================
        **Action: Return/set the default calendar used by this culture.
        ** This value can be overridden by regional option if this is a current culture.
        **Returns:
        **Arguments:
        **Exceptions:
        **  ArgumentNull_Obj if the set value is null.
        ============================================================================*/
        public virtual Calendar Calendar
        {
            get
            {
                if (_calendar == null)
                {
                    Debug.Assert(this.m_cultureData.CalendarIds.Length > 0, "this.m_cultureData.CalendarIds.Length > 0");
                    // Get the default calendar for this culture.  Note that the value can be
                    // from registry if this is a user default culture.
                    Calendar newObj = this.m_cultureData.DefaultCalendar;

                    System.Threading.Interlocked.MemoryBarrier();
                    newObj.SetReadOnlyState(_isReadOnly);
                    _calendar = newObj;
                }
                return (_calendar);
            }
        }

        /*=================================OptionCalendars==========================
        **Action: Return an array of the optional calendar for this culture.
        **Returns: an array of Calendar.
        **Arguments:
        **Exceptions:
        ============================================================================*/


        public virtual Calendar[] OptionalCalendars
        {
            get
            {
                Contract.Ensures(Contract.Result<Calendar[]>() != null);

                //
                // This property always returns a new copy of the calendar array.
                //
                CalendarId[] calID = this.m_cultureData.CalendarIds;
                Calendar[] cals = new Calendar[calID.Length];
                for (int i = 0; i < cals.Length; i++)
                {
                    cals[i] = GetCalendarInstance(calID[i]);
                }
                return (cals);
            }
        }


        private bool UseUserOverride
        {
            get
            {
                return (this.m_cultureData.UseUserOverride);
            }
        }

        public virtual Object Clone()
        {
            CultureInfo ci = (CultureInfo)MemberwiseClone();
            ci._isReadOnly = false;

            //If this is exactly our type, we can make certain optimizations so that we don't allocate NumberFormatInfo or DTFI unless
            //they've already been allocated.  If this is a derived type, we'll take a more generic codepath.
            if (!m_isInherited)
            {
                if (this.dateTimeInfo != null)
                {
                    ci.dateTimeInfo = (DateTimeFormatInfo)this.dateTimeInfo.Clone();
                }
                if (this.numInfo != null)
                {
                    ci.numInfo = (NumberFormatInfo)this.numInfo.Clone();
                }
            }
            else
            {
                ci.DateTimeFormat = (DateTimeFormatInfo)this.DateTimeFormat.Clone();
                ci.NumberFormat = (NumberFormatInfo)this.NumberFormat.Clone();
            }

            if (_textInfo != null)
            {
                ci._textInfo = (TextInfo)_textInfo.Clone();
            }

            if (_calendar != null)
            {
                ci._calendar = (Calendar)_calendar.Clone();
            }

            return (ci);
        }

        public static CultureInfo ReadOnly(CultureInfo ci)
        {
            if (ci == null)
            {
                throw new ArgumentNullException("ci");
            }
            Contract.Ensures(Contract.Result<CultureInfo>() != null);
            Contract.EndContractBlock();

            if (ci.IsReadOnly)
            {
                return (ci);
            }
            CultureInfo newInfo = (CultureInfo)(ci.MemberwiseClone());

            if (!ci.IsNeutralCulture)
            {
                //If this is exactly our type, we can make certain optimizations so that we don't allocate NumberFormatInfo or DTFI unless
                //they've already been allocated.  If this is a derived type, we'll take a more generic codepath.
                if (!ci.m_isInherited)
                {
                    if (ci.dateTimeInfo != null)
                    {
                        newInfo.dateTimeInfo = DateTimeFormatInfo.ReadOnly(ci.dateTimeInfo);
                    }
                    if (ci.numInfo != null)
                    {
                        newInfo.numInfo = NumberFormatInfo.ReadOnly(ci.numInfo);
                    }
                }
                else
                {
                    newInfo.DateTimeFormat = DateTimeFormatInfo.ReadOnly(ci.DateTimeFormat);
                    newInfo.NumberFormat = NumberFormatInfo.ReadOnly(ci.NumberFormat);
                }
            }

            if (ci._textInfo != null)
            {
                newInfo._textInfo = TextInfo.ReadOnly(ci._textInfo);
            }

            if (ci._calendar != null)
            {
                newInfo._calendar = Calendar.ReadOnly(ci._calendar);
            }

            // Don't set the read-only flag too early.
            // We should set the read-only flag here.  Otherwise, info.DateTimeFormat will not be able to set.
            newInfo._isReadOnly = true;

            return (newInfo);
        }


        public bool IsReadOnly
        {
            get
            {
                return (_isReadOnly);
            }
        }

        private void VerifyWritable()
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ReadOnly);
            }
        }

        // Helper function both both overloads of GetCachedReadOnlyCulture.
        internal static CultureInfo GetCultureInfoHelper(string name)
        {
            // There is a race condition in this code with the side effect that the second thread's value
            // clobbers the first in the dictionary. This is an acceptable race since the CultureInfo objects
            // are content equal (but not reference equal). Since we make no guarantees there, this race is
            // acceptable.

            // retval is our return value.
            CultureInfo retval;

            if (name == null)
            {
                return null;
            }

            // Temporary hashtable for the names.
            LowLevelDictionary<string, CultureInfo> tempNameHT = s_NameCachedCultures;

            name = CultureData.AnsiToLower(name);

            // We expect the same result for both hashtables, but will test individually for added safety.
            if (tempNameHT == null)
            {
                tempNameHT = new LowLevelDictionary<string, CultureInfo>();
            }
            else
            {
                bool ret;
                using (LockHolder.Hold(s_lock))
                {
                    ret = tempNameHT.TryGetValue(name, out retval);
                }

                if (ret && retval != null)
                {
                    return retval;
                }
            }
            try
            {
                retval = new CultureInfo(name, false);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // Set it to read-only
            retval._isReadOnly = true;

            // Remember our name (as constructed).  Do NOT use alternate sort name versions because
            // we have internal state representing the sort.  (So someone would get the wrong cached version)
            string newName = CultureData.AnsiToLower(retval.m_name);

            // We add this new culture info object to both tables.
            using (LockHolder.Hold(s_lock))
            {
                tempNameHT[newName] = retval;
            }

            s_NameCachedCultures = tempNameHT;

            // Finally, return our new CultureInfo object.
            return retval;
        }

        // Gets a cached copy of the specified culture from an internal hashtable (or creates it
        // if not found).  (Named version)
        internal static CultureInfo GetCultureInfo(string name)
        {
            // Make sure we have a valid, non-zero length string as name
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            CultureInfo retval = GetCultureInfoHelper(name);
            if (retval == null)
            {
                throw new CultureNotFoundException(
                    "name", name, SR.Argument_CultureNotSupported);
            }
            return retval;
        }
    }
}

