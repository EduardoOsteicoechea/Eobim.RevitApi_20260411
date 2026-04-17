using System.Reflection;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Framework
{
	internal class DtoFormatter
	{
		public static class DtoFormater
		{
			public static List<(string, object)> FormatAsObject<T>(T item)
			{
				if (item is null)
				{
					throw new ArgumentNullException(nameof(item));	
				}

				var printer = new List<(string, object)>();

				var type = item.GetType();

				var properties = type.GetProperties().Where(a => Attribute.IsDefined(a, typeof(Print)));

				foreach (var property in properties)
				{
					var attribute = (Print)property.GetCustomAttributes(typeof(Print), false).FirstOrDefault();

					object rawValue = property.GetValue(item);

					object displayValue;

					if (attribute.FormatterType != null && !string.IsNullOrEmpty(attribute.FormatterMethodName))
					{
						var methods = attribute
							.FormatterType
							.GetMethods(BindingFlags.Static | BindingFlags.Public)
							.Where(m => m.Name == attribute.FormatterMethodName);

						MethodInfo method = null;

						foreach (var m in methods)
						{
							var parameters = m.GetParameters();
							if (parameters.Length == 1)
							{
								if (parameters[0].ParameterType.IsAssignableFrom(property.PropertyType) ||
									parameters[0].ParameterType == typeof(object))
								{
									method = m;
									break;
								}
							}
						}

						if (method != null)
						{
							displayValue = method.Invoke(null, new object[] { rawValue });
						}
						else
						{
							displayValue = $"[{attribute.FormatterMethodName} method not found]";
						}
					}
					else
					{
						displayValue = rawValue;
					}

					printer.Add((property.Name, displayValue));
				}

				return printer;
			}
		}
	}
}
