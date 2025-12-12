using System.Collections.Generic;
using System.IO;
using System.Text.Json; // Requires System.Text.Json
using NetIngest.Models;

namespace NetIngest.Services
{
    public class PromptService
    {
        private const string FileName = "templates.json";

        public List<PromptTemplate> LoadTemplates()
        {
            if (!File.Exists(FileName))
            {
                // Return default templates if file doesn't exist
                return new List<PromptTemplate>
                {
                    new PromptTemplate { Name = "Raw Code", Content = "{SOURCE_CODE}" },
                    new PromptTemplate 
                    { 
                        Name = "Explain Code", 
                        Content = "Here is the source code of a project:\n\n====================================\n{SOURCE_CODE}\n====================================\n\nPlease explain the architecture and key flows of this application." 
                    },
                    new PromptTemplate 
                    { 
                        Name = "Refactor Request", 
                        Content = "I need to refactor the following code:\n\n{SOURCE_CODE}\n\nPlease identify code smells and suggest improvements." 
                    }
                };
            }

            try
            {
                string json = File.ReadAllText(FileName);
                var templates = JsonSerializer.Deserialize<List<PromptTemplate>>(json);
                return templates ?? new List<PromptTemplate>();
            }
            catch
            {
                return new List<PromptTemplate> { new PromptTemplate { Name = "Raw Code", Content = "{SOURCE_CODE}" } };
            }
        }

        public void SaveTemplates(List<PromptTemplate> templates)
        {
            try
            {
                string json = JsonSerializer.Serialize(templates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FileName, json);
            }
            catch
            {
                // Handle or log error
            }
        }
    }
}