using Serilog;
using System;
using System.Collections.Generic;

namespace GeoLens.Commands
{
    /// <summary>
    /// Manages command execution, undo, and redo stacks.
    /// Implements the Gang of Four Command pattern with undo/redo support.
    /// Maximum stack size: 50 operations.
    /// </summary>
    public class CommandManager
    {
        private const int MaxStackSize = 50;

        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();

        /// <summary>
        /// Event raised when undo/redo availability changes
        /// </summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// Whether undo is currently available
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Whether redo is currently available
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Number of commands in the undo stack
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Number of commands in the redo stack
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Execute a command and add it to the undo stack
        /// </summary>
        /// <param name="command">The command to execute</param>
        public void ExecuteCommand(ICommand command)
        {
            try
            {
                // Execute the command
                command.Execute();

                // Add to undo stack
                _undoStack.Push(command);

                // Limit stack size
                if (_undoStack.Count > MaxStackSize)
                {
                    // Remove oldest command (convert to list, remove first, convert back)
                    var tempList = new List<ICommand>(_undoStack);
                    tempList.RemoveAt(tempList.Count - 1);
                    _undoStack.Clear();
                    for (int i = tempList.Count - 1; i >= 0; i--)
                    {
                        _undoStack.Push(tempList[i]);
                    }
                }

                // Clear redo stack (new command invalidates redo history)
                _redoStack.Clear();

                Log.Information("[CommandManager] Executed command: {Description}", command.Description);
                OnStateChanged();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[CommandManager] Failed to execute command: {Description}", command.Description);
                throw;
            }
        }

        /// <summary>
        /// Undo the last executed command
        /// </summary>
        /// <returns>The description of the undone command, or null if nothing to undo</returns>
        public string? Undo()
        {
            if (!CanUndo)
            {
                Log.Warning("[CommandManager] Undo called but stack is empty");
                return null;
            }

            try
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);

                Log.Information("[CommandManager] Undid command: {Description}", command.Description);
                OnStateChanged();

                return command.Description;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[CommandManager] Failed to undo command");
                throw;
            }
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        /// <returns>The description of the redone command, or null if nothing to redo</returns>
        public string? Redo()
        {
            if (!CanRedo)
            {
                Log.Warning("[CommandManager] Redo called but stack is empty");
                return null;
            }

            try
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);

                Log.Information("[CommandManager] Redid command: {Description}", command.Description);
                OnStateChanged();

                return command.Description;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[CommandManager] Failed to redo command");
                throw;
            }
        }

        /// <summary>
        /// Clear all undo and redo history
        /// </summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            Log.Information("[CommandManager] Cleared command history");
            OnStateChanged();
        }

        /// <summary>
        /// Get a description of the command that would be undone
        /// </summary>
        public string? GetUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : null;
        }

        /// <summary>
        /// Get a description of the command that would be redone
        /// </summary>
        public string? GetRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : null;
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
