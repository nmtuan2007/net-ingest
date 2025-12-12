using System.Collections.ObjectModel;
using NetIngest.Core;

namespace NetIngest.Models
{
    public class IngestOptions
    {
        public string RootPath { get; set; } = string.Empty;
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024;
        public bool IncludeGitIgnored { get; set; } = false;
        public int? MaxFilesPerDirectory { get; set; } = null;
        public List<string> ForceFullIngestPatterns { get; set; } = new();
        public List<string> IgnorePatterns { get; set; } =
            new()
            {
                ".git",
                ".vs",
                ".vscode",
                ".idea",
                ".DS_Store",
                "bin",
                "obj",
                "__pycache__",
                "node_modules",
                "dist",
                "build",
                "coverage",
                "*.exe",
                "*.dll",
                "*.pdb",
                "*.png",
                "*.jpg",
                "*.zip", // ... (list rút gọn cho ngắn)
            };
    }

    public class PromptTemplate : ObservableObject
    {
        private string _name = "Default";
        private string _content = "{SOURCE_CODE}";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public override string ToString() => Name;
    }

    public class FileTreeNode : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }

        private long _tokenCount;
        public long TokenCount
        {
            get => _tokenCount;
            set
            {
                if (SetProperty(ref _tokenCount, value))
                    OnPropertyChanged(nameof(TokenDisplay));
            }
        }

        public ObservableCollection<FileTreeNode> Children { get; set; } = new();

        public string Icon => IsDirectory ? "📁" : "📄";
        public string TokenDisplay =>
            TokenCount > 1000 ? $"{TokenCount / 1000.0:F1}k tok" : $"{TokenCount} tok";
    }

    // IngestResult giữ nguyên
    public class IngestResult
    {
        public bool IsSuccess { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string TreeStructureText { get; set; } = string.Empty;
        public string FileContents { get; set; } = string.Empty;
        public ObservableCollection<FileTreeNode> RootNodes { get; set; } = new();
        public int FileCount { get; set; }
        public long TotalTokensEstimated { get; set; }
    }
}
