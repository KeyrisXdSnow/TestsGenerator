﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AssemblyBrowserLib.format
{
    public static class MethodFormatter
    {
        public static string Format(System.Reflection.MethodInfo methodInfo)
        {
            var result = string.Join(" ", GetTypeAccessorModifiers(methodInfo), GetTypeModifiers(methodInfo),
                GetType(methodInfo), methodInfo.Name, GetMethodArguments(methodInfo));
            return result;
        }

        private static string GetTypeAccessorModifiers(System.Reflection.MethodInfo methodInfo)
        {
            // new 
            if (methodInfo.IsPublic)
                return "public";
            if (methodInfo.IsPrivate)
                return "private";
            if (methodInfo.IsFamily)
                return "protected";
            if (methodInfo.IsAssembly)
                return "internal";
            if (methodInfo.IsFamilyOrAssembly)
                return "protected internal";

            return "";
        }
        
        private static string GetTypeModifiers(System.Reflection.MethodInfo methodInfo)
        {
            if (methodInfo.IsAbstract)
                return "abstract";
            if (methodInfo.IsStatic)
                return "static";
            if (methodInfo.IsVirtual)
                return "virtual";
            if (methodInfo.GetBaseDefinition() != methodInfo)
                return "override";

            return "";
        }

        private static string GetType(System.Reflection.MethodInfo methodInfo)
        {
            if (methodInfo.ReturnType.IsGenericType) return GetGenericType(methodInfo.ReturnType);
            return methodInfo.ReturnType.Name;
        }

        private static string GetMethodArguments(MethodBase methodInfo)
        {
            var stringBuilder = new StringBuilder("(");
            
            foreach (var parameter in methodInfo.GetParameters())
            {
                string parameterType;
                if (parameter.ParameterType.IsGenericType)
                {
                    parameterType = GetGenericType(parameter.ParameterType);
                }
                else parameterType = parameter.ParameterType.ToString();
                
                stringBuilder.Append(parameterType).Append(" ").Append(parameter.Name).Append(",");
            }

            if (stringBuilder.Length > 1) 
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
            stringBuilder.Append(")");
            
            return stringBuilder.ToString();

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