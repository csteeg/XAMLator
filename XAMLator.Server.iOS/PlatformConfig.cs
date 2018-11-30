using System;
namespace XAMLator.Server
{
	public static class PlatformConfig
	{
		public static bool SupportsEvaluation => ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.SIMULATOR;
	}
}
