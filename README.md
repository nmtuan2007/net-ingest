# NetIngest

Inspired by the clever simplicity of [GitIngest](https://github.com/coderamp-labs/gitingest) – which transforms any GitHub repo into an LLM-ready prompt by swapping "hub" for "ingest" in the URL – **NetIngest** brings that magic to your local machine. This WPF powerhouse scans, filters, and consolidates your entire codebase into a single, optimized text block, tailored for seamless feeding into ChatGPT, Claude, Grok, or any AI coding wizard.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)](https://www.microsoft.com/windows)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Overview

Manually copying and pasting dozens of files into an LLM chat is painful and error-prone.  
**NetIngest** fully automates this process:

- Recursively scans your project folder
- Automatically skips binaries, build artifacts, and large files
- Shows an interactive file tree with per-file token estimates
- Generates a single clean text block (or file) ready to paste into any AI coding assistant

## Features

| Feature                  | Description                                                                                   |
| ------------------------ | --------------------------------------------------------------------------------------------- |
| Smart Directory Scanning | Full recursive traversal with progress feedback                                               |
| Intelligent Filtering    | Respects `.gitignore`<br>Custom glob ignore patterns<br>Whitelist (force-include) support     |
| Performance Optimized    | Async file processing<br>Configurable max file size<br>Sampling mode (limit files per folder) |
| Interactive Tree View    | Visual folder structure<br>Real-time token count per file<br>Right-click to ignore/whitelist  |
| AI-Ready Output          | Token estimation (chars ÷ 4)<br>Built-in + custom prompt templates<br>3 view modes            |
| Flexible Export          | Copy current view<br>Copy with selected template<br>Save to `.txt`                            |

## Requirements

- OS: **Windows 10 / 11**
- Runtime: **.NET 9.0 Desktop Runtime**
- To build from source: **.NET 9.0 SDK**

## Installation & Quick Start

```bash
git clone https://github.com/nmtuan2007/net-ingest.git
cd net-ingest

# Restore packages
dotnet restore

# Publish the standalone executable
dotnet publish -c Release -r win-x64 /p:Platform=x64

# Publish with self-contained is false
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# Run
dotnet run --project NetIngest/NetIngest.csproj
```

Or simply run the generated `NetIngest.exe` from  
`bin/Release/net9.0-windows/`

## Usage

1. Click `...` → select your project root folder
2. (Optional) Adjust filters on the left sidebar:
   - Max file size slider
   - Limit files per directory (sampling)
   - Add custom ignore patterns
3. Click the big green **ANALYZE CODEBASE** button
4. Explore results in three tabs:
   - **Summary** – stats overview
   - **Tree View** – interactive structure + token counts
   - **Full Content** – complete consolidated text
5. Export:
   - **Copy View** → current tab
   - **Copy with Template** → wrapped in your chosen prompt
   - **Save** → export to `.txt`

## Configuration

### Ignore Patterns & Whitelist

Uses standard Glob syntax:

```
*.png
node_modules/
bin/
obj/
logs/**
```

### Prompt Templates

Stored in `templates.json` (same folder as the exe).  
Editable directly in the **Templates** tab.

Only one placeholder is supported: `{SOURCE_CODE}` → replaced with the full processed codebase.

## Contributing

Contributions are very welcome!

1. Fork the repo
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

Distributed under the **MIT License**. See `LICENSE` for details.

## Acknowledgments

- Built with love in **C# / WPF**
- Uses [Glob](https://github.com/kthomas/Glob) for pattern matching

**Author**: Tuan Nguyen – [@nmtuan2007](https://github.com/nmtuan2007)
