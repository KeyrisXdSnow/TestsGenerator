﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AssemblyBrowserLib.format
{
    public static class FieldFormatter
    {
        public static string Format(FieldInfo fieldInfo)
        {
            var result = string.Join(" ", GetTypeAccessorModifiers(fieldInfo), GetTypeModifiers(fieldInfo),
                GetType(fieldInfo), fieldInfo.Name);
            return result;
        }
        private static string GetTypeAccessorModifiers(FieldInfo filedInfo)
        {
            // new 
            if (filedInfo.IsPublic)
                return "public";
            if (filedInfo.IsPrivate)
                return "private";
            if (filedInfo.IsFamily)
                return "protected";
            if (filedInfo.IsAssembly)
                return "internal";
            if (filedInfo.IsFamilyOrAssembly)
                return "protected internal";

            return "";
        }

        private static string GetTypeModifiers(FieldInfo fieldInfo)
        {
            return fieldInfo.IsStatic ? "static" : "";
        }

        private static string GetType(FieldInfo fieldInfo)
        {
            if (fieldInfo.FieldType.IsGenericType) return GetGenericType(fieldInfo.FieldType);
            return fieldInfo.FieldType.Name;
        }
        
        private static string GetGenericType(Type parameter)
        {
            
            var stringBuilder = new StringBuilder(Regex.Replace(parameter.Name,"`[0-9]+$", ""));
           
            stringBuilder.Append("<"); 
            if (parameter.IsGenericType)
            {
               stringBuilder.Append(GetGenericArgumentsType(parameter.GenericTypeArguments));
            }
            stringBuilder.Append(">");
            
            return stringBuilder.ToString();
        }


        private static string GetGenericArgumentsType(IEnumerable<Type> arguments)
        {
            var stringBuilder = new StringBuilder();

            foreach (var argument in arguments)
            {
                if (argument.IsGenericType)
                {
                    stringBuilder.Append(GetGenericType(argument));
                }
                else stringBuilder.Append(argument);

                stringBuilder.Append(", ");
            }

            stringBuilder.Remove(stringBuilder.Length - 2, 2);

            return stringBuilder.ToString();
        }
    }
}