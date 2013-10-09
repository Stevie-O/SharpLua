using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace SharpLua
{
    /*
     * Type checking and conversion functions.
     *
     * Author: Fabio Mascarenhas
     * Version: 1.0
     */
    class CheckType
    {
        private ObjectTranslator translator;

        ExtractValue extractNetObject;
        ExtractValue extractNull;
        Dictionary<Type, ExtractValue> extractValues = new Dictionary<Type, ExtractValue>(FastTypeComparer.Instance);

        class FastTypeComparer : IEqualityComparer<Type>, IEqualityComparer<RuntimeTypeHandle>
        {
            public static readonly FastTypeComparer Instance = new FastTypeComparer();

            public bool Equals(Type x, Type y)
            {
                if (x == null) return (y == null);
                return x.Equals(y);
            }

            public int GetHashCode(Type obj)
            {
                if (obj == null) return 0;
                return obj.GetHashCode();
            }

            public bool Equals(RuntimeTypeHandle x, RuntimeTypeHandle y)
            {
#if WindowsCE
                return x.Equals(y);
#else
                return x.Value == y.Value;
#endif
            }

            public int GetHashCode(RuntimeTypeHandle obj)
            {
#if WindowsCE
                return obj.GetHashCode();
#else
                return (int)(long)obj.Value;
#endif
            }
        }

        public CheckType(ObjectTranslator translator)
        {
            this.translator = translator;

            extractValues.Add(typeof(object), new ExtractValue(getAsObject));
            extractValues.Add(typeof(sbyte), new ExtractValue(getAsSbyte));
            extractValues.Add(typeof(byte), new ExtractValue(getAsByte));
            extractValues.Add(typeof(short), new ExtractValue(getAsShort));
            extractValues.Add(typeof(ushort), new ExtractValue(getAsUshort));
            extractValues.Add(typeof(int), new ExtractValue(getAsInt));
            extractValues.Add(typeof(uint), new ExtractValue(getAsUint));
            extractValues.Add(typeof(long), new ExtractValue(getAsLong));
            extractValues.Add(typeof(ulong), new ExtractValue(getAsUlong));
            extractValues.Add(typeof(double), new ExtractValue(getAsDouble));
            extractValues.Add(typeof(char), new ExtractValue(getAsChar));
            extractValues.Add(typeof(float), new ExtractValue(getAsFloat));
            extractValues.Add(typeof(decimal), new ExtractValue(getAsDecimal));
            extractValues.Add(typeof(bool), new ExtractValue(getAsBoolean));
            extractValues.Add(typeof(string), new ExtractValue(getAsString));
            extractValues.Add(typeof(LuaFunction), new ExtractValue(getAsFunction));
            extractValues.Add(typeof(LuaTable), new ExtractValue(getAsTable));
            extractValues.Add(typeof(LuaUserData), new ExtractValue(getAsUserdata));

            extractNull = new ExtractValue(getNull);
            extractNetObject = new ExtractValue(getAsNetObject);
        }

        /*
         * Checks if the value at Lua stack index stackPos matches paramType,
         * returning a conversion function if it does and null otherwise.
         */
        internal ExtractValue getExtractor(IReflect paramType)
        {
            return getExtractor(paramType.UnderlyingSystemType);
        }
        internal ExtractValue getExtractor(Type paramType)
        {
            if (paramType.IsByRef) paramType = paramType.GetElementType();

            Type runtimeHandleValue = paramType;

            ExtractValue ev;
            if (!extractValues.TryGetValue(runtimeHandleValue, out ev))
                ev = extractNetObject;
            return ev;
        }

        /// <summary>
        /// Generator that yields a list of all Types we could find that match the given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static IEnumerable<Type> FindTypes(string name)
        {
            Type t = Type.GetType(name, false);
            if (t != null) yield return t;
#if !WindowsCE
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(name, false);
                if (t != null) yield return t;
            }
#endif
        }

        /// <summary>
        /// Tries to find a type with the specified name, that are derived from the specified base type
        /// </summary>
        /// <param name="name"></param>
        /// <param name="desiredBaseType"></param>
        /// <returns></returns>
        static Type FindCompatibleType(string name, Type desiredBaseType)
        {
            foreach (Type t in FindTypes(name))
            {
                if (!desiredBaseType.IsAssignableFrom(t)) continue;
                return t;
            }
            return null;
        }

        class NetCtorFactory
        {
            readonly ConstructorInfo ci;
            readonly CheckType owner;

            ObjectTranslator xlat { get { return owner.translator; } }

            /// <summary>
            /// Constructs a NetObjectFactory that invokes the specified constructor to do its job.
            /// </summary>
            /// <param name="ci">Constructor to be used</param>
            /// <param name="owner">CheckType instance providing access to an ObjectTranslator and other stuff</param>
            public NetCtorFactory(ConstructorInfo ci, CheckType owner)
            {
                this.ci = ci;
                this.owner = owner;
            }

            /// <summary>
            /// Constructs an object by invoking a method that takes a single argument of type LuaTable.
            /// </summary>
            /// <param name="L"></param>
            /// <param name="stackPos"></param>
            /// <returns></returns>
            public object ConstructFromLuaTable(Lua.LuaState L, int stackPos)
            {
                LuaTable ltbl = xlat.getTable(L, stackPos);
                return ci.Invoke(new object[] { ltbl });
            }

            /// <summary>
            /// Constructs an object by invoking a method that takes zero arguments, then initializes
            /// properties and fields of the object from the provided LuaTable.
            /// </summary>
            /// <param name="L"></param>
            /// <param name="stackPos"></param>
            /// <returns></returns>
            /// <remarks>
            /// The implementation of this method is extremely ugly, but it does the trick.
            /// </remarks>
            public object ConstructAndInit(Lua.LuaState L, int stackPos)
            {
                object obj = ci.Invoke(null);
                if (obj == null)
                {
                    LuaDLL.luaL_error(L, string.Format("The constructor method {0} returned null!", ci));
                    return null;
                }
                Type t = obj.GetType();

                LuaDLL.lua_pushvalue(L, stackPos);
                LuaDLL.lua_pushnil(L); // start at the beginning
                while (LuaDLL.lua_next(L, -2) != 0)
                {
                    // okay, now -2 is the key and -1 is the value

                    // coerce it into stringhood
                    LuaDLL.lua_pushvalue(L, -2);    // copy it
                    string key = LuaDLL.lua_tostring(L, -1);    // convert it to a string
                    LuaDLL.lua_pop(L, 1);              // return it

                    if (key == null)
                    {
                        LuaDLL.luaL_error(L, string.Format("Error trying to convert key of type {0} to a string", LuaDLL.luaL_typename(L, -2)));
                    }
                    else
                    {
                        PropertyInfo pi;
                        FieldInfo fi;
                        if ((pi = t.GetProperty(key)) != null)
                        {
                            ExtractValue extractor = owner.checkType(L, -1, pi.PropertyType);
                            if (extractor == null)
                                LuaDLL.luaL_error(L, string.Format("Error trying to convert object of type {0} to .NET type {1}", LuaDLL.luaL_typename(L, -1), pi.PropertyType.FullName));
                            else
                                pi.SetValue(obj, extractor(L, -1), null);
                        }
                        else if ((fi = t.GetField(key)) != null)
                        {
                            ExtractValue extractor = owner.checkType(L, -1, fi.FieldType);
                            if (extractor == null)
                                LuaDLL.luaL_error(L, string.Format("Error trying to convert object of type {0} to .NET type {1}", LuaDLL.luaL_typename(L, -1), fi.FieldType.FullName));
                            else
                                fi.SetValue(obj, extractor(L, -1));
                        }
                        else
                            LuaDLL.luaL_error(L, string.Format(".NET type {0} does not contain a public field or property named '{1}'", t.FullName, key));
                    }
                    LuaDLL.lua_pop(L, 1);
                }
                return obj;
            } // method ConstructAndInit
        }

        class NetArrayFactory
        {
            readonly Type arrayType;
            readonly CheckType owner;

            ObjectTranslator xlat { get { return owner.translator; } }

            /// <summary>
            /// Constructs a NetObjectFactory that invokes the specified constructor to do its job.
            /// </summary>
            /// <param name="ci">Constructor to be used</param>
            /// <param name="owner">CheckType instance providing access to an ObjectTranslator and other stuff</param>
            public NetArrayFactory(Type arrayType, CheckType owner)
            {
                this.arrayType = arrayType;
                this.owner = owner;
            }

            /// <summary>
            /// Constructs an array from a table.
            /// </summary>
            /// <param name="L"></param>
            /// <param name="stackPos"></param>
            /// <returns></returns>
            /// <remarks>
            /// The implementation of this method is extremely ugly, but it does the trick.
            /// </remarks>
            public object ConstructArray(Lua.LuaState L, int stackPos)
            {
                Type elt = arrayType.GetElementType();
                int dim = LuaDLL.lua_objlen(L, stackPos);
                Array a = Array.CreateInstance(elt, dim);

                int index = 1;
                LuaDLL.lua_rawgeti(L, stackPos, index);
                while (!LuaDLL.lua_isnil(L, -1))
                {
                    ExtractValue ev = owner.checkType(L, -1, elt);
                    if (ev == null)
                        LuaDLL.luaL_error(L, string.Format("Error trying to convert value of type {0} to .NET {1}", LuaDLL.luaL_typename(L, -1), elt.FullName));
                    a.SetValue(ev(L, -1), index - 1);
                    LuaDLL.lua_pop(L, 1);
                    index++;
                    LuaDLL.lua_rawgeti(L, stackPos, index);
                }
                LuaDLL.lua_pop(L, 1);
                return a;
            } // method ConstructAndInit

        }

#if WindowsCE
        static readonly Type[] EmptyTypes = new Type[0];
#else
        static Type[] EmptyTypes { get { return Type.EmptyTypes; } }
#endif

        /// <summary>
        /// Attempts to convert a Lua table (located on the stack of L at <paramref name="stackPos"/>) to a .NET object.
        /// </summary>
        /// <param name="desiredType">The desired object type.</param>
        /// <param name="L">Lua state</param>
        /// <param name="stackPos">Location in <paramref name="state"/>'s stack where the table lives</param>
        /// <returns>An ExtractValue delegate that may be called to construct the object, or null.</returns>
        /// <remarks>
        /// 
        /// </remarks>
        ExtractValue CheckConstructable(Type desiredType, Lua.LuaState L, int stackPos)
        {
            // First, allow the table to include a '__type' element declaring the desired .NET type we're interested in.
            LuaDLL.lua_getfield(L, stackPos, "__type");
            string __type = LuaDLL.lua_tostring(L, -1);
            LuaDLL.lua_pop(L, 1); // pop the getfield
            if (__type != null)
            {
                desiredType = FindCompatibleType(__type, desiredType);
                if (desiredType == null) return null;
            }

            if (desiredType.IsArray)
                return new NetArrayFactory(desiredType, this).ConstructArray;

            ConstructorInfo ctor;
            // First: If there is a constructor that takes a LuaTable, then use that
            ctor = desiredType.GetConstructor(new Type[] { typeof(LuaTable) });
            if (ctor != null) { return new NetCtorFactory(ctor, this).ConstructFromLuaTable; }

            ctor = desiredType.GetConstructor(EmptyTypes);
            if (ctor != null) { return new NetCtorFactory(ctor, this).ConstructAndInit; }

            return null;
        }

        internal ExtractValue checkType(SharpLua.Lua.LuaState luaState, int stackPos, Type paramType)
        {
            LuaTypes luatype = LuaDLL.lua_type(luaState, stackPos);

            if (paramType.IsByRef) paramType = paramType.GetElementType();

            Type underlyingType = Nullable.GetUnderlyingType(paramType);
            if (underlyingType != null)
            {
                paramType = underlyingType;     // Silently convert nullable types to their non null requics
            }

            Type runtimeHandleValue = paramType;

            if (paramType.Equals(typeof(object)))
                return extractValues[runtimeHandleValue];

            //CP: Added support for generic parameters
            if (paramType.IsGenericParameter)
            {
                if (luatype == LuaTypes.LUA_TBOOLEAN)
                    return extractValues[typeof(bool)];
                else if (luatype == LuaTypes.LUA_TSTRING)
                    return extractValues[typeof(string)];
                else if (luatype == LuaTypes.LUA_TTABLE)
                    return extractValues[typeof(LuaTable)];
                else if (luatype == LuaTypes.LUA_TUSERDATA)
                    return extractValues[typeof(object)];
                else if (luatype == LuaTypes.LUA_TFUNCTION)
                    return extractValues[typeof(LuaFunction)];
                else if (luatype == LuaTypes.LUA_TNUMBER)
                    return extractValues[typeof(double)];
                //else // suppress CS0642
                ;//an unsupported type was encountered
            }

            if (LuaDLL.lua_isnil(luaState, stackPos))
            {
                return extractNull;
            }

            if (LuaDLL.lua_isnumber(luaState, stackPos))
                return extractValues[runtimeHandleValue];

            if (paramType == typeof(bool))
            {
                if (LuaDLL.lua_isboolean(luaState, stackPos))
                    return extractValues[runtimeHandleValue];
            }
            else if (paramType == typeof(string))
            {
                if (LuaDLL.lua_isstring(luaState, stackPos))
                    return extractValues[runtimeHandleValue];
                else if (luatype == LuaTypes.LUA_TNIL)
                    return extractNetObject; // kevinh - silently convert nil to a null string pointer
            }
            else if (paramType == typeof(char))
            // string -> char support
            {
                if (LuaDLL.lua_isstring(luaState, stackPos))
                {
                    string str = LuaDLL.lua_tostring(luaState, stackPos);
                    if (str.Length == 1) // must be char length (Length == 1)
                        return extractValues[runtimeHandleValue];
                }
                else if (luatype == LuaTypes.LUA_TNIL)
                    return extractNetObject;
            }
            else if (paramType == typeof(LuaTable))
            {
                if (luatype == LuaTypes.LUA_TTABLE)
                    return extractValues[runtimeHandleValue];
            }
            else if (paramType == typeof(LuaUserData))
            {
                if (luatype == LuaTypes.LUA_TUSERDATA)
                    return extractValues[runtimeHandleValue];
            }
            else if (paramType == typeof(LuaFunction))
            {
                if (luatype == LuaTypes.LUA_TFUNCTION)
                    return extractValues[runtimeHandleValue];
            }
#if !WindowsCE
            else if (typeof(Delegate).IsAssignableFrom(paramType) && luatype == LuaTypes.LUA_TFUNCTION)
            {
                return new ExtractValue(new DelegateGenerator(translator, paramType).extractGenerated);
            }
            else if (paramType.IsInterface && luatype == LuaTypes.LUA_TTABLE)
            {
                return new ExtractValue(new ClassGenerator(translator, paramType).extractGenerated);
            }
#endif
            else if ((paramType.IsInterface || paramType.IsClass) && luatype == LuaTypes.LUA_TNIL)
            {
                // kevinh - allow nil to be silently converted to null - extractNetObject will return null when the item ain't found
                return extractNetObject;
            }
            else if (LuaDLL.lua_type(luaState, stackPos) == LuaTypes.LUA_TTABLE)
            {
                if (LuaDLL.luaL_getmetafield(luaState, stackPos, "__index"))
                {
                    object obj = translator.getNetObject(luaState, -1);
                    LuaDLL.lua_settop(luaState, -2);
                    if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
                        return extractNetObject;
                }
                else
                {
                    ExtractValue convextract = CheckConstructable(paramType, luaState, stackPos);
                    if (convextract != null) return convextract;
                    return null;
                }
            }
            else
            {
                object obj = translator.getNetObject(luaState, stackPos);
                if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
                    return extractNetObject;
            }

            return null;
        }

        /*
         * The following functions return the value in the Lua stack
         * index stackPos as the desired type if it can, or null
         * otherwise.
         */
        private object getAsSbyte(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            sbyte retVal = (sbyte)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsByte(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            byte retVal = (byte)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsShort(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            short retVal = (short)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsUshort(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            ushort retVal = (ushort)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsInt(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            int retVal = (int)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsUint(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            uint retVal = (uint)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsLong(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            long retVal = (long)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsUlong(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            ulong retVal = (ulong)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsDouble(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            double retVal = LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsChar(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            if (LuaDLL.lua_isstring(luaState, stackPos))
            {
                string s = LuaDLL.lua_tostring(luaState, stackPos);
                if (s.Length == 0) // return a null char
                    return '\0';
                else
                {
                    if (s.Length > 1)
                        System.Diagnostics.Debug.WriteLine("String Length was greater than 1! Truncating...");
                    return s[0];
                }
            }
            char retVal = (char)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsFloat(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            float retVal = (float)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsDecimal(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            decimal retVal = (decimal)LuaDLL.lua_tonumber(luaState, stackPos);
            if (retVal == 0 && !LuaDLL.lua_isnumber(luaState, stackPos)) return null;
            return retVal;
        }
        private object getAsBoolean(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            return LuaDLL.lua_toboolean(luaState, stackPos);
        }
        private object getAsString(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            string retVal = LuaDLL.lua_tostring(luaState, stackPos);
            if (retVal == "" && !LuaDLL.lua_isstring(luaState, stackPos))
                return null;
            return retVal;
        }
        private object getAsTable(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            return translator.getTable(luaState, stackPos);
        }
        private object getAsFunction(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            return translator.getFunction(luaState, stackPos);
        }
        private object getAsUserdata(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            return translator.getUserData(luaState, stackPos);
        }
        public object getAsObject(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            if (LuaDLL.lua_type(luaState, stackPos) == LuaTypes.LUA_TTABLE)
            {
                if (LuaDLL.luaL_getmetafield(luaState, stackPos, "__index"))
                {
                    if (LuaDLL.luaL_checkmetatable(luaState, -1))
                    {
                        LuaDLL.lua_insert(luaState, stackPos);
                        LuaDLL.lua_remove(luaState, stackPos + 1);
                    }
                    else
                    {
                        LuaDLL.lua_settop(luaState, -2);
                    }
                }
            }
            object obj = translator.getObject(luaState, stackPos);
            return obj;
        }
        public object getAsNetObject(SharpLua.Lua.LuaState luaState, int stackPos)
        {
            object obj = translator.getNetObject(luaState, stackPos);
            if (obj == null && LuaDLL.lua_type(luaState, stackPos) == LuaTypes.LUA_TTABLE)
            {
                if (LuaDLL.luaL_getmetafield(luaState, stackPos, "__index"))
                {
                    if (LuaDLL.luaL_checkmetatable(luaState, -1))
                    {
                        LuaDLL.lua_insert(luaState, stackPos);
                        LuaDLL.lua_remove(luaState, stackPos + 1);
                        obj = translator.getNetObject(luaState, stackPos);
                    }
                    else
                    {
                        LuaDLL.lua_settop(luaState, -2);
                    }
                }
            }
            return obj;
        }

        public object getNull(Lua.LuaState luaState, int stackPos)
        {
            if (LuaDLL.lua_isnil(luaState, stackPos))
                return null;
            else
            {
                Debug.WriteLine("Value isn't nil!");
                return null;
            }
        }
    }
}
