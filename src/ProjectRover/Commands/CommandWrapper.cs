// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace ICSharpCode.ILSpy
{
	abstract class CommandWrapper : ICommand
	{
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("Commands");
		static CommandWrapper()
		{
			// var app = Application.Current;
			// var lifetime = app?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
			// var window = lifetime?.MainWindow;
			// if (window is null)
			// 	return;
				
			// var bindings = Avalonia.Labs.Input.CommandManager.GetCommandBindings(window);
		}

		private readonly ICommand wrappedCommand;

		private static List<CommandBinding> commandBindings = new();
		private static Window? targetWindow;

		protected CommandWrapper(ICommand wrappedCommand)
		{
					 log.Debug("CommandWrapper created for {Cmd}. targetWindow is {State}", wrappedCommand, (targetWindow == null ? "null" : "set"));
			this.wrappedCommand = wrappedCommand;
			var binding = new CommandBinding(wrappedCommand, OnExecute, OnCanExecute);
			commandBindings.Add(binding);

			if (targetWindow != null)
			{
							 log.Debug("Late registering binding for {Cmd}", wrappedCommand);
				var windowBindings = Avalonia.Labs.Input.CommandManager.GetCommandBindings(targetWindow);
				windowBindings.Add(binding);
			}
            else
            {
				log.Warning("WARNING: targetWindow is null, binding not registered!");
            }
		}

		public static void RegisterBindings(Window window)
		{
			targetWindow = window;
					 log.Debug("Registering {Count} bindings to window {Hash}", commandBindings.Count, window.GetHashCode());
			var windowBindings = Avalonia.Labs.Input.CommandManager.GetCommandBindings(window);
			foreach (var binding in commandBindings)
			{
				if (!windowBindings.Contains(binding))
				{
					windowBindings.Add(binding);
				}
			}
		}

		public static ICommand Unwrap(ICommand command)
		{
			if (command is CommandWrapper w)
				return w.wrappedCommand;

			return command;
		}

		public event EventHandler CanExecuteChanged {
			add { wrappedCommand.CanExecuteChanged += value; }
			remove { wrappedCommand.CanExecuteChanged -= value; }
		}

		public void Execute(object parameter)
		{
					 log.Debug("CommandWrapper.Execute called for {Cmd}", wrappedCommand);
			if (targetWindow != null && wrappedCommand is Avalonia.Labs.Input.RoutedCommand rc)
			{
							 log.Debug("Executing with targetWindow: {Target}", targetWindow);
				rc.Execute(parameter, targetWindow);
				return;
			}
			wrappedCommand.Execute(parameter);
		}

		public bool CanExecute(object parameter)
		{
			if (targetWindow != null && wrappedCommand is Avalonia.Labs.Input.RoutedCommand rc)
			{
				if (rc.CanExecute(parameter, targetWindow))
					return true;
			}
			return wrappedCommand.CanExecute(parameter);
		}

		protected abstract void OnExecute(object sender, ExecutedRoutedEventArgs e);

		protected virtual void OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
	}
}
