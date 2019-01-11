using System;
using Mono.CSharp;

namespace XAMLator.Server
{
	public static class PlatformConfig
	{
		private static bool? supportsEvaluation;

		public static bool SupportsEvaluation
		{
			get
			{
				if (supportsEvaluation == null)
				{
					if (Xamarin.Forms.Device.RuntimePlatform != Xamarin.Forms.Device.iOS)
					{
						supportsEvaluation = true;
					}
					else
					{
						try
						{
							Mono.CSharp.Evaluator evaluator = new Mono.CSharp.Evaluator(
								new CompilerContext(new CompilerSettings(), new ConsoleReportPrinter()));
							evaluator.Evaluate("2+2");
							supportsEvaluation = true;
						}
						catch
						{
							supportsEvaluation = false;
						}

					}
				}
				return supportsEvaluation ?? false;
			}
			set
			{
				supportsEvaluation = value;
			}
		}
	}
}
