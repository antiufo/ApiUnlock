using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ApiUnlock
{
    public unsafe static class ApiUnlocker
    {
        // Unsafe code. Enable unsafe code from Project properties, Build

        const uint INVOCATION_FLAGS_UNKNOWN = 0u;
        const uint INVOCATION_FLAGS_INITIALIZED = 1u;
        const uint INVOCATION_FLAGS_NO_INVOKE = 2u;
        const uint INVOCATION_FLAGS_NEED_SECURITY = 4u;
        const uint INVOCATION_FLAGS_NO_CTOR_INVOKE = 8u;
        const uint INVOCATION_FLAGS_IS_CTOR = 16u;
        const uint INVOCATION_FLAGS_RISKY_METHOD = 32u;
        const uint INVOCATION_FLAGS_NON_W8P_FX_API = 64u;
        const uint INVOCATION_FLAGS_IS_DELEGATE_CTOR = 128u;
        const uint INVOCATION_FLAGS_CONTAINS_STACK_POINTERS = 256u;
        const uint INVOCATION_FLAGS_SPECIAL_FIELD = 16u;
        const uint INVOCATION_FLAGS_FIELD_SPECIAL_CAST = 32u;
        const uint INVOCATION_FLAGS_CONSTRUCTOR_INVOKE = 268435456u;

        const uint APPX_FLAGS_API_CHECK = 16;
        const uint APPX_FLAGS_APPX_MODEL = 2;




        private static bool initialized;
        private static bool initializing;

        private static void Initialize()
        {
            if (!initialized && !initializing)
            {
                initializing = true;

                if (Marshal.SizeOf(typeof(IntPtr)) != 4)
                    throw new NotSupportedException("Only 32 bit platforms are currently supported. Please set Project properties -> Build -> Target platform to x86");


                EnableReflectionForUntrustedAssembly(typeof(ApiUnlocker).GetTypeInfo().Assembly, (f, o, v) => f.SetValue(o, v));
                MarkAppDomainNoProfileCheck();
                initializing = false;


                initialized = true;
            }
        }



        private static void MarkAppDomainNoProfileCheck()
        {
            var t = Type.GetType("System.AppDomain");
            var value = GetField(t, "s_flags", null);
            var intval = Convert.ToUInt32(value);

            intval &= ~APPX_FLAGS_API_CHECK;
            intval &= ~APPX_FLAGS_APPX_MODEL;
            SetField(t, "s_flags", null, Enum.ToObject(value.GetType(), intval));
        }

        public unsafe static void EnableReflectionForUntrustedAssembly(Assembly userAssembly, Action<FieldInfo, object, object> commitCrimeSetValue)
        {

            try
            {
                // Warm up m_flags
                commitCrimeSetValue(typeof(Exception).GetTypeInfo().GetDeclaredField("_className"), new Exception(), null);
            }
            catch { }

            var ptr = (uint*)GetObjectAddress(userAssembly);
            for (int i = 0; ; i++)
            {
                if (*ptr == 0x1000000)
                {
                    (*ptr) |= 0x4000000;
                    break;
                }

                ptr++;
                if (i > 20) throw new Exception("Could not detect offset of RuntimeAssembly.m_flags field.");
            }
        }



        public static object InvokeMethod(Type type, string methodName, object thisObject, params object[] parameters)
        {
            return InvokeMethod(type.GetTypeInfo().GetDeclaredMethod(methodName), thisObject, parameters);
        }

        public static object InvokeMethod(MethodInfo method, object thisObject, params object[] parameters)
        {
            Initialize();
            try
            {
                return method.Invoke(thisObject, parameters);
            }
            catch (MemberAccessException)
            {
                MarkMemberDontNeedSecurity(method);
                return method.Invoke(thisObject, parameters);
            }
        }

        public static void SetField(Type type, string fieldName, object thisObject, object value)
        {
            SetField(type.GetTypeInfo().GetDeclaredField(fieldName), thisObject, value);
        }

        public static void SetField(FieldInfo field, object thisObject, object value)
        {
            Initialize();
            try
            {
                field.SetValue(thisObject, value);
            }
            catch (MemberAccessException)
            {
                MarkMemberDontNeedSecurity(field);
                field.SetValue(thisObject, value);
            }
        }

        public static object GetField(Type type, string fieldName, object thisObject)
        {
            return GetField(type.GetTypeInfo().GetDeclaredField(fieldName), thisObject);
        }

        public static object GetField(FieldInfo field, object thisObject)
        {
            Initialize();
            try
            {
                return field.GetValue(thisObject);
            }
            catch (MemberAccessException)
            {
                MarkMemberDontNeedSecurity(field);
                return field.GetValue(thisObject);
            }
        }



        private unsafe static void MarkMemberDontNeedSecurity(MemberInfo fieldOrMethod)
        {
            var ptr = (uint*)GetObjectAddress(fieldOrMethod);
            for (int i = 0; ; i++)
            {
                var val = *ptr;
                if ((val & INVOCATION_FLAGS_INITIALIZED) != 0 && (val & INVOCATION_FLAGS_NEED_SECURITY) != 0 && val < 1024)
                {
                    (*ptr) &= ~INVOCATION_FLAGS_NEED_SECURITY;
                    (*ptr) &= ~INVOCATION_FLAGS_NON_W8P_FX_API;
                    break;
                }

                ptr++;
                if (i > 20) throw new Exception("Could not detect offset of RuntimeMethodInfo.m_invocationFlags field.");
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct ManagedReferenceHolder
        {
            [FieldOffset(0)]
            public ulong Marker1;

            [FieldOffset(8)]
            public object Reference;
        }

        public unsafe static void* GetObjectAddress(object obj)
        {
            ManagedReferenceHolder holder;
            holder.Reference = obj;

            var q = &holder.Marker1;
            q++;
            var objptr = (void*)(*q);
            return objptr;
        }

        public static readonly Type Win32Native = Type.GetType("Microsoft.Win32.Win32Native");

        public static TDelegate GetWin32Function<TDelegate>(string module, string function)
        {
            var mod = (IntPtr)InvokeMethod(Win32Native, "GetModuleHandle", null, module);
            var func = (IntPtr)InvokeMethod(Win32Native, "GetProcAddress", null, mod, function);
            return GetDelegateForFunctionPointer<TDelegate>((void*)func);
        }

        private static VirtualAllocFunction _VirtualAlloc;

        public static void* AllocateExecutableMemory(int size)
        {
            if (_VirtualAlloc == null) _VirtualAlloc = GetWin32Function<VirtualAllocFunction>("kernel32.dll", "VirtualAlloc");
            return _VirtualAlloc(null, new UIntPtr((uint)size), MEM_COMMIT, PAGE_EXECUTE_READWRITE);
        }

        public static TDelegate GetDelegateForFunctionPointer<TDelegate>(void* pointer)
        {
            return (TDelegate)(object)Marshal.GetDelegateForFunctionPointer(new IntPtr(pointer), typeof(TDelegate));
        }


        public delegate bool VirtualProtectFunction(void* lpAddress, UIntPtr dwSize, int flNewProtect, int* lpflOldProtect);
        public delegate void* VirtualAllocFunction(void* lpAddress, UIntPtr dwSize, int flAllocationType, int flProtect);

        public delegate int MessageBoxWFunction(void* hwnd, string lpText, string lpCaption, uint uType);


        const int MEM_COMMIT = 0x00001000;
        const int PAGE_EXECUTE_READWRITE = 0x40;
        const int PAGE_EXECUTE = 0x10;
        const int PAGE_READWRITE = 0x04;

    }
}
