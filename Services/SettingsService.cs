using System;
using System.IO;
using System.Text.Json;
using NetIngest.Models;

namespace NetIngest.Services
{
    public class SettingsService
    {
        private const string FileName = "settings.json";

        public AppSettings LoadSettings()
        {
            if (!File.Exists(FileName))
            {
                return new AppSettings(); // Trả về mặc định nếu chưa có file
            }

            try
            {
                string json = File.ReadAllText(FileName);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch
            {
                // Nếu lỗi format JSON, trả về mặc định
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(FileName, json);
            }
            catch (Exception)
            {
                // Có thể log lỗi nếu cần, tạm thời bỏ qua để không crash app
            }
        }
    }
}
