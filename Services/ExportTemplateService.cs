using GeoLens.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GeoLens.Services
{
    /// <summary>
    /// Service for managing export templates (built-in and user-created)
    /// Stores templates in %LOCALAPPDATA%\GeoLens\export_templates.json
    /// </summary>
    public class ExportTemplateService : IDisposable
    {
        private readonly string _templatesFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private List<ExportTemplate> _templates = new();
        private bool _isInitialized = false;

        public ExportTemplateService()
        {
            // Store templates in %LOCALAPPDATA%\GeoLens\export_templates.json
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "GeoLens");
            Directory.CreateDirectory(appFolder);
            _templatesFilePath = Path.Combine(appFolder, "export_templates.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// Initialize the service and load templates
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                await LoadTemplatesAsync();
                _isInitialized = true;
                Debug.WriteLine($"[ExportTemplateService] Initialized with {_templates.Count} templates");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportTemplateService] Error initializing: {ex.Message}");
                // Initialize with built-in templates only
                _templates = ExportTemplatePresets.GetAllBuiltInTemplates();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Load templates from disk (or create with defaults)
        /// </summary>
        private async Task LoadTemplatesAsync()
        {
            try
            {
                if (File.Exists(_templatesFilePath))
                {
                    var json = await File.ReadAllTextAsync(_templatesFilePath);
                    var userTemplates = JsonSerializer.Deserialize<List<ExportTemplate>>(json, _jsonOptions) ?? new();

                    // Combine built-in templates with user templates
                    _templates = ExportTemplatePresets.GetAllBuiltInTemplates();
                    _templates.AddRange(userTemplates.Where(t => !t.IsBuiltIn));

                    Debug.WriteLine($"[ExportTemplateService] Loaded {userTemplates.Count} user templates from {_templatesFilePath}");
                }
                else
                {
                    // First run - create file with built-in templates
                    _templates = ExportTemplatePresets.GetAllBuiltInTemplates();
                    await SaveTemplatesAsync();
                    Debug.WriteLine($"[ExportTemplateService] Created templates file with {_templates.Count} built-in templates");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportTemplateService] Error loading templates: {ex.Message}");
                _templates = ExportTemplatePresets.GetAllBuiltInTemplates();
            }
        }

        /// <summary>
        /// Save templates to disk (only user templates, not built-in)
        /// </summary>
        private async Task SaveTemplatesAsync()
        {
            try
            {
                // Save only user-created templates (not built-in)
                var userTemplates = _templates.Where(t => !t.IsBuiltIn).ToList();
                var json = JsonSerializer.Serialize(userTemplates, _jsonOptions);
                await File.WriteAllTextAsync(_templatesFilePath, json);

                Debug.WriteLine($"[ExportTemplateService] Saved {userTemplates.Count} user templates to {_templatesFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportTemplateService] Error saving templates: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all templates (built-in + user-created)
        /// </summary>
        public List<ExportTemplate> GetAllTemplates()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            return new List<ExportTemplate>(_templates);
        }

        /// <summary>
        /// Get a template by ID
        /// </summary>
        public ExportTemplate? GetTemplateById(string id)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            return _templates.FirstOrDefault(t => t.Id == id);
        }

        /// <summary>
        /// Get a template by name
        /// </summary>
        public ExportTemplate? GetTemplateByName(string name)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            return _templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all built-in templates
        /// </summary>
        public List<ExportTemplate> GetBuiltInTemplates()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            return _templates.Where(t => t.IsBuiltIn).ToList();
        }

        /// <summary>
        /// Get all user-created templates
        /// </summary>
        public List<ExportTemplate> GetUserTemplates()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            return _templates.Where(t => !t.IsBuiltIn).ToList();
        }

        /// <summary>
        /// Add a new user template
        /// </summary>
        public async Task<ExportTemplate> AddTemplateAsync(ExportTemplate template)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (string.IsNullOrWhiteSpace(template.Name))
                throw new ArgumentException("Template name cannot be empty", nameof(template));

            // Check for duplicate name
            if (_templates.Any(t => t.Name.Equals(template.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A template with the name '{template.Name}' already exists");

            // Ensure not marked as built-in
            template.IsBuiltIn = false;
            template.Id = Guid.NewGuid().ToString();
            template.CreatedDate = DateTime.UtcNow;
            template.ModifiedDate = DateTime.UtcNow;

            _templates.Add(template);
            await SaveTemplatesAsync();

            Debug.WriteLine($"[ExportTemplateService] Added new template: {template.Name}");
            return template;
        }

        /// <summary>
        /// Update an existing user template
        /// </summary>
        public async Task UpdateTemplateAsync(ExportTemplate template)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (template.IsBuiltIn)
                throw new InvalidOperationException("Cannot modify built-in templates");

            var existingTemplate = _templates.FirstOrDefault(t => t.Id == template.Id);
            if (existingTemplate == null)
                throw new InvalidOperationException($"Template with ID '{template.Id}' not found");

            // Update template
            var index = _templates.IndexOf(existingTemplate);
            template.ModifiedDate = DateTime.UtcNow;
            _templates[index] = template;

            await SaveTemplatesAsync();
            Debug.WriteLine($"[ExportTemplateService] Updated template: {template.Name}");
        }

        /// <summary>
        /// Delete a user template
        /// </summary>
        public async Task DeleteTemplateAsync(string id)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            var template = _templates.FirstOrDefault(t => t.Id == id);
            if (template == null)
                throw new InvalidOperationException($"Template with ID '{id}' not found");

            if (template.IsBuiltIn)
                throw new InvalidOperationException("Cannot delete built-in templates");

            _templates.Remove(template);
            await SaveTemplatesAsync();

            Debug.WriteLine($"[ExportTemplateService] Deleted template: {template.Name}");
        }

        /// <summary>
        /// Create a copy of an existing template
        /// </summary>
        public async Task<ExportTemplate> DuplicateTemplateAsync(string id, string newName)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            var sourceTemplate = _templates.FirstOrDefault(t => t.Id == id);
            if (sourceTemplate == null)
                throw new InvalidOperationException($"Template with ID '{id}' not found");

            // Create a deep copy
            var json = JsonSerializer.Serialize(sourceTemplate, _jsonOptions);
            var newTemplate = JsonSerializer.Deserialize<ExportTemplate>(json, _jsonOptions)!;

            newTemplate.Id = Guid.NewGuid().ToString();
            newTemplate.Name = newName;
            newTemplate.IsBuiltIn = false;
            newTemplate.CreatedDate = DateTime.UtcNow;
            newTemplate.ModifiedDate = DateTime.UtcNow;

            return await AddTemplateAsync(newTemplate);
        }

        /// <summary>
        /// Reset to default built-in templates (removes all user templates)
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service not initialized. Call InitializeAsync() first.");

            _templates = ExportTemplatePresets.GetAllBuiltInTemplates();
            await SaveTemplatesAsync();

            Debug.WriteLine("[ExportTemplateService] Reset to default templates");
        }

        public void Dispose()
        {
            // No unmanaged resources to dispose
            Debug.WriteLine("[ExportTemplateService] Disposed");
        }
    }
}
