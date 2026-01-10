using System.Collections.Generic;

namespace NetIngest.Models
{
    public class AppSettings
    {
        public string LastSourcePath { get; set; } = "";
        public double MaxFileSizeKb { get; set; } = 100;
        public bool LimitFiles { get; set; } = false;
        public string MaxFilesStr { get; set; } = "5";
        public string Whitelist { get; set; } = "Models, DTOs";
        public string IgnorePatterns { get; set; } = "docs/, *.svg, test/";
        public bool IncludeGitIgnored { get; set; } = true;

        // --- NEW: Target Files Feature ---
        public bool UseTargetFiles { get; set; } = false;
        public string TargetFilePatterns { get; set; } = "Program.cs, Startup.cs, *.config";
    }
}
