using ChepInlineApp.Helpers;
using ChepInlineApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ChepInlineApp.DataServices
{
    public class JSONDataService
    {
        public readonly string _recipesFolderPath;
        private readonly string _appConfigFilePath;

        public JSONDataService()
        {
            _recipesFolderPath = PathConfig.RecipesFolder;
            _appConfigFilePath = PathConfig.AppConfigFile;

            Directory.CreateDirectory(_recipesFolderPath);
            Directory.CreateDirectory(Path.GetDirectoryName(_appConfigFilePath));
        }

        public List<string> GetAllRecipes()
        {
            List<string> recipes = new List<string>();

            if (Directory.Exists(_recipesFolderPath))
            {
                string[] files = Directory.GetFiles(_recipesFolderPath, "*.json");

                foreach (string file in files)
                {
                    string recipeName = Path.GetFileNameWithoutExtension(file);
                    recipes.Add(recipeName);
                }
            }
            else
            {
                AppLogger.Error($"Recipes folder does not exist at {PathConfig.RecipesFolder}.");
            }

            return recipes;
        }

        public async Task<Dictionary<string, object>?> LoadRecipeAsync(string recipeName)
        {
            string filePath = Path.Combine(_recipesFolderPath, recipeName + ".json");

            if (!File.Exists(filePath))
            {
                AppLogger.Error($"{recipeName} Recipe not found.");
                return null;
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(filePath);
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load recipe {recipeName}:", ex);
                return null;
            }
        }

        //public async Task CreateRecipeAsync(string recipeName, IEnumerable<RecipeParameterModel> parameters)
        //{
        //    if (string.IsNullOrWhiteSpace(recipeName))
        //        throw new ArgumentException("Recipe name cannot be empty.", nameof(recipeName));

        //    string filePath = Path.Combine(_recipesFolderPath, recipeName + ".json");

        //    if (File.Exists(filePath))
        //    {
        //        AppLogger.Error($"A recipe with the name: {recipeName} already exists.");
        //        throw new IOException("A recipe with that name already exists.");
        //    }

        //    Dictionary<string, object> recipeData = new();

        //    foreach (var param in parameters)
        //    {
        //        recipeData[param.ParameterName] = param.Value;
        //    }

        //    string jsonContent = JsonConvert.SerializeObject(recipeData, Formatting.Indented);
        //    await File.WriteAllTextAsync(filePath, jsonContent);
        //}

        public async Task SaveRecipeAsync(string recipeName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                AppLogger.Error("Recipe name cannot be empty.");
                throw new ArgumentException("Recipe name cannot be empty.", nameof(recipeName));
            }

            string filePath = Path.Combine(_recipesFolderPath, recipeName + ".json");

            try
            {
                string jsonContent = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, jsonContent);
            }
            catch (IOException ex)
            {
                AppLogger.Error($"Could not save recipe {recipeName}:", ex);
                throw;
            }
        }

        public async Task<AppConfigModel> LoadAppConfigAsync()
        {
            try
            {
                if (File.Exists(_appConfigFilePath))
                {
                    string jsonContent = await File.ReadAllTextAsync(_appConfigFilePath);
                    return JsonConvert.DeserializeObject<AppConfigModel>(jsonContent) ?? new AppConfigModel();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load app configuration:", ex);
            }

            return new AppConfigModel();
        }

        public async Task SaveAppConfigAsync(AppConfigModel config)
        {
            string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            await File.WriteAllTextAsync(_appConfigFilePath, jsonContent);
        }       
    }
}
