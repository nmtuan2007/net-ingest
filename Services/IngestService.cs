using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using GlobExpressions;
using NetIngest.Models;

namespace NetIngest.Services
{
    public class IngestService
    {
        private const string SeparatorLine = "================================================";

        public async Task<IngestResult> IngestAsync(
            IngestOptions options,
            IProgress<string>? progress = null
        )
        {
            var result = new IngestResult();
            var sbTreeText = new StringBuilder();
            var sbContent = new StringBuilder();

            try
            {
                if (!Directory.Exists(options.RootPath))
                    throw new DirectoryNotFoundException(
                        $"Directory not found: {options.RootPath}"
                    );

                var rootDirInfo = new DirectoryInfo(options.RootPath);
                var rootNode = new FileTreeNode
                {
                    Name = rootDirInfo.Name + "/",
                    FullPath = rootDirInfo.FullName,
                    RelativePath = "",
                    IsDirectory = true,
                };

                progress?.Report("Scanning directory structure...");

                // Chạy trên Background thread thực sự
                await Task.Run(async () =>
                {
                    long totalTokens = await ProcessDirectoryAsync(
                        rootDirInfo,
                        rootNode,
                        "",
                        true,
                        options,
                        sbTreeText,
                        sbContent,
                        result,
                        progress
                    );

                    sbTreeText.Insert(0, "Directory structure:\n");
                    result.TreeStructureText = sbTreeText.ToString();
                    result.FileContents = sbContent.ToString();
                    result.RootNodes = new ObservableCollection<FileTreeNode> { rootNode };
                    result.TotalTokensEstimated = totalTokens;
                    result.Summary = GenerateSummary(
                        rootDirInfo.Name,
                        result.FileCount,
                        result.FileContents.Length,
                        totalTokens
                    );
                });
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Error: {ex.Message}";
            }

            return result;
        }

        private async Task<long> ProcessDirectoryAsync(
            DirectoryInfo currentDir,
            FileTreeNode currentNode,
            string indent,
            bool isLast,
            IngestOptions options,
            StringBuilder sbTreeText,
            StringBuilder sbContent,
            IngestResult result,
            IProgress<string>? progress
        )
        {
            long currentDirTokens = 0;
            sbTreeText.AppendLine($"{indent}{(isLast ? "└── " : "├── ")}{currentDir.Name}/");
            string childIndent = indent + (isLast ? "    " : "│   ");

            try
            {
                var allItems = currentDir.GetFileSystemInfos();

                // Filter items
                var filteredItems = allItems
                    .Where(item => !ShouldIgnore(item, options.RootPath, options.IgnorePatterns))
                    .OrderBy(item => item.Name)
                    .ToList();

                var directories = filteredItems.OfType<DirectoryInfo>().ToList();
                var files = filteredItems.OfType<FileInfo>().ToList();

                // Apply Limits
                if (options.MaxFilesPerDirectory.HasValue && options.MaxFilesPerDirectory > 0)
                {
                    if (
                        !ShouldForceInclude(
                            currentDir,
                            options.RootPath,
                            options.ForceFullIngestPatterns
                        )
                    )
                    {
                        files = files.Take(options.MaxFilesPerDirectory.Value).ToList();
                    }
                }

                var itemsToProcess = directories
                    .Cast<FileSystemInfo>()
                    .Concat(files)
                    .OrderBy(i => i.Name)
                    .ToList();

                for (int i = 0; i < itemsToProcess.Count; i++)
                {
                    var item = itemsToProcess[i];
                    bool isItemLast = (i == itemsToProcess.Count - 1);

                    var childNode = new FileTreeNode
                    {
                        Name = item.Name,
                        FullPath = item.FullName,
                        RelativePath = Path.GetRelativePath(options.RootPath, item.FullName)
                            .Replace("\\", "/"),
                        IsDirectory = (item is DirectoryInfo),
                    };

                    // UI Update cần thread safe (thêm vào collection sau khi xử lý xong hoặc dispatch về UI -
                    // Ở đây ta add vào Children trước vì ObservableCollection này chưa gắn vào UI lúc đang chạy background)
                    currentNode.Children.Add(childNode);

                    if (item is DirectoryInfo subDir)
                    {
                        childNode.Name += "/";
                        long subTokens = await ProcessDirectoryAsync(
                            subDir,
                            childNode,
                            childIndent,
                            isItemLast,
                            options,
                            sbTreeText,
                            sbContent,
                            result,
                            progress
                        );
                        childNode.TokenCount = subTokens;
                        currentDirTokens += subTokens;
                    }
                    else if (item is FileInfo file)
                    {
                        long fileTokens = await ProcessFileAsync(
                            file,
                            childNode,
                            childIndent,
                            isItemLast,
                            options,
                            sbTreeText,
                            sbContent,
                            result
                        );
                        childNode.TokenCount = fileTokens;
                        currentDirTokens += fileTokens;

                        if (result.FileCount % 10 == 0) // Report mỗi 10 file để đỡ lag UI
                            progress?.Report($"Processed {result.FileCount} files...");
                    }
                }
            }
            catch (UnauthorizedAccessException) { }

            currentNode.TokenCount = currentDirTokens;
            return currentDirTokens;
        }

