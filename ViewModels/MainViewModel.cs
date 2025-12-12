using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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

        // --- Properties cho Binding ---
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
        public bool IncludeGitIgnored { get; set; } = true;

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

        // Editing Template properties
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

        // Result Data
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
        } // Dùng để hiển thị nội dung

        // State
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMsg = "Ready";
        public string StatusMsg
        {
            get => _statusMsg;
            set => SetProperty(ref _statusMsg, value);
        }

        // Stats
        public string TokenCountDisplay =>
            _lastResult.TotalTokensEstimated > 1000000
                ? $"{_lastResult.TotalTokensEstimated / 1000000.0:F1}M"
                : (
                    _lastResult.TotalTokensEstimated > 1000
                        ? $"{_lastResult.TotalTokensEstimated / 1000.0:F1}k"
                        : _lastResult.TotalTokensEstimated.ToString()
                );

        public int FileCountDisplay => _lastResult.FileCount;

        // View Mode (Radio Buttons)
        private string _viewMode = "Summary"; // Summary, Tree, Content
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

        // Context Menu Commands
        public ICommand AddIgnoreCommand { get; }
        public ICommand AddWhitelistCommand { get; }
        public ICommand CopyPathCommand { get; }

        public MainViewModel()
        {
            _ingestService = new IngestService();
            _promptService = new PromptService();
            var tpls = _promptService.LoadTemplates();
            Templates = new ObservableCollection<PromptTemplate>(tpls);
            SelectedTemplate = Templates.FirstOrDefault();

            // Init Commands
            BrowseCommand = new RelayCommand(_ => BrowseFolder());
            AnalyzeCommand = new RelayCommand(async _ => await AnalyzeAsync(), _ => !IsBusy);
            CopyViewCommand = new RelayCommand(_ => CopyView());
            CopyTemplateCommand = new RelayCommand(_ => CopyWithTemplate());
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

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.FolderName;
                StatusMsg = "Directory selected.";
            }
        }

        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || SourcePath == "Select folder...")
            {
                MessageBox.Show("Please select a directory first.");
                return;
            }

            IsBusy = true;
            StatusMsg = "Initializing...";

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

            _lastResult = await _ingestService.IngestAsync(options, progress);

            if (_lastResult.IsSuccess)
            {
                TreeRoots = _lastResult.RootNodes;
                OnPropertyChanged(nameof(TokenCountDisplay));
                OnPropertyChanged(nameof(FileCountDisplay));
                UpdateResultView();
                StatusMsg = "Analysis Complete!";
            }
            else
            {
                StatusMsg = "Error occurred.";
                MessageBox.Show(_lastResult.ErrorMessage);
            }

            IsBusy = false;
        }

        private void UpdateResultView()
        {
            if (ViewMode == "Tree")
            {
                // UI tự handle Visibility dựa trên binding
            }
            else if (ViewMode == "Content")
            {
                ResultText = _lastResult.FileContents;
            }
            else
            {
                ResultText = _lastResult.Summary;
            }
        }

        private void CopyView()
        {
            string content = ViewMode == "Tree" ? _lastResult.TreeStructureText : ResultText;
            if (!string.IsNullOrEmpty(content))
            {
                Clipboard.SetText(content);
                StatusMsg = "Copied to clipboard.";
            }
        }

        private void CopyWithTemplate()
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
            {
                existing.Content = EditingTemplateContent;
            }
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

        // Helpers
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
            }
        }
    }
}
