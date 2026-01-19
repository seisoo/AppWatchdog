# AppWatchdog  
**Windows Application & Service Watchdog**

![AppWatchdog Overview]([assets/screenshots/overview.png](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_service.png?raw=true))

AppWatchdog ist ein **robuster Windows Watchdog**, bestehend aus einem **Windows Service** und einer **WPF UI**, entwickelt für den produktiven Einsatz.  
Er überwacht definierte Anwendungen, erkennt Ausfälle zuverlässig und stellt Prozesse oder Dienste automatisch wieder her – inklusive Benachrichtigungen, Logging und Self-Healing.

---

## ✨ Features

- 🔍 **Zuverlässige Prozessüberwachung**
  - Mehrstufige Down-Detection (keine False Positives)
  - Zeitbasierte Bestätigung vor Recovery
- 🔁 **Automatische Wiederherstellung**
  - Neustart von Anwendungen
  - Wiederholversuche mit Backoff-Strategie
- 🛠 **Windows Service + WPF UI**
  - Service läuft unabhängig von der Benutzeranmeldung
  - UI zur Konfiguration, Steuerung und Diagnose
- 🔔 **Benachrichtigungen**
  - SMTP (E-Mail)
  - ntfy
  - Optional: Uptime Kuma Push
- 📜 **Integriertes Logging**
  - Strukturierte Logfiles
  - UI-Logviewer
- 🔐 **IPC über Named Pipes**
  - Versioniertes Protokoll
  - Timeout- und Kompatibilitätsprüfung
- 🧠 **Self-Healing**
  - Erkennt fehlenden oder inkompatiblen Service
  - Reparatur & Neuinstallation direkt aus der UI

---

## 🧩 Architektur

![Architecture Diagram](assets/diagrams/architecture.png)

**Komponentenübersicht:**

- **AppWatchdog.Service**
  - Windows Service
  - Führt die eigentliche Überwachung aus
  - Startet Prozesse neu und protokolliert Status
- **AppWatchdog.UI (WPF)**
  - MVVM-Architektur
  - Konfiguration der überwachten Anwendungen
  - Anzeige von Status, Logs und Benachrichtigungen
- **IPC (Named Pipes)**
  - Kommunikation zwischen UI und Service
  - Versioniert, fehlertolerant, timeout-geschützt

---

## 🖥️ Screenshots

### Service Management
![Service Page](assets/screenshots/service.png)

### Application Monitoring
![Apps Page](assets/screenshots/apps.png)

### Notifications
![Notifications Page](assets/screenshots/notifications.png)

### Logs
![Logs Page](assets/screenshots/logs.png)

---

## 🚀 Installation

### Voraussetzungen
- Windows 10 / 11
- .NET Framework 4.7.2+ **oder** .NET 8.0 (je nach Build)
- Administratorrechte (für Service-Installation)

### Schritte
1. Lade das passende Release für deine Architektur herunter (`x86`, `x64`, `ARM64`)
2. Starte **AppWatchdog.UI.exe**
3. Installiere und starte den Service über die UI
4. Konfiguriere die zu überwachenden Anwendungen
5. Aktiviere Benachrichtigungen (optional)

---

## ⚙️ Konfiguration

- Anwendungen werden über die UI definiert:
  - Executable-Pfad
  - Argumente
  - Startverhalten
- Benachrichtigungen:
  - SMTP (Host, Port, Benutzer, TLS)
  - ntfy Topic & Server
- Logs werden lokal gespeichert  
  *(keine Cloud-Verbindungen außer explizit konfiguriert)*

> **Hinweis:**  
> Zugangsdaten werden lokal gespeichert. Für produktive Umgebungen wird empfohlen, den Zugriff auf die Konfigurationsdateien entsprechend abzusichern.

---

## 🧪 Build & Releases

- Builds werden über **GitHub Actions** erstellt
- Unterstützte Architekturen:
  - Windows x86
  - Windows x64
  - Windows ARM64 (bei .NET 6+/8 Builds)
- Service und UI werden **als getrennte EXEs** ausgeliefert

---

## 🔐 Sicherheit

- Keine externen Netzwerkverbindungen ohne explizite Konfiguration
- IPC ist versioniert und validiert
- Service läuft mit minimal notwendigen Rechten

Sicherheitsprobleme bitte **nicht öffentlich** melden, sondern über einen privaten Kontakt.

---

## 🤝 Contribution

Beiträge sind willkommen:

- Bug Reports
- Feature Requests
- Pull Requests

Bitte vor größeren Änderungen ein Issue eröffnen.

---

## 📄 Lizenz

Dieses Projekt ist unter der **MIT License** lizenziert.  
Siehe [LICENSE](LICENSE) für Details.

---

## 📌 Projektstatus

AppWatchdog ist **produktionsreif** und wird aktiv weiterentwickelt.  
Der Fokus liegt bewusst auf **Windows-Systemen**, um eine tiefe Integration in den Service Control Manager, Event Logs und Desktop-Umgebungen zu ermöglichen.
