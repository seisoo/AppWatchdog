# 🛡️ AppWatchdog  
**Windows Application & Service Watchdog**

![overview](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_service.png?raw=true)

**AppWatchdog** ist ein **robuster Windows Watchdog**, bestehend aus einem **Windows Service** und einer **WPF-Benutzeroberfläche**, entwickelt für den produktiven Einsatz auf Windows-Systemen.

Er überwacht definierte Anwendungen, erkennt Ausfälle zuverlässig und stellt Prozesse oder Dienste automatisch wieder her – inklusive **Benachrichtigungen**, **Logging** und **Self-Healing-Mechanismen**.

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
      
┌──────────────────────┐      
│ AppWatchdog.UI.WPF │ ← Konfiguration & Monitoring (WPF)
└──────────▲───────────┘  
│ Named Pipes (IPC)        
┌──────────┴───────────┐      
│ AppWatchdog.Service │ ← Windows Service (Watchdog Engine)
└──────────────────────┘      
      

### Komponenten

- **AppWatchdog.Service**
  - Windows Service
  - Führt die Überwachung aus
  - Startet Prozesse neu und protokolliert Status
- **AppWatchdog.UI.WPF**
  - MVVM-Architektur
  - Konfiguration der überwachten Anwendungen
  - Anzeige von Status, Logs und Benachrichtigungen
- **IPC (Named Pipes)**
  - Versioniert & fehlertolerant
  - Schutz vor Inkompatibilitäten und Timeouts

---

## 🖥️ Screenshots

### Service Management
![Service Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_service.png?raw=true)

### Application Monitoring
![Apps Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_apps.png?raw=true)

### Notifications
![Notifications Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_notifications.png?raw=true)

### Logs
![Logs Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_logs.png?raw=true)

---

## 🚀 Installation

### Voraussetzungen

- Windows 10 / 11 (x64)
- **Administratorrechte** (für Service-Installation)

> ℹ️ Die bereitgestellten Builds sind **self-contained**  
> → **keine .NET Runtime Installation erforderlich**

---

### Schritte

1. Lade das passende Release herunter
2. Entpacke beide Dateien in ein gemeinsames Verzeichnis:
AppWatchdog.Service.exe
AppWatchdog.UI.WPF.exe

3. Starte **AppWatchdog.UI.WPF.exe**
4. Installiere und starte den Service über die UI
5. Konfiguriere die zu überwachenden Anwendungen
6. Aktiviere Benachrichtigungen (optional)

---

## ⚙️ Konfiguration

- Über die UI konfigurierbar:
- Executable-Pfad
- Argumente
- Startverhalten
- Benachrichtigungen:
- SMTP (Host, Port, Benutzer, TLS)
- ntfy Topic & Server
- Logs werden **lokal gespeichert**

> 🔒 **Hinweis**  
> Zugangsdaten werden lokal abgelegt.  
> Für produktive Umgebungen wird empfohlen, den Zugriff auf Konfigurationsdateien entsprechend abzusichern.

---

## 🧪 Build & Releases

- Builds werden über **GitHub Actions** erzeugt
- Zielplattform:
- **Windows x64**
- Service und UI werden als **getrennte, self-contained Single-EXEs** ausgeliefert
- Keine Abhängigkeiten zur Laufzeit

---

## 🔐 Sicherheit

- Keine externen Netzwerkverbindungen ohne explizite Konfiguration
- IPC ist versioniert und validiert
- Service läuft mit minimal notwendigen Rechten

Sicherheitsrelevante Themen bitte **nicht öffentlich** melden, sondern über einen privaten Kontakt.

---

## 📄 Lizenz

Dieses Projekt ist unter der **MIT License** lizenziert.  
Siehe [LICENSE](LICENSE) für Details.

---

## 📌 Projektstatus

**AppWatchdog ist produktionsreif** und wird aktiv weiterentwickelt.  
Der Fokus liegt bewusst auf **Windows-Systemen**, um eine tiefe Integration in:

- Service Control Manager
- Event Logs
- Desktop- & Server-Umgebungen

zu ermöglichen.
