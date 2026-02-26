## TrayBaked

TrayBaked is a small Windows tray application that helps you recover your workflow after `explorer.exe` restarts (for example after a crash, Windows update, or when you restart Explorer manually). It watches for Explorer being restarted and then offers to restart your own applications so you do not need to hunt them down and launch them one by one.

The app is built with WPF on .NET and runs quietly in the system tray.

---

## Features

- **Explorer restart detection**: Monitors for `explorer.exe` restarts using a lightweight background watcher.
- **Quick app restart**:
  - Automatically restart the apps you had running before Explorer restarted, or
  - Interactively choose which configured apps to restart.
- **System tray integration**:
  - Right‑click the tray icon for a themed context menu.
  - Open settings, restart apps on demand, view the activity log, or exit the app.
- **Activity log**: Shows a history of restarts and other important actions performed by TrayBaked.
- **Configurable app list**: Choose which applications TrayBaked can restart and how aggressively it should debounce Explorer restart events.

---

## Installation

- **Download the installer**
  - Go to the [TrayBaked releases page](https://github.com/filmstarr/TrayBaked/releases).
  - Download the latest `TrayBaked-Setup.exe` from the **Assets** section.

- **Run the installer**
  - Double‑click `TrayBaked-Setup.exe`.
  - Follow the on‑screen steps to choose an install location and complete setup.
  - After installation, TrayBaked will be available from the Start menu and in the system tray.

---

## Basic Usage

Once running, TrayBaked lives in the system tray (notification area).

- **Open the tray menu**
  - **Right‑click** the TrayBaked icon.
  - Use the menu items to open settings, restart apps, view the activity log, restart Explorer, or exit.

- **Open the activity log**
  - **Left‑click** the tray icon.
  - This opens a window listing recent Explorer restarts and application restart events.

---

## Configuring Applications to Restart

1. **Open settings**
   - Right‑click the tray icon and choose **“Settings…”**.

2. **Add applications**
   - Use the settings window to add the apps you want TrayBaked to manage.
   - Each entry should include at least:
     - **Display name** (how it appears in the UI).
     - **Process name** (the running process to look for, without `.exe`).

3. **Save**
   - Click **Save** in the settings window.
   - The configuration is persisted and used the next time Explorer restarts.

4. **Debounce settings**
   - In settings, you can adjust how long TrayBaked waits after detecting an Explorer restart before acting (to avoid reacting to multiple rapid restarts).

---

## What Happens When Explorer Restarts

When TrayBaked detects that `explorer.exe` has restarted:

1. **Detection**
   - The background monitor observes that Explorer has gone away and come back.
   - An entry is written to the activity log.

2. **Auto‑restart vs. prompt**
   - If **Auto Restart** is enabled in the settings:
     - TrayBaked automatically restarts your configured applications that were running before Explorer restarted.
   - If **Auto Restart** is disabled:
     - TrayBaked shows a Windows notification (toast) asking what you want to do:
       - Restart **all** configured apps.
       - Restart only the apps that were **running**.
       - **Select…** which apps to restart.

3. **Selection dialog (Restart window)**
   - If you choose **Select…** (or if notifications are unavailable), a restart dialog appears:
     - It lists your configured apps and indicates which ones are currently running.
     - You can tick the apps you want to restart, then click **Restart**.
     - While restarting, a progress list shows success or error for each app.

4. **Activity log**
   - Each restart attempt is recorded in the activity log with success or error messages.

---

## Tray Menu Commands

From the tray context menu you can:

- **Settings…**: Configure the list of applications TrayBaked manages and debounce/behavior options.
- **Restart Apps…**: Manually open the restart dialog at any time, even if Explorer has not just restarted.
- **Activity Log…**: Open the log window to review recent actions.
- **Restart Explorer**: Instruct Windows to restart `explorer.exe`. TrayBaked then reacts as usual when Explorer comes back.
- **Exit**: Stop monitoring, hide the tray icon, and close the application.

---

## Troubleshooting

- **I don’t see the tray icon**
  - Make sure TrayBaked is running (check Task Manager).
  - Windows may hide tray icons; click the “Show hidden icons” arrow in the notification area.

- **My app is not restarted**
  - Open **Settings…** and confirm:
    - The app is listed and enabled.
    - The **process name** is correct and matches the running process.
  - Check the **Activity Log** for error messages about that application.

- **Notifications don’t appear**
  - If Windows toast notifications are disabled or fail, TrayBaked automatically falls back to showing the restart dialog window instead.

---

## Building & Contributing

- **Requirements**
  - Windows 10 or later.
  - .NET 8 SDK with Windows desktop development tools.

- **Contributions**
  - Issues and pull requests are welcome.
  - Please include clear reproduction steps for bugs and concise descriptions for feature requests.

