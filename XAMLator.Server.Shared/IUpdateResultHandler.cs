//
//  Copyright (C) 2018 Fluendo S.A.
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XAMLator.Server
{
	public interface IUpdateResultHandler
	{
		Task ProcessResult(EvalResult res);

		Task NotifyError(ErrorViewModel errorViewModel);
	}
}
