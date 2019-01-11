using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace XAMLator.Server.Abstractions
{
    public static class VMState
    {
        public static ConcurrentDictionary<Type, Type> TypeReplacements { get; } = new ConcurrentDictionary<Type, Type>();
        public static ConcurrentDictionary<Type, EvalResult> EvalResults { get; } = new ConcurrentDictionary<Type, EvalResult>();
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, string>> AssemblyResources { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
    }
}
