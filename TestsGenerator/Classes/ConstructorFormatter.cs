﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AssemblyBrowserLib.format
{
    public static class ConstructorFormatter
    {
        static ConstructorFormatter()
        {
            
        }
        public static string Format(ConstructorInfo constrInfo)
        {
            var result = string.Join(" ", GetTypeAccessorModifiers(constrInfo),
                GetName(constrInfo), GetConstructArguments(constrInfo));
            return result;
        }

        public static string GetName(ConstructorInfo constrInfo)
        {
            return constrInfo.Name; 
        }
        public static string GetTypeAccessorModifiers(ConstructorInfo constrInfo)
        {
            // new 
            if (constrInfo.IsPublic)
                return "public";
            if (constrInfo.IsPrivate)
                return "private";
            if (constrInfo.IsFamily)
                return "protected";
            if (constrInfo.IsAssembly)
                return "internal";
            if (constrInfo.IsFamilyOrAssembly)
                return "protected internal";

            return "";
        }
        private static string GetConstructArguments(ConstructorInfo constrInfo)
        {
            var stringBuilder = new StringBuilder("(");

            foreach (var parameter in constrInfo.GetParameters())
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

            var stringBuilder = new StringBuilder(Regex.Replace(parameter.Name, "`[0-9]+$", ""));

            stringBuilder.Append("<");
            if (parameter.IsGenericType)
            {
                stringBuilder.Append(GetGenericArgumentsType(parameter.GenericTypeArguments));
            }

            stringBuilder.Append(">");

            return stringBuilder.ToString();
        }


        public static string GetGenericArgumentsType(IEnumerable<Type> arguments)
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