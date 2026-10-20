# Privacy Policy

Effective Date: October 21, 2026

OpenNetMeter ("the Application") is a Windows desktop utility designed to monitor and report local network usage statistics for the user's device. The Application is intended to provide visibility into how the system and installed applications consume network data over time, including daily and historical totals, per-process traffic summaries, and session-based usage information.

This Privacy Policy explains the data the Application processes, where that data is stored, and the extent to which information is retained or disclosed. The Application is designed to minimize data collection and to process only information necessary for the feature set described in the project documentation.

## 1. Information Processed by the Application

The Application may process data required to provide network monitoring and reporting features, including:

- total bytes sent and received through the active network interface;
- the calendar date of observed network activity (usage is stored as daily totals);
- network adapter identifiers and connection status;
- application or process names associated with network traffic;
- locally stored historical usage data used to generate daily summaries, trend data, and process-level views.

The Application does not intentionally collect, monitor, or analyze the content of user communications, files, browsing activity, messages, credentials, or other personal or sensitive content. The Application is designed to measure network usage metadata and system activity, not the payload or substance of network communications.

## 2. Local Processing and Local Storage

The Application processes data primarily on the user's local device. Usage information may be stored locally in application data, a local SQLite database, or other local storage mechanisms in order to support features such as historical tracking, daily totals, and per-process usage summaries.

For the downloadable (MSI) version, this data is stored in `%LOCALAPPDATA%\OpenNetMeter`. For the Microsoft Store version, Windows stores it in the Application's private package storage instead.

Usage history is kept for the retention period the user selects in Settings › Data Management (60 days by default; 180 days, 1 year, or Forever can also be chosen). Daily records older than the selected period are deleted automatically. If "Forever" is selected, history is never deleted automatically and the database continues to grow until the user exports, shortens the retention period, or deletes the data.

Unless the user expressly chooses to export, share, or transmit data, usage information is retained only on the local device and is not transmitted to the Application developer, affiliated entities, or third parties as part of normal operation. The Application does not require account registration, cloud-based profile creation, or online synchronization to function.

## 3. Administrator Privileges and Network Access

The Application requires administrative privileges on Windows because it uses low-level Windows system interfaces to observe per-process network activity and adapter-level usage data. This elevated access is used solely to enable the Application's monitoring features described above and is not used to collect unrelated personal information or to inspect the contents of user traffic.

The Application does not use elevated privileges for any purpose other than enabling local network monitoring and usage accounting for the device.

## 4. User-Initiated Online Features

The Application never connects to the internet in the background. The following actions contact an online service only when the user clicks them:

- **Search about a process.** The Summary and History tabs include a search action for each listed process. When the user clicks it, the Application opens a Google search for that process name in the user's default web browser. No usage or traffic data accompanies the process name. The search is handled by the browser and is subject to Google's privacy policy.
- **Check for updates.** When the user clicks "Check for updates" in Settings, the Application requests information about the latest release from GitHub (`api.github.com`). As with any web request, GitHub receives the device's IP address and basic request details (including an "OpenNetMeter" user-agent). No usage data is sent. The request is subject to GitHub's privacy statement.
- **Project page links.** The repository link in the About dialog, and the "Download Update" button shown after an update check finds a newer version, open GitHub pages in the user's default web browser.

## 5. Data Export

Users can export stored usage history to a CSV or JSON file from Settings › Data Management or the History tab. Exported files contain dates, network adapter names, process names, and byte counts. They are saved only to the location the user chooses and are not transmitted anywhere by the Application. Once exported, the user is responsible for how those files are stored or shared.

## 6. No Sale, Transfer, or Commercial Exploitation of Personal Data

The Application does not sell, rent, lease, trade, or otherwise commercialize personal information. The Application does not use usage data for advertising, profiling, behavioral tracking, or other unrelated commercial purposes.

The Application may disclose information only:

- where required by applicable law, regulation, or legal process;
- to protect the security or integrity of the Application or user data; or
- where the user has explicitly chosen to export or share data.

## 7. Third-Party Components and Telemetry

The Application relies on local operating system APIs and Windows networking features required to observe traffic and present usage statistics. The Application does not include third-party telemetry, analytics, crash-reporting, or auto-update frameworks. Data collection is limited to locally generated network usage metadata necessary for the Application's core functionality, and the user-initiated features described in Section 4.

The Application also uses local SQLite storage for historical usage records and standard Windows system interfaces for network monitoring. No remote telemetry services are configured as part of the application's normal operation.

**Microsoft Store version.** If the Application is installed from the Microsoft Store, Microsoft operates the Store, delivers updates, and may collect data about the installation and use of Store apps under the Microsoft Privacy Statement and the user's Windows diagnostic data settings. Microsoft may make aggregated information available to the developer, such as install counts and crash or health reports. The developer does not receive the user's network usage history through the Store.

## 8. Security Measures

The Application implements reasonable technical and organizational measures intended to protect locally stored data against unauthorized access, loss, or misuse. However, no software or electronic storage method is completely immune to risk, and users are encouraged to maintain appropriate device security, including keeping the operating system and software up to date.

## 9. User Control and Data Deletion

Users control locally stored usage history in Settings › Data Management, where they can:

- choose how long history is kept (History retention);
- export history to a CSV or JSON file; and
- permanently delete all stored usage history (Delete All).

Users may also delete the local usage database files directly while the Application is closed.

Uninstalling the Application removes its stored usage history and settings. For the downloadable (MSI) version this deletes `%LOCALAPPDATA%\OpenNetMeter` and the "Start with Windows" task; upgrading to a newer version keeps them. For the Microsoft Store version, Windows removes the app's private package storage. Users who want to keep their history should export it before uninstalling.

Any data exported or manually shared by the user is governed by the user's own actions and is not transmitted automatically by the Application.

## 10. Changes to This Policy

This Privacy Policy may be updated from time to time to reflect changes in functionality, legal requirements, or project practices. Revised versions will be published in the project repository or related application documentation. Continued use of the Application after such changes constitutes acceptance of the updated Privacy Policy.

## 11. Contact

Questions or concerns regarding this Privacy Policy may be directed to the project maintainer at ashfaaq.riphque@gmail.com, or through the project's GitHub Issues page.

This Privacy Policy is intended to provide a clear statement of the Application's data-handling practices in a public-facing context and should be reviewed in conjunction with applicable local requirements and privacy obligations.