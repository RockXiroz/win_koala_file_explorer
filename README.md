# Koala File Explorer

A Windows WPF file explorer with customer file tagging and media playback.

## Features

- **File Tree**: Browse the Windows directory structure
- **File List**: View files in the selected directory
- **Media Player**: Play `.mp4`, `.wmv`, `.avi`, `.mkv`, `.mov`, `.mp3`, `.wav`, `.flac` files inline
- **Customer Tags**: Create named customer tags (with color) and attach them to files
- **Search by Name**: Filter files by filename
- **Search by Tag**: Filter files by customer tag name

## Requirements

- Windows 10/11
- .NET 8.0 Runtime ([download](https://dotnet.microsoft.com/download/dotnet/8.0))

## Build & Run

```bash
dotnet build KoalaFileExplorer.sln
dotnet run --project KoalaFileExplorer/KoalaFileExplorer.csproj
```

Or open `KoalaFileExplorer.sln` in Visual Studio 2022 and press F5.

## Usage

1. **Navigate**: Click folders in the File Tree to browse directories
2. **Preview**: Select a media file and click ▶ Play, or double-click a media file
3. **Tag a file**: Select a file → pick a customer tag → click "＋ Tag File"
4. **Create a tag**: Enter a name in the tag box → click "+ Add Tag" (click the color square to change color)
5. **Search by name**: Type in the search bar → click "🔍 By Name"
6. **Search by tag**: Type a customer tag name → click "🏷 By Tag"

## Data Storage

Tags and mappings are stored in:
```
%APPDATA%\KoalaFileExplorer\tags.json
%APPDATA%\KoalaFileExplorer\mappings.json
```
