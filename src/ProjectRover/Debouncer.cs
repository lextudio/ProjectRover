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

using System;
using System.Threading;

namespace ProjectRover;

public class Debouncer
{
    private readonly TimeSpan time;
    private PeriodicTimer? lastPeriodicTimer;

    public Debouncer(TimeSpan time)
    {
        this.time = time;
    }
    
    public async void Debounce(Action action)
    {
        lastPeriodicTimer?.Dispose();

        lastPeriodicTimer = new PeriodicTimer(time);
        if (await lastPeriodicTimer.WaitForNextTickAsync())
        {
            action();
        }
    }
}
