using System;
using System.Collections.Generic;
using System.Data.Entity.Design.PluralizationServices;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace NhCodeFirst
{
	public static class Extensions
{
        public static Type GetTypeOrGenericArgumentTypeForInterface(this Type t, Type interfaceType)
        {
            var collectionType = t.RawGenericInterface(interfaceType);
            if (collectionType == null) return t;
            return collectionType.GetGenericArguments()[0];
        }
        public static Type GetTypeOrGenericArgumentTypeForICollection(this Type t)
        {
            return t.GetTypeOrGenericArgumentTypeForInterface(typeof (ICollection<>));
        }
        public static Type GetTypeOrGenericArgumentTypeForIEnumerable(this Type t)
        {
            return t.GetTypeOrGenericArgumentTypeForInterface(typeof(IEnumerable<>));
        }
        public static Type GetTypeOrGenericArgumentTypeForIQueryable(this Type t)
        {
            return t.GetTypeOrGenericArgumentTypeForInterface(typeof(IQueryable<>));
        }

		static Type SubclassOfRawGeneric(this Type toCheck, Type generic) {
			while (toCheck != typeof(object)) {
				var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
				if (generic == cur) {
					return cur;
				}
				toCheck = toCheck.BaseType;
			}
			return null;
		}
		public static Type RawGenericInterface(this Type toCheck, Type generic) {
            
			var implementedInterfaces = toCheck.GetInterfaces().ToList();
            if (toCheck.IsInterface) implementedInterfaces.Add(toCheck);
			return implementedInterfaces
				.Where(i => i.IsGenericType)
				.Where(i => i.GetGenericTypeDefinition() == generic)
				.SingleOrDefault();
			
		}
		 
		 
		 
		public static Type GetTypeOrCollectionArgumentType(this Type type)
		{
			var genericSubclass = type.SubclassOfRawGeneric(typeof(ICollection<>));
			if (genericSubclass == null)
			{
				return type;
			}
			return genericSubclass.GetGenericArguments().Single ();
			
		}

		public static T Apply<T>(this T t, Action<T> action)
		{
			action(t);
			return t;
		}

		public static IEnumerable<T> Each<T>(this IEnumerable<T> collection, Action<T> action) 
		{
			foreach (var item in collection)
			{
				action(item);
				yield return item;
			}
		}

		public static T Copy<T>(this T t)
		{
			var xml = t.ToString();
			var obj = typeof (T).GetMethod("Parse", BindingFlags.Public | BindingFlags.Static).Invoke(null, new[] {xml});
			return (T) obj;
		}

		public static List<T> Copy<T>(this IList<T> list)
		{
			var newList = new List<T>();
			foreach (var l in list)
			{
				newList.Add(l.Copy());
			}
			return newList;
		}
		 


		public static string Pluralize(this string text)
		{
			return PluralizationService.CreateService(CultureInfo.CurrentCulture).Pluralize(text);
		}

	    private static BindingFlags _instanceBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	    public static IEnumerable<MemberInfo> GetFieldsAndProperties(this Type type)
        {
            return
                type.GetAllMembers().Where(
                    m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property);
        }
        public static IEnumerable<MemberInfo> GetSettableFieldsAndProperties(this Type type)
        {
            return type.GetFieldsAndProperties().Where(m => !m.IsReadOnlyProperty());
        }

        public static bool IsReadOnlyProperty(this MemberInfo mi)
        {
            return mi.MemberType == MemberTypes.Property &&
                   !mi.DeclaringType.GetProperty(mi.Name, _instanceBindingFlags).CanWrite;
        }

        public static bool IsReadOnlyField(this MemberInfo mi)
        {
            return mi.MemberType == MemberTypes.Field &&
                   mi.DeclaringType.GetField(mi.Name, _instanceBindingFlags).IsInitOnly;
        }
		public static IEnumerable<MemberInfo> GetAllMembers(this Type type)
		{
            return type.GetMembers(_instanceBindingFlags).Where(p => p.Name.Contains("__BackingField") == false);
		}

		public static void SetValue(this MemberInfo memberInfo, object obj, object value)
		{
			if (memberInfo.MemberType == MemberTypes.Property)
			{
				var propertyInfo = memberInfo.DeclaringType.GetProperty(memberInfo.Name,
																		BindingFlags.NonPublic | BindingFlags.Public |
																		BindingFlags.Instance | BindingFlags.Static);
				propertyInfo.GetSetMethod().Invoke(obj, new object[] { value });
			}
			else if (memberInfo.MemberType == MemberTypes.Field)
			{
				var fieldInfo = memberInfo.DeclaringType.GetField(memberInfo.Name,
																	   BindingFlags.NonPublic | BindingFlags.Public |
																	   BindingFlags.Instance | BindingFlags.Static);
				fieldInfo.SetValue(obj, value);
			}
		}

		public static IEnumerable<Type> MakeGenericTypes(this IEnumerable<Type> openGenerics, Type type)
		{
			return openGenerics.Select(og => og.MakeGenericType(type));
		}

		public static Type ReturnType(this MemberInfo memberInfo)
		{
			if (memberInfo.MemberType == MemberTypes.Field)
			{
				return memberInfo.DeclaringType.GetField(memberInfo.Name, _instanceBindingFlags).FieldType;
			}
			if (memberInfo.MemberType == MemberTypes.Property)
			{
				return memberInfo.DeclaringType.GetProperty(memberInfo.Name, _instanceBindingFlags).PropertyType;
			}
			return null;
		}
		public static bool Inherits(this Type toCheck, Type generic)
		{
			while (toCheck != typeof(object))
			{
				var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
				if (generic == cur)
				{
					return true;
				}
				toCheck = toCheck.BaseType;
			}
			return false;
		}
		public static bool Implements(this Type toCheck, Type generic)
		{
			return toCheck.GetInterfaces()
				.Select(@interface => @interface.IsGenericType ? @interface.GetGenericTypeDefinition() : @interface)
				.Any(cur => generic == cur);
		}

		public static bool CanBeInstantiated(this Type type)
		{
			return type.IsClass && !type.ContainsGenericParameters && !type.IsAbstract;
		}
		public static IEnumerable<Type> GetTypesSafe(this Assembly assembly)
		{
			return assembly.GetTypes().Where(t => !string.IsNullOrEmpty(t.Namespace));
		}
		public static bool ImplementsGeneric<T>(this Type type, Type genericType)
		{
			return typeof (T).MakeGenericType(genericType).IsAssignableFrom(type);
		}

		public static bool IsBackingFieldFor(this MemberInfo bf, MemberInfo potentialProp)
		{
            if(potentialProp.MemberType != MemberTypes.Property) return false;
            if (potentialProp.DeclaringType != bf.DeclaringType) return false;
            if (potentialProp.ReturnType() != bf.ReturnType()) return false;
            if (potentialProp == bf) return false;
		    return bf.Name.ToLower().TrimStart('_') == potentialProp.Name.ToLower();
		}
	}
}