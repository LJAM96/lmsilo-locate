using GeoLens.Models;
using Serilog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GeoLens.Commands
{
    /// <summary>
    /// Command to clear all images from the queue (undoable)
    /// </summary>
    public class ClearAllCommand : ICommand
    {
        private readonly ObservableCollection<ImageQueueItem> _imageQueue;
        private readonly ObservableCollection<EnhancedLocationPrediction> _predictions;
        private List<ImageQueueItem>? _savedImages;
        private List<EnhancedLocationPrediction>? _savedPredictions;

        public string Description => "Clear all images";

        public ClearAllCommand(
            ObservableCollection<ImageQueueItem> imageQueue,
            ObservableCollection<EnhancedLocationPrediction> predictions)
        {
            _imageQueue = imageQueue;
            _predictions = predictions;
        }

        public void Execute()
        {
            // Save current state before clearing
            _savedImages = _imageQueue.ToList();
            _savedPredictions = _predictions.ToList();

            // Clear both collections
            _imageQueue.Clear();
            _predictions.Clear();

            Log.Information("[ClearAllCommand] Cleared {ImageCount} images and {PredictionCount} predictions",
                _savedImages.Count, _savedPredictions.Count);
        }

        public void Undo()
        {
            if (_savedImages != null && _savedPredictions != null)
            {
                // Restore images
                _imageQueue.Clear();
                foreach (var image in _savedImages)
                {
                    _imageQueue.Add(image);
                }

                // Restore predictions
                _predictions.Clear();
                foreach (var prediction in _savedPredictions)
                {
                    _predictions.Add(prediction);
                }

                Log.Information("[ClearAllCommand] Restored {ImageCount} images and {PredictionCount} predictions",
                    _savedImages.Count, _savedPredictions.Count);
            }
            else
            {
                Log.Warning("[ClearAllCommand] No saved state to restore");
            }
        }
    }
}
