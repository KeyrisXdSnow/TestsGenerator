﻿using System;

namespace AssemblyBrowserLib.format
{
    public static class ClassFormatter
    {
        public static string Format(Type type)
        {
            
            var na = type.Namespace;
            var name = type.Name;

            var result = string.Join(" ", GetTypeAccessorModifiers(type), GetTypeModifiers(type),
                GetType(type), type.Name);
            return result;
        }

        private static string GetTypeAccessorModifiers(Type type)
        {
            if (type.IsNestedPublic || type.IsPublic)
                return "public";
            if (type.IsNestedPrivate)
                return "private";
            if (type.IsNestedFamily)
                return "protected";
            if (type.IsNestedAssembly)
                return "internal";
            if (type.IsNestedFamORAssem)
                return "protected internal";
            if (type.IsNestedFamANDAssem)
                return "private protected ";
            if (type.IsNotPublic)
                return "private ";

            return "";
        }
        
        private static string GetTypeModifiers(Type type)
        {
            if (type.IsAbstract && type.IsSealed)
                return "static";
            if (type.IsAbstract)
                return "abstract";
            if (type.IsSealed)
                return "sealed";

            return "";
        }

        private static string GetType(Type type)
        {
            if (type.IsClass)
                return "class ";
            if (type.IsEnum)
                return "enum ";
            if (type.IsInterface)
                return "interface ";
            if (type.IsGenericType)
                return "generic ";
            if (type.IsValueType && !type.IsPrimitive)
                return "struct ";

            return "";
        }
    }

    class TestsClass
    {
        
    }
}