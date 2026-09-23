# Privacy Policy

Effective Date: September 23, 2026

OpenNetMeter ("the Application") is a Windows desktop utility designed to monitor and report local network usage statistics for the user's device. The Application is intended to provide visibility into how the system and installed applications consume network data over time, including daily and historical totals, per-process traffic summaries, and session-based usage information.

This Privacy Policy explains the data the Application processes, where that data is stored, and the extent to which information is retained or disclosed. The Application is designed to minimize data collection and to process only information necessary for the feature set described in the project documentation.

## 1. Information Processed by the Application

The Application may process data required to provide network monitoring and reporting features, including:

- total bytes sent and received through the active network interface;
- timestamps associated with observed network activity;
- network adapter identifiers and connection status;
- application or process names associated with network traffic;
- locally stored historical usage data used to generate daily summaries, trend data, and process-level views.

The Application does not intentionally collect, monitor, or analyze the content of user communications, files, browsing activity, messages, credentials, or other personal or sensitive content. The Application is designed to measure network usage metadata and system activity, not the payload or substance of network communications.

## 2. Local Processing and Local Storage

The Application processes data primarily on the user's local device. Usage information may be stored locally in application data, a local SQLite database, or other local storage mechanisms in order to support features such as historical tracking, daily totals, and per-process usage summaries.

Unless the user expressly chooses to export, share, or transmit data, usage information is retained only on the local device and is not transmitted to the Application developer, affiliated entities, or third parties as part of normal operation. The Application does not require account registration, cloud-based profile creation, or online synchronization to function.

## 3. Administrator Privileges and Network Access

The Application requires administrative privileges on Windows because it uses low-level Windows system interfaces to observe per-process network activity and adapter-level usage data. This elevated access is used solely to enable the Application's monitoring features described above and is not used to collect unrelated personal information or to inspect the contents of user traffic.

The Application does not use elevated privileges for any purpose other than enabling local network monitoring and usage accounting for the device.

## 4. Search About Process Feature

The Summary and History tabs include an optional "search about process" action. When a user clicks this option for a listed process, the process name is sent to the user's default web browser to perform a search. This is user-initiated on a per-click basis; no usage or traffic data accompanies the process name, and the Application does not perform this lookup automatically or in the background.

## 5. No Sale, Transfer, or Commercial Exploitation of Personal Data

The Application does not sell, rent, lease, trade, or otherwise commercialize personal information. The Application does not use usage data for advertising, profiling, behavioral tracking, or other unrelated commercial purposes.

The Application may disclose information only:

- where required by applicable law, regulation, or legal process;
- to protect the security or integrity of the Application or user data; or
- where the user has explicitly chosen to export or share data.

## 6. Third-Party Components and Telemetry

The Application relies on local operating system APIs and Windows networking features required to observe traffic and present usage statistics. In the current project implementation, the Application does not include known third-party telemetry, analytics, crash-reporting, or auto-update frameworks for collection of usage data. Data collection is limited to locally generated network usage metadata necessary for the Application's core functionality, and the process-name lookup described in Section 4.

The Application also uses local SQLite storage for historical usage records and standard Windows system interfaces for network monitoring. No remote telemetry services are configured as part of the application's normal operation.

## 7. Security Measures

The Application implements reasonable technical and organizational measures intended to protect locally stored data against unauthorized access, loss, or misuse. However, no software or electronic storage method is completely immune to risk, and users are encouraged to maintain appropriate device security, including keeping the operating system and software up to date.

## 8. User Control and Data Deletion

Users may control or remove locally stored usage history through the Application's History view and by deleting the local usage database files generated by the Application. Any data exported or manually shared by the user is governed by the user's own actions and is not transmitted automatically by the Application.

## 9. Changes to This Policy

This Privacy Policy may be updated from time to time to reflect changes in functionality, legal requirements, or project practices. Revised versions will be published in the project repository or related application documentation. Continued use of the Application after such changes constitutes acceptance of the updated Privacy Policy.

## 10. Contact

Questions or concerns regarding this Privacy Policy may be directed to the project maintainer at ashfaaq.riphque@gmail.com, or through the project's GitHub Issues page.

This Privacy Policy is intended to provide a clear statement of the Application's data-handling practices in a public-facing context and should be reviewed in conjunction with applicable local requirements and privacy obligations.