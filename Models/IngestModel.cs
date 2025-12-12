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
                "*.zip",
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

        // --- MỚI: Lưu nội dung file thô tại đây ---
        public string Content { get; set; } = string.Empty;

        // --- MỚI: Checkbox state ---
        private bool _isChecked = true;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetProperty(ref _isChecked, value))
                {
                    // Nếu là thư mục, tự động check/uncheck tất cả con cái
                    if (IsDirectory && Children != null)
                    {
                        foreach (var child in Children)
                        {
                            child.IsChecked = value;
                        }
                    }
                }
            }
        }

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

        // --- MỚI: Đếm số lượng file (dùng cho thống kê thư mục) ---
        public int FileCount { get; set; } = 0;

        public ObservableCollection<FileTreeNode> Children { get; set; } = new();

        public string Icon => IsDirectory ? "📁" : "📄";
        public string TokenDisplay =>
            TokenCount > 1000 ? $"{TokenCount / 1000.0:F1}k tok" : $"{TokenCount} tok";
    }

    public class IngestResult
    {
        public bool IsSuccess { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;

        // Các trường này sẽ được tính toán động (dynamic) sau này
        public string Summary { get; set; } = string.Empty;
        public string TreeStructureText { get; set; } = string.Empty;
        public string FileContents { get; set; } = string.Empty;

        public ObservableCollection<FileTreeNode> RootNodes { get; set; } = new();
        public int FileCount { get; set; }
        public long TotalTokensEstimated { get; set; }
    }

    // File mới cho AppSettings đã tạo ở Giai đoạn 1 (giữ nguyên hoặc gộp vào đây nếu muốn)
    // Nhưng vì file này là IngestModel.cs, ta để các class logic ở đây.
    // Class AppSettings nằm ở file riêng AppSettings.cs là tốt nhất.
}
