/*
    Copyright 2024 CodeMerx
    Copyright 2025 LeXtudio Inc.
    This file is part of ProjectRover.

    ProjectRover is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ProjectRover is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.
*/

using System.Collections.ObjectModel;
using ProjectRover.Notifications;
using ProjectRover.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProjectRover.ViewModels;

public partial class UpdatePanelViewModel : ObservableObject, IUpdatePanelViewModel, INotificationHandler
{
    public UpdatePanelViewModel(INotificationService notificationService)
    {
        notificationService.RegisterHandler(this);
    }

    public ObservableCollection<Notification> Notifications { get; } = new();

    public void ShowNotification(Notification notification)
    {
        Notifications.Add(notification);
    }

    public void ReplaceNotification(Notification notificationToBeReplaced, Notification replacementNotification)
    {
        CloseNotification(notificationToBeReplaced);
        ShowNotification(replacementNotification);
    }

    [RelayCommand]
    private void CloseNotification(Notification notification)
    {
        Notifications.Remove(notification);
    }

    [RelayCommand]
    private void ExecuteNotificationAction(NotificationAction notificationAction) => notificationAction.Action();
}
