using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using NetIngest.Core;
using NetIngest.Models;
using NetIngest.Services;

namespace NetIngest.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IngestService _ingestService;
        private readonly PromptService _promptService;
        private readonly SettingsService _settingsService;
        private CancellationTokenSource? _cts;
        private readonly DispatcherTimer _debounceTimer;

        // --- Properties ---
        private string _sourcePath = "Select folder...";
        public string SourcePath
        {
            get => _sourcePath;
            set => SetProperty(ref _sourcePath, value);
        }

        private double _maxSizeKb = 100;
        public double MaxSizeKb
        {
            get => _maxSizeKb;
            set
            {
                if (SetProperty(ref _maxSizeKb, value))
                    OnPropertyChanged(nameof(SizeLabel));
            }
        }
        public string SizeLabel =>
            MaxSizeKb >= 1024 ? $"{MaxSizeKb / 1024.0:F2} MB" : $"{MaxSizeKb:F0} KB";

        private bool _limitFiles;
        public bool LimitFiles
        {
            get => _limitFiles;
            set => SetProperty(ref _limitFiles, value);
        }

        private string _maxFilesStr = "2";
        public string MaxFilesStr
        {
            get => _maxFilesStr;
            set => SetProperty(ref _maxFilesStr, value);
        }

        private string _whitelist = "Models, DTOs";
        public string Whitelist
        {
            get => _whitelist;
            set => SetProperty(ref _whitelist, value);
        }

        private string _ignorePatterns = "docs/, *.svg, test/";
        public string IgnorePatterns
        {
            get => _ignorePatterns;
            set => SetProperty(ref _ignorePatterns, value);
        }

        private bool _includeGitIgnored = true;
        public bool IncludeGitIgnored
        {
            get => _includeGitIgnored;
            set => SetProperty(ref _includeGitIgnored, value);
        }

        private ObservableCollection<PromptTemplate> _templates;
        public ObservableCollection<PromptTemplate> Templates
        {
            get => _templates;
            set => SetProperty(ref _templates, value);
        }

        private PromptTemplate? _selectedTemplate;
        public PromptTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value) && value != null)
                {
                    EditingTemplateName = value.Name;
                    EditingTemplateContent = value.Content;
                }
            }
        }

        private string _editingTemplateName = "";
        public string EditingTemplateName
        {
            get => _editingTemplateName;
            set => SetProperty(ref _editingTemplateName, value);
        }

        private string _editingTemplateContent = "";
        public string EditingTemplateContent
        {
            get => _editingTemplateContent;
            set => SetProperty(ref _editingTemplateContent, value);
        }

        private IngestResult _lastResult = new();
        private ObservableCollection<FileTreeNode> _treeRoots = new();
        public ObservableCollection<FileTreeNode> TreeRoots
        {
            get => _treeRoots;
            set => SetProperty(ref _treeRoots, value);
        }

        private string _resultText = "";
        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(AnalyzeButtonText));
                    OnPropertyChanged(nameof(AnalyzeButtonColor));
                }
            }
        }

        public string AnalyzeButtonText => IsBusy ? "STOP ANALYSIS" : "ANALYZE CODEBASE";
        public string AnalyzeButtonColor => IsBusy ? "#EF4444" : "#10B981";

        private string _statusMsg = "Ready";
        public string StatusMsg
        {
            get => _statusMsg;
            set => SetProperty(ref _statusMsg, value);
        }

        // Button Visual States
        private string _copyViewBtnText = "Copy View";
        public string CopyViewBtnText
        {
            get => _copyViewBtnText;
            set => SetProperty(ref _copyViewBtnText, value);
        }

        private string _copyViewBtnColor = "#64748B";
        public string CopyViewBtnColor
        {
            get => _copyViewBtnColor;
            set => SetProperty(ref _copyViewBtnColor, value);
        }

        private string _copyTplBtnText = "COPY WITH TEMPLATE";
        public string CopyTplBtnText
        {
            get => _copyTplBtnText;
            set => SetProperty(ref _copyTplBtnText, value);
        }

        private string _copyTplBtnColor = "#F59E0B";
        public string CopyTplBtnColor
        {
            get => _copyTplBtnColor;
            set => SetProperty(ref _copyTplBtnColor, value);
        }

        public string TokenCountDisplay =>
            _lastResult.TotalTokensEstimated > 1000000
                ? $"{_lastResult.TotalTokensEstimated / 1000000.0:F1}M"
                : (
                    _lastResult.TotalTokensEstimated > 1000
                        ? $"{_lastResult.TotalTokensEstimated / 1000.0:F1}k"
                        : _lastResult.TotalTokensEstimated.ToString()
                );

        public int FileCountDisplay => _lastResult.FileCount;

        private string _viewMode = "Summary";
        public string ViewMode
        {
            get => _viewMode;
            set
            {
                if (SetProperty(ref _viewMode, value))
                    UpdateResultView();
            }
        }

        // --- Commands ---
        public ICommand BrowseCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand CopyViewCommand { get; }
        public ICommand CopyTemplateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand NewTemplateCommand { get; }
        public ICommand SaveTemplateCommand { get; }
        public ICommand AddIgnoreCommand { get; }
        public ICommand AddWhitelistCommand { get; }
        public ICommand CopyPathCommand { get; }

        public MainViewModel()
        {
            _ingestService = new IngestService();
            _promptService = new PromptService();
            _settingsService = new SettingsService();

            var tpls = _promptService.LoadTemplates();
            Templates = new ObservableCollection<PromptTemplate>(tpls);
            SelectedTemplate = Templates.FirstOrDefault();

            LoadSettings();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                RecalculateOutput();
            };

            BrowseCommand = new RelayCommand(_ => BrowseFolder());
            AnalyzeCommand = new RelayCommand(async _ => await OnAnalyzeClicked());
            CopyViewCommand = new RelayCommand(async _ => await CopyViewAsync());
            CopyTemplateCommand = new RelayCommand(async _ => await CopyWithTemplateAsync());
            SaveCommand = new RelayCommand(_ => SaveToFile());

            NewTemplateCommand = new RelayCommand(_ =>
            {
                SelectedTemplate = null;
                EditingTemplateName = "New Template";
                EditingTemplateContent = "{SOURCE_CODE}";
            });

            SaveTemplateCommand = new RelayCommand(_ => SaveCurrentTemplate());

            AddIgnoreCommand = new RelayCommand(p =>
            {
                if (p is string path)
                    AppendConfig(ref _ignorePatterns, path, nameof(IgnorePatterns));
            });

            AddWhitelistCommand = new RelayCommand(p =>
            {
                if (p is string name)
                    AppendConfig(ref _whitelist, name.TrimEnd('/'), nameof(Whitelist));
            });

            CopyPathCommand = new RelayCommand(p =>
            {
                if (p is string path)
                {
                    Clipboard.SetText(path);
                    StatusMsg = $"Copied path: {path}";
                }
            });
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            if (!string.IsNullOrEmpty(settings.LastSourcePath))
                SourcePath = settings.LastSourcePath;
            MaxSizeKb = settings.MaxFileSizeKb;
            LimitFiles = settings.LimitFiles;
            MaxFilesStr = settings.MaxFilesStr;
            Whitelist = settings.Whitelist;
            IgnorePatterns = settings.IgnorePatterns;
            IncludeGitIgnored = settings.IncludeGitIgnored;
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                LastSourcePath = SourcePath,
                MaxFileSizeKb = MaxSizeKb,
                LimitFiles = LimitFiles,
                MaxFilesStr = MaxFilesStr,
                Whitelist = Whitelist,
                IgnorePatterns = IgnorePatterns,
                IncludeGitIgnored = IncludeGitIgnored,
            };
            _settingsService.SaveSettings(settings);
        }

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.FolderName;
                StatusMsg = "Directory selected.";
            }
        }

        private async Task OnAnalyzeClicked()
        {
            if (IsBusy)
            {
                _cts?.Cancel();
                StatusMsg = "Cancelling...";
                return;
            }
            await AnalyzeAsync();
        }

        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || SourcePath == "Select folder...")
            {
                MessageBox.Show("Please select a directory first.");
                return;
            }

            SaveSettings();
            IsBusy = true;
            StatusMsg = "Initializing...";
            _cts = new CancellationTokenSource();

            try
            {
                int? maxFiles = LimitFiles && int.TryParse(MaxFilesStr, out int val) ? val : null;
                long maxBytes = (long)(MaxSizeKb * 1024);

                var options = new IngestOptions
                {
                    RootPath = SourcePath,
                    MaxFileSize = maxBytes,
                    IncludeGitIgnored = IncludeGitIgnored,
                    MaxFilesPerDirectory = maxFiles,
                };

                AddPatternsToList(options.IgnorePatterns, IgnorePatterns);
                AddPatternsToList(options.ForceFullIngestPatterns, Whitelist);

                var progress = new Progress<string>(msg => StatusMsg = msg);

                _lastResult = await _ingestService.IngestAsync(options, _cts.Token, progress);

                if (_lastResult.IsSuccess)
                {
                    TreeRoots = _lastResult.RootNodes;
                    SubscribeToNodeEvents(TreeRoots);
                    RecalculateOutput();
                    StatusMsg = "Analysis Complete!";
                }
                else
                {
                    StatusMsg = _lastResult.ErrorMessage;
                }
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                IsBusy = false;
            }
        }

        private void RecalculateOutput()
        {
            if (_lastResult == null || TreeRoots == null)
                return;

            var sbTree = new StringBuilder();
            var sbContent = new StringBuilder();

            sbTree.AppendLine("Directory structure:");

            int totalFiles = 0;
            long totalTokens = 0;

            void ProcessNodes(IEnumerable<FileTreeNode> nodes, string indent)
            {
                var nodeList = nodes.ToList();
                for (int i = 0; i < nodeList.Count; i++)
                {
                    var node = nodeList[i];
                    if (!node.IsChecked)
                        continue;

                    sbTree.AppendLine($"{indent}├── {node.Name}");

                    if (node.IsDirectory)
                    {
                        ProcessNodes(node.Children, indent + "│   ");
                    }
                    else
                    {
                        totalFiles++;
                        totalTokens += node.TokenCount;
                        if (!string.IsNullOrEmpty(node.Content))
                        {
                            sbContent.AppendLine(
                                "================================================"
                            );
                            sbContent.AppendLine($"FILE: {node.RelativePath}");
                            sbContent.AppendLine(
                                "================================================"
                            );
                            sbContent.AppendLine(node.Content);
                            sbContent.AppendLine();
                        }
                    }
                }
            }

            if (TreeRoots.Count > 0)
            {
                var root = TreeRoots[0];
                sbTree.AppendLine($"└── {root.Name}");
                ProcessNodes(root.Children, "    ");
            }

            _lastResult.FileCount = totalFiles;
            _lastResult.TotalTokensEstimated = totalTokens;
            _lastResult.TreeStructureText = sbTree.ToString();
            _lastResult.FileContents = sbContent.ToString();
            _lastResult.Summary = GenerateSummary(
                new DirectoryInfo(SourcePath).Name,
                totalFiles,
                sbContent.Length,
                totalTokens
            );

            OnPropertyChanged(nameof(TokenCountDisplay));
            OnPropertyChanged(nameof(FileCountDisplay));
            UpdateResultView();
        }

        private string GenerateSummary(string repoName, int files, long chars, long tokens) =>
            $"Directory: {repoName}\nFiles selected: {files}\nTotal characters: {chars:N0}\nEstimated tokens: {tokens:N0}";

        private void SubscribeToNodeEvents(IEnumerable<FileTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.PropertyChanged += OnNodePropertyChanged;
                if (node.Children.Count > 0)
                    SubscribeToNodeEvents(node.Children);
            }
        }

        private void OnNodePropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e
        )
        {
            if (e.PropertyName == nameof(FileTreeNode.IsChecked))
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void UpdateResultView()
        {
            if (ViewMode == "Tree") { }
            else if (ViewMode == "Content")
                ResultText = _lastResult.FileContents;
            else
                ResultText = _lastResult.Summary;
        }

        private async Task CopyViewAsync()
        {
            string content = ViewMode == "Tree" ? _lastResult.TreeStructureText : ResultText;
            if (!string.IsNullOrEmpty(content))
            {
                Clipboard.SetText(content);
                StatusMsg = "Copied to clipboard.";

                string oldText = CopyViewBtnText;
                string oldColor = CopyViewBtnColor;

                CopyViewBtnText = "COPIED! ✅";
                CopyViewBtnColor = "#10B981";

                await Task.Delay(2000);

                CopyViewBtnText = oldText;
                CopyViewBtnColor = oldColor;
            }
        }

        private async Task CopyWithTemplateAsync()
        {
            if (_lastResult.FileCount == 0)
                return;
            string fullDigest =
                $"{_lastResult.Summary}\n\n{_lastResult.TreeStructureText}\n\n{_lastResult.FileContents}";
            string final =
                SelectedTemplate != null
                    ? SelectedTemplate.Content.Replace("{SOURCE_CODE}", fullDigest)
                    : fullDigest;

            Clipboard.SetText(final);
            StatusMsg = "Copied with template.";

            string oldText = CopyTplBtnText;
            string oldColor = CopyTplBtnColor;

            CopyTplBtnText = "COPIED! ✅";
            CopyTplBtnColor = "#10B981";

            await Task.Delay(2000);

            CopyTplBtnText = oldText;
            CopyTplBtnColor = oldColor;
        }

        private void SaveToFile()
        {
            if (_lastResult.FileCount == 0)
                return;
            var dlg = new SaveFileDialog
            {
                Filter = "Text (*.txt)|*.txt",
                FileName = $"digest_{DateTime.Now:MMdd_HHmm}.txt",
            };
            if (dlg.ShowDialog() == true)
            {
                string fullDigest =
                    $"{_lastResult.Summary}\n\n{_lastResult.TreeStructureText}\n\n{_lastResult.FileContents}";
                string final =
                    SelectedTemplate != null
                        ? SelectedTemplate.Content.Replace("{SOURCE_CODE}", fullDigest)
                        : fullDigest;
                File.WriteAllText(dlg.FileName, final);
                StatusMsg = "File saved.";
            }
        }

        private void SaveCurrentTemplate()
        {
            if (string.IsNullOrWhiteSpace(EditingTemplateName))
                return;
            var existing = Templates.FirstOrDefault(t => t.Name == EditingTemplateName);
            if (existing != null)
                existing.Content = EditingTemplateContent;
            else
            {
                var newTpl = new PromptTemplate
                {
                    Name = EditingTemplateName,
                    Content = EditingTemplateContent,
                };
                Templates.Add(newTpl);
                SelectedTemplate = newTpl;
            }
            _promptService.SaveTemplates(Templates.ToList());
            StatusMsg = $"Template '{EditingTemplateName}' saved.";
        }

        private void AddPatternsToList(List<string> list, string raw)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var items = raw.Split(
                        new[] { ',', '\n', '\r' },
                        StringSplitOptions.RemoveEmptyEntries
                    )
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p));
                list.AddRange(items);
            }
        }

        private void AppendConfig(ref string field, string value, string propName)
        {
            if (!field.Contains(value))
            {
                field = string.IsNullOrEmpty(field) ? value : field + ", " + value;
                OnPropertyChanged(propName);
                SaveSettings();
            }
        }
    }
}
