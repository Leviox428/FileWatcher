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

---

## 🎬 Showcase

### 🖼️ Screenshots
| Home / Solar System View | Planet Navigation Example |
|:--:|:--:|
| ![Screenshot 1](developer-portfolio-website/public/images/myPortfolio1.png) | ![Screenshot 2](developer-portfolio-website/public/images/myPortfolio2.png) |

<!--
### 🎥 Demo
> [🎞️ Watch the Demo Video](https://youtu.be/your-demo-link)  
or  
> ![Demo GIF](assets/demo.gif)

*(Keep GIFs under ~5MB for faster loading.)*
-->
---

## 💡 Project Highlights

- **Real-time monitoring:** Uses `FileSystemWatcher` to detect changes instantly.  
- **Multi-folder sync:** Supports multiple source and destination directories.  
- **File filtering:** Copy only specific extensions if needed.  
- **Safe copy with retries:** Handles locked files gracefully.  
- **Debouncing:** Prevents redundant operations during rapid file changes.  
- **Config-driven:** Easy to customize behavior through `config.json`.

---

## 🧭 How It Works

1. **Configuration:**  
   On first run, the app creates a `config.json` with default folder paths and options. Users edit it to specify their actual folders, extensions, and preferences.  

2. **Monitoring:**  
   `FileSystemWatcher` instances watch all configured source folders, including subdirectories, for changes, creations, or renames.  

3. **Debouncing:**  
   Changes are tracked to prevent processing the same file multiple times in rapid succession.  

4. **Copying & Deleting:**  
   Files are copied to their respective destination folders, respecting filters and other settings. Renamed files in the source are reflected in the destination.  

5. **Retry Logic:**  
   Locked files are retried multiple times with delays to ensure reliable copying without errors.  

---

## 📚 What I Learned

- Deep understanding of **FileSystemWatcher** and its quirks.  
- Handling **concurrent file system events** safely using `ConcurrentDictionary`.  
- Implementing **debouncing** and **retry logic** for real-time applications.  
- Working with **configuration-driven design** to make apps flexible.  
- Improving **robustness** in desktop console applications.

---

## 🏁 Conclusion

This project demonstrates **robust C# programming** for real-time file system monitoring, concurrency management, and error handling.  
It is a practical tool for automating folder synchronization and showcases how to handle **real-world edge cases** in desktop applications.
