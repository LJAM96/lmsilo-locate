using GeoLens.Models;
using Serilog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GeoLens.Commands
{
    /// <summary>
    /// Command to clear predictions for the current image (undoable)
    /// </summary>
    public class ClearPredictionsCommand : ICommand
    {
        private readonly ObservableCollection<EnhancedLocationPrediction> _predictions;
        private List<EnhancedLocationPrediction>? _savedPredictions;
        private readonly string _imagePath;

        public string Description => $"Clear predictions for: {System.IO.Path.GetFileName(_imagePath)}";

        public ClearPredictionsCommand(
            ObservableCollection<EnhancedLocationPrediction> predictions,
            string imagePath)
        {
            _predictions = predictions;
            _imagePath = imagePath;
        }

        public void Execute()
        {
            // Save current predictions before clearing
            _savedPredictions = _predictions.ToList();

            // Clear the collection
            _predictions.Clear();

            Log.Information("[ClearPredictionsCommand] Cleared {Count} predictions for: {ImagePath}",
                _savedPredictions.Count, _imagePath);
        }

        public void Undo()
        {
            if (_savedPredictions != null)
            {
                // Restore predictions
                _predictions.Clear();
                foreach (var prediction in _savedPredictions)
                {
                    _predictions.Add(prediction);
                }

                Log.Information("[ClearPredictionsCommand] Restored {Count} predictions for: {ImagePath}",
                    _savedPredictions.Count, _imagePath);
            }
            else
            {
                Log.Warning("[ClearPredictionsCommand] No saved predictions to restore");
            }
        }
    }
}
