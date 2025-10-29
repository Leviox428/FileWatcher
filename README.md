# 🖥️ Real-Time Folder Syncer (C# Desktop Console App)

> A desktop console application that monitors one or more folders in real-time and automatically synchronizes changes to specified destination directories.

---

## 🖼️ Overview

This project is a **real-time folder synchronization tool** built with C#. It monitors multiple source directories and automatically copies, updates, or removes files in destination directories whenever changes occur.  

It is especially useful for developers or other users who want to **keep multiple folders in sync automatically** without manual copying.

**Key features include:**
- Multi-folder monitoring
- Real-time updates with minimal delay
- Extension filtering
- Optional removal of files before copying
- Retry mechanism for locked files
- Robust debouncing to prevent duplicate operations

---

## 🧰 Tech Stack
[![My Skills](https://skillicons.dev/icons?i=cs)](https://skillicons.dev)

## 💡 Project Highlights

- **Real-time monitoring:** Uses `FileSystemWatcher` to detect changes instantly.  
- **Multi-folder sync:** Supports multiple source and destination directories.  
- **File filtering:** Copy only specific extensions if needed.  
- **Safe copy with retries:** Handles locked files gracefully.  
- **Debouncing:** Prevents redundant operations during rapid file changes.  
- **Config-driven:** Easy to customize behavior through `config.json`.

---
