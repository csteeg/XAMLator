using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.CSharp;

namespace XAMLator.Server
{
	public class Evaluator : IEvaluator
	{
		Mono.CSharp.Evaluator eval;
		Printer printer;
		private IEnumerable<Assembly> assemblies;

		public IEnumerable<Assembly> Assemblies
		{
			get => assemblies ?? AppDomain.CurrentDomain.GetAssemblies();
			set => assemblies = value;
		}

		public Task<IEnumerable<EvalMessage>> EvaluateCode(string code)
		{
			if (string.IsNullOrEmpty(code))
				return Task.FromResult(Enumerable.Empty<EvalMessage>());

			EnsureConfigured();

			try
			{
				printer.Reset();
				eval.Evaluate(code, out object result, out bool result_set);
			}
			catch (Exception ex)
			{
				Log.Error($"Error evalutaing code");
				eval = null;
				if (printer.Messages.Count != 0)
				{
					return Task.FromResult((IEnumerable<EvalMessage>)printer.Messages.ToArray());
				}
				return Task.FromResult((IEnumerable<EvalMessage>)new[] { new EvalMessage("error", ex.ToString()) });
			}
			return Task.FromResult(Enumerable.Empty<EvalMessage>());
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

			foreach (var a in Assemblies.Where(asm => asm != null))
			{
				LoadAssembly(a);
			}
		}

		void LoadAssembly(Assembly assembly)
		{
			var name = assembly.GetName().Name;
			if (name == "mscorlib" || name == "System" || name == "System.Core" || name.StartsWith("eval-"))
				return;
			eval?.ReferenceAssembly(assembly);
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
