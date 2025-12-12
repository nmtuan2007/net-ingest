using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using GlobExpressions;
using NetIngest.Models;

namespace NetIngest.Services
{
    public class IngestService
    {
        // Xóa biến const SeparatorLine ở đây vì việc format text sẽ chuyển về ViewModel/Helper

        public async Task<IngestResult> IngestAsync(
            IngestOptions options,
            CancellationToken token,
            IProgress<string>? progress = null
        )
        {
            var result = new IngestResult();
            // Không cần StringBuilder content ở đây nữa

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

                await Task.Run(
                    async () =>
                    {
                        // Hàm đệ quy giờ đây chỉ tập trung xây dựng Tree và điền dữ liệu vào Node
                        await ProcessDirectoryAsync(
                            rootDirInfo,
                            rootNode,
                            options,
                            result,
                            progress,
                            token
                        );

                        result.RootNodes = new ObservableCollection<FileTreeNode> { rootNode };

                        // Các thông số tổng hợp ban đầu (khi tất cả đều Checked)
                        result.FileCount = rootNode.FileCount;
                        result.TotalTokensEstimated = rootNode.TokenCount;

                        // Lưu ý: Summary và FileContents string sẽ được tạo bởi ViewModel sau này
                        result.IsSuccess = true;
                    },
                    token
                );
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Operation cancelled by user.";
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Error: {ex.Message}";
            }

            return result;
        }

        private async Task ProcessDirectoryAsync(
            DirectoryInfo currentDir,
            FileTreeNode currentNode,
            IngestOptions options,
            IngestResult result,
            IProgress<string>? progress,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();

            long currentDirTokens = 0;
            int currentDirFileCount = 0;

            try
            {
                var allItems = currentDir.GetFileSystemInfos();

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

                // Dùng List tạm để tránh add từng item vào ObservableCollection gây chậm UI (dù đang ở background nhưng chuẩn bị cho tương lai)
                var childNodes = new List<FileTreeNode>();

                foreach (var item in itemsToProcess)
                {
                    token.ThrowIfCancellationRequested();

                    var childNode = new FileTreeNode
                    {
                        Name = item.Name,
                        FullPath = item.FullName,
                        RelativePath = Path.GetRelativePath(options.RootPath, item.FullName)
                            .Replace("\\", "/"),
                        IsDirectory = (item is DirectoryInfo),
                        IsChecked = true, // Mặc định chọn
                    };

                    if (item is DirectoryInfo subDir)
                    {
                        childNode.Name += "/";
                        await ProcessDirectoryAsync(
                            subDir,
                            childNode,
                            options,
                            result,
                            progress,
                            token
                        );
                        // Cộng dồn token từ con lên cha
                        currentDirTokens += childNode.TokenCount;
                        currentDirFileCount += childNode.FileCount;
                    }
                    else if (item is FileInfo file)
                    {
                        long fileTokens = await ProcessFileAsync(file, childNode, options, token);
                        // Chỉ đếm nếu file hợp lệ (có nội dung hoặc token > 0, hoặc tùy logic)
                        // Ở đây ta đếm tất cả file được add vào tree
                        currentDirTokens += fileTokens;
                        currentDirFileCount++;

                        // Report tiến độ dựa trên số file toàn cục (đếm sơ bộ)
                        // Vì ta không còn biến result.FileCount toàn cục chính xác ngay lúc chạy
                        // nên có thể dùng biến tạm hoặc bỏ qua report chi tiết số lượng.
                        // Để đơn giản, ta report tên file đang xử lý
                        // progress?.Report($"Processing: {file.Name}...");
                    }

                    childNodes.Add(childNode);
                }

                // Add một lần vào Children của node cha
                foreach (var node in childNodes)
                    currentNode.Children.Add(node);
            }
            catch (UnauthorizedAccessException) { }

            currentNode.TokenCount = currentDirTokens;
            currentNode.FileCount = currentDirFileCount;
        }

        private async Task<long> ProcessFileAsync(
            FileInfo file,
            FileTreeNode node,
            IngestOptions options,
            CancellationToken token
        )
        {
            long tokenCount = 0;

            if (file.Length <= options.MaxFileSize && !IsBinaryFile(file.FullName))
            {
                try
                {
                    string content = await File.ReadAllTextAsync(file.FullName, token);

                    // LƯU Ý: Lưu nội dung vào Node
                    node.Content = content;

                    tokenCount = content.Length / 4;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch { }
            }
            // Gán token cho node
            node.TokenCount = tokenCount;
            // FileCount của node lá luôn là 1 (hoặc 0 nếu lỗi, nhưng cứ để 1 cho file hiện hữu)
            node.FileCount = 1;

            return tokenCount;
        }

        // --- Các hàm Helper (ShouldIgnore, ShouldForceInclude, IsBinaryFile) GIỮ NGUYÊN ---
        private bool ShouldIgnore(FileSystemInfo item, string rootPath, List<string> patterns)
        {
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
    }
}
