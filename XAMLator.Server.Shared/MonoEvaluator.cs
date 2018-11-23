using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Mono.CSharp;

namespace XAMLator.Server
{
	public class Evaluator : IEvaluator
	{
		Mono.CSharp.Evaluator eval;
		Printer printer;
		bool canEvaluate = false;

		public Task<bool> EvaluateExpression(string expression, string code, EvalResult result)
		{
			EnsureConfigured();
			try
			{
				//on real devices we cannot do eval.Evaluate! https://github.com/mono/mono/issues/6616
				if (!canEvaluate)
				{
					//TODO: do we really need expression? why not just pass the type to instantiate
					//we wouldn't need evaluate for just instantiating the type if there is no code change
					if (!expression.StartsWith("new ") || !expression.EndsWith("()"))
						return Task.FromResult(false);

					var typeName = expression.Substring("new ".Length, expression.Length - "new ".Length - "()".Length).Trim();
					var type = getTypeByName(typeName);
					if (type == null)
						return Task.FromResult(false);
					result.Result = Activator.CreateInstance(type);
					return Task.FromResult(true);
				}

				object retResult;
				bool hasResult;

				printer.Reset();
				if (!String.IsNullOrEmpty(code))
				{
					var ret = eval.Evaluate(code, out retResult, out hasResult);
				}
				result.Result = eval.Evaluate(expression);
				return Task.FromResult(true);
			}
			catch (Exception ex)
			{
				Log.Error($"Error creating a new instance of {expression}");
				if (printer.Messages.Count != 0)
				{
					result.Messages = printer.Messages.ToArray();
				}
				else
				{
					result.Messages = new EvalMessage[] { new EvalMessage("error", ex.ToString()) };
				}
				if (!result.HasResult && result.Messages.Length == 0)
				{
					result.Messages = new EvalMessage[] { new EvalMessage("error", "Internal Error") };
				}
				eval = null;
			}
			return Task.FromResult(false);
		}

		void EnsureConfigured()
		{
			if (eval != null)
			{
				return;
			}

			var settings = new CompilerSettings();
			printer = new Printer();
			var context = new CompilerContext(settings, printer);
			eval = new Mono.CSharp.Evaluator(context);
			try
			{
				canEvaluate = (eval.Evaluate("new Object()") != null);

			}
			catch (NotSupportedException)
			{
				canEvaluate = false;
			}
			if (canEvaluate)
			{
				AppDomain.CurrentDomain.AssemblyLoad += (_, e) =>
				{
					LoadAssembly(e.LoadedAssembly);
				};
				foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
				{
					LoadAssembly(a);
				}
			}
		}

		void LoadAssembly(Assembly assembly)
		{
			var name = assembly.GetName().Name;
			if (name == "mscorlib" || name == "System" || name == "System.Core")
				return;
			eval?.ReferenceAssembly(assembly);
		}

		/// <summary>
		/// Gets a all Type instances matching the specified class name.
		/// </summary>
		/// <param name="className">Name of the class sought.</param>
		/// <returns>Types that have the class name specified. They may not be in the same namespace.</returns>
		static Type getTypeByName(string className)
		{
			List<Type> returnVal = new List<Type>();

			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					Type[] assemblyTypes = a.GetTypes();
					for (int j = 0; j < assemblyTypes.Length; j++)
					{
						if (assemblyTypes[j].FullName == className)
						{
							return (assemblyTypes[j]);
						}
					}
				}
				catch
				{
					//just continue with the next assembly
				}
			}
			return null;
		}
	}

	class Printer : ReportPrinter
	{
		public readonly List<EvalMessage> Messages = new List<EvalMessage>();

		public new void Reset()
		{
			Messages.Clear();
			base.Reset();
		}

		public override void Print(AbstractMessage msg, bool showFullPath)
		{
			if (msg.MessageType != "error")
			{
				return;
			}
			AddMessage(msg.MessageType, msg.Text, msg.Location.Row, msg.Location.Column);
		}

		public void AddError(Exception ex)
		{
			AddMessage("error", ex.ToString(), 0, 0);
		}

		void AddMessage(string messageType, string text, int line, int column)
		{
			var m = new EvalMessage(messageType, text, line, column);
			Messages.Add(m);
			if (m.MessageType == "error")
			{
				Log.Error(m.Text);
			}
		}
	}
}
