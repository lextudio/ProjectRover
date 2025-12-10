#    Copyright 2024 CodeMerx
#    Copyright 2025 LeXtudio Inc.
#    This file is part of ProjectRover.

#    ProjectRover is free software: you can redistribute it and/or modify
#    it under the terms of the GNU Affero General Public License as published by
#    the Free Software Foundation, either version 3 of the License, or
#    (at your option) any later version.

#    ProjectRover is distributed in the hope that it will be useful,
#    but WITHOUT ANY WARRANTY; without even the implied warranty of
#    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
#    GNU Affero General Public License for more details.

#    You should have received a copy of the GNU Affero General Public License
#    along with ProjectRover.  If not, see<https://www.gnu.org/licenses/>.

#!/bin/sh

mkdir -p ProjectRover.app/Contents/{MacOS,Resources}

script_dir=$(dirname $0)
cp $script_dir/Info.plist ProjectRover.app/Contents
cp $script_dir/projectrover.icns ProjectRover.app/Contents/Resources

cp -Rp src/ProjectRover/bin/Release/net10.0/$1/publish/ ProjectRover.app/Contents/MacOS
