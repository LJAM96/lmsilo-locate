namespace GeoLens.Commands
{
    /// <summary>
    /// Command pattern interface for undoable operations.
    /// Implements the Gang of Four Command pattern for undo/redo functionality.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Execute the command (perform the operation)
        /// </summary>
        void Execute();

        /// <summary>
        /// Undo the command (reverse the operation)
        /// </summary>
        void Undo();

        /// <summary>
        /// Human-readable description of the command for logging and UI feedback
        /// </summary>
        string Description { get; }
    }
}
