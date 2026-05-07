# 🖥️ Real-Time Folder Syncer (C# Desktop Console App)

> A desktop console application that monitors folders in real-time and synchronizes files locally, with optional manual FTP release support.
---

## 🖼️ Overview

This project is a **real-time folder synchronization tool** built with C#. It continuously monitors one or more source directories and automatically synchronizes changes to configured destination folders.

In addition to local synchronization, the application also supports a **manual release system** using the `--release` command, allowing users to deploy files to a FTP server when needed.

The FTP release system supports:
- Uploading the **contents of a folder**
- Creating and uploading a **ZIP archive** of the folder

This makes the tool useful for developers, deployment workflows, backups, and automated local synchronization tasks.

## ✨ Features
- 📂 **Multi-folder monitoring**
- ⚡ **Real-time local synchronization**
- 🌐 **Manual FTP deployment via `--release`**
- 🗜️ **Optional ZIP packaging for releases**
- 📦 **Direct folder content FTP upload**
- 🔍 **Extension filtering**
- 🧹 **Optional cleanup before synchronization**
- 🔁 **Retry mechanism for locked files**
- 🛡️ **Debouncing to prevent duplicate operations**
- ⚙️ **Easily configurable in Config.json files**

---

## 🧰 Tech Stack
[![My Skills](https://skillicons.dev/icons?i=cs)](https://skillicons.dev)

---

## 🎬 Showcase

### 🖼️ Screenshots
| Program | Config example |
|:--:|:--:|
| ![Screenshot 1](/Showcase/showcase1.png) | ![Screenshot 2](/Showcase/showcase2.png) |

<!--
### 🎥 Demo
> [🎞️ Watch the Demo Video](https://youtu.be/your-demo-link)  
or  
> ![Demo GIF](assets/demo.gif)

-->
---

## 💡 Project Highlights

- **Real-time monitoring:** Uses `FileSystemWatcher` to detect file changes instantly.
- **Local folder synchronization:** Automatically mirrors changes between directories.
- **Manual FTP releases:** Deploy files only when explicitly triggered with `--release`.
- **ZIP deployment mode:** Compress folders into ZIP archives before FTP upload.
- **Direct upload mode:** Upload raw folder contents directly to FTP.
- **File filtering:** Process only selected file extensions.
- **Safe copy with retries:** Handles locked files gracefully.
- **Debouncing:** Prevents duplicate processing during rapid file operations.
- **Config-driven architecture:** Easily customizable through `config.json`.

---

## 🧭 How It Works

### 1. Configuration

On first run, the application generates a `config.json` file with example settings.

Users can configure:
- Source folders
- Destination folders
- FTP credentials
- Release mode (ZIP or direct upload)
- File extension filters
- Cleanup and overwrite behavior

---

### 2. Real-Time Monitoring

The application creates `FileSystemWatcher` instances for all configured source folders, including subdirectories.

It listens for:
- File creation
- File modification
- File deletion
- File renaming

---

### 3. Debouncing

Rapid file system events are grouped together to prevent duplicate processing and unnecessary operations.

This improves:
- Stability
- Performance
- Reliability during bulk file changes

---

### 4. Synchronization

Changed files are automatically synchronized to configured local destination folders while respecting filters and overwrite settings.

Renamed and deleted files are also reflected in destination folders.

---

### 5. Manual FTP Release

When the application is started with the `--release` argument, it can:

- Upload folder contents directly to a FTP server
- Generate a ZIP archive and upload it
- Deploy prepared release files manually

This keeps FTP deployment separate from the real-time synchronization workflow.

---

### 6. Retry Logic

If files are temporarily locked by another process, the application retries operations multiple times with delays to ensure reliable synchronization.

---

## 🏁 Conclusion

This project demonstrates practical and robust C# development focused on:

- Real-time file synchronization
- Manual deployment workflows
- FTP file releases
- ZIP packaging automation
- Concurrency and error handling
- Configurable desktop tooling

It is a useful automation tool for developers and power users who need reliable local synchronization with optional FTP release capabilities.
