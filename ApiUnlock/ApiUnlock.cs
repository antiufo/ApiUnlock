// (c) 2012 Andrea Martinelli <martinelli-andrea@outlook.com>
// http://at-my-window.blogspot.com/?page=fullwin32
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
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
            var t = GetType("System.AppDomain");
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


        #region method/field/property reflection method wrappers
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
        public static void SetProperty(Type type, string propertyName, object thisObject, object value, params object[] index)
        {
            SetProperty(type.GetTypeInfo().GetDeclaredProperty(propertyName), thisObject, value, index);
        }

        public static void SetProperty(PropertyInfo property, object thisObject, object value, params object[] index)
        {
            Initialize();
            try
            {
                property.SetValue(thisObject, value, index);
            }
            catch (MemberAccessException)
            {
                MarkMemberDontNeedSecurity(property);
                property.SetValue(thisObject, value, index);
            }
        }


        public static object GetProperty(Type type, string propertyName, object thisObject, params object[] index)
        {
            return GetProperty(type.GetTypeInfo().GetDeclaredProperty(propertyName), thisObject, index);
        }

        public static object GetProperty(PropertyInfo property, object thisObject, params object[] index)
        {
            Initialize();
            try
            {
                return property.GetValue(thisObject, index);
            }
            catch (MemberAccessException)
            {
                MarkMemberDontNeedSecurity(property);
                return property.GetValue(thisObject, index);
            }
        }

        public static Type GetType(string typeName)
        {
            return Type.GetType(typeName);
        }
        #endregion



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
            public ulong Marker;

            [FieldOffset(8)]
            public object Reference;
        }

        public unsafe static void* GetObjectAddress(object obj)
        {
            ManagedReferenceHolder holder;
            holder.Reference = obj;

            var q = &holder.Marker;
            q++;
            var objptr = (void*)(*q);
            return objptr;
        }

        public static readonly Type Win32Native = GetType("Microsoft.Win32.Win32Native");

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

        // This doesn't work yet. Assemblies are loaded, but some types may fail to load
        // due to missing referenced assemblies, even if they have already been loaded manually.

        //private static List<Assembly> loadedAssemblies = new List<Assembly>();
        //public static Assembly LoadFrameworkAssembly(string name, bool loadReferencedAssemblies)
        //{
        //    //var asm = (Assembly)InvokeMethod(typeof(Assembly).GetTypeInfo().GetDeclaredMethods("LoadWithPartialName").Single(x =>
        //    //{
        //    //    var parameters = x.GetParameters();
        //    //    return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
        //    //}), null, name);
        //    var mscorlib = (string)GetProperty(typeof(Assembly), "Location", typeof(object).GetTypeInfo().Assembly);
        //    var root = Path.GetDirectoryName(mscorlib);

        //    var asm = LoadAssemblyFromFile(Path.Combine(root, name + ".dll"));
        //    if (loadedAssemblies.Contains(asm)) return asm;
        //    loadedAssemblies.Add(asm);
        //    if (loadReferencedAssemblies)
        //    {
        //        var referenced = (AssemblyName[])InvokeMethod(typeof(Assembly).GetTypeInfo().GetDeclaredMethod("GetReferencedAssemblies"), asm);
        //        foreach (var reference in referenced)
        //        {
        //            LoadFrameworkAssembly(reference.Name, loadReferencedAssemblies);
        //        }
        //    }
        //    return asm;
        //}

        public static Assembly LoadAssemblyFromFile(string path)
        {
            return (Assembly)InvokeMethod(typeof(Assembly).GetTypeInfo().GetDeclaredMethods("LoadFile").Single(x => x.GetParameters().Length == 1), null, path);
        }

        private delegate bool VirtualProtectFunction(void* lpAddress, UIntPtr dwSize, int flNewProtect, int* lpflOldProtect);
        private delegate void* VirtualAllocFunction(void* lpAddress, UIntPtr dwSize, int flAllocationType, int flProtect);

        const int MEM_COMMIT = 0x00001000;

        const int PAGE_EXECUTE_READWRITE = 0x40;
        const int PAGE_EXECUTE = 0x10;
        const int PAGE_READWRITE = 0x04;

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



    }
}
