namespace RudeBuild
{
    public interface IOutput
    {
        void WriteLine(string line);
        void WriteLine();
        void Activate();
        void Clear();
    }
}