        private async Task<long> ProcessFileAsync(
            FileInfo file,
            FileTreeNode node,
            string indent,
            bool isLast,
            IngestOptions options,
            StringBuilder sbTreeText,
            StringBuilder sbContent,
            IngestResult result
        )
        {
            sbTreeText.AppendLine($"{indent}{(isLast ? "└── " : "├── ")}{file.Name}");
            long tokenCount = 0;

            if (file.Length <= options.MaxFileSize && !IsBinaryFile(file.FullName))
            {
                try
                {
                    // Dùng ReadAllTextAsync để không block thread
                    string content = await File.ReadAllTextAsync(file.FullName);

                    sbContent.AppendLine(SeparatorLine);
                    sbContent.AppendLine($"FILE: {node.RelativePath}");
                    sbContent.AppendLine(SeparatorLine);
                    sbContent.AppendLine(content);
                    sbContent.AppendLine();

                    result.FileCount++;
                    tokenCount = content.Length / 4;
                }
                catch { }
            }
            return tokenCount;
        }

        // --- Các hàm Helper giữ nguyên logic ---
        private bool ShouldIgnore(FileSystemInfo item, string rootPath, List<string> patterns)
        {
            // Logic giữ nguyên như bản cũ
            string relativePath = Path.GetRelativePath(rootPath, item.FullName).Replace("\\", "/");
            string relativePathWithSlash = relativePath + (item is DirectoryInfo ? "/" : "");

            foreach (var pattern in patterns)
            {
                string clean = pattern.Trim();
                if (string.IsNullOrEmpty(clean))
                    continue;
                if (
                    string.Equals(item.Name, clean.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                )
                    return true;
                if (Glob.IsMatch(relativePath, clean))
                    return true;
                if (item is DirectoryInfo && Glob.IsMatch(relativePathWithSlash, clean))
                    return true;
                if (
                    !clean.Contains("/")
                    && string.Equals(item.Name, clean, StringComparison.OrdinalIgnoreCase)
                )
                    return true;
            }
            return false;
        }

        private bool ShouldForceInclude(DirectoryInfo dir, string rootPath, List<string> patterns)
        {
            // Logic giữ nguyên như bản cũ
            if (patterns == null || patterns.Count == 0)
                return false;
            string relativePath = Path.GetRelativePath(rootPath, dir.FullName).Replace("\\", "/");
            foreach (var pattern in patterns)
            {
                string clean = pattern.Trim();
                if (string.IsNullOrEmpty(clean))
                    continue;
                if (string.Equals(dir.Name, clean, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (Glob.IsMatch(relativePath, clean))
                    return true;
            }
            return false;
        }

        private bool IsBinaryFile(string filePath)
        {
            // Logic giữ nguyên như bản cũ
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                int length = (int)Math.Min(8192, stream.Length);
                var buffer = new byte[length];
                stream.Read(buffer, 0, length);
                return buffer.Any(b => b == 0);
            }
            catch
            {
                return true;
            }
        }

        private string GenerateSummary(string repoName, int files, long chars, long tokens) =>
            $"Directory: {repoName}\nFiles analyzed: {files}\nTotal characters: {chars:N0}\nEstimated tokens: {tokens:N0}";
    }
}
