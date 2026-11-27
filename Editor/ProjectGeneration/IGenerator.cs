namespace Coxlin.Antigravity.Editor.ProjectGeneration
{
    [Flags]
    public enum ProjectGenerationFlag
    {
        None = 0,
        Embedded = 1,
        Local = 2,
        Registry = 4,
        Git = 8,
        BuiltIn = 16,
        Unknown = 32,
        PlayerAssemblies = 64,
        LocalTarBall = 128
    }

    public interface IGenerator
    {
        string ProjectDirectory { get; }
        ProjectGenerationFlag GenerationFlags { get; set; }
        bool SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles);
        void Sync();
        string SolutionFile();
        bool HasSolutionBeenGenerated();
    }
}
