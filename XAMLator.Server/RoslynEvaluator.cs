using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace XAMLator.Server
{
	/// Evaluates expressions using Roslyn's C# Scripint API.
	public class Evaluator : IEvaluator
	{
		ScriptOptions options;

		private IEnumerable<Assembly> assemblies;

		public IEnumerable<Assembly> Assemblies
		{
			get => assemblies ?? AppDomain.CurrentDomain.GetAssemblies();
			set => assemblies = value;
		}

		public async Task<IEnumerable<EvalMessage>> EvaluateCode(string code)
		{
			if (string.IsNullOrEmpty(code))
				return Enumerable.Empty<EvalMessage>();

			EnsureConfigured();
			try
			{
				var state = await CSharpScript.RunAsync(code);
			}
			catch (CompilationErrorException ex)
			{
				Log.Error($"Error evaluating code");
				return new EvalMessage[] { new EvalMessage("error", ex.ToString()) };
			}
			return Enumerable.Empty<EvalMessage>(); ;
		}

		void EnsureConfigured()
		{
			if (options == null)
			{
				ConfigureVM();
			}

		}

		void ConfigureVM()
		{
			options = ScriptOptions.Default.WithReferences(Assemblies.Where(a => !a.IsDynamic).ToArray());
		}
	}
}
