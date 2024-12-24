﻿using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace VimeoDotNet.Helpers
{
    /// <summary>
    /// Class EnumHelpers.
    /// </summary>
    internal static class EnumHelpers
    {
        /// <summary>
        /// Gets an enum value's string name, using it's <see cref="EnumMemberAttribute" />
        /// if present
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="value">The enum value</param>
        /// <returns>Either the value set by a <see cref="EnumMemberAttribute" />, or the
        /// enum member's name as returned by <see cref="Enum.ToString()" /></returns>
        public static string MemberValue<T>(this T value) where T : Enum
        {
            return typeof(T)
                .GetTypeInfo()
                .DeclaredMembers
                .SingleOrDefault(x => x.Name == value.ToString())
                ?.GetCustomAttribute<EnumMemberAttribute>(false)
                ?.Value ?? value.ToString();
        }
    }
}