using System;
using System.Collections.Specialized;
using System.ComponentModel;
using ICSharpCode.ILSpy.TextView;

namespace ICSharpCode.ILSpy.Util
{
    public static class MessageBus
    {
        public static void Send<T>(object? sender, T e)
            where T : EventArgs
        {
            MessageBus<T>.Send(sender, e);
        }
    }

    public static class MessageBus<T>
        where T : EventArgs
    {
        private static EventHandler<T>? subscribers;

        public static event EventHandler<T> Subscribers
        {
            add => subscribers += value;
            remove => subscribers -= value;
        }

        public static void Send(object? sender, T e)
        {
            subscribers?.Invoke(sender!, e);
        }
    }

    public class CurrentAssemblyListChangedEventArgs : EventArgs
    {
        public NotifyCollectionChangedEventArgs Inner { get; }
        public CurrentAssemblyListChangedEventArgs(NotifyCollectionChangedEventArgs e) => Inner = e;
    }

    public class TabPagesCollectionChangedEventArgs : EventArgs
    {
        public NotifyCollectionChangedEventArgs Inner { get; }
        public TabPagesCollectionChangedEventArgs(NotifyCollectionChangedEventArgs e) => Inner = e;
    }

    public class NavigateToReferenceEventArgs : EventArgs
    {
        public object Reference { get; }
        public object? Source { get; }
        public bool InNewTabPage { get; }

        public NavigateToReferenceEventArgs(object reference, object? source = null, bool inNewTabPage = false)
        {
            Reference = reference;
            Source = source;
            InNewTabPage = inNewTabPage;
        }
    }

    public class NavigateToEventArgs : EventArgs
    {
        // Request navigation details are WPF-specific; use object to avoid WPF dependency.
        public object Request { get; }
        public bool InNewTabPage { get; }

        public NavigateToEventArgs(object request, bool inNewTabPage = false)
        {
            Request = request;
            InNewTabPage = inNewTabPage;
        }
    }

    public class AssemblyTreeSelectionChangedEventArgs : EventArgs { }

    public class ApplySessionSettingsEventArgs : EventArgs
    {
        public SessionSettings SessionSettings { get; }
        public ApplySessionSettingsEventArgs(SessionSettings sessionSettings) => SessionSettings = sessionSettings;
    }

    public class MainWindowLoadedEventArgs : EventArgs { }

    public class ActiveTabPageChangedEventArgs : EventArgs
    {
        public ViewState? ViewState { get; }
        public ActiveTabPageChangedEventArgs(ViewState? viewState) => ViewState = viewState;
    }

    public class ResetLayoutEventArgs : EventArgs { }

    public class ShowAboutPageEventArgs : EventArgs
    {
        public object TabPage { get; }
        public ShowAboutPageEventArgs(object tabPage) => TabPage = tabPage;
    }

    public class ShowSearchPageEventArgs : EventArgs
    {
        public string? SearchTerm { get; }
        public ShowSearchPageEventArgs(string? searchTerm) => SearchTerm = searchTerm;
    }

    public class CheckIfUpdateAvailableEventArgs : EventArgs
    {
        public bool Notify { get; }
        public CheckIfUpdateAvailableEventArgs(bool notify = false) => Notify = notify;
    }
}
