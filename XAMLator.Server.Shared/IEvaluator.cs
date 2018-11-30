using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XAMLator.Server
{
	public interface IEvaluator
	{
		/// <summary>
		/// Evaluates an expression and code before the expression if requested.
		/// </summary>
		/// <returns>True if succeeded.</returns>
		/// <param name="code">The class code.</param>
		Task<IEnumerable<EvalMessage>> EvaluateCode(string code);
	}
}
