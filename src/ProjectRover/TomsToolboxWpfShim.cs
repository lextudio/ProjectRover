// Minimal shim for TomsToolbox.Wpf.ObservableObjectBase used by linked ILSpy sources
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Avalonia.Threading;

namespace TomsToolbox.Wpf
{
	public class ObservableObjectBase : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class ObservableObject : ObservableObjectBase
	{
		/// <summary>
		/// Gets the dispatcher of the thread where this object was created.
		/// </summary>
		public System.Windows.Threading.Dispatcher Dispatcher { get; } = new System.Windows.Threading.Dispatcher(); // TODO: further simplify

		protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			if (propertyName == null)
				return;

			base.OnPropertyChanged(propertyName);

			// raise for dependent properties
			foreach (var dependent in GetDependentProperties(propertyName))
			{
				base.OnPropertyChanged(dependent);
			}
		}

		static readonly ConcurrentDictionary<Type, Dictionary<string, string[]>> _cache = new();

		static IReadOnlyList<string> GetDependentProperties(string propertyName, Type? type = null)
		{
			type ??= typeof(ObservableObject);

			var map = _cache.GetOrAdd(type, t => {
				var dict = new Dictionary<string, List<string>>();

				foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					var attrs = prop.GetCustomAttributes<PropertyDependencyAttribute>();
					foreach (var attr in attrs)
					{
						foreach (var source in attr.PropertyNames)
						{
							if (!dict.TryGetValue(source, out var list))
								list = dict[source] = new List<string>();

							list.Add(prop.Name);
						}
					}
				}

				return dict.ToDictionary(k => k.Key, v => v.Value.ToArray());
			});

			return map.TryGetValue(propertyName, out var deps) ? deps : Array.Empty<string>();
		}
	}

	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
	public sealed class PropertyDependencyAttribute : Attribute
	{
		public PropertyDependencyAttribute(params string[] propertyNames)
		{
			PropertyNames = propertyNames;
		}

		public string[] PropertyNames { get; }
	}

	// <summary>
	/// Implements a simple timed throttle.<para/>
	/// Calling <see cref="Tick()"/> multiple times will restart the timer; there will be one single 
	/// call to the action when the delay time has elapsed after the last tick.
	/// </summary>
	public class Throttle
	{
		private readonly Action _target;
		private readonly DispatcherTimer _timer;

		/// <summary>
		/// Initializes a new instance of the <see cref="Throttle"/> class with a default timeout of 100ms.
		/// </summary>
		/// <param name="target">The target action to invoke when the throttle condition is hit.</param>
		public Throttle(Action target)
			: this(TimeSpan.FromMilliseconds(100), target)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Throttle"/> class.
		/// </summary>
		/// <param name="timeout">The timeout to wait for after the last <see cref="Tick()"/>.</param>
		/// <param name="target">The target action to invoke when the throttle condition is hit.</param>
		public Throttle(TimeSpan timeout, Action target)
		{
			_target = target;
			_timer = new DispatcherTimer { Interval = timeout };
			_timer.Tick += Timer_Tick;
		}

		/// <summary>
		/// Ticks this instance to trigger the throttle.
		/// </summary>
		public void Tick()
		{
			_timer.Stop();
			_timer.Start();
		}

		private void Timer_Tick(object? sender, EventArgs e)
		{
			_timer.Stop();
			_target();
		}
	}
}

namespace TomsToolbox.Wpf.Composition
{ }
