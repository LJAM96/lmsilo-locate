using GeoLens.Models;
using Serilog;
using System.Collections.ObjectModel;

namespace GeoLens.Commands
{
    /// <summary>
    /// Command to reorder images in the queue via drag-and-drop (undoable)
    /// </summary>
    public class ReorderImagesCommand : ICommand
    {
        private readonly ObservableCollection<ImageQueueItem> _imageQueue;
        private readonly ImageQueueItem _imageToMove;
        private readonly int _oldIndex;
        private readonly int _newIndex;

        public string Description => $"Reorder image: {_imageToMove.FileName} (from {_oldIndex} to {_newIndex})";

        public ReorderImagesCommand(
            ObservableCollection<ImageQueueItem> imageQueue,
            ImageQueueItem imageToMove,
            int oldIndex,
            int newIndex)
        {
            _imageQueue = imageQueue;
            _imageToMove = imageToMove;
            _oldIndex = oldIndex;
            _newIndex = newIndex;
        }

        public void Execute()
        {
            if (_oldIndex >= 0 && _oldIndex < _imageQueue.Count &&
                _newIndex >= 0 && _newIndex < _imageQueue.Count)
            {
                _imageQueue.Move(_oldIndex, _newIndex);
                Log.Information("[ReorderImagesCommand] Moved image: {FileName} from index {OldIndex} to {NewIndex}",
                    _imageToMove.FileName, _oldIndex, _newIndex);
            }
            else
            {
                Log.Warning("[ReorderImagesCommand] Invalid indices: oldIndex={OldIndex}, newIndex={NewIndex}, count={Count}",
                    _oldIndex, _newIndex, _imageQueue.Count);
            }
        }

        public void Undo()
        {
            // Reverse the move operation
            if (_newIndex >= 0 && _newIndex < _imageQueue.Count &&
                _oldIndex >= 0 && _oldIndex < _imageQueue.Count)
            {
                _imageQueue.Move(_newIndex, _oldIndex);
                Log.Information("[ReorderImagesCommand] Restored image: {FileName} from index {NewIndex} back to {OldIndex}",
                    _imageToMove.FileName, _newIndex, _oldIndex);
            }
            else
            {
                Log.Warning("[ReorderImagesCommand] Cannot undo, invalid indices: oldIndex={OldIndex}, newIndex={NewIndex}",
                    _oldIndex, _newIndex);
            }
        }
    }
}
