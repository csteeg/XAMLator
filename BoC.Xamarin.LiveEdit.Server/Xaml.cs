using System;
using System.Reflection;

namespace XAMLator.Server.Abstractions
{
	public static class Xaml
	{
		static MethodInfo loadXAML;
		static MethodInfo originalLoadXAML;

		static Xaml()
		{
			ResolveLoadMethod();
		}

		static void ResolveLoadMethod()
		{
			var xamlAssembly = Assembly.Load(new AssemblyName("Xamarin.Forms.Xaml"));
			var xamlLoader = xamlAssembly.GetType("Xamarin.Forms.Xaml.XamlLoader");
			loadXAML = xamlLoader.GetRuntimeMethod("Load", new[] { typeof(object), typeof(string) });
			originalLoadXAML = xamlLoader.GetRuntimeMethod("Load", new[] { typeof(object), typeof(Type) });
		}

		/// <summary>
		/// Hook used by new instances to load their XAML instead of retrieving
		/// it from the assembly.
		/// </summary>
		/// <param name="view">View.</param>
		public static void LoadXaml(object view)
		{
			if (view == null)
				return;
			if (VMState.EvalResults.TryGetValue(view.GetType(), out EvalResult result))
			{
				loadXAML.Invoke(null, new object[] { view, result?.Code });
			}
			else
			{
				originalLoadXAML.Invoke(null, new object[] { view, view.GetType() });
			}
		}
	}
}
