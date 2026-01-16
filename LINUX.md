Launching Project Rover on Linux
================================

One of the following commands usually launches this tool on your Linux distribution,

``` sh
./ProjectRover
dotnet ProjectRover.dll
```

And if both fail with errors, you can read the common issues below.

Missing .NET Runtime
--------------------

The executable `ProjectRover` is a native binary that initializes the .NET runtime. But it makes certain assumptions about the runtime installation on your system.

So, if you see it asks for a missing .NET runtime version, you can run `dotnet --info` at terminal to check if you have the required .NET runtime installed.

``` sh
$ ./ProjectRover
You must install .NET to run this application.

App: /home/lextudio/Downloads/ProjectRover-linux-x64/ProjectRover
Architecture: x64
App host version: 10.0.1
.NET location: Not found

The following locations were searched:
  Application directory:
    /home/lextudio/Downloads/ProjectRover-linux-x64/
  Environment variable:
    DOTNET_ROOT_X64 = <not set>
    DOTNET_ROOT = <not set>
  Registered location:
    /etc/dotnet/install_location_x64 = <not set>
  Default location:
    /usr/share/dotnet

Learn more:
https://aka.ms/dotnet/app-launch-failed

Download the .NET runtime:
https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch=x64&rid=linux-x64&os=ubuntu.24.04&apphost_version=10.0.1
```

Note that since Ubuntu now recommends using Snap packages, the traditional `/usr/share/dotnet` location may not exist. So, this `ProjectRover` executable may not find the runtime even if you have installed it via Snap.

SkiaSharp Exceptions
--------------------

If you see an exception like below when launching Project Rover,

``` sh
$ dotnet ProjectRover.dll
[16:20:38 ERR] Unhandled exception in Main
System.TypeInitializationException: The type initializer for 'SkiaSharp.SKImageInfo' threw an exception.
 ---> System.DllNotFoundException: Unable to load shared library 'libSkiaSharp' or one of its dependencies. In order to help diagnose loading problems, consider using a tool like strace. If you're using glibc, consider setting the LD_DEBUG environment variable: 
/snap/core22/current/lib/x86_64-linux-gnu/libc.so.6: version `GLIBC_2.38' not found (required by /lib/x86_64-linux-gnu/libfontconfig.so.1)
/var/snap/dotnet/common/dotnet/shared/Microsoft.NETCore.App/10.0.1/libSkiaSharp.so: cannot open shared object file: No such file or directory
/home/lextudio/Downloads/ProjectRover-linux-x64/liblibSkiaSharp.so: cannot open shared object file: No such file or directory
/var/snap/dotnet/common/dotnet/shared/Microsoft.NETCore.App/10.0.1/liblibSkiaSharp.so: cannot open shared object file: No such file or directory
/home/lextudio/Downloads/ProjectRover-linux-x64/libSkiaSharp: cannot open shared object file: No such file or directory
/var/snap/dotnet/common/dotnet/shared/Microsoft.NETCore.App/10.0.1/libSkiaSharp: cannot open shared object file: No such file or directory
/home/lextudio/Downloads/ProjectRover-linux-x64/liblibSkiaSharp: cannot open shared object file: No such file or directory
/var/snap/dotnet/common/dotnet/shared/Microsoft.NETCore.App/10.0.1/liblibSkiaSharp: cannot open shared object file: No such file or directory

   at SkiaSharp.SkiaApi.sk_colortype_get_default_8888()
   at SkiaSharp.SkiaApi.sk_colortype_get_default_8888()
   at SkiaSharp.SKImageInfo..cctor()
   --- End of inner exception stack trace ---
   at Avalonia.Skia.PlatformRenderInterface..ctor(Nullable`1 maxResourceBytes)
   at Avalonia.Skia.SkiaPlatform.Initialize(SkiaOptions options)
   at Avalonia.SkiaApplicationExtensions.<>c.<UseSkia>b__0_0()
   at Avalonia.AppBuilder.SetupUnsafe()
   at Avalonia.AppBuilder.Setup()
   at Avalonia.AppBuilder.SetupWithLifetime(IApplicationLifetime lifetime)
   at Avalonia.ClassicDesktopStyleApplicationLifetimeExtensions.StartWithClassicDesktopLifetime(AppBuilder builder, String[] args, Action`1 lifetimeBuilder)
   at ProjectRover.Program.Main(String[] args) in /home/runner/work/ProjectRover/ProjectRover/src/ProjectRover/Program.cs:line 60
```

it is not a surprise that the packaged .NET runtime on that Linux distribution is not compatible with the Rover shipped SkiaSharp native libraries.

You can switch to a custom .NET runtime installation outside of the package system (Snap or another). To do so, you might refer to the example below,

``` sh
# install to ~/.dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh -o ~/dotnet-install.sh
chmod +x ~/dotnet-install.sh
~/dotnet-install.sh --channel 10.0 --runtime dotnet --install-dir ~/.dotnet

# Use it to run the app
~/.dotnet/dotnet --info
~/.dotnet/dotnet ~/Downloads/ProjectRover-linux-x64/ProjectRover.dll
```

This way, you can avoid the Snap-packaged .NET runtime and use the official .NET runtime from Microsoft that is more compatible with SkiaSharp.
