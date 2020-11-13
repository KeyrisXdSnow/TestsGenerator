﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AssemblyBrowserLib.format
{
    public class PropertiesFormatter
    {
        public static string Format(PropertyInfo propertyInfo)
        {
            var result = string.Join(" ", GetTypeAccessorModifiers(propertyInfo.GetGetMethod(true)),
                GetType(propertyInfo), propertyInfo.Name,GetPropertyAccessors(propertyInfo));
            return result;
        }
        private static string GetPropertyAccessors(PropertyInfo propertyInfo)
        {
            const string begin = "{", end = "}", separator = "; ";
            
            var accessors = propertyInfo.GetAccessors(true);
            var stringBuilder = new StringBuilder(begin).Append(" ");
            
            foreach (var accessor in accessors)
            {
                if (accessor.IsSpecialName)
                {
                    stringBuilder.Append(GetTypeAccessorModifiers(accessor)).Append(" ").Append(accessor.Name).Append(separator);
                }

            }
            stringBuilder.Append(end);
            
            return stringBuilder.ToString();
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
        
        private static string GetType(PropertyInfo propertyInfo)
        {
            return propertyInfo.PropertyType.IsGenericType ? GetGenericType(propertyInfo.PropertyType) : propertyInfo.PropertyType.Name;
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