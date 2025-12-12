NetIngest 🚀

NetIngest is a powerful Windows Presentation Foundation (WPF) application designed to ingest, analyze, and consolidate local codebases into a single, optimized text format. It is specifically built to help developers prepare code context for Large Language Models (LLMs) like ChatGPT, Claude, or DeepSeek.

![alt text](https://img.shields.io/badge/license-MIT-blue.svg)


![alt text](https://img.shields.io/badge/platform-Windows-lightgrey.svg)


![alt text](https://img.shields.io/badge/.NET-9.0-purple.svg)

📖 Overview

When working with AI coding assistants, pasting multiple files manually is tedious. NetIngest automates this by recursively scanning your project directory, filtering out binary or irrelevant files, visualizing the structure, and generating a single prompt-ready text block containing both the directory tree and file contents.

✨ Key Features

📁 Smart Directory Scanning: Recursively traverses project folders.

🚫 Intelligent Filtering:

Respects .gitignore files automatically.

Supports custom Ignore Patterns (Glob syntax, e.g., docs/, *.svg).

Whitelist (Force Include) specific critical files even if limits are applied.

⚡ Performance Optimized:

Asynchronous file processing.

Configurable Max File Size limits to avoid bloating.

Option to limit the number of files processed per directory (Sampling mode).

🌳 Tree View Visualization:

Interactive file tree showing individual file token counts.

Context menu to quickly add files to Ignore/Whitelist or copy paths.

🤖 AI-Ready Output:

Token Estimation: Real-time estimation of token usage (approx. chars / 4).

Prompt Templates: Built-in and custom templates (e.g., "Explain Code", "Refactor Request") to wrap your codebase automatically.

📋 multiple View Modes: Summary, Tree Structure, and Full Content.

🛠 Prerequisites

OS: Windows 10/11 (WPF Application).

Runtime: .NET 9.0 Desktop Runtime.

Build: .NET 9.0 SDK (if compiling from source).

🚀 Getting Started
Installation

Clone the repository:

code
Bash
download
content_copy
expand_less
git clone https://github.com/yourusername/net-ingest.git
cd net-ingest

Build the project:

code
Bash
download
content_copy
expand_less
dotnet restore
dotnet build -c Release

Run the application:
Navigate to bin/Release/net9.0-windows/ and run NetIngest.exe, or run via CLI:

code
Bash
download
content_copy
expand_less
dotnet run --project NetIngest.csproj
📖 Usage Guide

Select Source: Click the ... button to choose the root folder of the project you want to analyze.

Configure Filters (Sidebar):

Adjust Max File Size slider (default: 100KB).

Check Limit files per directory if you only want a sample structure (e.g., top 2 files per folder).

Edit Ignore Patterns to exclude folders like node_modules, bin, or obj.

Analyze: Click the big green ANALYZE CODEBASE button.

Review Results:

Summary: General stats (File count, Total tokens).

Tree View: Visual structure. Right-click nodes to ignore/whitelist.

Full Content: The raw consolidated text.

Export:

Copy View: Copies the currently visible tab content.

Copy with Template: Wraps the entire codebase in your selected Prompt Template and copies to clipboard.

Save...: Exports the result to a .txt file.

⚙️ Configuration
Ignore Patterns & Whitelist

NetIngest uses Glob patterns.

*.png - Ignores all PNG files.

test/ - Ignores the test directory.

src/**/*.cs - Matches C# files in src.

Prompt Templates

Templates are stored in templates.json in the application directory. You can create new ones via the UI (Templates Tab).
Format placeholders:

{SOURCE_CODE}: Will be replaced by the Summary + Directory Tree + File Contents.

🤝 Contributing

Contributions are welcome! Please follow these steps:

Fork the project.

Create your feature branch (git checkout -b feature/AmazingFeature).

Commit your changes (git commit -m 'Add some AmazingFeature').

Push to the branch (git push origin feature/AmazingFeature).

Open a Pull Request.

📄 License

Distributed under the MIT License. See LICENSE for more information.

🙏 Acknowledgments

Built with ❤️ in C# / WPF.

Uses Glob for file pattern matching.

Author: Tuan Nguyen
Repository: github.com/yourusername/net-ingest
