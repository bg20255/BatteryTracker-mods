# Battery Tracker
 <a href="https://hosted.weblate.org/engage/battery-tracker/">
<img src="https://hosted.weblate.org/widgets/battery-tracker/-/svg-badge.svg" alt="Translation status" />
</a>

Show remaining battery percentage on Windows taskbar.

## Modifications (unpacked + modernized)
- **Packaged and unpackaged coexistence:** Local `Logs/` and `Config/` folders beside the EXE plus runtime detection keep both modes runnable; the Windows App Runtime is optional outside the Store.  
- **Persistent discharge reminder:** A 10-second timer wakes while unplugged, fires a toast at your configured minute interval with “I know” and “Snooze” buttons (Snooze carries five dropdown presets that persist), and doesn’t restart the countdown until you acknowledge it; the timer stops completely when you plug in to avoid extra wake-ups. This reminder encourages keeping the laptop plugged in to slow battery degradation and extend lifespan.  
- **Respectful notifier behavior:** Charging stops the timer entirely to avoid needless polling, and acknowledging the reminder is the only way to reset the wait state.  
- **Navigation cleanup:** Settings/About behave like tabs, clearing the back stack when switching—only About leaves a single history entry so the back arrow lights up.  
- **Title bar polish & startup control:** In unpackaged runs we load `Assets/logo.ico` for the custom title bar icon, and auto-start defaults to **off** so users opt in intentionally.

<img style="width:60%;" src=Assets/showcase5.png />
<table>
	<tr>
		<td><img src=Assets/showcase2.png border=0></td>
		<td><img src=Assets/showcase1.png border=0></td>
	</tr>
	<tr>
		<td style="text-align: center; vertical-align: middle;">Charging</td>
		<td style="text-align: center; vertical-align: middle;">Fully Charged</td>
	</tr>
</table>

## Features
1. It will **automatically switch icon color** when user change the system theme. ✨
2. It can notify users when the battery life is lower/higher than a threshold or the battery is fully charged. And the notifications are fully **customizable**!
3. **Modern setting UI** that fits the Operating System
	
	<img style="width:60%;" src=Assets/showcase5.png />

## Prerequisites
The application requires **.NET 7 Desktop Runtime** and **Windows App SDK Runtime**. If you install from Microsoft Store, dependencies will be automatically installed. 
Otherwise, you can download them from [Microsoft .NET Website](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) and [Windows App SDK Downloading Page](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads).

## How to Install
<a href='https://www.microsoft.com/store/apps/9P1FBSLRNM43'>
	<img src='https://developer.microsoft.com/en-us/store/badges/images/English_get-it-from-MS.png' alt='Microsoft Store' width='160'/>
</a>

Alternatively, you can download the installers in the release page.

## Privacy Policy
See [Privacy.md](./Privacy.md).

## How to Contribute
Priority place for bugs: https://github.com/myfix16/BatteryTracker/issues  
Priority place for ideas and general questions: https://github.com/myfix16/BatteryTracker/discussions

### Translations
You can make Battery Tracker more accessible by translating it to new languages! ❤️

We use Weblate's cool [free Libre plan](https://hosted.weblate.org/hosting/) that supports open source software projects!
You can find our project here: https://hosted.weblate.org/projects/battery-tracker/app/
<p align="left">

<a href="https://hosted.weblate.org/engage/battery-tracker/">
<img src="https://hosted.weblate.org/widgets/battery-tracker/-/app/multi-auto.svg" alt="Translation status" />
</a>
</p>

Alternatively, you can manually edit the translation files in `src/Strings`.

## Building from source

### 1. Prerequisites

- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the following individual components:
    - Windows 11 SDK (10.0.22000.0)
    - .NET 7 SDK
    - Git for Windows
- [Windows App SDK 1.2](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads#current-releases)
    
### 2. Clone the repository

```ps
git clone https://github.com/myfix16/BatteryTracker
```

This will create a local copy of the repository.

### 3. Build the project

To build Files for development, open the `BatteryTracker.sln` item in Visual Studio. Right-click on the `BatteryTracker` project in solution explorer and select ‘Set as Startup item’.

In the top pane, select the items which correspond to your desired build mode and the processor architecture of your device and click 'run'.

## Acknowledgments

Special thanks to:
- HavenDV's [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)
- www.flaticon.com's <a href="https://www.flaticon.com/free-icons/info" title="info icons">Info icons created by Pixel perfect - Flaticon</a>
