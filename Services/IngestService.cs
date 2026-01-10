using System.Collections.ObjectModel;
using System.IO;
using GlobExpressions;
using NetIngest.Models;

namespace NetIngest.Services
{
    public class IngestService
    {
        public async Task<IngestResult> IngestAsync(
            IngestOptions options,
            CancellationToken token,
            IProgress<string>? progress = null
        )
        {
            var result = new IngestResult();

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
                        await ProcessDirectoryAsync(
                            rootDirInfo,
                            rootNode,
                            options,
                            result,
                            progress,
                            token
                        );

                        result.RootNodes = new ObservableCollection<FileTreeNode> { rootNode };

                        // Initial stats
                        result.FileCount = rootNode.FileCount;
                        result.TotalTokensEstimated = rootNode.TokenCount;

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
                
                // Separate Dirs and Files to apply different filtering logic
                var rawDirs = allItems.OfType<DirectoryInfo>();
                var rawFiles = allItems.OfType<FileInfo>();

                // 1. Filter Directories
                // We always respect IgnorePatterns for directories to avoid traversing junk (like .git, node_modules)
                // UNLESS the directory is explicitly whitelisted.
                var directories = rawDirs
                    .Where(d => 
                        ShouldForceInclude(d, options.RootPath, options.ForceFullIngestPatterns) ||
                        !ShouldIgnore(d, options.RootPath, options.IgnorePatterns))
                    .OrderBy(d => d.Name)
                    .ToList();

                // 2. Filter Files
                List<FileInfo> files;
                
                // CHECK: Is Target Mode active?
                if (options.TargetFilePatterns != null && options.TargetFilePatterns.Any())
                {
                    // PRIORITY MODE: Only include files matching Target Patterns.
                    // effectively ignoring "IgnorePatterns" for files.
                    files = rawFiles
                        .Where(f => MatchesPatternList(f, options.RootPath, options.TargetFilePatterns))
                        .OrderBy(f => f.Name)
                        .ToList();
                }
                else
                {
                    // NORMAL MODE: Exclude files matching IgnorePatterns
                    files = rawFiles
                        .Where(f => !ShouldIgnore(f, options.RootPath, options.IgnorePatterns))
                        .OrderBy(f => f.Name)
                        .ToList();
                }

                // 3. Apply Max Files Limit (Sampling)
                // (Note: Typically sampling applies to the final valid list of files in a folder)
                if (options.MaxFilesPerDirectory.HasValue && options.MaxFilesPerDirectory > 0)
                {
                    // If whitelist overrides limit, check it. 
                    // Note: Target Files mode usually implies we want ALL found target files, 
                    // but to stay consistent with UI ("Limit files per directory" checkbox), 
                    // we apply the limit if the user checked it.
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

                // Combine for processing order
                var itemsToProcess = directories
                    .Cast<FileSystemInfo>()
                    .Concat(files)
                    .OrderBy(i => i.Name)
                    .ToList();

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
                        IsChecked = true,
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
                        // Sum up stats
                        currentDirTokens += childNode.TokenCount;
                        currentDirFileCount += childNode.FileCount;
                    }
                    else if (item is FileInfo file)
                    {
                        long fileTokens = await ProcessFileAsync(file, childNode, options, token);
                        currentDirTokens += fileTokens;
                        currentDirFileCount++;
                    }

                    childNodes.Add(childNode);
                }

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

            // In Target Mode, we might still want to respect MaxFileSize for performance,
            // but let's assume if user targets a file, they want it. 
            // However, sticking to global MaxFileSize is safer for the app.
            if (file.Length <= options.MaxFileSize && !IsBinaryFile(file.FullName))
            {
                try
                {
                    string content = await File.ReadAllTextAsync(file.FullName, token);
                    node.Content = content;
                    tokenCount = content.Length / 4;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch { }
            }
            node.TokenCount = tokenCount;
            node.FileCount = 1;

            return tokenCount;
        }

        // --- Helpers ---

        private bool MatchesPatternList(FileSystemInfo item, string rootPath, List<string> patterns)
        {
             if (patterns == null || patterns.Count == 0)
                return false;

            string relativePath = Path.GetRelativePath(rootPath, item.FullName).Replace("\\", "/");
            string relativePathWithSlash = relativePath + (item is DirectoryInfo ? "/" : "");

            foreach (var pattern in patterns)
            {
                string clean = pattern.Trim();
                if (string.IsNullOrEmpty(clean))
                    continue;

                // Name match
                if (string.Equals(item.Name, clean.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // Glob match
                if (Glob.IsMatch(relativePath, clean))
                    return true;

                // Directory Glob match
                if (item is DirectoryInfo && Glob.IsMatch(relativePathWithSlash, clean))
                    return true;
            }
            return false;
        }

        private bool ShouldIgnore(FileSystemInfo item, string rootPath, List<string> patterns)
        {
            // Re-using MatchesPatternList logic but conceptually distinct
            return MatchesPatternList(item, rootPath, patterns);
        }

        private bool ShouldForceInclude(DirectoryInfo dir, string rootPath, List<string> patterns)
        {
            return MatchesPatternList(dir, rootPath, patterns);
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
