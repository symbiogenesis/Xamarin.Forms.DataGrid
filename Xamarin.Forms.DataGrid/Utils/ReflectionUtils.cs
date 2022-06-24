using System.Reflection;

namespace Xamarin.Forms.DataGrid.Utils
{
    public static class ReflectionUtils
	{
		public static object GetPropertyValue(object obj, int index)
		{
			try
			{
				return obj?.GetType().GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)[index]?.GetValue(obj);
			}
			catch
			{
				return null;
			}
		}
	}
}