using System;
using Avalonia.Threading;
using ICSharpCode.ILSpy.Util;

namespace ICSharpCode.ILSpy.Commands
{
	internal static class CommandManagerExtensions
	{
		static bool registered;

		public static void RegisterNavigationRequery()
		{
			if (registered)
				return;
			registered = true;

			MessageBus<AssemblyTreeSelectionChangedEventArgs>.Subscribers += (_, _) => RequestRequery();
			MessageBus<ActiveTabPageChangedEventArgs>.Subscribers += (_, _) => RequestRequery();
			MessageBus<TabPagesCollectionChangedEventArgs>.Subscribers += (_, _) => RequestRequery();
			MessageBus<NavigateToEventArgs>.Subscribers += (_, _) => RequestRequery();
		}

		static void RequestRequery()
		{
			if (Dispatcher.UIThread.CheckAccess())
			{
				CommandManager.InvalidateRequerySuggested();
			}
			else
			{
				Dispatcher.UIThread.Post(CommandManager.InvalidateRequerySuggested);
			}
		}
	}
}
