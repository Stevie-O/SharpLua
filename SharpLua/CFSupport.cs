using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Globalization;

// placeholder types for Compact Framework

#if WindowsCE

namespace System.Reflection
{
    interface IReflect
    {
        Type UnderlyingSystemType { get; }
    }
    // this didn't work so well :(
#if false
    struct IReflect
    {
        // the guts of this structure were copied from ProxyType
        Type proxy;

        public IReflect(Type proxy)
        {
            this.proxy = proxy;
        }

        /// <summary>
        /// Provide human readable short hand for this proxy object
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return UnderlyingSystemType.ToString();
        }


        public Type UnderlyingSystemType
        {
            get
            {
                return proxy;
            }
        }

        public FieldInfo GetField(string name, BindingFlags bindingAttr)
        {
            return proxy.GetField(name, bindingAttr);
        }

        public FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            return proxy.GetFields(bindingAttr);
        }

        public MemberInfo[] GetMember(string name, BindingFlags bindingAttr)
        {
            return proxy.GetMember(name, bindingAttr);
        }

        public MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            return proxy.GetMembers(bindingAttr);
        }

        public MethodInfo GetMethod(string name, BindingFlags bindingAttr)
        {
            return proxy.GetMethod(name, bindingAttr);
        }

        public MethodInfo GetMethod(string name, BindingFlags bindingAttr, Binder binder, Type[] types, ParameterModifier[] modifiers)
        {
            return proxy.GetMethod(name, bindingAttr, binder, types, modifiers);
        }

        public MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            return proxy.GetMethods(bindingAttr);
        }

        public PropertyInfo GetProperty(string name, BindingFlags bindingAttr)
        {
            return proxy.GetProperty(name, bindingAttr);
        }

        public PropertyInfo GetProperty(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            return proxy.GetProperty(name, bindingAttr, binder, returnType, types, modifiers);
        }

        public PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return proxy.GetProperties(bindingAttr);
        }

        public object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
        {
            return proxy.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
        }

        public static implicit operator IReflect(Type t) { return new IReflect(t); }
        public static explicit operator Type(IReflect r) { return r.proxy; }
        public static bool operator ==(IReflect a, Type t) { return a.proxy == t; }
        public static bool operator ==(Type t, IReflect a) { return a.proxy == t; }
        public static bool operator !=(IReflect a, Type t) { return a.proxy != t; }
        public static bool operator !=(Type t, IReflect a) { return a.proxy != t; }
        public override int GetHashCode() { return proxy.GetHashCode(); }
        public override bool Equals(object obj)
        {
            //   this is not transitive
            //if (obj == null) return this.proxy == null;
            //if (obj is Type) return this.proxy == ((Type)obj);
            if (obj is IReflect) return this.proxy == ((IReflect)obj).proxy;
            return false;
        }
    }
#endif
}

namespace System.Runtime.Serialization
{
    static class dummy { }
}

namespace SharpLua
{

    class ThreadLocalSlot<T>
    {
        readonly LocalDataStoreSlot _slot = Thread.AllocateDataSlot();
        public T Value
        {
            get
            {
                object tmp = Thread.GetData(_slot);
                if (tmp == null) return default(T);
                return (T)tmp;
            }
            set { Thread.SetData(_slot, value); }
        }
    } // class ThreadLocalSlot<T>

    static class ExtensionMethods
    {
        public static StringBuilder AppendFormat(this StringBuilder sb, string format, params object[] args)
        {
            return sb.AppendFormat(null, format, args);
        }

        /// <summary>
        /// Surrogate for AppDomain.GetAssemblies()
        /// </summary>
        /// <param name="unused"></param>
        /// <returns></returns>
        public static List<Assembly> GetAssemblies(this AppDomain unused)
        {
            return ObjectTranslator.SearchAssemblies;
        }

        public static MemberInfo[] GetMember(this Type t, string memberName, MemberTypes memberTypes, BindingFlags bindingFlags)
        {
            MemberInfo[] members = t.GetMember(memberName, bindingFlags);
            if (members == null || members.Length == 0) return members;
            int i;
            for (i = 0; i < members.Length; i++)
            {
                if ((members[i].MemberType & memberTypes) == 0) break;
            }
            if (i == members.Length) return members; // all members were valid
            List<MemberInfo> memberList = new List<MemberInfo>(members);
            for (; i < memberList.Count; i++)
            {
                if ((memberList[i].MemberType & memberTypes) == 0)
                {
                    memberList.RemoveAt(i);
                    i--;
                }
            }
            return memberList.ToArray();
        }
    }
}

#endif
