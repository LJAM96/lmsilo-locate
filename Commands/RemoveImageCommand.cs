using GeoLens.Models;
using Serilog;
using System.Collections.ObjectModel;

namespace GeoLens.Commands
{
    /// <summary>
    /// Command to remove an image from the queue (undoable)
    /// </summary>
    public class RemoveImageCommand : ICommand
    {
        private readonly ObservableCollection<ImageQueueItem> _imageQueue;
        private readonly ImageQueueItem _imageToRemove;
        private int _originalIndex;

        public string Description => $"Remove image: {_imageToRemove.FileName}";

        public RemoveImageCommand(
            ObservableCollection<ImageQueueItem> imageQueue,
            ImageQueueItem imageToRemove)
        {
            _imageQueue = imageQueue;
            _imageToRemove = imageToRemove;
            _originalIndex = -1;
        }

        public void Execute()
        {
            // Store the original index before removing
            _originalIndex = _imageQueue.IndexOf(_imageToRemove);

            if (_originalIndex >= 0)
            {
                _imageQueue.RemoveAt(_originalIndex);
                Log.Information("[RemoveImageCommand] Removed image: {FileName} from index {Index}",
                    _imageToRemove.FileName, _originalIndex);
            }
            else
            {
                Log.Warning("[RemoveImageCommand] Image not found in queue: {FileName}",
                    _imageToRemove.FileName);
            }
        }

        public void Undo()
        {
            if (_originalIndex >= 0 && _originalIndex <= _imageQueue.Count)
            {
                _imageQueue.Insert(_originalIndex, _imageToRemove);
                Log.Information("[RemoveImageCommand] Restored image: {FileName} at index {Index}",
                    _imageToRemove.FileName, _originalIndex);
            }
            else
            {
                Log.Warning("[RemoveImageCommand] Cannot restore image, invalid index: {Index}",
                    _originalIndex);
            }
        }
    }
}
